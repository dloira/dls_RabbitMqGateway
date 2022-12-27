using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using dls_RabbitMqGateway.Entities;

namespace dls_RabbitMqGateway.Interfaces
{
    /// <summary>
    /// Handler for message received event
    /// </summary>
    /// <param name="source"></param>
    /// <param name="msg"></param>
    public delegate void MessageReceivedHandler(IMessageConsumer source, Message msg);

    /// <summary>
    /// Handler for message error event
    /// </summary>
    /// <param name="source"></param>
    /// <param name="msg"></param>
    /// <param name="ex"></param>
    public delegate void MessageErrorHandler(IMessageConsumer source, Message msg, Exception ex);

    /// <summary>
    /// Handler for message batch received event
    /// </summary>
    /// <param name="source"></param>
    /// <param name="msgBatch"></param>
    public delegate void MessageBatchReceivedHandler(IMessageConsumer source, IReadOnlyList<Message> msgBatch);

    /// <summary>
    /// Handler for message batch error event
    /// </summary>
    /// <param name="source"></param>
    /// <param name="msgBatch"></param>
    /// <param name="ex"></param>
    public delegate void MessageBatchErrorHandler(IMessageConsumer source, IReadOnlyList<Message> msgBatch, Exception ex);


    /// <summary>
    /// Message consumer abstraction
    /// </summary>
    public interface IMessageConsumer : IMessageBrokerConnector, IDisposable
    {
        /// <summary>
        /// If batch processing is enabled, then MessageReceived and MessageError events will not be fired. 
        /// Instead, MessageBatchReceived and MessageBatchError will be fired,
        /// and the list of messages arrived will be provided to the event handlers.
        ///
        /// This can be enabled in <see cref="RabbitMqConsumerBuilder.WithBatchProcessing"/>.
        ///
        /// The number of messages in the batch will never exceed prefetchCount 
        /// configured in <see cref="RabbitMqConsumerBuilder.WithPrefetchCount"/>
        /// </summary>
        bool BatchProcessingEnabled { get; }

        /// <summary>
        /// Starts consuming messages
        /// </summary>
        void Start();

        /// <summary>
        /// Stop consuming messages
        /// </summary>
        void Stop();

        /// <summary>
        /// Fired when a message is received. This event will fire only if BatchProcessingEnabled is false.
        /// IMPORTANT: If the handler does not throw exception, the message will be confirmed as processed to the broker.
        /// If the handler throws exception, the same message will be retried after a configurable number of seconds.
        /// This retry time is configured in <see cref="RabbitMqConsumerBuilder.WithMessageRetryTime"/> and it defaults to 10 secs.
        /// </summary>
        event MessageReceivedHandler MessageReceived;

        /// <summary>
        /// Fired when a batch of messages is received. This event will fire only if BatchProcessingEnabled is true.
        /// IMPORTANT: If the handler does not throw exception, all messages will be confirmed as processed to the broker.
        /// If the handler throws exception, the same message will be retried after a configurable number of seconds.
        /// This retry time is configured in <see cref="RabbitMqConsumerBuilder.WithMessageRetryTime"/> and it defaults to 10 secs.
        /// </summary>
        event MessageBatchReceivedHandler MessageBatchReceived;

        /// <summary>
        /// Fired when an exception is thrown inside MessageReceived handler. This event will fire only if BatchProcessingEnabled is false.
        /// This gives the client a chance to log the error. No special error handling is needed.
        /// The message will be retried after a configurable number of seconds.
        /// This retry time is configured in <see cref="RabbitMqConsumerBuilder.WithMessageRetryTime"/> and it defaults to 10 secs.
        /// </summary>
        event MessageErrorHandler MessageError;

        /// <summary>
        /// Fired when an exception is thrown inside MessageBatchReceived handler. This event will fire only if BatchProcessingEnabled is true.
        /// This gives the client a chance to log the error. No special error handling is needed.
        /// The message batch will be retried after a configurable number of seconds.
        /// This retry time is configured in <see cref="RabbitMqConsumerBuilder.WithMessageRetryTime"/> and it defaults to 10 secs.
        /// </summary>
        event MessageBatchErrorHandler MessageBatchError;
    }
}
