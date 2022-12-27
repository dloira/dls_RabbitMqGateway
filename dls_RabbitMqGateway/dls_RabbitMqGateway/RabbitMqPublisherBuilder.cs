using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dls_RabbitMqGateway.Interfaces;

namespace dls_RabbitMqGateway
{
    /// <summary>
    /// Configures and builds a message publisher.
    /// It cannot be instantiated directly. Use <see cref="RabbitMqServerBuilder"/> to build publishers and consumers.
    /// </summary>
    public class RabbitMqPublisherBuilder
    {
        private readonly RabbitMqServerBuilder _clientBuilder;

        internal RabbitMqPublisherBuilder(RabbitMqServerBuilder clientBuilder)
        {
            _clientBuilder = clientBuilder;
        }

        /// <summary>
        /// Builds a message publisher with setting from the builder.
        /// </summary>
        /// <returns></returns>
        public IMessagePublisher Build()
        {
            _clientBuilder.ValidateCommonSettings();
            return new Impl.MessagePublisher(_clientBuilder, this);
        }


    }
}
