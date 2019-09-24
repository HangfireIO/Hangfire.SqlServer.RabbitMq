using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using Hangfire.Annotations;
using Hangfire.Logging;
using Hangfire.Storage;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Hangfire.SqlServer.RabbitMQ
{
    /// <summary>
    /// An <see cref="IPersistentJobQueue"/> implementatation for RabbitMQ.
    /// Maintains a single RabbitMQ <see cref="IConnection"/>, as well as a dedicated <see cref="IModel"/> (aka channel) for publishing and 
    /// one for consuming messages. The consumer channel owns a <see cref="QueueingBasicConsumer"/> for each
    /// queue configured on the parent <see cref="IPersistentJobQueueProvider"/>.
    /// </summary>
    public class RabbitMqJobQueue : IPersistentJobQueue, IDisposable
    {
        private static readonly int SyncReceiveTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;
        private static readonly object ConnectionLock = new object(); // used when re-creating the connection
        private static readonly object ConsumerLock = new object();   // used for channel creation and serialzing ACK messages
        private static readonly object PublisherLock = new object();  // used for channel creation and serializing Publish messages
        private readonly IEnumerable<string> _queues;
        private readonly ConnectionFactory _factory;
        private readonly Action<IModel> _confConsumer;
        private readonly ConcurrentDictionary<string, QueueingBasicConsumer> _consumers;
        private IConnection _connection;
        private IModel _consumerChannel;
        private IModel _publisherChannel;

        private static readonly Hangfire.Logging.ILog Logger = Hangfire.Logging.LogProvider.For<RabbitMqJobQueue>();

        public RabbitMqJobQueue(IEnumerable<string> queues, ConnectionFactory factory,
            [CanBeNull] Action<IModel> confConsumer = null)
        {
            _queues = queues ?? throw new ArgumentNullException("queues");
            _factory = factory ?? throw new ArgumentNullException("factory");
            _confConsumer = confConsumer ?? (_ => {});
            _connection = factory.CreateConnection();
            _consumers = new ConcurrentDictionary<string, QueueingBasicConsumer>();
        }

        internal IModel Channel
        {
            get
            {
                EnsureConsumerChannel();
                return _consumerChannel;
            }
        }

        public IFetchedJob Dequeue(string[] queues, CancellationToken cancellationToken)
        {
            EnsureConsumerChannel();

            BasicDeliverEventArgs message;
            var queueIndex = 0;

            do
            {
                queueIndex = (queueIndex + 1) % queues.Length;
                var queueName = queues[queueIndex];

                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var consumer = GetConsumerForQueue(queueName, cancellationToken);
                    consumer.Queue.Dequeue(SyncReceiveTimeout, out message);
                }
                catch (global::RabbitMQ.Client.Exceptions.AlreadyClosedException)
                {
                    EnsureConsumerChannel();
                    message = null;
                }
                catch (System.IO.EndOfStreamException)
                {
                    EnsureConsumerChannel();
                    message = null;
                }

            } while (message == null);

            var jobId = Encoding.UTF8.GetString(message.Body);
            var deliveryTag = message.DeliveryTag;

            return new RabbitMqFetchedJob(jobId,
                () => {
                    try
                    {
                        // not calling CreateChannel() as Ack/Nack need to be send on originating channel,
                        // instead logging possible exceptions here
                        lock (ConsumerLock)
                        {
                            _consumerChannel.BasicAck(deliveryTag, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorException($"An error occurred sending basic.ack for Job#{jobId}.", ex);
                    }
                },
                () => {
                    try
                    {
                        lock (ConsumerLock)
                        {
                            _consumerChannel.BasicNack(deliveryTag, false, true);
                            _consumerChannel.Close(global::RabbitMQ.Client.Framing.Constants.ReplySuccess, "Requeue");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorException($"An error occurred sending basic.nack for Job#{jobId}.", ex);
                    }
                });
        }

        public void Enqueue(IDbConnection connection, string queue, string jobId)
        {
            EnsurePublisherChannel();

            lock (PublisherLock) // Serializes all messages on publish channel
            {
                var body = Encoding.UTF8.GetBytes(jobId);
                var properties = _publisherChannel.CreateBasicProperties();
                properties.Persistent = true;

                // TODO Allow to specify non-default exchange
                _publisherChannel.BasicPublish("", queue, properties, body);

                Logger.Debug($"Job enqueued: {jobId}");
            }
        }

        public void Enqueue(DbConnection connection, DbTransaction transaction, string queue, string jobId)
        {
            Enqueue(connection, queue, jobId);
        }

        public void Dispose()
        {
            if (_consumerChannel != null)
            {
                if (_consumerChannel.IsOpen) _consumerChannel.Close();
                _consumerChannel.Dispose();
                _consumerChannel = null;
            }

            if (_publisherChannel != null)
            {
                if (_publisherChannel.IsOpen) _publisherChannel.Close();
                _publisherChannel.Dispose();
                _publisherChannel = null;
            }

            if (_connection != null)
            {
                if (_connection.IsOpen) _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }

        private void EnsureConsumerChannel()
        {
            if (_consumerChannel != null && _consumerChannel.IsOpen && _connection.IsOpen) return;

            lock (ConsumerLock)
            {
                CreateChannel(ref _consumerChannel);
                _confConsumer(_consumerChannel);
            }
        }

        private void EnsurePublisherChannel()
        {
            if (_publisherChannel != null && _publisherChannel.IsOpen && _connection.IsOpen) return;

            lock (PublisherLock)
            {
                CreateChannel(ref _publisherChannel);
            }
        }

        private void CreateChannel(ref IModel channel)
        {
            if (channel != null && channel.IsOpen) channel.Abort();
            if (!_connection.IsOpen)
            {
                lock (ConnectionLock)
                    if (!_connection.IsOpen) _connection = _factory.CreateConnection();
            }
            channel = _connection.CreateModel();

            // QueueDeclare is idempotent
            foreach (var queue in _queues)
                channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false, arguments: null); // be aware that monitoring API also performs QueueDeclare
        }

        private QueueingBasicConsumer GetConsumerForQueue(string queue, CancellationToken cancellationToken)
        {
            QueueingBasicConsumer consumer;

            cancellationToken.ThrowIfCancellationRequested();

            if(!_consumers.TryGetValue(queue, out consumer))
            {
                // Need to create a new consumer since a consumer for the queue does not exist
                lock (ConsumerLock)
                {
                    if (!_consumers.TryGetValue(queue, out consumer))
                    {
                        consumer = new QueueingBasicConsumer(_consumerChannel);
                        _consumers.AddOrUpdate(queue, consumer, (dq, dc) => consumer);
                        _consumerChannel.BasicConsume(queue, false, "Hangfire.RabbitMq." + Thread.CurrentThread.Name, consumer);
                    }
                }
            }
            else
            {
                // Consumer for the queue exists, ensure that the channel (Model) is not closed
                if (consumer.Model.IsClosed)
                {
                    lock (ConsumerLock)
                    {
                        if (consumer.Model.IsClosed)
                        {
                            // Recreate the consumer with the new channel
                            var newConsumer = new QueueingBasicConsumer(_consumerChannel);
                            _consumers.AddOrUpdate(queue, newConsumer, (dq, dc) => newConsumer);
                            _consumerChannel.BasicConsume(queue, false, "Hangfire.RabbitMq." + Thread.CurrentThread.Name, newConsumer);
                            consumer = newConsumer;
                        }
                    }
                }
            }

            return consumer;
        }
    }
}
