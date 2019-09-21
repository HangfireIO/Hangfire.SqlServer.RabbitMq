using System;
using RabbitMQ.Client;

namespace Hangfire.SqlServer.RabbitMQ
{
    public class RabbitMqConnectionConfiguration
    {
        public const string DefaultHost = "localhost";
        public const int DefaultPort = AmqpTcpEndpoint.UseDefaultPort;
        public const string DefaultUser = "guest";
        public const string DefaultPassword = "guest";
        public const string DefaultVirtualHost = "/";
        public const ushort DefaultPrefetchCount = 1;

        public RabbitMqConnectionConfiguration()
            : this(DefaultHost, DefaultPort, DefaultUser, DefaultPassword)
        {
        }

        public RabbitMqConnectionConfiguration(string host)
            : this(host, DefaultPort, DefaultUser, DefaultPassword)
        {
        }

        public RabbitMqConnectionConfiguration(string host, int port)
            : this(host, port, DefaultUser, DefaultPassword)
        {
        }

        public RabbitMqConnectionConfiguration(Uri uri)
        {
            Uri = uri ?? throw new ArgumentNullException("uri");
        }

        public RabbitMqConnectionConfiguration(string host, int port, string username, string password, ushort prefetchCount = DefaultPrefetchCount)
        {
            HostName = host ?? throw new ArgumentNullException("host");
            Username = username ?? throw new ArgumentNullException("username");
            Password = password ?? throw new ArgumentNullException("password");
            Port = port;
            VirtualHost = DefaultVirtualHost;
            PrefetchCount = DefaultPrefetchCount;
        }

        public string Username { get; set; }

        public string Password { get; set; }

        public string HostName { get; set; }

        public string VirtualHost { get; set; }

        public int Port { get; set; }

        public Uri Uri { get; set; }

        /// <summary>
        /// The <c>prefetchCount</c> RabbitMQ consumers are configured with. This specified the 
        /// number of unacked messages a consumer is allowed to have on the server. Hangfire jobs
        /// leave an unacked message in their queue while they are executing. Hence, effectively,
        /// this setting determines how many jobs from each queue can be executed in parallel -
        /// provided the same number of workers is available.
        /// </summary>
        /// <remarks>
        /// Default value is 1 for this to be a non-breaking change with previous releases.
        /// However, it is strongly recommended to increase the value in order not to effectively
        /// establish a per-queue concurrency constraint of only one job execution at a time!
        /// </remarks>
        public ushort PrefetchCount { get; set; }
    }
}