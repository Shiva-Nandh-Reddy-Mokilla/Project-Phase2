using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

Console.WriteLine("Event Hubs Consumer Starting (Phase 2)...");
Console.WriteLine();

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// Event Hub configuration
var eventHubConnectionString = configuration["EventHub:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");
var eventHubName = configuration["EventHub:EventHubName"]
    ?? Environment.GetEnvironmentVariable("EVENTHUB_NAME");
var consumerGroup = configuration["EventHub:ConsumerGroup"] ?? "$Default";

// Storage configuration (for checkpoints and processed messages)
var storageConnectionString = configuration["Storage:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
var blobContainerName = configuration["Storage:ContainerName"] ?? "processed-messages";
var checkpointContainerName = "checkpoints";

// Service Bus configuration
var serviceBusConnectionString = configuration["ServiceBus:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");
var serviceBusQueueName = configuration["ServiceBus:QueueName"] ?? "retry-queue";

// Validate configuration
if (string.IsNullOrEmpty(eventHubConnectionString) || string.IsNullOrEmpty(eventHubName))
{
    Console.WriteLine("ERROR: Missing Event Hub configuration");
    return;
}
if (string.IsNullOrEmpty(storageConnectionString))
{
    Console.WriteLine("ERROR: Missing Storage configuration");
    return;
}
if (string.IsNullOrEmpty(serviceBusConnectionString))
{
    Console.WriteLine("ERROR: Missing Service Bus configuration");
    return;
}

Console.WriteLine($"Event Hub: {eventHubName}");
Console.WriteLine($"Storage Container: {blobContainerName}");
Console.WriteLine($"Service Bus Queue: {serviceBusQueueName}");
Console.WriteLine();

// Create clients
var blobContainerClient = new BlobContainerClient(storageConnectionString, blobContainerName);
await blobContainerClient.CreateIfNotExistsAsync();

var checkpointBlobClient = new BlobContainerClient(storageConnectionString, checkpointContainerName);
await checkpointBlobClient.CreateIfNotExistsAsync();

var serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
var serviceBusSender = serviceBusClient.CreateSender(serviceBusQueueName);
var serviceBusProcessor = serviceBusClient.CreateProcessor(serviceBusQueueName, new ServiceBusProcessorOptions());

// Create Event Processor Client (with checkpointing)
var processor = new EventProcessorClient(
    checkpointBlobClient,
    consumerGroup,
    eventHubConnectionString,
    eventHubName);

// Setup cancellation for graceful shutdown
var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, args) =>
{
    args.Cancel = true;
    cancellationSource.Cancel();
    Console.WriteLine();
    Console.WriteLine("Shutting down...");
};

// Event Hub message handler
processor.ProcessEventAsync += async (args) =>
{
    try
    {
        if (args.Data == null)
            return;

        var messageBody = Encoding.UTF8.GetString(args.Data.EventBody.ToArray());
        var partition = args.Partition.PartitionId;

        // Parse message
        string messageId = "N/A";
        string timestamp = "N/A";
        bool shouldRetry = false;

        try
        {
            var json = JsonDocument.Parse(messageBody);
            if (json.RootElement.TryGetProperty("MessageId", out var idProp))
                messageId = idProp.GetString() ?? "N/A";
            if (json.RootElement.TryGetProperty("Timestamp", out var tsProp))
                timestamp = tsProp.GetString() ?? "N/A";
            if (json.RootElement.TryGetProperty("ShouldRetry", out var retryProp))
                shouldRetry = retryProp.GetBoolean();
        }
        catch { }

        Console.WriteLine("========================================");
        Console.WriteLine("EVENT HUB MESSAGE RECEIVED");
        Console.WriteLine($"  Partition:   {partition}");
        Console.WriteLine($"  Message ID:  {messageId}");
        Console.WriteLine($"  Timestamp:   {timestamp}");
        Console.WriteLine($"  Body:        {messageBody}");
        Console.WriteLine("========================================");

        // Store to Blob Storage
        var blobName = $"eventhub/{DateTime.UtcNow:yyyy-MM-dd}/{messageId}_{Guid.NewGuid()}.json";
        var blobClient = blobContainerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(new BinaryData(messageBody), overwrite: true);
        Console.WriteLine($"✓ Stored to Blob: {blobName}");

        // Decide if message should go to Service Bus
        // Rule: Forward if ShouldRetry=true OR messageId is divisible by 5
        bool forwardToServiceBus = shouldRetry;
        if (!string.IsNullOrEmpty(messageId) && int.TryParse(messageId, out int idNum))
        {
            if (idNum % 5 == 0)
                forwardToServiceBus = true;
        }

        if (forwardToServiceBus)
        {
            var sbMessage = new ServiceBusMessage(messageBody);
            await serviceBusSender.SendMessageAsync(sbMessage);
            Console.WriteLine($"✓ Forwarded to Service Bus");
        }

        Console.WriteLine();

        // Checkpoint (mark message as processed)
        await args.UpdateCheckpointAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR processing Event Hub message: {ex.Message}");
    }
};

// Event Hub error handler
processor.ProcessErrorAsync += (args) =>
{
    Console.WriteLine($"ERROR: {args.Exception.Message}");
    return Task.CompletedTask;
};

// Service Bus message handler
serviceBusProcessor.ProcessMessageAsync += async (args) =>
{
    try
    {
        var messageBody = args.Message.Body.ToString();

        // Parse message
        string messageId = "N/A";
        try
        {
            var json = JsonDocument.Parse(messageBody);
            if (json.RootElement.TryGetProperty("MessageId", out var idProp))
                messageId = idProp.GetString() ?? "N/A";
        }
        catch { }

        Console.WriteLine("========================================");
        Console.WriteLine("SERVICE BUS MESSAGE RECEIVED");
        Console.WriteLine($"  Message ID:  {messageId}");
        Console.WriteLine($"  Body:        {messageBody}");
        Console.WriteLine("========================================");

        // Store to Blob Storage with different prefix
        var blobName = $"servicebus/{DateTime.UtcNow:yyyy-MM-dd}/{messageId}_{Guid.NewGuid()}.json";
        var blobClient = blobContainerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(new BinaryData(messageBody), overwrite: true);
        Console.WriteLine($"✓ Stored to Blob: {blobName}");
        Console.WriteLine();

        // Complete the message
        await args.CompleteMessageAsync(args.Message);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR processing Service Bus message: {ex.Message}");
    }
};

// Service Bus error handler
serviceBusProcessor.ProcessErrorAsync += (args) =>
{
    Console.WriteLine($"ERROR: {args.Exception.Message}");
    return Task.CompletedTask;
};

// Start processing
Console.WriteLine("Starting Event Hub processor...");
await processor.StartProcessingAsync(cancellationSource.Token);
Console.WriteLine("Event Hub processor started");

Console.WriteLine("Starting Service Bus processor...");
await serviceBusProcessor.StartProcessingAsync(cancellationSource.Token);
Console.WriteLine("Service Bus processor started");

Console.WriteLine();
Console.WriteLine("Listening for messages... (Press Ctrl+C to stop)");
Console.WriteLine();

// Wait until cancelled
try
{
    await Task.Delay(Timeout.Infinite, cancellationSource.Token);
}
catch (TaskCanceledException)
{
    // Expected when Ctrl+C is pressed
}

// Cleanup
Console.WriteLine("Stopping processors...");
await processor.StopProcessingAsync();
await serviceBusProcessor.StopProcessingAsync();
await serviceBusSender.DisposeAsync();
await serviceBusClient.DisposeAsync();
Console.WriteLine("Consumer stopped");
