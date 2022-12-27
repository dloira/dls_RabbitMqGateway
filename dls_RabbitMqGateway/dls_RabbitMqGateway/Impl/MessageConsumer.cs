using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using dls_RabbitMqGateway.Enum;
using dls_RabbitMqGateway.Entities;
using dls_RabbitMqGateway.Interfaces;

namespace dls_RabbitMqGateway.Impl
{
    internal class MessageConsumer : MessageBrokerConnector, IMessageConsumer
    {
        public bool BatchProcessingEnabled { get; }

        public event MessageReceivedHandler MessageReceived;
        public event MessageErrorHandler MessageError;
        public event MessageBatchReceivedHandler MessageBatchReceived;
        public event MessageBatchErrorHandler MessageBatchError;

        private readonly bool _declareQueue;
        private readonly string _queueName;
        private readonly int _queueMaxLength;
        private readonly string _queueOverflowPolicy;
        private readonly string[] _routingKeys;
        private readonly ushort _prefetchCount;
        private readonly bool _basicConsumeExclusive;
        private readonly ushort _batchSize;
        private readonly int _messageRetryTimeMs;

        private ConcurrentQueue<Message> _messages;

        private readonly AutoResetEvent _messageArrivedSignal = new AutoResetEvent(false);
        private readonly AutoResetEvent _stoppingSignal = new AutoResetEvent(false);

        private readonly object _startStopLock = new object();
        private bool _running;

        private Thread _eventFiringThread;

        public MessageConsumer(RabbitMqServerBuilder clientBuilder, RabbitMqConsumerBuilder consumerBuilder)
            : base(clientBuilder)
        {
            BatchProcessingEnabled = consumerBuilder.BatchProcessingEnabled;

            _queueName = consumerBuilder.QueueName;
            _queueMaxLength = consumerBuilder.QueueLimit.MaxMessages;
            _queueOverflowPolicy = consumerBuilder.QueueLimit.OverFlowPolicy;

            _routingKeys = consumerBuilder.RoutingKeys.ToArray();

            _prefetchCount = consumerBuilder.PrefetchCount;
            _basicConsumeExclusive = consumerBuilder.BasicConsumeExclusive;

            _batchSize = _prefetchCount == 0 ? (ushort)100 : _prefetchCount;

            if (consumerBuilder.RetryTimeSec.HasValue && consumerBuilder.RetryTimeSec.Value >= 1)
            {
                _messageRetryTimeMs = consumerBuilder.RetryTimeSec.Value * 1000;
            }
            else
            {
                _messageRetryTimeMs = 10 * 1000;
            }

            _declareQueue = consumerBuilder.DeclareQueue;
        }

        public void Start()
        {
            lock (_startStopLock)
            {
                if (_running) return;
                _running = true;

                if (BatchProcessingEnabled && MessageBatchReceived == null)
                {
                    throw new Exception("MessageBatchReceived event is not handled and batch processing is enabled. You will lose messages.");
                }
                if (!BatchProcessingEnabled && MessageReceived == null)
                {
                    throw new Exception("MessageReceived event is not handled. You will lose messages.");
                }

                _eventFiringThread = new Thread(EventFiringThreadHandler) { IsBackground = true };
                _eventFiringThread.Start();
            }
        }

        public void Stop()
        {
            lock (_startStopLock)
            {
                if (!_running) return;
                _running = false;

                _messageArrivedSignal.Set(); //to stop waiting for messages

                _stoppingSignal.WaitOne(); //wait for the event firing thread to finish

                _eventFiringThread = null;
            }
        }

        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }

