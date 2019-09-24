using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.MessagePatterns;

namespace Hangfire.SqlServer.RabbitMQ
{
    internal class RabbitMqMonitoringApi : IPersistentJobQueueMonitoringApi
    {
        private readonly IEnumerable<string> _queues;
        private readonly ConnectionFactory _factory;

        public RabbitMqMonitoringApi(ConnectionFactory factory, params string[] queues)
        {
            _queues = queues ?? throw new ArgumentNullException("queues");
            _factory = factory ?? throw new ArgumentNullException("factory");
        }

        public IEnumerable<string> GetQueues()
        {
            return _queues;
        }

        /// <remarks>
        /// RabbitMq does not have a Peek feature, the solution is to dequeue all messages
        /// with acknowledgments required (noAck = false). After all messages have been read
        /// we dispose the RabbitMqJobQueue causing the channel to close. All unack'd
        /// messages then get requeued in order.
        /// </remarks>
        public IEnumerable<long> GetEnqueuedJobIds(string queue, int from, int perPage)
        {
            using (var client = new RabbitMqJobQueue(new[] {queue}, _factory))
            {
                var consumer = new Subscription(client.Channel, queue, true);
                var jobIds = new List<long>();

                while (consumer.Next(1000, out BasicDeliverEventArgs delivery))
                {
                    var body = Encoding.UTF8.GetString(delivery.Body);
                    jobIds.Add(Convert.ToInt32(body));
                }

                return jobIds.Skip(from).Take(perPage);
            }
        }

        public IEnumerable<long> GetFetchedJobIds(string queue, int from, int perPage)
        {
            return Enumerable.Empty<long>();
        }

        /// <remarks>
        /// Calling QueueDeclare will return the number of messages that exist in the queue.
        /// QueueDeclare is idempotent so it can be called regardless if the queue exists.
        /// </remarks>
        public EnqueuedAndFetchedCountDto GetEnqueuedAndFetchedCount(string queue)
        {
            using (var client = new RabbitMqJobQueue(new[] {queue}, _factory, null))
            {
                var channel = client.Channel.QueueDeclare(queue, true, false, false, null);

                return new EnqueuedAndFetchedCountDto
                {
                    EnqueuedCount = (int) channel.MessageCount
                };
            }
        }
    }
}
