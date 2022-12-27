using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dls_RabbitMqGateway.Entities;

namespace dls_RabbitMqGateway.Interfaces
{
    /// <summary>
    /// Message publisher interface
    /// </summary>
    public interface IMessagePublisher : IMessageBrokerConnector, IDisposable
    {
        /// <summary>
        /// Publishes a list of messages with given routing keys.
        /// ContentType is assumed to be application/json
        /// The first routing key is the main routing key, other keys are sent as "cc" in basicProperties.
        /// </summary>
        /// <param name="msgs"></param>
        /// <param name="timeoutMs"></param>
        void PublishMessages(IEnumerable<Message> msgs, int timeoutMs = 5000);
    }
}
