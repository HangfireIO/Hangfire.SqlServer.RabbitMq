using Hangfire.SqlServer.RabbitMQ;
using System;
using System.Linq;
using Xunit;

namespace Hangfire.SqlServer.RabbitMq.Tests
{
    public class RabbitMqExtensionsFacts : IDisposable
    {
        private readonly IGlobalConfiguration<SqlServerStorage> _globalconfiguration;

        public RabbitMqExtensionsFacts()
        {
            _globalconfiguration = GlobalConfiguration.Configuration.UseSqlServerStorage(@"Server=.\sqlexpress;Database=TheDatabase;Trusted_Connection=True;", new SqlServerStorageOptions { PrepareSchemaIfNecessary = false });
        }

        [Fact]
        public void UseRabbitMqQueues_ThrowsAnException_WhenConfigurationIsNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => RabbitMqExtensions.UseRabbitMqQueues(null, conf => conf.HostName = "localhost", null, "default"));

            Assert.Equal("configuration", exception.ParamName);
        }

        [Fact]
        public void UseRabbitMqQueues_AddsRabbitMqJobQueueProvider()
        {
            _globalconfiguration.UseRabbitMqQueues(conf => conf.HostName = "localhost", "default");

            var providerTypes = _globalconfiguration.Entry.QueueProviders.Select(x => x.GetType());
            Assert.Contains(typeof(RabbitMqJobQueueProvider), providerTypes);
        }

        public void Dispose()
        {
            foreach (var provider in _globalconfiguration.Entry.QueueProviders)
            {
                (provider as IDisposable)?.Dispose();
            }
        }
    }
}
