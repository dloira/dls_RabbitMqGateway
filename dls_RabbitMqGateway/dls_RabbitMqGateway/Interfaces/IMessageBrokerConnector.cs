using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using dls_RabbitMqGateway.Enum;

namespace dls_RabbitMqGateway.Interfaces
{
    /// <summary>
    /// Called when an exception is thrown inside RabbitMq utils callbacks.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="ex"></param>
    public delegate void CallbackExceptionHandler(IMessageBrokerConnector source, Exception ex);

    /// <summary>
    /// Handler for changes of connection state
    /// </summary>
    /// <param name="source"></param>
    /// <param name="state"></param>
    /// <param name="reason"></param>
    public delegate void ConnectionStateChangedHandler(IMessageBrokerConnector source, ConnectionState state, string reason);

    /// <summary>
    /// Handler for recovery error events
    /// </summary>
    /// <param name="source"></param>
    /// <param name="ex"></param>
    public delegate void ConnectionRecoveryErrorHandler(IMessageBrokerConnector source, Exception ex);

    /// <summary>
    /// Abstraction of a message broker client, with behaviour common for both consumer and publisher.
    /// </summary>
    public interface IMessageBrokerConnector
    {
        /// <summary>
        /// Fired when any of the event handlers throws an exception. 
        /// </summary>
        event CallbackExceptionHandler CallbackException;

        /// <summary>
        /// Fired whan connection state changes (Disconnected, Connected, Blocked)
        /// </summary>
        event ConnectionStateChangedHandler ConnectionStateChanged;

        /// <summary>
        /// Fires when the client fails to recover from lost connection.
        /// </summary>
        event ConnectionRecoveryErrorHandler ConnectionRecoveryError;
    }
}
