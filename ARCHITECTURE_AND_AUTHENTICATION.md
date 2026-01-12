# Complete Architecture & Authentication Guide

## Table of Contents
1. [System Overview](#system-overview)
2. [Component Connections](#component-connections)
3. [Authentication Explained](#authentication-explained)
4. [Kubernetes Role](#kubernetes-role)
5. [Data Flow](#data-flow)
6. [Security & Secrets](#security--secrets)
7. [Complete Example](#complete-example)

---

## System Overview

### What You Built

```
┌─────────────┐
│  Producer   │ (Your laptop - sends test messages)
│  (Local)    │
└──────┬──────┘
       │
       │ ① Connection String (SAS Token)
       ↓
┌─────────────────────────────────────┐
│  Azure Event Hubs                   │ (Message streaming service)
│  Namespace: MyNameSpace-1           │
│  Event Hub: my-event-hub            │
└──────┬──────────────────────────────┘
       │
       │ ② Connection String (SAS Token)
       ↓
┌─────────────────────────────────────┐
│  Consumer (Running in AKS Pod)      │ (Your app in the cloud)
│  • Reads from Event Hubs            │
│  • Stores to Blob Storage           │
│  • Forwards to Service Bus          │
│  • Processes Service Bus messages   │
└──────┬──────────────────────────────┘
       │
       ├─────────────────┬─────────────────┐
       │                 │                 │
       │ ③               │ ④               │ ⑤
       ↓                 ↓                 ↓
┌─────────────┐   ┌──────────────┐   ┌─────────────┐
│Azure Storage│   │Service Bus   │   │Service Bus  │
│(Blob)       │   │Queue         │   │Queue        │
│             │   │(Send)        │   │(Receive)    │
└─────────────┘   └──────┬───────┘   └──────┬──────┘
                         │                  │
                         └──────────────────┘
                                ⑥
                         Consumer reads back
                                │
                                ↓
                         ┌─────────────┐
                         │Azure Storage│
                         │(Blob)       │
                         └─────────────┘
```

**5 Azure Services:**
1. **Event Hubs** - Message streaming (like Kafka)
2. **Blob Storage** - File storage for processed messages
3. **Service Bus** - Message queue for retry logic
4. **Container Registry (ACR)** - Stores your Docker image
5. **Kubernetes (AKS)** - Runs your consumer app

---

## Component Connections

### 1️⃣ Producer → Event Hubs

**What happens:**
- Producer sends JSON messages to Event Hubs

**Connection:**
```
Connection String = Endpoint + SAS Token (Shared Access Signature)

Example:
Endpoint=sb://mynamespace-1.servicebus.windows.net/;
SharedAccessKeyName=phase1-policy;
SharedAccessKey=S3AHvrmH15wsumbx...
```

**How Producer gets it:**
```csharp
// Producer/Program.cs reads from environment variable
var connectionString = Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");
var eventHubName = Environment.GetEnvironmentVariable("EVENTHUB_NAME");

// Creates client
var producerClient = new EventHubProducerClient(connectionString, eventHubName);

// Sends message
await producerClient.SendAsync(eventBatch);
```

**Where connection string comes from:**
- Azure Portal → MyNameSpace-1 → Shared access policies → phase1-policy → Copy connection string
- You set it: `export EVENTHUB_CONNECTION_STRING="..."`

---

### 2️⃣ Event Hubs → Consumer (in AKS)

**What happens:**
- Consumer (running in Kubernetes pod) reads messages from Event Hubs
- Uses **checkpointing** to remember what's been processed

**Connection:**
```
Same Event Hub connection string + Blob Storage for checkpoints

EventProcessorClient needs:
1. Event Hub connection string (to read messages)
2. Blob Storage connection string (to save checkpoints)
```

**How Consumer gets it:**
```csharp
// Consumer/Program.cs reads from environment variables
var eventHubConnectionString = Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");
var storageConnectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");

// Creates checkpoint storage
var checkpointBlobClient = new BlobContainerClient(storageConnectionString, "checkpoints");

// Creates processor with checkpointing
var processor = new EventProcessorClient(
    checkpointBlobClient,      // Where to save checkpoint
    "$Default",                // Consumer group
    eventHubConnectionString,  // Event Hub
    eventHubName
);

// When message is processed, save checkpoint
await args.UpdateCheckpointAsync();
```

**Where consumer gets connection strings:**
- Kubernetes injects them as environment variables
- Source: Kubernetes Secret (`k8s/secret.yaml`)

---

### 3️⃣ Consumer → Blob Storage

**What happens:**
- Consumer stores every message as a JSON file
- Two folders: `eventhub/` and `servicebus/`

**Connection:**
```
Storage Connection String = Account info + Account Key

Example:
DefaultEndpointsProtocol=https;
AccountName=shivastorage123;
AccountKey=XfS4862QwFKYWQfWZJt5...;
EndpointSuffix=core.windows.net
```

**How it works:**
```csharp
// Consumer/Program.cs
var blobContainerClient = new BlobContainerClient(
    storageConnectionString,
    "processed-messages"
);

// Create container if it doesn't exist
await blobContainerClient.CreateIfNotExistsAsync();

// Store message as JSON file
var blobName = $"eventhub/2026-01-05/243_abc123.json";
var blobClient = blobContainerClient.GetBlobClient(blobName);
await blobClient.UploadAsync(new BinaryData(messageBody), overwrite: true);
```

**Where connection string comes from:**
- Azure Portal → shivastorage123 → Access keys → key1 → Connection string
- Stored in Kubernetes Secret
- Injected as environment variable `STORAGE_CONNECTION_STRING`

---

### 4️⃣ Consumer → Service Bus (Send)

**What happens:**
- When message ID is divisible by 5, forward it to Service Bus queue
- Service Bus acts as a "retry queue"

**Connection:**
```
Service Bus Connection String = Endpoint + SAS Token

Example:
Endpoint=sb://shivaservicebus123.servicebus.windows.net/;
SharedAccessKeyName=RootManageSharedAccessKey;
SharedAccessKey=oGJBGi1m4P4...
```

**How it works:**
```csharp
// Consumer/Program.cs
var serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
var serviceBusSender = serviceBusClient.CreateSender("retry-queue");

// Decide if message should be forwarded
if (int.Parse(messageId) % 5 == 0)
{
    var sbMessage = new ServiceBusMessage(messageBody);
    await serviceBusSender.SendMessageAsync(sbMessage);
    Console.WriteLine("✓ Forwarded to Service Bus");
}
```

**Where connection string comes from:**
- Azure Portal → shivaservicebus123 → Shared access policies → RootManageSharedAccessKey → Connection string
- Stored in Kubernetes Secret
- Injected as environment variable `SERVICEBUS_CONNECTION_STRING`

---

### 5️⃣ Service Bus → Consumer (Receive)

**What happens:**
- Same Consumer app ALSO reads from Service Bus queue
- Processes "retry" messages

**How it works:**
```csharp
// Consumer/Program.cs - Service Bus processor
var serviceBusProcessor = serviceBusClient.CreateProcessor(
    "retry-queue",
    new ServiceBusProcessorOptions()
);

// Handler for Service Bus messages
serviceBusProcessor.ProcessMessageAsync += async (args) =>
{
    var messageBody = args.Message.Body.ToString();
    
    // Store to Blob in servicebus/ folder
    var blobName = $"servicebus/2026-01-05/810_xyz789.json";
    await blobClient.UploadAsync(new BinaryData(messageBody));
    
    // Complete the message (removes from queue)
    await args.CompleteMessageAsync(args.Message);
};

// Start processing
await serviceBusProcessor.StartProcessingAsync();
```

**Same connection string as #4** - Service Bus connection is used for both send and receive.

---

## Authentication Explained

### What is a Connection String?

A connection string is like a **username + password + server address** all in one string.

**Format:**
```
Endpoint=sb://SERVER_ADDRESS/;
SharedAccessKeyName=USERNAME;
SharedAccessKey=PASSWORD
```

### Example - Event Hub Connection String Breakdown:

```
Endpoint=sb://mynamespace-1.servicebus.windows.net/;
         ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
         (Server address - where the service is)

SharedAccessKeyName=phase1-policy;
                    ^^^^^^^^^^^^^
                    (Policy name - like a username)

SharedAccessKey=S3AHvrmH15wsumbx+L+ZWBeKFJV062YK4+AEhDoHt2U=
                ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
                (Secret key - like a password)
```

### How Authentication Happens:

1. **You create a connection string** in Azure Portal
2. **Azure SDK uses it** to generate a **SAS Token** (Shared Access Signature)
3. **Token is sent** with every request to prove identity
4. **Azure validates** the token and allows/denies access

**Example flow:**
```
Producer sends message:
1. Producer: "I want to send a message to Event Hubs"
2. SDK: "Here's my SAS token (generated from connection string)"
3. Event Hubs: "Token valid? ✅ Yes. Message accepted."

Consumer reads message:
1. Consumer: "I want to read messages from Event Hubs"
2. SDK: "Here's my SAS token"
3. Event Hubs: "Token valid? ✅ Yes. Here are your messages."
```

### Why Connection Strings Rotate (Change):

Azure automatically rotates keys for security:
- **Primary key** and **Secondary key** available
- Rotate periodically to prevent unauthorized access
- When rotated, old connection strings stop working (401 error)

---

## Kubernetes Role

### What is Kubernetes (AKS)?

**Kubernetes** = Container orchestration platform
**AKS** = Azure Kubernetes Service (managed Kubernetes)

Think of it as:
- **Docker** = Runs one container on your laptop
- **Kubernetes** = Runs many containers in the cloud, with auto-restart, scaling, networking, etc.

### Your Kubernetes Setup:

```
┌─────────────────────────────────────────────┐
│  AKS Cluster (phase1-aks)                  │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │  Deployment: eventhub-consumer      │   │
│  │  (Desired state: 1 replica)         │   │
│  │                                      │   │
│  │  ┌──────────────────────────────┐   │   │
│  │  │  Pod: eventhub-consumer-xyz  │   │   │
│  │  │                               │   │   │
│  │  │  Container:                  │   │   │
│  │  │  Image: shivaacr123.azurecr │   │   │
│  │  │         .io/eventhub-        │   │   │
│  │  │         consumer:v2          │   │   │
│  │  │                               │   │   │
│  │  │  Environment Variables:      │   │   │
│  │  │  - EVENTHUB_CONNECTION_...   │   │   │
│  │  │  - STORAGE_CONNECTION_...    │   │   │
│  │  │  - SERVICEBUS_CONNECTION_... │   │   │
│  │  │                               │   │   │
│  │  │  Your Consumer app runs here │   │   │
│  │  └──────────────────────────────┘   │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │  ConfigMap: consumer-config         │   │
│  │  - EVENTHUB_NAME: "my-event-hub"    │   │
│  │  - STORAGE_CONTAINER: "processed-..." │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │  Secret: azure-secrets              │   │
│  │  - eventhub-connection: <base64>    │   │
│  │  - storage-connection: <base64>     │   │
│  │  - servicebus-connection: <base64>  │   │
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

### Kubernetes Components:

#### 1. **Deployment** (`k8s/deployment.yaml`)
Defines:
- What container image to run
- How many replicas (pods) to run
- What environment variables to inject
- Resource limits (CPU, memory)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: eventhub-consumer
spec:
  replicas: 1  # Run 1 pod
  template:
    spec:
      containers:
      - name: consumer
        image: shivaacr123.azurecr.io/eventhub-consumer:v2
        env:
        - name: EVENTHUB_CONNECTION_STRING
          valueFrom:
            secretKeyRef:
              name: azure-secrets
              key: eventhub-connection
```

**What it does:**
- Tells Kubernetes: "Always keep 1 pod running with this image"
- If pod crashes, Kubernetes restarts it automatically
- Injects secrets as environment variables

#### 2. **ConfigMap** (`k8s/configmap.yaml`)
Stores **non-sensitive** configuration:

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

**What it does:**
- Stores names, not secrets
- Can be updated without rebuilding Docker image
- Injected as environment variables into pod

#### 3. **Secret** (`k8s/secret.yaml`)
Stores **sensitive** connection strings:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: azure-secrets
type: Opaque
data:
  eventhub-connection: RW5kcG9pbnQ9c2I6Ly9...  # Base64 encoded
  storage-connection: RGVmYXVsdEVuZHBvaW50...   # Base64 encoded
  servicebus-connection: RW5kcG9pbnQ9c2I6...    # Base64 encoded
```

**What it does:**
- Stores connection strings securely (base64 encoded)
- Only accessible by pods in the cluster
- Injected as environment variables
- **NOT committed to git!**

#### 4. **Pod**
- The actual running container
- Created by Deployment
- Runs your Consumer app
- Gets environment variables from ConfigMap + Secret

### How Kubernetes Injects Secrets:

```
1. You create secret.yaml with base64-encoded connection strings
   └─> kubectl apply -f k8s/secret.yaml

2. Kubernetes stores it securely in etcd (encrypted database)

3. Deployment references the secret:
   env:
   - name: EVENTHUB_CONNECTION_STRING
     valueFrom:
       secretKeyRef:
         name: azure-secrets
         key: eventhub-connection

4. When pod starts, Kubernetes:
   - Decodes base64
   - Sets environment variable: EVENTHUB_CONNECTION_STRING="Endpoint=sb://..."
   
5. Your C# code reads it:
   var conn = Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");
```

---

## Data Flow

### Complete Message Journey:

```
┌──────────────────────────────────────────────────────────────┐
│                     Message: ID=810                          │
└──────────────────────────────────────────────────────────────┘

Step 1: Producer Sends
├─> Producer (local) creates JSON:
│   {"MessageId":"810","Timestamp":"...","Payload":{...}}
│
├─> Uses Event Hub connection string to authenticate
│
└─> Sends to Event Hubs → my-event-hub

Step 2: Consumer Receives from Event Hubs
├─> EventProcessorClient polls Event Hubs
│
├─> Uses Event Hub connection string to authenticate
│
├─> Downloads message
│
├─> Logs: "EVENT HUB MESSAGE RECEIVED"
│
└─> Saves checkpoint to Blob Storage ("I processed message 810")

Step 3: Store to Blob Storage
├─> Creates filename: eventhub/2026-01-05/810_abc123.json
│
├─> Uses Storage connection string to authenticate
│
├─> Uploads JSON file
│
└─> Logs: "✓ Stored to Blob: eventhub/..."

Step 4: Check if Should Forward (810 % 5 == 0 ✅)
├─> if (810 % 5 == 0) → TRUE
│
├─> Creates Service Bus message
│
├─> Uses Service Bus connection string to authenticate
│
├─> Sends to Service Bus → retry-queue
│
└─> Logs: "✓ Forwarded to Service Bus"

Step 5: Consumer Receives from Service Bus
├─> ServiceBusProcessor polls retry-queue
│
├─> Uses Service Bus connection string to authenticate
│
├─> Downloads message (same JSON)
│
└─> Logs: "SERVICE BUS MESSAGE RECEIVED"

Step 6: Store to Blob Storage (Again)
├─> Creates filename: servicebus/2026-01-05/810_xyz789.json
│
├─> Uses Storage connection string to authenticate
│
├─> Uploads JSON file
│
├─> Logs: "✓ Stored to Blob: servicebus/..."
│
└─> Completes Service Bus message (removes from queue)

Result:
Message 810 is now in TWO places in Blob Storage:
1. eventhub/2026-01-05/810_abc123.json
2. servicebus/2026-01-05/810_xyz789.json
```

### Authentication at Each Step:

| Step | Action | Auth Method | Connection String Used |
|------|--------|-------------|------------------------|
| 1 | Send to Event Hubs | SAS Token | Event Hub connection string |
| 2 | Read from Event Hubs | SAS Token | Event Hub connection string |
| 2b | Save checkpoint | Account Key | Storage connection string |
| 3 | Write to Blob | Account Key | Storage connection string |
| 4 | Send to Service Bus | SAS Token | Service Bus connection string |
| 5 | Read from Service Bus | SAS Token | Service Bus connection string |
| 6 | Write to Blob | Account Key | Storage connection string |

---

## Security & Secrets

### Where Secrets Are Stored:

#### ❌ **NOT in Code** (Good!)
```csharp
// ❌ BAD (hardcoded secret)
var conn = "Endpoint=sb://mynamespace...";

// ✅ GOOD (read from environment)
var conn = Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");
```

#### ❌ **NOT in Git** (Good!)
- `k8s/secret.yaml` is in `.gitignore`
- Only `k8s/secret.yaml.template` is in git (no real secrets)
- `appsettings.json` has `null` values, not real connection strings

#### ✅ **In Kubernetes Secrets** (Good!)
```bash
# You create it locally (not committed)
kubectl apply -f k8s/secret.yaml

# Kubernetes stores it securely in the cluster
# Only pods can access it
```

#### ✅ **In Your Local Environment** (For Producer)
```bash
# Temporary - only for your current terminal session
export EVENTHUB_CONNECTION_STRING="..."
export EVENTHUB_NAME="my-event-hub"
```

### Base64 Encoding Explained:

**Q: Why base64 encode connection strings?**
**A:** Kubernetes Secrets require data to be base64 encoded (not encrypted, just encoded)

**How it works:**
```bash
# Original connection string
Endpoint=sb://mynamespace-1.servicebus.windows.net/;SharedAccessKeyName=phase1-policy;SharedAccessKey=ABC123...

# Base64 encoded (for Kubernetes)
RW5kcG9pbnQ9c2I6Ly9teW5hbWVzcGFjZS0xLnNlcnZpY2VidXMud2luZG93cy5uZXQvO1NoYXJlZEFjY2Vzc0tleU5hbWU9...

# To encode:
echo -n "YOUR_CONNECTION_STRING" | base64

# To decode (for debugging):
echo "BASE64_STRING" | base64 --decode
```

**Important:** Base64 is NOT encryption! Anyone with access to the Kubernetes cluster can decode it.

### Better Security (Future - Managed Identity):

Instead of connection strings, use **Managed Identity**:
- No connection strings needed
- Azure automatically authenticates pods
- More secure
- Out of scope for this exercise

---

## Complete Example

### Scenario: Message ID 810 Gets Processed

#### Step 1: Azure Portal Setup (You did this)

**Event Hubs:**
```
Resource: MyNameSpace-1
Event Hub: my-event-hub
Access Policy: phase1-policy
→ Connection String: Endpoint=sb://mynamespace-1...
```

**Storage:**
```
Resource: shivastorage123
Container: processed-messages
Container: checkpoints
→ Connection String: DefaultEndpointsProtocol=https;AccountName=shivastorage123...
```

**Service Bus:**
```
Resource: shivaservicebus123
Queue: retry-queue
Access Policy: RootManageSharedAccessKey
→ Connection String: Endpoint=sb://shivaservicebus123...
```

#### Step 2: Kubernetes Secret Creation

```bash
# Encode connection strings
echo -n "Endpoint=sb://mynamespace-1..." | base64
# Output: RW5kcG9pbnQ9c2I6Ly9teW5hbWVzcGFjZS0x...

# Create k8s/secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: azure-secrets
data:
  eventhub-connection: RW5kcG9pbnQ9c2I6Ly9teW5hbWVzcGFjZS0x...
  storage-connection: RGVmYXVsdEVuZHBvaW50c1Byb3RvY29sPWh0dHBz...
  servicebus-connection: RW5kcG9pbnQ9c2I6Ly9zaGl2YXNlcnZpY2VidXMx...

# Apply to cluster
kubectl apply -f k8s/secret.yaml
```

#### Step 3: Deploy Consumer to AKS

```bash
# Kubernetes creates pod with environment variables
kubectl apply -f k8s/deployment.yaml

# Pod starts with:
EVENTHUB_NAME=my-event-hub
EVENTHUB_CONNECTION_STRING=Endpoint=sb://mynamespace-1...
STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https...
SERVICEBUS_CONNECTION_STRING=Endpoint=sb://shivaservicebus123...
```

#### Step 4: Producer Sends Message

```bash
# Terminal:
export EVENTHUB_CONNECTION_STRING="Endpoint=sb://mynamespace-1..."
export EVENTHUB_NAME="my-event-hub"
cd src/Producer
dotnet run

# Producer code:
var conn = Environment.GetEnvironmentVariable("EVENTHUB_CONNECTION_STRING");
var client = new EventHubProducerClient(conn, "my-event-hub");

// Azure SDK generates SAS token from connection string
// Sends HTTP request to Event Hubs:
POST https://mynamespace-1.servicebus.windows.net/my-event-hub/messages
Authorization: SharedAccessSignature sr=...&sig=...&se=...&skn=phase1-policy
Content-Type: application/json

{"MessageId":"810","Timestamp":"2026-01-05T02:59:19Z",...}

# Event Hubs validates token ✅
# Message stored in Event Hubs
```

#### Step 5: Consumer Processes Message

```bash
# Consumer (in AKS pod):

# Event Hub Processor polls
GET https://mynamespace-1.servicebus.windows.net/my-event-hub/partitions/1/messages
Authorization: SharedAccessSignature sr=...&sig=...

# Response: Message 810

# Consumer stores to Blob Storage
PUT https://shivastorage123.blob.core.windows.net/processed-messages/eventhub/2026-01-05/810_abc.json
Authorization: SharedKey shivastorage123:BASE64_SIGNATURE

# Blob Storage validates account key ✅
# File saved

# Consumer checks: 810 % 5 == 0 ✅ True
# Forwards to Service Bus
POST https://shivaservicebus123.servicebus.windows.net/retry-queue/messages
Authorization: SharedAccessSignature sr=...

# Service Bus validates token ✅
# Message queued

# Consumer's Service Bus processor receives it
GET https://shivaservicebus123.servicebus.windows.net/retry-queue/messages/head
Authorization: SharedAccessSignature sr=...

# Response: Message 810 (from queue)

# Consumer stores to Blob Storage (again)
PUT https://shivastorage123.blob.core.windows.net/processed-messages/servicebus/2026-01-05/810_xyz.json
Authorization: SharedKey shivastorage123:BASE64_SIGNATURE

# File saved

# Consumer completes Service Bus message
DELETE https://shivaservicebus123.servicebus.windows.net/retry-queue/messages/810
Authorization: SharedAccessSignature sr=...

# Message removed from queue
```

#### Step 6: Verify in Azure Portal

**Blob Storage:**
```
shivastorage123 → processed-messages:
├── eventhub/
│   └── 2026-01-05/
│       └── 810_abc123.json ✅
└── servicebus/
    └── 2026-01-05/
        └── 810_xyz789.json ✅
```

**Service Bus:**
```
retry-queue:
├── Active messages: 0
└── Completed messages: 1 ✅
```

---

## Summary - The Big Picture

### 🔑 **Authentication Chain:**

1. **You create resources** in Azure Portal → Get connection strings
2. **Connection strings** = Endpoint + Access Key (like username + password)
3. **Base64 encode** connection strings for Kubernetes
4. **Kubernetes Secret** stores them securely
5. **Kubernetes injects** them as environment variables into pod
6. **Your C# code** reads environment variables
7. **Azure SDK** uses connection strings to generate SAS tokens
8. **Azure services** validate tokens on every request

### 🔗 **Connection Flow:**

```
Producer (local)
    │ Uses: Event Hub connection string
    ↓
Event Hubs (Azure)
    │ Sends: Messages
    ↓
Consumer (AKS pod)
    │ Uses: Event Hub connection string
    │       Storage connection string (for checkpoints)
    ↓
Blob Storage (Azure)
    │ Stores: eventhub/*.json
    │
Consumer (same pod)
    │ Uses: Service Bus connection string
    ↓
Service Bus Queue (Azure)
    │ Queues: Retry messages
    ↓
Consumer (same pod)
    │ Uses: Service Bus connection string
    │       Storage connection string
    ↓
Blob Storage (Azure)
    │ Stores: servicebus/*.json
```

### 🎯 **Key Concepts:**

1. **Connection String** = Authentication credentials (like password)
2. **Base64** = Encoding format (not encryption)
3. **Kubernetes Secret** = Secure storage in cluster
4. **Environment Variable** = How app accesses secrets
5. **SAS Token** = Temporary token generated from connection string
6. **Checkpointing** = Remembering which messages were processed

---

## Questions to Understand Better

**Q: Why do I need so many connection strings?**
A: Each Azure service requires its own authentication. You can't use Event Hubs credentials for Storage.

**Q: Why base64 encode?**
A: Kubernetes Secrets require base64 format. It handles special characters safely.

**Q: Why store in Kubernetes Secret instead of code?**
A: So you can change credentials without rebuilding the app, and keep them out of git.

**Q: What if connection string changes?**
A: Update k8s/secret.yaml, apply it, restart pod. Common when Azure rotates keys.

**Q: Could someone steal my secrets?**
A: If they have kubectl access to your cluster, yes. For production, use Managed Identity instead.

**Q: Why two paths for messages (Event Hub and Service Bus)?**
A: Simulates real-world: some messages need retry/special processing. Service Bus acts as a secondary queue.

**Q: How does checkpointing help?**
A: If consumer crashes, it can resume from last checkpoint instead of re-processing all messages.

---

**You now understand the complete architecture!** 🎓

Every connection, every authentication method, every data flow - it all ties together to create a resilient, cloud-native message processing system.




