using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.Logging;
using Hangfire.Storage;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Linq.Expressions;

namespace Hangfire.SqlServer.RabbitMQ
{
    /// <summary>
    /// An <see cref="IPersistentJobQueue"/> implementatation for RabbitMQ.
    /// Maintains a single RabbitMQ <see cref="IConnection"/>, as well as a dedicated <see cref="IModel"/> (aka channel) for publishing and 
    /// one for consuming messages. The consumer channel owns a <see cref="EventingBasicConsumer"/> for each
    /// queue configured on the parent <see cref="IPersistentJobQueueProvider"/>.
    /// </summary>
    public class RabbitMqJobQueue : IPersistentJobQueue, IDisposable
    {
        private static readonly int SyncReceiveTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

        private static readonly Hangfire.Logging.ILog Logger = Hangfire.Logging.LogProvider.For<RabbitMqJobQueue>();
        private readonly ManualResetEventSlim _retrieveEvent = new ManualResetEventSlim(false);

        private readonly RabbitMqTransport _transport;

        internal IModel Channel
        {
            get
            {
                return _transport.Channel;
            }
        }

        public RabbitMqJobQueue(IEnumerable<string> queues, ConnectionFactory factory,
            [CanBeNull] Action<IModel> confConsumer = null)
        {
            _transport = new RabbitMqTransport(queues, factory, _retrieveEvent, confConsumer);
            _transport.Connect();
        }

        private void checkCancellationRequest(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Dispose();
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        public IFetchedJob Dequeue(string[] queues, CancellationToken cancellationToken)
        {
            _transport.CancellationToken = cancellationToken;
            Logger.Debug($"Background process '{Thread.CurrentThread.Name}': start dequeue");
            BasicDeliverEventArgs argsJob = null;

            checkCancellationRequest(cancellationToken);
            while (!_transport.HasReceivedJobs)
            {
                _retrieveEvent.Wait(SyncReceiveTimeout, cancellationToken);
                _retrieveEvent.Reset();
            }
            checkCancellationRequest(cancellationToken);
            _transport.TryDequeue(out argsJob);
            string jobId = Encoding.UTF8.GetString(argsJob.Body);
            ulong deliveryTag = argsJob.DeliveryTag;
            return new RabbitMqFetchedJob(jobId,
                new Func<bool>(() => {
                    bool result = false;
                    try
                    {
                        Logger.Debug($"Background process '{Thread.CurrentThread.Name}': start send basic.ack for Job#{jobId}");
                        _transport.BasicAck(deliveryTag, false);
                        result = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorException($"Background process '{Thread.CurrentThread.Name}': an error occurred sending basic.ack for Job#{jobId}.", ex);
                    }
                    return result;
                }),
                new Func<bool>(() => {
                    bool result = false;
                    try
                    {
                        Logger.Debug($"Background process '{Thread.CurrentThread.Name}': start send basic.nack for Job#{jobId}");
                        _transport.BasicNack(deliveryTag, false, true);
                        //_transport.Close(global::RabbitMQ.Client.Framing.Constants.ReplySuccess, "Requeue");
                        result = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorException($"Background process '{Thread.CurrentThread.Name}': an error occurred sending basic.nack for Job#{jobId}.[Worker]", ex);
                    }
                    return result;
                }),
                cancellationToken);
        }

        public void Enqueue(IDbConnection connection, string queue, string jobId)
        {
            var body = Encoding.UTF8.GetBytes(jobId);
            _transport.BasicPublish("", queue, body);
             Logger.Debug($"Background process '{Thread.CurrentThread.Name}': enqueued Job#{jobId}");
        }

        public void Dispose()
        {
            _transport.Disconnect();
            _transport.Dispose();
        }
    }
}