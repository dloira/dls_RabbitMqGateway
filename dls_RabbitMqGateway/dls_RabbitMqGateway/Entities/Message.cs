using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dls_RabbitMqGateway.Entities
{
    /// <summary>
    /// Abstraction of the message being published or received to/from the MessageBroker
    /// </summary>
    public class Message
    {
        /// <summary>
        /// Routing key to be used for publishing and consuming
        /// </summary>
        public string RoutingKey { get; }

        /// <summary>
        /// Body of the message
        /// </summary>
        public byte[] Body { get; }

        /// <summary>
        /// Message type
        /// </summary>
        public string MessageType { get; }

        /// <summary>
        /// CC List of routing keys
        /// </summary>
        public IReadOnlyList<string> CcList { get; }

        internal ulong DeliveryTag { get; }

        /// <summary>
        /// Creates a message for sending to the broker.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="body"></param>
        /// <param name="routingKey"></param>
        /// <param name="ccList"></param>
        public Message(string messageType, string body, string routingKey, params string[] ccList)
            : this(0, messageType, Encoding.UTF8.GetBytes(body), routingKey, null)
        {
            CcList = new List<string>(ccList);
        }

        /// <summary>
        /// Creates a message received from the broker.
        /// </summary>
        /// <param name="messageType"></param>
        /// <param name="body"></param>
        /// <param name="routingKey"></param>
        /// <param name="ccList"></param>
        public Message(ulong deliveryTag, string messageType, byte[] body, string routingKey, IReadOnlyList<object> ccList)
        {
            DeliveryTag = deliveryTag;
            Body = body;
            MessageType = messageType;
            RoutingKey = routingKey;

            if (ccList != null)
            {
                CcList = ccList.Select(raw => Encoding.UTF8.GetString((byte[])raw)).ToList();
            }
        }

        /// <summary>
        /// Returns body as a string converting using UTF8 encoding.
        /// </summary>
        /// <returns></returns>
        public string BodyAsString()
        {
            return Encoding.UTF8.GetString(Body);
        }
    }
}
