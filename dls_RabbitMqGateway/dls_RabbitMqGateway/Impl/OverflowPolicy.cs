using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dls_RabbitMqGateway.Impl
{
    /// <summary>
    /// OverflowPolicy constants. Use with <see cref="RabbitMqConsumerBuilder.WithQueueLimit"/>
    /// </summary>
    public static class OverflowPolicy
    {
        /// <summary>
        /// In case the queue gets larger than maximum size, oldest messages will be discarded. 
        /// </summary>
        public const string DropHead = "drop-head";

        /// <summary>
        /// In case the queue gets larger than maximum size, broker will send nack to clients.
        /// This will prevent publish to the exchange if even one of the queues bound to it is full.
        /// </summary>
        public const string RejectPublish = "reject-publish";
    }
}
