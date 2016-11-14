using System;
using Hangfire.Annotations;
using RabbitMQ.Client;

namespace Hangfire.SqlServer.RabbitMQ
{
    internal class RabbitMqJobQueueProvider : IPersistentJobQueueProvider, IDisposable
    {
        private readonly RabbitMqJobQueue _jobQueue;
        private readonly RabbitMqMonitoringApi _monitoringApi;

        public RabbitMqJobQueueProvider(string[] queues, ConnectionFactory configureAction,
            [CanBeNull] Action<IModel> configureConsumer = null,
            ushort prefetchCount = RabbitMqConnectionConfiguration.DefaultPrefetchCount)
        {
            if (queues == null) throw new ArgumentNullException("queues");
            if (configureAction == null) throw new ArgumentNullException("configureAction");

            _jobQueue = new RabbitMqJobQueue(queues, configureAction, configureConsumer, prefetchCount);
            _monitoringApi = new RabbitMqMonitoringApi(configureAction, queues);
        }

        public IPersistentJobQueue GetJobQueue()
        {
            return _jobQueue;
        }

        public IPersistentJobQueueMonitoringApi GetJobQueueMonitoringApi()
        {
            return _monitoringApi;
        }

        public void Dispose()
        {
            _jobQueue.Dispose();
        }
    }
}
