using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dls_RabbitMqGateway.Enum;

namespace dls_RabbitMqGateway
{
    /// <summary>
    /// Builder for creating message publishers and consumers.
    /// Stores the server connection settings (HostName, VirtualHost, Exchange, Username, Password).
    /// </summary>
    public class RabbitMqServerBuilder
    {
        internal string[] HostNames { get; private set; } = new string[0];
        internal int Port { get; private set; }
        internal string VirtualHost { get; private set; }
        internal string Username { get; private set; }
        internal string Password { get; private set; }
        internal string ExchangeName { get; private set; }
        internal bool SslEnabled { get; private set; }
        internal string SslServerName { get; private set; }
        internal string SslClientCertPath { get; private set; }
        internal string SslClientCertPassphrase { get; private set; }

        internal bool DeclareExchange = true;

        internal ExchangeType ExchangeType = ExchangeType.Topic;

        /// <summary>
        /// Starts the building of either publisher or consumer.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="virtualHost"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static RabbitMqServerBuilder Server(string hostName, string virtualHost, string username, string password, int port = RabbitMQ.Client.AmqpTcpEndpoint.UseDefaultPort)
        {
            return Server(new[] { hostName }, virtualHost, username, password, port);
        }

        /// <summary>
        /// Starts building a client using a list of hostnames using the specified port.
        /// By default each hostname is tried in a random order until a successful connection is
        /// found or the list is exhausted.
        /// </summary>
        /// <param name="hostNames">
        /// List of hostnames to use for the initial
        /// connection and recovery.
        /// </param>
        /// <param name="virtualHost"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static RabbitMqServerBuilder Server(string[] hostNames, string virtualHost, string username, string password, int port = RabbitMQ.Client.AmqpTcpEndpoint.UseDefaultPort)
        {
            return new RabbitMqServerBuilder
            {
                HostNames = hostNames,
                VirtualHost = virtualHost,
                Username = username,
                Password = password,
                Port = port
            };
        }

        /// <summary>
        /// Builds a message consumer
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="routingKey"></param>
        /// <param name="additionalRoutingKeys"></param>
        /// <returns></returns>
        public RabbitMqConsumerBuilder Consumer(string queueName, string routingKey = "#",
            string[] additionalRoutingKeys = null)
        {
            var routingKeys = new List<string>();

            if (routingKey != null)
            {
                routingKeys.Add(routingKey);
            }

            if (additionalRoutingKeys != null)
            {
                routingKeys.AddRange(additionalRoutingKeys);
            }

            return new RabbitMqConsumerBuilder(this, queueName, routingKeys);
        }

        /// <summary>
        /// Builds a message publisher.
        /// </summary>
        /// <returns></returns>
        public RabbitMqPublisherBuilder Publisher()
        {
            return new RabbitMqPublisherBuilder(this);
        }

        /// <summary>
        /// Enables Ssl type of connection.
        /// </summary>
        /// <param name="sslServerName">.NET expects this to match the Subject Alternative Namee (SAN) or Common Name (CN) on the certificate that the server sends over.</param>
        /// <param name="clientCertPath">This is the path to the client's certificate in PKCS#12 format if your server expects client side verification. This is optional.</param>
        /// <param name="clientCertPassphrase">If you are using a client certificate in PKCS#12 format then it'll probably have a password, which you specify in this field.</param>
        /// <returns></returns>
        public RabbitMqServerBuilder WithSsl(string sslServerName = null, string clientCertPath = null, string clientCertPassphrase = null)
        {
            SslEnabled = true;
            SslServerName = sslServerName;
            SslClientCertPath = clientCertPath;
            SslClientCertPassphrase = clientCertPassphrase;
            return this;
        }

        /// <summary>
        /// Specifies exchange name, the type of exchange
        /// </summary>
        /// <param name="exchangeName"></param>
        /// <param name="declareExchange"></param>
        /// <param name="exchangeType"></param>
        /// <returns></returns>
        public RabbitMqServerBuilder WithExchange(string exchangeName,
            bool declareExchange = true,
            ExchangeType exchangeType = ExchangeType.Topic)
        {
            ExchangeName = exchangeName;
            ExchangeType = exchangeType;
            DeclareExchange = declareExchange;

            return this;
        }

        internal void ValidateCommonSettings()
        {
            if ((HostNames?.Length ?? 0) == 0)
                throw new Exception("Hostname is not specified!");

            foreach (var hostName in HostNames)
            {
                if (string.IsNullOrWhiteSpace(hostName))
                    throw new Exception("Empty host name is not allowed");
            }

            if (string.IsNullOrWhiteSpace(VirtualHost))
                throw new Exception("VirtualHost is not specified!");

            if (string.IsNullOrWhiteSpace(Username))
                throw new Exception("Username is not specified!");

            if (string.IsNullOrWhiteSpace(Password))
                throw new Exception("Password is not specified!");

            if (string.IsNullOrWhiteSpace(ExchangeName))
                throw new Exception("Exchange name is not specified!");
        }
    }
}
