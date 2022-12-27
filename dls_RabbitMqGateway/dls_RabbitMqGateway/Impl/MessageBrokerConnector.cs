using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using dls_RabbitMqGateway.Enum;
using dls_RabbitMqGateway.Interfaces;

namespace dls_RabbitMqGateway.Impl
{
    internal abstract class MessageBrokerConnector : IMessageBrokerConnector, IDisposable
    {
        public event CallbackExceptionHandler CallbackException;
        public event ConnectionStateChangedHandler ConnectionStateChanged;
        public event ConnectionRecoveryErrorHandler ConnectionRecoveryError;

        protected readonly string ExchangeName;
        protected readonly bool DeclareExchange;
        protected readonly Enum.ExchangeType ExchangeType;

        protected volatile IConnection Connection;

        private readonly string[] _hostNames;
        private readonly ConnectionFactory _connectionFactory;
        private readonly object _connectionLock = new object();

        protected MessageBrokerConnector(RabbitMqServerBuilder builder)
        {
            _connectionFactory = new ConnectionFactory
            {
                HostName = builder.HostNames[0],
                Port = builder.Port,
                UserName = builder.Username,
                Password = builder.Password,
                VirtualHost = builder.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                Ssl =
                {
                    Enabled = builder.SslEnabled,
                    ServerName = builder.SslServerName,
                    CertPath = builder.SslClientCertPath,
                    CertPassphrase = builder.SslClientCertPassphrase
                }
            };

            ExchangeName = builder.ExchangeName;
            ExchangeType = builder.ExchangeType;
            DeclareExchange = builder.DeclareExchange;

            _hostNames = builder.HostNames;
        }

        public virtual void Dispose()
        {
            CloseConnection();
        }

        protected void CloseConnection()
        {
            lock (_connectionLock)
            {
                Connection.TryClose();
                Connection.TryDispose();
                Connection = null;
            }
        }

        protected void EnsureConnectionExists()
        {
            if (Connection != null) return;

            lock (_connectionLock)
            {
                if (Connection != null) return;

                SafeFireEvent(ConnectionStateChanged, handler => handler(this, ConnectionState.Connecting, string.Empty));

                try
                {
                    Connection = _connectionFactory.CreateConnection(_hostNames);
                    Connection.CallbackException += ConnectionOnCallbackException;
                    Connection.ConnectionBlocked += ConnectionOnConnectionBlocked;
                    Connection.ConnectionUnblocked += ConnectionOnConnectionUnblocked;
                    Connection.ConnectionShutdown += ConnectionOnConnectionShutdown;
                }
                catch (Exception ex)
                {
                    SafeFireEvent(ConnectionStateChanged, handler => handler(this, ConnectionState.Disconnected, ex.Message));
                    throw;
                }
            }

            SafeFireEvent(ConnectionStateChanged, handler => handler(this, ConnectionState.Connected, "Ready for messaging..."));
        }

        protected void SafeFireEvent<THandler>(THandler handler, Action<THandler> action)
        {
            try
            {
                if (handler != null)
                {
                    action(handler);
                }
            }
            catch (Exception ex)
            {
                OnCallbackException(ex);
            }
        }

        protected void OnCallbackException(Exception ex)
        {
            CallbackException?.Invoke(this, ex);
        }

        private void ConnectionOnCallbackException(object sender, CallbackExceptionEventArgs args)
        {
            OnCallbackException(args.Exception);
        }

        private void ConnectionOnConnectionShutdown(object sender, ShutdownEventArgs args)
        {
            SafeFireEvent(ConnectionStateChanged,
                handler => handler(this, ConnectionState.Disconnected, args.ReplyText));
        }

        private void ConnectionOnConnectionUnblocked(object sender, EventArgs args)
        {
            SafeFireEvent(ConnectionStateChanged, handler => handler(this, ConnectionState.Connected, ""));
        }

        private void ConnectionOnConnectionBlocked(object sender, ConnectionBlockedEventArgs args)
        {
            SafeFireEvent(ConnectionStateChanged, handler => handler(this, ConnectionState.Blocked, args.Reason));
        }
    }
}