        private bool TryCreateModel(ref IModel model, ref EventingBasicConsumer consumer)
        {
            if (model != null) 
                return true;

            model = null;
            consumer = null;
            try
            {
                _messages = new ConcurrentQueue<Message>();

                EnsureConnectionExists();

                try
                {
                    model = Connection.CreateModel();
                    model.BasicQos(0, _prefetchCount, false);

                    if (DeclareExchange)
                    {
                        switch (ExchangeType)
                        {
                            case Enum.ExchangeType.Topic:
                                model.ExchangeDeclare(ExchangeName, RabbitMQ.Client.ExchangeType.Topic, true);
                                break;
                            case Enum.ExchangeType.Fanout:
                                model.ExchangeDeclare(ExchangeName, RabbitMQ.Client.ExchangeType.Fanout, true);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException($"Exchange type {ExchangeType} not supported.");
                        }
                    }

                    if (_declareQueue)
                    {
                        model.QueueDeclare(_queueName, true, false, false, new Dictionary<string, object>
                        {
                            {"x-max-length", _queueMaxLength},
                            {"x-overflow", _queueOverflowPolicy}
                        });

                        if (ExchangeType == Enum.ExchangeType.Topic)
                        {
                            foreach (var routingKey in _routingKeys)
                            {
                                model.QueueBind(_queueName, ExchangeName, routingKey);
                            }
                        }
                        else
                        {
                            model.QueueBind(_queueName, ExchangeName, "#");
                        }
                    }

                    consumer = new EventingBasicConsumer(model);
                    consumer.Received += ConsumerOnReceived;

                    // It is recommended to use exclusive consumer to allow only one consumer per queue.
                    // This is to ensure that we always get messages in order from the queue.
                    //
                    // For example - if there is a network failure, we can reconnect before the broker
                    // detected that the previous connection is dead. In that case, we will skip all
                    // un-acked messages (broker thinks they are being processed by previous connection).
                    // Then when broker finally detects the dead connection, it will requeue un-acked messages,
                    // and we will get them out of order. 
                    // 
                    // With exclusive consumer, broker will not allow second connection, until the first one
                    // is closed (at which point, un-acked messages will be requeued).

                    model.BasicConsume(consumer, _queueName, exclusive: _basicConsumeExclusive);

                    return true;
                }
                catch (Exception ex)
                {
                    OnCallbackException(ex);
                    throw;
                }
            }
            catch
            {
                CloseModelAndConnection(ref model, ref consumer);
                return false;
            }
        }

        private void CloseModelAndConnection(ref IModel model, ref EventingBasicConsumer consumer)
        {
            if (consumer != null)
            {
                consumer.Received -= ConsumerOnReceived;
                consumer = null;
            }

            model.TryDispose();
            model = null;

            CloseConnection();

            _messages = new ConcurrentQueue<Message>();
        }

        private void ConsumerOnReceived(object sender, BasicDeliverEventArgs args)
        {
            _messages.Enqueue(CreateMessage(args));
            _messageArrivedSignal.Set();
        }

        private void EventFiringThreadHandler()
        {
            IModel model = null;
            EventingBasicConsumer consumer = null;

            var messageBatch = new List<Message>();

            while (_running)
            {
                Message currentMessage = null;

                try
                {
                    if (!TryCreateModel(ref model, ref consumer))
                    {
                        Wait();
                        continue;
                    }

                    _messageArrivedSignal.WaitOne();

                    messageBatch.Clear();

                    while (_running
                        && messageBatch.Count < _batchSize
                        && _messages.TryDequeue(out currentMessage))
                    {
                        if (BatchProcessingEnabled)
                        {
                            messageBatch.Add(currentMessage);
                        }
                        else
                        {
                            MessageReceived?.Invoke(this, currentMessage);
                            model.BasicAck(currentMessage.DeliveryTag, false);
                        }
                    }

                    if (messageBatch.Count > 0)
                    {
                        MessageBatchReceived?.Invoke(this, messageBatch);
                        model.BasicAck(messageBatch[messageBatch.Count - 1].DeliveryTag, true);
                    }
                }
                catch (Exception ex)
                {
                    if (BatchProcessingEnabled)
                    {
                        SafeFireEvent(MessageBatchError, handler => handler(this, messageBatch, ex));
                    }
                    else
                    {
                        SafeFireEvent(MessageError, handler => handler(this, currentMessage, ex));
                    }

                    // On any error in MessageReceived handler, we close the connection.
                    // This will cause all un-acked messages to be requeued by the broker, 
                    // which is what we want.
                    // 
                    // We will retry the connection after RetryMilliseconds.
                    //
                    // This is because we assume, if MessageReceived throws, 
                    // there is some unexpected problem with message handling 
                    // (such as network failure, database problems, a bug in the handler etc.). 
                    // 
                    // All other "expected" errors should be caught by the handler and dealt with 
                    // appropriately (maybe send the message to an error queue).

                    CloseModelAndConnection(ref model, ref consumer);
                    Wait();
                }
            }

            CloseModelAndConnection(ref model, ref consumer);
            _stoppingSignal.Set();
        }

        private void Wait()
        {
            for (var i = 0; _running && i < _messageRetryTimeMs / 100; i++)
            {
                Thread.Sleep(100);
            }
        }

        private static Message CreateMessage(BasicDeliverEventArgs args)
        {
            object ccList = null;
            args.BasicProperties?.Headers?.TryGetValue("CC", out ccList);

            return new Message(
                args.DeliveryTag,
                args.BasicProperties?.Type,
                args.Body.ToArray(),
                args.RoutingKey,
                ccList as List<object>);
        }
    }
}
