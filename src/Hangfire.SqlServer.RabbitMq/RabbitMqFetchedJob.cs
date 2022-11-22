using System;
using Hangfire.Annotations;
using Hangfire.Logging;
using Hangfire.Storage;
using System.Threading;

namespace Hangfire.SqlServer.RabbitMQ
{
    internal class RabbitMqFetchedJob : IFetchedJob
    {
        private readonly Func<bool> _removeFromQueue;
        private readonly Func<bool> _requeue;
        private bool _completed;
        private bool _disposed;
        private readonly CancellationToken _cancellationToken;

        private static readonly Hangfire.Logging.ILog Logger = Hangfire.Logging.LogProvider.For<RabbitMqJobQueue>();

        public RabbitMqFetchedJob(string jobId, [NotNull] Func<bool> removeFromQueue, [NotNull] Func<bool> requeue, CancellationToken cancellationToken)
        {
            if (removeFromQueue == null) throw new ArgumentNullException(nameof(removeFromQueue));
            if (requeue == null) throw new ArgumentNullException(nameof(requeue));
            _removeFromQueue = removeFromQueue;
            _requeue = requeue;
            _cancellationToken = cancellationToken;

            JobId = jobId;
            Logger.Debug($"Background process '{Thread.CurrentThread.Name}': dequeued Job#{JobId}");
        }

        public string JobId { get; private set; }

        public void RemoveFromQueue()
        {
            if (_completed) throw new InvalidOperationException("Job already completed");
            if (!_cancellationToken.IsCancellationRequested)
            {
                if (_removeFromQueue())
                {
                    _completed = true;
                    Logger.Debug($"Background process '{Thread.CurrentThread.Name}': ack'ed: Job#{JobId}");
                }
            }
        }

        public void Requeue()
        {
            if (_completed) throw new InvalidOperationException("Job already completed");
            if (_requeue())
            {
                _completed = true;
                Logger.Debug($"Background process '{Thread.CurrentThread.Name}': requeue'ed: Job#{JobId}");
            }
        }

        public void Dispose()
        {
            if (!_completed && !_disposed)
                Requeue();

            _disposed = true;
        }
    }
}
