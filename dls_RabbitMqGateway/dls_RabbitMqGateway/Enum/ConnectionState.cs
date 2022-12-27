using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dls_RabbitMqGateway.Enum
{
    /// <summary>
    /// Possible connection states
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// Disconnected - this is the initial state
        /// </summary>
        Disconnected,
        /// <summary>
        /// Connecting - this is the state while the client is trying to connect.
        /// </summary>
        Connecting,

        /// <summary>
        /// Connected - the client is connected successfuly
        /// </summary>
        Connected,

        /// <summary>
        /// Blocked - the connection is blocked
        /// </summary>
        Blocked
    }
}
