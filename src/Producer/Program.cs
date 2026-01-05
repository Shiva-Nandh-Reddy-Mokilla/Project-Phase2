using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EventHubProducer;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Event Hubs Producer Starting...");
        Console.WriteLine();

        // Load configuration from appsettings.json and environment variables
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get Event Hub connection details
        var connectionString = configuration["EventHub:ConnectionString"] 
            ?? Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");
        
        var eventHubName = configuration["EventHub:EventHubName"] 
            ?? Environment.GetEnvironmentVariable("EVENTHUB_NAME");

        if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(eventHubName))
        {
            Console.WriteLine("ERROR: Missing configuration. Set EVENTHUB_CONNECTION_STRING and EVENTHUB_NAME");
            return;
        }

        // Create producer client to send messages to Event Hubs
        await using var producerClient = new EventHubProducerClient(connectionString, eventHubName);
        Console.WriteLine($"Connected to Event Hub: {eventHubName}");
        Console.WriteLine();

        // Send messages continuously
        Console.WriteLine("Sending messages... (Press Ctrl+C to stop)");
        Console.WriteLine();

        int messagesSent = 0;
        while (true)
        {
            try
            {
                // Generate a random test message
                var message = MessageGenerator.GenerateMessage();

                // Create a batch (Event Hubs requires batches)
                using EventDataBatch eventBatch = await producerClient.CreateBatchAsync();
                
                // Serialize message to JSON
                var jsonMessage = JsonSerializer.Serialize(message);
                var eventData = new EventData(jsonMessage);
                
                // Add message to batch
                if (!eventBatch.TryAdd(eventData))
                {
                    Console.WriteLine("ERROR: Message too large");
                    continue;
                }

                // Send the batch to Event Hubs
                await producerClient.SendAsync(eventBatch);
                
                messagesSent++;
                Console.WriteLine($"[{messagesSent}] Sent: {message.MessageId} at {message.Timestamp}");
                
                // Wait 2 seconds before sending next message
                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                await Task.Delay(5000);
            }
        }
    }
}

// Message structure that gets sent to Event Hubs
public class Message
{
    public string MessageId { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public object Payload { get; set; } = new { };
}
