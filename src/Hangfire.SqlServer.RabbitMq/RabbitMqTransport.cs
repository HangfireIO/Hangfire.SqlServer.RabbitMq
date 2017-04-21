using Hangfire.Annotations;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Hangfire.SqlServer.RabbitMQ
{
    internal class RabbitMqTransport : IDisposable
    {

        private readonly IEnumerable<string> _queues;
        private readonly ConnectionFactory _factory;
        private readonly Action<IModel> _confConsumer;

        private readonly ConcurrentDictionary<string, EventingBasicConsumer> _consumers = new ConcurrentDictionary<string, EventingBasicConsumer>();

        private IConnection _connection;
        private IModel _consumerChannel;
        private IModel _publisherChannel;

        private static readonly object ConnectionLock = new object(); // used when re-creating the connection
        private static readonly object ConsumerLock = new object();   // used for channel creation and serialzing ACK messages
        private static readonly object PublisherLock = new object();  // used for channel creation and serializing Publish messages
        private static readonly object RetrieveMessageConsumerLock = new object();

        private readonly ManualResetEventSlim _retrieveEvent;
        private readonly ConcurrentQueue<BasicDeliverEventArgs> _receivedJobs = new ConcurrentQueue<BasicDeliverEventArgs>();

        CancellationToken _cancellationToken;

        public ConcurrentQueue<BasicDeliverEventArgs> ReceivedJobs
        {
            get { return _receivedJobs; }
        }

        public bool HasReceivedJobs
        {
            get { return !_receivedJobs.IsEmpty; }
        }

        public CancellationToken CancellationToken
        {
            get { return _cancellationToken; }
            set { _cancellationToken = value; }
        }

        private bool IsCancellationRequested
        {
            get
            {
                if (_cancellationToken != null && _cancellationToken.IsCancellationRequested)
                    return true;
                return false;
            }
        }

        public RabbitMqTransport(IEnumerable<string> queues, ConnectionFactory factory,
            ManualResetEventSlim retrieveEvent, [CanBeNull] Action<IModel> confConsumer = null)
        {
            if (queues == null) throw new ArgumentNullException("queues");
            if (factory == null) throw new ArgumentNullException("factory");
            if (retrieveEvent == null) throw new ArgumentNullException("retrieveEvent");
            _queues = queues;
            _factory = factory;
            _retrieveEvent = retrieveEvent;
            _confConsumer = confConsumer ?? (_ => { });
            _connection = factory.CreateConnection();
        }

        internal IModel Channel
        {
            get
            {
                return _consumerChannel;
            }
        }

        public bool TryDequeue(out BasicDeliverEventArgs result)
        {
            return _receivedJobs.TryDequeue(out result);
        }

        public void Dispose()
        {
            Disconnect();
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

        private bool IsChannelOpen(IModel channel)
        {
            if (channel != null && channel.IsOpen && _connection.IsOpen)
                return true;
            return false;
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

        public void Connect()
        {
            if (!IsChannelOpen(_consumerChannel))
            {
                lock (ConsumerLock)
                {
                    CreateChannel(ref _consumerChannel);
                    _confConsumer(_consumerChannel);
                }
            }
            if (!IsChannelOpen(_publisherChannel))
            {
                lock (PublisherLock)
                {
                    CreateChannel(ref _publisherChannel);
                }
            }
            Consume();
        }

        private void Consume()
        {
            foreach (string queue in _queues)
            {
                EventingBasicConsumer consumer;

                if (!_consumers.TryGetValue(queue, out consumer))
                {
                    // Need to create a new consumer since a consumer for the queue does not exist
                    lock (ConsumerLock)
                    {
                        if (!_consumers.TryGetValue(queue, out consumer))
                        {
                            consumer = new EventingBasicConsumer(_consumerChannel);
                            consumer.Received += Consumer_Received;
                            _consumers.AddOrUpdate(queue, consumer, (dq, dc) => consumer);
                            _consumerChannel.BasicConsume(queue, false, $"Hangfire.RabbitMq.{Thread.CurrentThread.Name}.{queue}", consumer);
                        }
                    }
                }
            }
        }

        public void Disconnect()
        {
            while (!_receivedJobs.IsEmpty)
            {
                BasicDeliverEventArgs argsJob = null;
                _receivedJobs.TryDequeue(out argsJob);
                string jobId = Encoding.UTF8.GetString(argsJob.Body);
                ulong deliveryTag = argsJob.DeliveryTag;
                BasicNack(deliveryTag, false, true);
            }
            Close(global::RabbitMQ.Client.Framing.Constants.ReplySuccess, "Requeue");
        }

        private void Consumer_Received(object sender, BasicDeliverEventArgs args)
        {
            _receivedJobs.Enqueue(args);
            _retrieveEvent.Set();
        }


        public void BasicAck(ulong deliveryTag, bool multiple)
        {
            if (IsCancellationRequested) return;
            _consumerChannel.BasicAck(deliveryTag, false);
        }

        public void BasicNack(ulong deliveryTag, bool multiple, bool requeue)
        {
            if (IsCancellationRequested) return;
            _consumerChannel.BasicNack(deliveryTag, multiple, requeue);
        }

        public void Close(ushort replyCode, string replyText)
        {
            if (IsCancellationRequested) return;
            _consumerChannel.Close(replyCode, replyText);
        }

        public void BasicPublish(string exchange, string routingKey, byte[] body)
        {
            if (IsCancellationRequested) return;
            var properties = _publisherChannel.CreateBasicProperties();
            properties.Persistent = true;

            _publisherChannel.BasicPublish(exchange, routingKey, properties, body);
        }
    }
}
