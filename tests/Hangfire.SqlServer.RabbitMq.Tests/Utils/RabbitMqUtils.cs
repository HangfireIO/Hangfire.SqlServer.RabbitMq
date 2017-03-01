using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Hangfire.SqlServer.RabbitMq.Tests
{
    internal class RabbitMqUtils
    {
        public static void EnqueueJobId(string queue, string jobId)
        {
            using (var messageQueue = CleanRabbitMqQueueAttribute.GetMessageQueue(queue))
            {
                var body = Encoding.UTF8.GetBytes(jobId);

                var properties = messageQueue.Channel.CreateBasicProperties();
                properties.Persistent = true;

                messageQueue.Channel.BasicPublish("", queue, properties, body);
            }
        }

        public static string DequeueJobId(string queue, TimeSpan timeout)
        {
            int timeoutMilliseconds = (int)timeout.TotalMilliseconds;
            bool dequeued = false;

            using (var messageQueue = CleanRabbitMqQueueAttribute.GetMessageQueue(queue))
            {
                string message = null;
                var cancel = new CancellationTokenSource();

                messageQueue.Channel.BasicQos(0, 1, false);
                var consumer = new EventingBasicConsumer(messageQueue.Channel);
                consumer.Received += (model, ea) =>
                {
                    message = Encoding.UTF8.GetString(ea.Body);
                    dequeued = true;
                    messageQueue.Channel.BasicAck(ea.DeliveryTag, false);
                    cancel.Cancel();
                };

                messageQueue.Channel.BasicConsume(queue, false, consumer);

                try
                {
                    Task.Delay(timeoutMilliseconds, cancel.Token).Wait(cancel.Token);
                }
                catch (System.OperationCanceledException)
                {
                    // Cancellation token triggered
                }
                finally
                {
                    if (dequeued == false) throw new TimeoutException(queue);
                }

                return message;
            }
        }
    }
}
