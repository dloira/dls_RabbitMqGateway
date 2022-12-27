using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dls_RabbitMqGateway.Impl;
using dls_RabbitMqGateway.Interfaces;

namespace dls_RabbitMqGateway
{
    /// <summary>
    /// Configures and builds a message consumer.
    /// It cannot be instantiated directly. Use <see cref="RabbitMqServerBuilder"/> to build publishers and consumers.
    /// </summary>
    public class RabbitMqConsumerBuilder
    {
        internal RabbitMqServerBuilder ClientBuilder { get; }
        internal string QueueName { get; }
        internal List<string> RoutingKeys { get; }
        internal ushort PrefetchCount { get; private set; } = 10;
        internal bool BatchProcessingEnabled { get; private set; }
        internal int? RetryTimeSec { get; private set; }
        internal bool DeclareQueue { get; private set; } = true;
        internal QueueLimitClass QueueLimit { get; private set; }
            = new QueueLimitClass { MaxMessages = 1000000, OverFlowPolicy = OverflowPolicy.RejectPublish };
        internal bool BasicConsumeExclusive { get; private set; } = true;

        internal RabbitMqConsumerBuilder(RabbitMqServerBuilder clientBuilder, string queueName, List<string> routingKeys)
        {
            ClientBuilder = clientBuilder;
            QueueName = queueName;
            RoutingKeys = routingKeys;
        }

        /// <summary>
        /// Builds a message consumer with setting from the builder.
        /// </summary>
        /// <returns></returns>
        public IMessageConsumer Build()
        {
            ClientBuilder.ValidateCommonSettings();
            ValidateConsumerSettings();
            return new Impl.MessageConsumer(ClientBuilder, this);
        }

        /// <summary>
        /// Sets max count of messages that can be delivered by the broker without being acknowledged.
        /// Allowed values are from 1 to ushort.MaxValue. Default is 100.
        /// Pre-fetch count 0 is not allowed because it probably means "no specific limit" which
        /// could cause out of memory errors on the client. Maybe.
        /// See: http://www.rabbitmq.com/amqp-0-9-1-reference.html
        /// </summary>
        /// <param name="prefetchCount"></param>
        /// <returns></returns>
        public RabbitMqConsumerBuilder WithPrefetchCount(ushort prefetchCount)
        {
            PrefetchCount = prefetchCount;
            return this;
        }

        /// <summary>
        /// Number of seconds to wait before retrying a message if the message handler throws exception.
        /// If not set - it defaults to 10 seconds. It cannot be set to less than 1 second.
        /// </summary>
        /// <param name="retryTimeSec"></param>
        /// <returns></returns>
        public RabbitMqConsumerBuilder WithMessageRetryTime(int retryTimeSec)
        {
            RetryTimeSec = retryTimeSec;
            return this;
        }

        /// <summary>
        /// Enables batch processing of messages.
        /// If this is set to 'false' (which is the default), then the following events will fire on consumer:
        /// - <see cref="IMessageConsumer.MessageError"/>
        /// - <see cref="IMessageConsumer.MessageReceived"/>
        /// If this is set to 'true', then the following events will fire on consumer:
        /// - <see cref="IMessageConsumer.MessageBatchError"/>
        /// - <see cref="IMessageConsumer.MessageBatchReceived"/>
        /// </summary>
        /// <param name="enabled">Set to 'true' to enable firing messages in batches.</param>
        /// <returns></returns>
        public RabbitMqConsumerBuilder WithBatchProcessing(bool enabled)
        {
            BatchProcessingEnabled = enabled;
            return this;
        }

        /// <summary>
        /// Enables or disables multiple consumers connecting to the queue. By default in this RabbitMq utils library,
        /// multiple consumers will not be allowed. That means exclusive is set to 'true' by default - this is the safe option.
        /// WARNING: If you set exclusive to 'false' there can be situations where messages can come out of order,
        /// even if you have only one client application!
        /// Be sure to know what you are doing, before setting this exclusive consumer to 'false'!
        /// </summary>
        /// <param name="exclusive">Set to false to enable multiple consumers connecting to queue. Default is true.</param>
        /// <returns></returns>
        public RabbitMqConsumerBuilder WithExclusiveConsumer(bool exclusive)
        {
            BasicConsumeExclusive = exclusive;
            return this;
        }

        /// <summary>
        /// Set the limits of the queue on the broker side. Default is 1 million messages with "reject-publish" policy.
        /// This seems like a safe option, to prevent losing messages. Please consider carefully before changing.
        /// </summary>
        /// <param name="maxMessages"></param>
        /// <param name="overflowPolicy">Use constants in <see cref="OverflowPolicy"/></param>
        /// <returns></returns>
        public RabbitMqConsumerBuilder WithQueueLimit(int maxMessages, string overflowPolicy)
        {
            if (overflowPolicy != OverflowPolicy.DropHead
                && overflowPolicy != OverflowPolicy.RejectPublish)
            {
                throw new Exception("Invalid overflow policy");
            }

            if (maxMessages < 0)
            {
                throw new Exception("Use non-negative value for maxMessages");
            }

            QueueLimit = new QueueLimitClass
            {
                MaxMessages = maxMessages,
                OverFlowPolicy = overflowPolicy
            };

            return this;
        }

        /// <summary>
        /// Do not declare exchange (exchange must already exists on the rabbit mq server)
        /// </summary>
        /// <returns></returns>
        public RabbitMqConsumerBuilder DoNotDeclareQueue()
        {
            DeclareQueue = false;
            return this;
        }

        private void ValidateConsumerSettings()
        {
            if (string.IsNullOrWhiteSpace(QueueName))
                throw new Exception("Queue name is not specified!");

            if (RoutingKeys == null || RoutingKeys.Count == 0 || string.IsNullOrWhiteSpace(RoutingKeys[0]))
                throw new Exception("Routing key is not specified.");

            // Could not find what prefetch count = 0 means. Based on the link below it probably means "no specific limit".
            // That sounds a bit scary (could we get out of memory if our consumer starts pilling up messages).
            // http://www.rabbitmq.com/amqp-0-9-1-reference.html
            if (PrefetchCount == 0)
                throw new Exception("Please use pre-fetch count larger than 0. If you want to disable pre-fetching, use 1.");
        }

        internal class QueueLimitClass
        {
            public int MaxMessages { get; set; }
            public string OverFlowPolicy { get; set; }
        }
    }
}
