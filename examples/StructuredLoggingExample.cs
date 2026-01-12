using System;
using System.Text.Json;

namespace Consumer.Examples
{
    /// <summary>
    /// Example: How to add structured logging (Stretch Phase requirement)
    /// This replaces simple Console.WriteLine with JSON-formatted logs
    /// </summary>
    public class StructuredLoggingExample
    {
        // Simple helper method for structured logging
        private static void LogStructured(string level, string eventType, object data)
        {
            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                level = level,
                eventType = eventType,
                application = "eventhub-consumer",
                data = data
            };
            
            // Output as single-line JSON (easy to parse!)
            Console.WriteLine(JsonSerializer.Serialize(logEntry));
        }

        public static void ExampleUsage()
        {
            // Instead of:
            // Console.WriteLine($"✓ Received: {messageId}");
            
            // Use structured logging:
            LogStructured("INFO", "message_received", new 
            { 
                messageId = "810",
                source = "EventHub",
                partition = "0"
            });

            // Instead of:
            // Console.WriteLine($"✓ Stored to Blob: {blobName}");
            
            // Use structured logging:
            LogStructured("INFO", "blob_stored", new 
            { 
                messageId = "810",
                blobName = "eventhub/2026-01-05/810_abc123.json",
                container = "processed-messages"
            });

            // For errors:
            LogStructured("ERROR", "processing_failed", new 
            { 
                messageId = "123",
                error = "Connection timeout",
                retryAttempt = 1
            });
        }

        /* OUTPUT (easy to parse and query!):
        
        {"timestamp":"2026-01-05T02:59:19.123Z","level":"INFO","eventType":"message_received","application":"eventhub-consumer","data":{"messageId":"810","source":"EventHub","partition":"0"}}
        {"timestamp":"2026-01-05T02:59:19.456Z","level":"INFO","eventType":"blob_stored","application":"eventhub-consumer","data":{"messageId":"810","blobName":"eventhub/2026-01-05/810_abc123.json","container":"processed-messages"}}
        
        Benefits:
        - Still logs to console (kubectl logs works!)
        - Still visible in Azure Portal
        - Machine-readable (JSON)
        - Easy to filter: grep '"messageId":"810"'
        - Works with Log Analytics / Application Insights
        - Satisfies Stretch Phase requirement! ✅
        */
    }
}

