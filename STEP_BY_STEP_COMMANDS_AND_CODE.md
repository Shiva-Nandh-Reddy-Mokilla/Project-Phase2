# Step-by-Step: Terminal Commands + Code Flow

This document shows **EXACTLY** what commands you ran, what they created, and what code uses them.

---

## Table of Contents
1. [Azure Resources Setup](#1-azure-resources-setup)
2. [Get Connection Strings](#2-get-connection-strings)
3. [Encode Connection Strings](#3-encode-connection-strings)
4. [Create Kubernetes Secrets](#4-create-kubernetes-secrets)
5. [Deploy to AKS](#5-deploy-to-aks)
6. [Run Producer](#6-run-producer)
7. [Complete Flow](#7-complete-flow)

---

## 1. Azure Resources Setup

### What You Created in Azure Portal:

#### Event Hubs (Already existed from Phase 1)
```
Resource: MyNameSpace-1
Event Hub: my-event-hub
Policy: phase1-policy
```

#### Storage Account (Phase 2 - New)
```
Portal Steps:
1. Create storage account → shivastorage123
2. Create container → processed-messages
3. Create container → checkpoints
```

**No terminal command - done in Portal UI**

#### Service Bus (Phase 2 - New)
```
Portal Steps:
1. Create Service Bus namespace → shivaservicebus123
2. Create queue → retry-queue
```

**No terminal command - done in Portal UI**

---

## 2. Get Connection Strings

### Terminal Commands to View Connection Strings:

#### Event Hubs Connection String
```bash
# In Azure Portal → MyNameSpace-1 → Shared access policies → phase1-policy
# Click "Show" and copy Primary Connection String

# Result:
Endpoint=sb://mynamespace-1.servicebus.windows.net/;SharedAccessKeyName=phase1-policy;SharedAccessKey=S3AHvrmH15wsumbx+L+ZWBeKFJV062YK4+AEhDoHt2U=
```

#### Storage Connection String
```bash
# In Azure Portal → shivastorage123 → Access keys → key1
# Click "Show" and copy Connection string

# Result:
DefaultEndpointsProtocol=https;AccountName=shivastorage123;AccountKey=XfS4862QwFKYWQfWZJt5+Dv3GxLO2ZpQSr2+0UPLljOeJMMOyAKk/TtYKxCvmUsgPKhcyl8RNELi+AStjRy87w==;EndpointSuffix=core.windows.net
```

#### Service Bus Connection String
```bash
# In Azure Portal → shivaservicebus123 → Shared access policies → RootManageSharedAccessKey
# Click "Show" and copy Primary Connection String

# Result:
Endpoint=sb://shivaservicebus123.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=oGJBGi1m4P4oAWAog8NZKhGL+iScE2ItQ+ASbDDkDTM=
```

---

## 3. Encode Connection Strings

### Why?
Kubernetes Secrets require base64-encoded data.

### Terminal Commands You Ran:

#### Encode Event Hub Connection String
```bash
echo -n "Endpoint=sb://mynamespace-1.servicebus.windows.net/;SharedAccessKeyName=phase1-policy;SharedAccessKey=S3AHvrmH15wsumbx+L+ZWBeKFJV062YK4+AEhDoHt2U=" | base64

# Output:
RW5kcG9pbnQ9c2I6Ly9teW5hbWVzcGFjZS0xLnNlcnZpY2VidXMud2luZG93cy5uZXQvO1NoYXJlZEFjY2Vzc0tleU5hbWU9cGhhc2UxLXBvbGljeTtTaGFyZWRBY2Nlc3NLZXk9UzNBSHZybUgxNXdzdW1ieCtMK1pXQmVLRkpWMDYyWUs0K0FFaERvSHQyVT0=
```

#### Encode Storage Connection String
```bash
echo -n "DefaultEndpointsProtocol=https;AccountName=shivastorage123;AccountKey=XfS4862QwFKYWQfWZJt5+Dv3GxLO2ZpQSr2+0UPLljOeJMMOyAKk/TtYKxCvmUsgPKhcyl8RNELi+AStjRy87w==;EndpointSuffix=core.windows.net" | base64

# Output:
RGVmYXVsdEVuZHBvaW50c1Byb3RvY29sPWh0dHBzO0FjY291bnROYW1lPXNoaXZhc3RvcmFnZTEyMztBY2NvdW50S2V5PVhmUzQ4NjJRd0ZLWVdRZldaSnQ1K0R2M0d4TE8yWnBRU3IyKzBVUExsak9lSk1NT3lBS2svVHRZS3hDdm1Vc2dQS2hjeWw4Uk5FTGkrQVN0alJ5ODd3PT07RW5kcG9pbnRTdWZmaXg9Y29yZS53aW5kb3dzLm5ldA==
```

#### Encode Service Bus Connection String
```bash
echo -n "Endpoint=sb://shivaservicebus123.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=oGJBGi1m4P4oAWAog8NZKhGL+iScE2ItQ+ASbDDkDTM=" | base64

# Output:
RW5kcG9pbnQ9c2I6Ly9zaGl2YXNlcnZpY2VidXMxMjMuc2VydmljZWJ1cy53aW5kb3dzLm5ldC87U2hhcmVkQWNjZXNzS2V5TmFtZT1Sb290TWFuYWdlU2hhcmVkQWNjZXNzS2V5O1NoYXJlZEFjY2Vzc0tleT1vR0pCR2kxbTRQNG9BV0FvZzhOWktoR0wraVNjRTJJdFErQVNiRERrRFRNPQ==
```

### What This Creates:
Base64-encoded strings ready for Kubernetes Secret

---

## 4. Create Kubernetes Secrets

### File: `k8s/secret.yaml`

#### Step 1: Copy Template
```bash
cp k8s/secret.yaml.template k8s/secret.yaml
```

**What this does:** Creates a copy you can edit with real secrets

#### Step 2: Edit with Base64 Values
```yaml
# k8s/secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: azure-secrets
  namespace: default
type: Opaque
data:
  eventhub-connection: RW5kcG9pbnQ9c2I6Ly9teW5hbWVzcGFjZS0xLnNlcnZpY2VidXMud2luZG93cy5uZXQvO1NoYXJlZEFjY2Vzc0tleU5hbWU9cGhhc2UxLXBvbGljeTtTaGFyZWRBY2Nlc3NLZXk9UzNBSHZybUgxNXdzdW1ieCtMK1pXQmVLRkpWMDYyWUs0K0FFaERvSHQyVT0=
  storage-connection: RGVmYXVsdEVuZHBvaW50c1Byb3RvY29sPWh0dHBzO0FjY291bnROYW1lPXNoaXZhc3RvcmFnZTEyMztBY2NvdW50S2V5PVhmUzQ4NjJRd0ZLWVdRZldaSnQ1K0R2M0d4TE8yWnBRU3IyKzBVUExsak9lSk1NT3lBS2svVHRZS3hDdm1Vc2dQS2hjeWw4Uk5FTGkrQVN0alJ5ODd3PT07RW5kcG9pbnRTdWZmaXg9Y29yZS53aW5kb3dzLm5ldA==
  servicebus-connection: RW5kcG9pbnQ9c2I6Ly9zaGl2YXNlcnZpY2VidXMxMjMuc2VydmljZWJ1cy53aW5kb3dzLm5ldC87U2hhcmVkQWNjZXNzS2V5TmFtZT1Sb290TWFuYWdlU2hhcmVkQWNjZXNzS2V5O1NoYXJlZEFjY2Vzc0tleT1vR0pCR2kxbTRQNG9BV0FvZzhOWktoR0wraVNjRTJJdFErQVNiRERrRFRNPQ==
```

**What this file contains:** Your 3 connection strings (base64 encoded)

#### Step 3: Apply Secret to Kubernetes
```bash
kubectl apply -f k8s/secret.yaml
```

**Output:**
```
secret/azure-secrets configured
```

**What this does:**
- Uploads secret to Kubernetes cluster
- Stores it securely in etcd database
- Makes it available to pods

#### Verify Secret Was Created
```bash
kubectl get secrets
```

**Output:**
```
NAME            TYPE     DATA   AGE
azure-secrets   Opaque   3      5m
```

#### (Optional) View Secret Contents
```bash
# View secret (shows base64)
kubectl get secret azure-secrets -o yaml

# Decode a specific value
kubectl get secret azure-secrets -o jsonpath='{.data.eventhub-connection}' | base64 --decode
```

**Output:** Shows your original connection string (decoded)

---

## 5. Deploy to AKS

### File: `k8s/configmap.yaml`

#### Apply ConfigMap
```bash
kubectl apply -f k8s/configmap.yaml
```

**Output:**
```
configmap/consumer-config configured
```

**What this file contains:**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: consumer-config
data:
  EVENTHUB_NAME: "my-event-hub"
  STORAGE_CONTAINER: "processed-messages"
  SERVICEBUS_QUEUE: "retry-queue"
```

**What this does:** Stores non-sensitive config that pods can read

---

### File: `k8s/deployment.yaml`

#### Apply Deployment
```bash
kubectl apply -f k8s/deployment.yaml
```

**Output:**
```
deployment.apps/eventhub-consumer configured
```

**What this file contains:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: eventhub-consumer
spec:
  replicas: 1
  template:
    spec:
      containers:
      - name: consumer
        image: shivaacr123.azurecr.io/eventhub-consumer:v2
        env:
        - name: EVENTHUB_NAME
          valueFrom:
            configMapKeyRef:
              name: consumer-config
              key: EVENTHUB_NAME
        - name: EVENTHUB_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: azure-secrets
              key: eventhub-connection
        - name: STORAGE_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: azure-secrets
              key: storage-connection
        - name: SERVICEBUS_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: azure-secrets
              key: servicebus-connection
```

**What this does:**
1. Pulls Docker image from ACR
2. Creates 1 pod
3. Injects environment variables from ConfigMap and Secret
4. Starts consumer app

#### Check Pod Status
```bash
kubectl get pods
```

**Output:**
```
NAME                                 READY   STATUS    RESTARTS   AGE
eventhub-consumer-5c96ff9cd4-7nlk6   1/1     Running   0          30s
```

#### Watch Pod Logs
```bash
kubectl logs -f deployment/eventhub-consumer
```

**Output:**
```
Event Hubs Consumer Starting (Phase 2)...

Event Hub: my-event-hub
Storage Container: processed-messages
Service Bus Queue: retry-queue

Starting Event Hub processor...
Event Hub processor started
Starting Service Bus processor...
Service Bus processor started

Listening for messages...
```

**What this shows:** Consumer successfully connected to all services using the secrets

---

## 6. Run Producer

### Set Environment Variables
```bash
cd /Users/shivanandh/Downloads/ShivaProject-Phase2/src/Producer

export EVENTHUB_CONNECTION_STRING="Endpoint=sb://mynamespace-1.servicebus.windows.net/;SharedAccessKeyName=phase1-policy;SharedAccessKey=S3AHvrmH15wsumbx+L+ZWBeKFJV062YK4+AEhDoHt2U="

export EVENTHUB_NAME="my-event-hub"
```

**What this does:** 
- Sets environment variables for current terminal session
- Producer will read these to connect to Event Hubs

### Run Producer
```bash
dotnet run
```

**Output:**
```
Event Hubs Producer Starting...
Connected to Event Hub: my-event-hub

Sending messages... (Press Ctrl+C to stop)

[1] Sent: 810 at 2026-01-05T02:59:19Z
[2] Sent: 510 at 2026-01-05T02:59:22Z
...
```

**What this does:** Sends messages to Event Hubs

---

## 7. Complete Flow: Commands → Code → Connections

### Flow 1: Producer → Event Hubs

#### You Ran:
```bash
# Set connection string
export EVENTHUB_CONNECTION_STRING="Endpoint=sb://mynamespace-1..."
export EVENTHUB_NAME="my-event-hub"

# Run producer
cd src/Producer
dotnet run
```

#### Code That Uses It:
```csharp
// src/Producer/Program.cs

// Read from environment variable (set by export command)
var connectionString = configuration["EventHub:ConnectionString"] 
    ?? Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");

var eventHubName = configuration["EventHub:EventHubName"] 
    ?? Environment.GetEnvironmentVariable("EVENTHUB_NAME");

// Create client using connection string
await using var producerClient = new EventHubProducerClient(
    connectionString,    // ← Uses your connection string
    eventHubName        // ← Uses "my-event-hub"
);

// Behind the scenes: Azure SDK generates SAS token from connection string
// Sends authenticated HTTP request to Event Hubs
await producerClient.SendAsync(eventBatch);
```

#### What Happens:
```
1. Producer reads EVENTHUB_CONNECTION_STRING from environment
2. EventHubProducerClient uses it to connect
3. Azure SDK extracts:
   - Endpoint: mynamespace-1.servicebus.windows.net
   - KeyName: phase1-policy
   - Key: S3AHvrmH15wsumbx...
4. Generates SAS token (like a temporary password)
5. Sends HTTP request:
   POST https://mynamespace-1.servicebus.windows.net/my-event-hub/messages
   Authorization: SharedAccessSignature sr=...&sig=...&se=...
6. Event Hubs validates token ✅
7. Message accepted
```

---

### Flow 2: Event Hubs → Consumer (in AKS)

#### You Ran:
```bash
# Created secret with base64-encoded connection string
kubectl apply -f k8s/secret.yaml

# Deployed consumer
kubectl apply -f k8s/deployment.yaml
```

#### Kubernetes Does:
```yaml
# k8s/deployment.yaml tells Kubernetes:
env:
- name: EVENTHUB_CONNECTION_STRING
  valueFrom:
    secretKeyRef:
      name: azure-secrets        # ← Read from this secret
      key: eventhub-connection   # ← This specific key
```

**What happens:**
1. Kubernetes reads `azure-secrets` secret
2. Gets value of `eventhub-connection` key (base64 string)
3. Decodes base64 → gets original connection string
4. Sets environment variable in pod: `EVENTHUB_CONNECTION_STRING="Endpoint=sb://..."`

#### Code That Uses It:
```csharp
// src/Consumer/Program.cs

// Read from environment variable (injected by Kubernetes)
var eventHubConnectionString = Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING")
    ?? configuration["EventHub:ConnectionString"];

var storageConnectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING")
    ?? configuration["Storage:ConnectionString"];

// Create blob client for checkpoints
var checkpointBlobClient = new BlobContainerClient(
    storageConnectionString,  // ← Uses storage connection string from K8s secret
    "checkpoints"
);

// Create Event Processor with checkpointing
var processor = new EventProcessorClient(
    checkpointBlobClient,           // ← Where to save progress
    "$Default",
    eventHubConnectionString,       // ← Uses Event Hub connection string from K8s secret
    eventHubName
);

// Set up message handler
processor.ProcessEventAsync += async (args) =>
{
    var messageBody = Encoding.UTF8.GetString(args.Data.EventBody.ToArray());
    Console.WriteLine($"Received: {messageBody}");
    
    // Save checkpoint (to Blob Storage)
    await args.UpdateCheckpointAsync();
};

// Start processing
await processor.StartProcessingAsync();
```

#### What Happens:
```
1. Kubernetes injects environment variables into pod
2. Consumer reads EVENTHUB_CONNECTION_STRING
3. EventProcessorClient connects to Event Hubs
4. Polls for new messages every few seconds
5. When message arrives:
   - Downloads message
   - Calls your handler code
   - Saves checkpoint to Blob Storage (using STORAGE_CONNECTION_STRING)
```

---

### Flow 3: Consumer → Blob Storage

#### You Ran:
```bash
# Created secret with storage connection string (already done in Flow 2)
kubectl apply -f k8s/secret.yaml
```

#### Code That Uses It:
```csharp
// src/Consumer/Program.cs

// Get storage connection string from environment
var storageConnectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
var blobContainerName = "processed-messages";

// Create blob container client
var blobContainerClient = new BlobContainerClient(
    storageConnectionString,  // ← Uses connection string from K8s secret
    blobContainerName
);

// Create container if it doesn't exist
await blobContainerClient.CreateIfNotExistsAsync();

// In message handler:
processor.ProcessEventAsync += async (args) =>
{
    var messageBody = Encoding.UTF8.GetString(args.Data.EventBody.ToArray());
    
    // Create unique filename
    var blobName = $"eventhub/{DateTime.UtcNow:yyyy-MM-dd}/{messageId}_{Guid.NewGuid()}.json";
    
    // Get blob client for this file
    var blobClient = blobContainerClient.GetBlobClient(blobName);
    
    // Upload message as JSON file
    await blobClient.UploadAsync(
        new BinaryData(messageBody),
        overwrite: true
    );
    
    Console.WriteLine($"✓ Stored to Blob: {blobName}");
};
```

#### What Happens:
```
1. Consumer reads STORAGE_CONNECTION_STRING from environment (K8s injected it)
2. BlobContainerClient connects to shivastorage123.blob.core.windows.net
3. Azure SDK extracts account name and key from connection string
4. For each upload:
   - Generates authorization signature using account key
   - Sends HTTP request:
     PUT https://shivastorage123.blob.core.windows.net/processed-messages/eventhub/2026-01-05/810_abc.json
     Authorization: SharedKey shivastorage123:BASE64_SIGNATURE
5. Blob Storage validates signature ✅
6. File saved
```

---

### Flow 4: Consumer → Service Bus → Consumer

#### You Ran:
```bash
# Created secret with Service Bus connection string (already done)
kubectl apply -f k8s/secret.yaml
```

#### Code That Uses It (Send):
```csharp
// src/Consumer/Program.cs

// Get Service Bus connection string from environment
var serviceBusConnectionString = Environment.GetEnvironmentVariable("SERVICEBUS_CONNECTION_STRING");

// Create Service Bus client and sender
var serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
var serviceBusSender = serviceBusClient.CreateSender("retry-queue");

// In Event Hub message handler:
processor.ProcessEventAsync += async (args) =>
{
    // ... process message ...
    
    // Decide if should forward to Service Bus
    if (int.Parse(messageId) % 5 == 0)
    {
        // Create Service Bus message
        var sbMessage = new ServiceBusMessage(messageBody);
        
        // Send to queue
        await serviceBusSender.SendMessageAsync(sbMessage);
        
        Console.WriteLine("✓ Forwarded to Service Bus");
    }
};
```

#### Code That Uses It (Receive):
```csharp
// Same file - Service Bus processor (runs in parallel)

// Create processor to read from queue
var serviceBusProcessor = serviceBusClient.CreateProcessor(
    "retry-queue",
    new ServiceBusProcessorOptions()
);

// Handler for Service Bus messages
serviceBusProcessor.ProcessMessageAsync += async (args) =>
{
    var messageBody = args.Message.Body.ToString();
    
    Console.WriteLine($"SERVICE BUS MESSAGE RECEIVED: {messageBody}");
    
    // Store to Blob (servicebus/ folder)
    var blobName = $"servicebus/{DateTime.UtcNow:yyyy-MM-dd}/{messageId}_{Guid.NewGuid()}.json";
    await blobClient.UploadAsync(new BinaryData(messageBody));
    
    // Complete message (remove from queue)
    await args.CompleteMessageAsync(args.Message);
};

// Start Service Bus processor
await serviceBusProcessor.StartProcessingAsync();
```

#### What Happens:
```
SEND:
1. Consumer reads SERVICEBUS_CONNECTION_STRING from environment
2. ServiceBusSender connects to shivaservicebus123.servicebus.windows.net
3. When message ID % 5 == 0:
   - Creates Service Bus message
   - Generates SAS token from connection string
   - Sends HTTP request:
     POST https://shivaservicebus123.servicebus.windows.net/retry-queue/messages
     Authorization: SharedAccessSignature sr=...
4. Service Bus validates token ✅
5. Message queued

RECEIVE:
1. ServiceBusProcessor polls queue
2. When message available:
   - Downloads message
   - Calls your handler
   - Handler stores to Blob Storage
   - Handler completes message (removes from queue)
```

---

## Summary: Commands You Actually Ran

### Phase 1: Azure Setup
```bash
# Done in Azure Portal (no terminal commands)
# - Created Event Hubs, Storage, Service Bus
# - Copied connection strings
```

### Phase 2: Encode Secrets
```bash
# Encode Event Hub connection string
echo -n "Endpoint=sb://mynamespace-1..." | base64

# Encode Storage connection string
echo -n "DefaultEndpointsProtocol=https;AccountName=shivastorage123..." | base64

# Encode Service Bus connection string
echo -n "Endpoint=sb://shivaservicebus123..." | base64
```

### Phase 3: Create Kubernetes Secret
```bash
# Copy template
cp k8s/secret.yaml.template k8s/secret.yaml

# Edit k8s/secret.yaml with base64 values (manually)

# Apply secret to cluster
kubectl apply -f k8s/secret.yaml
```

### Phase 4: Deploy to AKS
```bash
# Connect to AKS
az aks get-credentials --resource-group EventHub-test --name phase1-aks --overwrite-existing

# Apply ConfigMap
kubectl apply -f k8s/configmap.yaml

# Apply Deployment
kubectl apply -f k8s/deployment.yaml

# Check status
kubectl get pods

# View logs
kubectl logs -f deployment/eventhub-consumer
```

### Phase 5: Run Producer
```bash
# Set environment variables
export EVENTHUB_CONNECTION_STRING="Endpoint=sb://mynamespace-1..."
export EVENTHUB_NAME="my-event-hub"

# Run producer
cd src/Producer
dotnet run
```

### Phase 6: Verify
```bash
# Watch consumer logs
kubectl logs -f deployment/eventhub-consumer

# You see:
# ✓ EVENT HUB MESSAGE RECEIVED
# ✓ Stored to Blob: eventhub/...
# ✓ Forwarded to Service Bus
# ✓ SERVICE BUS MESSAGE RECEIVED
# ✓ Stored to Blob: servicebus/...
```

---

## Connection Summary Table

| Connection | Command That Set It Up | Code That Uses It | What It Does |
|------------|------------------------|-------------------|--------------|
| Producer → Event Hubs | `export EVENTHUB_CONNECTION_STRING="..."` | `new EventHubProducerClient(connectionString, name)` | Sends messages |
| Event Hubs → Consumer | `kubectl apply -f secret.yaml` | `new EventProcessorClient(checkpoints, group, conn, name)` | Reads messages |
| Consumer → Blob (checkpoints) | `kubectl apply -f secret.yaml` | `new BlobContainerClient(storageConn, "checkpoints")` | Saves progress |
| Consumer → Blob (messages) | `kubectl apply -f secret.yaml` | `blobClient.UploadAsync(data)` | Stores JSON files |
| Consumer → Service Bus | `kubectl apply -f secret.yaml` | `serviceBusSender.SendMessageAsync(message)` | Forwards to queue |
| Service Bus → Consumer | `kubectl apply -f secret.yaml` | `serviceBusProcessor.ProcessMessageAsync` | Processes retry messages |

---

## Key Takeaways

1. **Connection strings** are set as:
   - Environment variables (Producer - local)
   - Kubernetes Secrets (Consumer - AKS)

2. **Kubernetes Secret flow:**
   ```
   Connection string → base64 encode → secret.yaml → kubectl apply → 
   Kubernetes stores → Deployment references → Pod gets environment variable → 
   Code reads Environment.GetEnvironmentVariable()
   ```

3. **Every Azure SDK client** needs a connection string:
   - `EventHubProducerClient(connectionString, name)`
   - `EventProcessorClient(checkpoints, group, connectionString, name)`
   - `BlobContainerClient(connectionString, container)`
   - `ServiceBusClient(connectionString)`

4. **Authentication happens automatically** when you provide connection string:
   - Azure SDK extracts keys
   - Generates SAS tokens
   - Includes in HTTP headers
   - Azure validates and allows access

---

**You now have the complete terminal commands + code flow!** 🎯




