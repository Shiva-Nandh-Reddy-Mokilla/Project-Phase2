using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Extensions.Configuration;
using System.Text;

Console.WriteLine("Event Hubs Consumer Starting...");
Console.WriteLine();

// Load configuration from appsettings.json and environment variables
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Get Event Hub connection details
var connectionString = configuration["EventHub:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");

var eventHubName = configuration["EventHub:EventHubName"]
    ?? Environment.GetEnvironmentVariable("EVENTHUB_NAME");

var consumerGroup = configuration["EventHub:ConsumerGroup"] ?? "$Default";

if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(eventHubName))
{
    Console.WriteLine("ERROR: Missing configuration. Set EVENTHUB_CONNECTION_STRING and EVENTHUB_NAME");
    return;
}

Console.WriteLine($"Event Hub: {eventHubName}");
Console.WriteLine($"Consumer Group: {consumerGroup}");
Console.WriteLine();

// Create consumer client to read messages from Event Hubs
await using var consumer = new EventHubConsumerClient(
    consumerGroup,
    connectionString,
    eventHubName);

Console.WriteLine("Connected to Event Hub");
Console.WriteLine("Listening for messages... (Press Ctrl+C to stop)");
Console.WriteLine();

try
{
    // Get all partitions in the Event Hub
    string[] partitionIds = await consumer.GetPartitionIdsAsync();
    Console.WriteLine($"Event Hub has {partitionIds.Length} partition(s)");
    Console.WriteLine();

    // Setup Ctrl+C handler for graceful shutdown
    var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, args) =>
    {
        args.Cancel = true;
        cancellationSource.Cancel();
        Console.WriteLine();
        Console.WriteLine("Shutting down...");
    };

    // Read from all partitions at the same time
    var readTasks = partitionIds.Select(partitionId =>
        ReadPartitionAsync(consumer, partitionId, cancellationSource.Token));

    await Task.WhenAll(readTasks);
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}

Console.WriteLine("Consumer stopped");

// Reads messages from a single partition
static async Task ReadPartitionAsync(
    EventHubConsumerClient consumer,
    string partitionId,
    CancellationToken cancellationToken)
{
    try
    {
        // Read events from this partition (starts from latest messages)
        await foreach (var partitionEvent in consumer.ReadEventsFromPartitionAsync(
            partitionId,
            Azure.Messaging.EventHubs.Consumer.EventPosition.Latest,
            cancellationToken))
        {
            if (partitionEvent.Data == null)
                continue;

            // Get message body
            var messageBody = Encoding.UTF8.GetString(partitionEvent.Data.EventBody.ToArray());

            // Try to parse message properties if it's JSON
            string messageId = "N/A";
            string timestamp = "N/A";

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(messageBody);
                if (json.RootElement.TryGetProperty("MessageId", out var idProp))
                    messageId = idProp.GetString() ?? "N/A";
                if (json.RootElement.TryGetProperty("Timestamp", out var tsProp))
                    timestamp = tsProp.GetString() ?? "N/A";
            }
            catch
            {
            }

            // Log the received message
            Console.WriteLine("========================================");
            Console.WriteLine("RECEIVED MESSAGE");
            Console.WriteLine($"  Partition:   {partitionId}");
            Console.WriteLine($"  Message ID:  {messageId}");
            Console.WriteLine($"  Timestamp:   {timestamp}");
            Console.WriteLine($"  Body:        {messageBody}");
            Console.WriteLine("========================================");
            Console.WriteLine();
        }
    }
    catch (TaskCanceledException)
    {
        // Expected when Ctrl+C is pressed
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR reading from partition {partitionId}: {ex.Message}");
    }
}
