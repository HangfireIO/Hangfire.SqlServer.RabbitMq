using System;
using RabbitMQ.Client;

namespace Hangfire.SqlServer.RabbitMQ
{
    public static class RabbitMqSqlServerStorageExtensions
    {
        public static SqlServerStorage UseRabbitMq(this SqlServerStorage storage, Action<RabbitMqConnectionConfiguration> configureAction, params string[] queues)
        {
            if (storage == null) throw new ArgumentNullException("storage");
            if (queues == null) throw new ArgumentNullException("queues");
            if (queues.Length == 0) throw new ArgumentException("No queue(s) specified for RabbitMQ provider.", "queues");
            if (configureAction == null) throw new ArgumentNullException("configureAction");

            var conf = new RabbitMqConnectionConfiguration();
            configureAction(conf);           

            var provider = new RabbitMqJobQueueProvider(queues, conf.CreateConnectionFactory(), channel => 
                channel.BasicQos(0,
                    conf.PrefetchCount,
                    false // applied separately to each new consumer on the channel
                ));

            storage.QueueProviders.Add(provider, queues);

            return storage;
        }
    }
}
