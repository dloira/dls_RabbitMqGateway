using System.Collections.Generic;
using System;
using System.Linq;

using dls_RabbitMqGateway;
using dls_RabbitMqGateway.Enum;
using dls_RabbitMqGateway.Entities;
using dls_RabbitMqGateway.Interfaces;
using dls_RabbitMqGateway.Impl;
using System.IO;
using Microsoft.Extensions.Configuration;

public class Program
{
    protected static IConfiguration conf = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .Build()
              .GetSection("ServerSettings");

    /// <summary>
    /// Set this project as starting point and Run (F5).
    /// Once the command prompt window opens and the message "Connected: Ready for messaging..." arises, every time the enter key will be pressed a bunch of messages will be sent to the server.
    /// </summary>
    public static int Main(string[] args)
    {
        var builder = RabbitMqServerBuilder.Server(conf["hostName"], conf["virtualHost"], conf["userName"], conf["password"], Int32.Parse(conf["portNumber"]));

        using (var consumer = builder
            .WithExchange(conf["exchangeName"], true, ExchangeType.Fanout)
            .Consumer(conf["queueName"])
            .WithQueueLimit(1000000, OverflowPolicy.DropHead)
            .WithMessageRetryTime(2)
            //.WithBatchProcessing(true)
            //.WithPrefetchCount(5)
            .Build())
        {
            consumer.MessageReceived += ConsumerOnMessageReceived;
            consumer.MessageError += ConsumerOnMessageError;
            //consumer.MessageBatchReceived += ConsumerOnMessageBatchReceived;
            //consumer.MessageBatchError += ConsumerOnMessageBatchError;
            consumer.ConnectionStateChanged += ConsumerOnConnectionStateChanged;
            consumer.ConnectionRecoveryError += ConsumerOnConnectionRecoveryError;
            consumer.CallbackException += ConsumerOnCallbackException;
            consumer.Start();

            PublishTestMessages(builder, 1);

        }

        return 0;
    }

    private static void PublishTestMessages(RabbitMqServerBuilder builder, int bunchSize)
    {
        using (var publisher = builder
            .WithExchange(conf["exchangeName"], false, ExchangeType.Fanout)
            .Publisher()
            .Build())
        {
            while (Console.ReadLine() != "end")
            {
                try
                {
                    var messages = new Message[bunchSize];

                    for (var i = 0; i < messages.Length; i++)
                    {
                        messages[i] = new Message("hello", "hello_world "+i, "dls.hello", new string[] { "dispatch time: " + DateTime.Now.ToString() });
                    }

                    Console.WriteLine($"Published {messages.Length} messages.");
                    publisher.PublishMessages(messages);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to publish messages. Error message: {ex.Message}");
                }
            }
        }
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

    private static void ConsumerOnMessageError(IMessageConsumer source, Message msg, Exception exception)
    {
        Console.WriteLine($"Message error: RoutingKey={msg.RoutingKey}, Body={msg.BodyAsString()}, Error={exception.Message}");
    }

    private static void ConsumerOnMessageBatchReceived(IMessageConsumer source, IReadOnlyList<Message> msgBatch)
    {
        foreach (var msg in msgBatch)
        {
            Console.WriteLine(
                "Message received: " +
                $"RoutingKey={msg.RoutingKey}, " +
                $"CcList={msg.CcList?.Aggregate("", (seed, current) => seed + "," + current).Substring(1)}, " +
                $"Body={msg.Body}"
            );
        }
    }

    private static void ConsumerOnMessageBatchError(IMessageConsumer source, IReadOnlyList<Message> msgBatch, Exception exception)
    {
        var msg = msgBatch.FirstOrDefault();
        Console.WriteLine($"Message error: RoutingKey={msg?.RoutingKey}, Body={msg?.Body}, Error={exception.Message}");
    }

    private static void ConsumerOnConnectionStateChanged(IMessageBrokerConnector source, ConnectionState state, string reason)
    {
        Console.WriteLine($"{state}: {reason}");
    }

    private static void ConsumerOnConnectionRecoveryError(IMessageBrokerConnector source, Exception exception)
    {
        Console.WriteLine($"Connection recovery error: {exception?.Message}");
    }

    private static void ConsumerOnCallbackException(IMessageBrokerConnector source, Exception exception)
    {
        Console.WriteLine(exception.Message);
    }
}