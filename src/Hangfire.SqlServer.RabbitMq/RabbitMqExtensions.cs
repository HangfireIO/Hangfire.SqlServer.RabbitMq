using Hangfire.SqlServer;
using Hangfire.SqlServer.RabbitMQ;
using Hangfire.States;
using System;

namespace Hangfire
{
    public static class RabbitMqExtensions
    {

        public static IGlobalConfiguration<SqlServerStorage> UseRabbitMQQueues(
            this IGlobalConfiguration<SqlServerStorage> configuration,
            Action<RabbitMqConnectionConfiguration> configureAction,
            params string[] queues)
        {
            configuration.Entry.UseRabbitMq(configureAction, queues);
            return configuration;
        }
    }
}
