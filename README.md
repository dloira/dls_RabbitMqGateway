# DotNet RabbitMQ gateway

Here, you could find an approach to use the Observer pattern for handling the official RabbitMQ client. Up and running basic dispatcher and messages consumer is not a really headache following the official guidelines, squeezing the broker features without code coupled is another.

To make that easier, RabbitMQ setup is provided using docker containers technology.

The code was built in .NET Core 6.0 using the official .NET client provided by RabbitMQ and distributed via nuget.

## Getting Started

The idea behind is to get an event manager (MessageConsumer) receiving messages from the broker and sending notification to the objects subscribed (Program.consumer). The Observer pattern make easier to perform different actions in response to notifications issued by the event manager and not to be coupled to concrete listener (Program.consumer.MessageReceived delegate method).

Take a look for a while in this GoF observer pattern link for begginers https://refactoring.guru/design-patterns/observer

Out of the box .NET gives an event solution based on the delegate model. The delegate model follows the observer design pattern, which enables a subscriber to register with and receive notifications from a provider. 

Please, find below the concept diagram for overview better understanding.

![Concept diagram](https://github.com/dloira/dls_RabbitMqGateway/blob/master/concept_diagram.jpg)

### Delegates and Events

A delegate is a type that holds a reference to a method and is declared with a signature that shows the return type and parameters for the methods it references, and it can hold references only to methods that match its signature. A delegate is thus equivalent to a type-safe function pointer or a callback. In a nutshell, delegates allow functions to be passed as parameters, returned from a function as a value and stored in an array.

Each time a new message arrives into the MessageConsumer an event is triggered. To respond to the events, several event handler methods are defined matching the signature of the delegate for each concrete event.

Drilling down the code two interfaces arise with delegate and event definition: IMessageBrokerClient and IMessageConsumer. The first one focus on server connection state and the second one on messages data. Please, find below the most relevant piece of code from both:

```
/// <summary>
/// Handler for changes of connection state
/// </summary>
public delegate void ConnectionStateChangedHandler(IMessageBrokerClient source, ConnectionState state, string reason);

public interface IMessageBrokerClient
{
    /// <summary>
    /// Fired whan connection state changes (Disconnected, Connected, Blocked)
    /// </summary>
    event ConnectionStateChangedHandler ConnectionStateChanged;
}
```

```
/// <summary>
/// Handler for message received event
/// </summary>
public delegate void MessageReceivedHandler(IMessageConsumer source, Message msg);

public interface IMessageConsumer : IMessageBrokerClient, IDisposable
{
    /// <summary>
    /// Fired when a message is received.
    /// </summary>
    event MessageReceivedHandler MessageReceived;
}
```

The events will be fired from MessageConsumer within EventFiringThreadHandler method.

```
MessageReceived?.Invoke(this, currentMessage);

MessageBatchReceived?.Invoke(this, messageBatch);
```

The event handler method assigned to the MessageConsumer object in Program class will be executed as soon as a new event arrives.

```
using (var consumer = builder
    .WithExchange(conf["exchangeName"], true, ExchangeType.Fanout)
    .Consumer(conf["queueName"])
    .WithQueueLimit(1000000, OverflowPolicy.DropHead)
    .WithMessageRetryTime(2)
    .Build())
{
    consumer.MessageReceived += ConsumerOnMessageReceived;
    consumer.Start();
}

private static void ConsumerOnMessageReceived(IMessageConsumer source, Message msg)
{
    Console.WriteLine(
        "Message received: " +
        "delivery time: " + DateTime.Now.ToString() + " " +
        $"RoutingKey={msg.RoutingKey}, " +
        $"CcList={msg.CcList?.Aggregate("", (seed, current) => seed + "," + current).Substring(1)}, " +
        $"Body={msg.BodyAsString()}"
    );
}
```

### Openning connection

Working with RabbitMQ requires an openned connection, otherwise, any further action will be refused; regarding this principle, the main aim of the code built is to get a connection ready for when it is needed, dealing automatically with creation, retries, shut downs and so on.

The MessageBrokerConnector class manages the connection within EnsureConnectionExists method, where the driver ConnectionFactory is used.

```
protected void EnsureConnectionExists()
{
    Connection = _connectionFactory.CreateConnection(_hostNames);
    Connection.CallbackException += ConnectionOnCallbackException;
    Connection.ConnectionBlocked += ConnectionOnConnectionBlocked;
    Connection.ConnectionUnblocked += ConnectionOnConnectionUnblocked;
    Connection.ConnectionShutdown += ConnectionOnConnectionShutdown;
 }
```

### Producing messages

In RabbitMQ a message can never be sent directly to the queue, it always needs to go through an exchange. But let's not get dragged down by the details ‒ you can read more about exchanges in the official link https://www.rabbitmq.com/tutorials/tutorial-three-dotnet.html.

The Program class is the entry point for publisher object creation and where the messages will fire from.

```
var builder = RabbitMqServerBuilder.Server(conf["hostName"], conf["virtualHost"], conf["userName"], conf["password"], Int32.Parse(conf["portNumber"]));

var publisher = builder
            .WithExchange(conf["exchangeName"], false, ExchangeType.Fanout)
            .Publisher()
            .Build())

publisher.PublishMessages(messages);
```

The MessagePublisher class contains the PublishMessages method where the driver IModel is used.

```
public void PublishMessages(IEnumerable<Message> msgs, int timeoutMs)
{
    _model.BasicPublish(ExchangeName,
        msg.RoutingKey,
        basicProperties,
        msg.Body);
}
```

### Consuming messages

As for the consumer, it is listening for messages from RabbitMQ. So unlike the publisher which publishes a single message, the Program class will keep the consumer running continuously to listen for messages and print them out.

```
using (var consumer = builder
    .WithExchange(conf["exchangeName"], true, ExchangeType.Fanout)
    .Consumer(conf["queueName"])
    .WithQueueLimit(1000000, OverflowPolicy.DropHead)
    .WithMessageRetryTime(2)
    .Build())
{
    consumer.MessageReceived += ConsumerOnMessageReceived;
    consumer.MessageError += ConsumerOnMessageError;
    consumer.ConnectionStateChanged += ConsumerOnConnectionStateChanged;
    consumer.ConnectionRecoveryError += ConsumerOnConnectionRecoveryError;
    consumer.CallbackException += ConsumerOnCallbackException;
    consumer.Start();

}
```

The Start method in MessageConsumer class fires a new thread executing EventFiringThreadHandler logic; getting dragged down, the algorithm holds an infinite loop where message arriving is stored temporary in a memory queue for later event firing following the individual or batch set up. Finally, the messages ACK will be sent to the server.

REMARK: The method ConsumerOnReceived is executed every time a new message is comming from driver within an event envelope. 

```
private ConcurrentQueue<Message> _messages;

public void Start()
{
    _eventFiringThread = new Thread(EventFiringThreadHandler) { IsBackground = true };
    _eventFiringThread.Start();
}

private void EventFiringThreadHandler()
{
    while (_running)
    {
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
}

private bool TryCreateModel(ref IModel model, ref EventingBasicConsumer consumer)
{
    consumer = new EventingBasicConsumer(model);
    consumer.Received += ConsumerOnReceived;
}

private void ConsumerOnReceived(object sender, BasicDeliverEventArgs args)
{
    _messages.Enqueue(CreateMessage(args));
    _messageArrivedSignal.Set();
}
```

### RabbitMQ with Docker Compose

Docker Compose tool was used to provide an easy way to get up a local RabbitMQ server where to run the code for dispatch and consume messages.

The docker-compose.yml file, within docker folder, downloads the official docker image with management plugin installed https://hub.docker.com/_/rabbitmq (Alpine Linux version was used to be as much smaller as possible); a couple of environment settings were also needed to get an administrator credentials.

```
version: "3.6"

networks:
  rabbitmq_net:
    name: rabbitmq_net
    driver: bridge
      
services:
  rabbitmq:
    hostname: rabbitmq
    container_name: rabbitmq
    image: rabbitmq:3.11.5-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    networks:
      - rabbitmq_net
    environment:
      - RABBITMQ_DEFAULT_USER=admin
      - RABBITMQ_DEFAULT_PASS=admin
      - RABBITMQ_DEFAULT_VHOST=test-vhost
```

Whether the exchange and/or queue does not exist when the Gateway code runs, it will be created automatically from scratch with the proper config settings. However, it is mandatory to get ready the virtual host, otherwise an exception will be found; to avoid the crashing, the docker-compose.yml setups also the default virtual host used by testing project.

Once you get installed Docker Desktop locally https://www.docker.com/products/docker-desktop/, to run the docker compose file it is only needed to run the terminal prompt, place the path where the file is and execute the following command.

```
docker-compose -f docker-compose.yaml up
```

## Running the tests

The code could be downloaded and executed with Visual Studio 2022; just recompile the solution for downloading dependencies and enable the dls_RabbitMqGateway_Test project as the starting project.

The config parameters are setup in appsettings.json file; it is not needed to change anything inside to run the test; by the way, feel free to configure wathever you like there. 

Once the project is up and running, the command promt window arises logging the connection state; when the server is ready and the code could reached it, a message is dispatched and consumed automatically, printing the console with the message data.

```
Connecting:
Connected: Ready for messaging...
Message received: delivery time: 27/12/2022 14:39:51 RoutingKey=dls.hello, CcList=dispatch time: 26/12/2022 19:41:17, Body=hello_world 0
```

The window will keep openned and every time the Enter key is pushed a new message will be dispatched and consumed, printing a new log trace with message data.

```
Published 1 messages.
Message received: delivery time: 27/12/2022 14:49:23 RoutingKey=dls.hello, CcList=dispatch time: 27/12/2022 14:49:23, Body=hello_world 0
```

## Built With

* [.Net-6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) - The .Net toolkit framework
* [RabbitMQ.Client-6.4.0](https://www.rabbitmq.com/dotnet.html) - RabbitMQ driver
* [VisualStudio-22](https://visualstudio.microsoft.com/es/vs/community/) - IDE
* [Docker-20.10.13](https://www.docker.com/) - Containers
* [Microsoft.Extensions.Configuration-7.0.0](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration?view=dotnet-plat-ext-7.0) - Nuget package for setting up key/value application configuration properties 
* [Microsoft.Extensions.Configuration.Json-7.0.0](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration.json?view=dotnet-plat-ext-7.0) - Nuget package for getting up configuration data from JSON file

## Versioning

I use [SemVer](http://semver.org/) for versioning. For the versions available, see the tags on this repository. 
