using dls_RabbitMqGateway.Entities;
using dls_RabbitMqGateway.Enum;
using dls_RabbitMqGateway.Interfaces;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dls_RabbitMqGateway.Impl
{
    internal class MessagePublisher : MessageBrokerConnector, IMessagePublisher
    {
        private readonly object _modelLock = new object();
        private volatile IModel _model;

        public MessagePublisher(RabbitMqServerBuilder clientBuilder, RabbitMqPublisherBuilder publisherBuilder)
            : base(clientBuilder) { }

        public void PublishMessages(IEnumerable<Message> msgs, int timeoutMs)
        {
            EnsureConnectionExists();

            lock (_modelLock)
            {
                EnsureModelExists();

                foreach (var msg in msgs)
                {
                    var basicProperties = _model.CreateBasicProperties();
                    basicProperties.Type = msg.MessageType;
                    basicProperties.Persistent = true;

                    if (msg.CcList != null && msg.CcList.Count > 0)
                    {
                        basicProperties.Headers = new Dictionary<string, object> { { "CC", msg.CcList.ToList() } };
                    }

                    _model.BasicPublish(ExchangeName,
                        msg.RoutingKey,
                        basicProperties,
                        msg.Body);
                }

                _model.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(timeoutMs));
            }
        }

        public override void Dispose()
        {
            lock (_modelLock)
            {
                _model?.TryDispose();
                _model = null;
            }

            base.Dispose();
        }

        private void EnsureModelExists()
        {
            if (_model != null)
            {
                if (_model.IsOpen)
                {
                    return;
                }

                _model.TryDispose();
                _model = null;
            }


            try
            {
                _model = Connection.CreateModel();
                _model.ConfirmSelect();

                if (DeclareExchange)
                {
                    switch (ExchangeType)
                    {
                        case Enum.ExchangeType.Topic:
                            _model.ExchangeDeclare(ExchangeName, RabbitMQ.Client.ExchangeType.Topic, true);
                            break;
                        case Enum.ExchangeType.Fanout:
                            _model.ExchangeDeclare(ExchangeName, RabbitMQ.Client.ExchangeType.Fanout, true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Exchange type {ExchangeType} not supported.");
                    }
                }
            }
            catch
            {
                _model?.TryDispose();
                _model = null;

                throw;
            }
        }
    }
}
