using System;
using System.Linq;
using Hangfire.SqlServer.RabbitMQ;
using Xunit;

namespace Hangfire.SqlServer.RabbitMq.Tests
{
    public class RabbitMqSqlServerStorageExtensionsFacts : IDisposable
    {
        private readonly SqlServerStorage _storage;

        public RabbitMqSqlServerStorageExtensionsFacts()
        {
            _storage = new SqlServerStorage(
                @"Server=.\sqlexpress;Database=TheDatabase;Trusted_Connection=True;",
                new SqlServerStorageOptions { PrepareSchemaIfNecessary = false });
        }

        [Fact]
        public void UseRabbitMq_ThrowsAnException_WhenStorageIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => RabbitMqSqlServerStorageExtensions.UseRabbitMq(null, conf => conf.HostName = "localhost"));
            
            Assert.Equal("storage", exception.ParamName);
        }

        [Fact]
        public void UseRabbitMq_AddsRabbitMqJobQueueProvider()
        {
            _storage.UseRabbitMq(conf => conf.HostName = "localhost", "default");

            var providerTypes = _storage.QueueProviders.Select(x => x.GetType());
            Assert.Contains(typeof(RabbitMqJobQueueProvider), providerTypes);
        }

        public void Dispose()
        {
            foreach (var provider in _storage.QueueProviders)
            {
                (provider as IDisposable)?.Dispose();
            }
        }
    }
}
