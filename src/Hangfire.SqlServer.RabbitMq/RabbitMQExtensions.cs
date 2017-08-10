using Hangfire.SqlServer;
using Hangfire.SqlServer.RabbitMQ;
using System;

namespace Hangfire
{
    public static class RabbitMqExtensions
    {
        public static IGlobalConfiguration<SqlServerStorage> UseRabbitMqQueues(this IGlobalConfiguration<SqlServerStorage> configuration, Action<RabbitMqConnectionConfiguration> configureAction, params string[] queues)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
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

            configuration.Entry.QueueProviders.Add(provider, queues);
            return configuration;
        }
    }
}
