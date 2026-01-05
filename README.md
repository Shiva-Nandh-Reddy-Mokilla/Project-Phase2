# Phase 2: Event Hubs + Storage + Service Bus on AKS

## Overview

This project demonstrates a complete message processing pipeline using Azure Event Hubs, Azure Storage, Azure Service Bus, and Azure Kubernetes Service.

**What it does:**
- Producer sends test messages to Azure Event Hubs (runs locally)
- Consumer reads from Event Hubs, stores to Blob Storage, and forwards selected messages to Service Bus (runs in AKS)
- Consumer also processes messages from Service Bus and stores them to Blob Storage

**Phase 2 Architecture:**

```
Producer (Local)
    ↓
Azure Event Hubs
    ↓
Consumer (AKS) → Azure Blob Storage (eventhub/*.json)
    ↓ (messageId % 5 == 0 OR ShouldRetry=true)
Azure Service Bus Queue
    ↓
Consumer (AKS) → Azure Blob Storage (servicebus/*.json)
```

---

## Prerequisites

### Azure Resources Needed
- Event Hubs Namespace + Event Hub
- Storage Account (for checkpoints + processed messages)
- Service Bus Namespace + Queue
- Container Registry (ACR)
- AKS Cluster

### Local Tools
- .NET 8 SDK
- Docker Desktop
- kubectl
- Azure CLI

---

## Phase 2 Setup

### Step 1: Create Azure Resources in Azure Portal

I'll give you instructions to create these in the Azure Portal (not CLI) so you can learn the UI:

#### 1.1 Create Storage Account

1. Go to **Azure Portal** → Search for "Storage accounts" → Click **Create**
2. Fill in:
   - **Subscription**: Your subscription
   - **Resource group**: Same as your Event Hub resource group
   - **Storage account name**: `shivastorage123` (must be globally unique, lowercase, no dashes)
   - **Region**: Same as your Event Hub (e.g., East US)
   - **Performance**: Standard
   - **Redundancy**: LRS (Locally Redundant Storage)
3. Click **Review + Create** → **Create**
4. Once created, go to your storage account → **Containers** (left menu) → Click **+ Container**
   - **Name**: `processed-messages`
   - **Public access level**: Private
   - Click **Create**
5. Repeat to create another container:
   - **Name**: `checkpoints`
   - Click **Create**
6. Get connection string:
   - Go to **Access keys** (left menu)
   - Copy **Connection string** under key1 (save this - you'll need it later)

#### 1.2 Create Service Bus

1. Go to **Azure Portal** → Search for "Service Bus" → Click **Create**
2. Fill in:
   - **Subscription**: Your subscription
   - **Resource group**: Same as your Event Hub
   - **Namespace name**: `shivaservicebus123` (must be globally unique)
   - **Location**: Same as your Event Hub
   - **Pricing tier**: Standard (Basic won't work - we need queues)
3. Click **Review + Create** → **Create**
4. Once created, go to your Service Bus namespace → **Queues** (left menu) → Click **+ Queue**
   - **Name**: `retry-queue`
   - Leave other settings as default
   - Click **Create**
5. Get connection string:
   - Go to **Shared access policies** (left menu)
   - Click **RootManageSharedAccessKey**
   - Copy **Primary Connection String** (save this - you'll need it later)

#### 1.3 Verify Your Event Hub (from Phase 1)

You should already have:
- Event Hubs Namespace (e.g., `shivaeh123`)
- Event Hub (e.g., `messages`)
- Connection string saved

If not, get it:
- Go to your Event Hubs Namespace → **Shared access policies** → **RootManageSharedAccessKey**
- Copy **Primary Connection String**

---

### Step 2: Update ConfigMap

Edit `k8s/configmap.yaml`:

```yaml
data:
  EVENTHUB_NAME: "messages"  # <-- Your Event Hub name
  STORAGE_CONTAINER: "processed-messages"
  SERVICEBUS_QUEUE: "retry-queue"
```

---

### Step 3: Create Kubernetes Secret

You need to base64-encode your 3 connection strings:

```bash
# Encode Event Hub connection string
echo -n "Endpoint=sb://shivaeh123.servicebus.windows.net/;SharedAccessKeyName=..." | base64

# Encode Storage connection string
echo -n "DefaultEndpointsProtocol=https;AccountName=shivastorage123;..." | base64

# Encode Service Bus connection string
echo -n "Endpoint=sb://shivaservicebus123.servicebus.windows.net/;..." | base64
```

Create `k8s/secret.yaml` from template:

```bash
cp k8s/secret.yaml.template k8s/secret.yaml
```

Edit `k8s/secret.yaml` and paste your base64-encoded strings:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: azure-secrets
  namespace: default
type: Opaque
data:
  eventhub-connection: <PASTE_BASE64_EVENTHUB_HERE>
  storage-connection: <PASTE_BASE64_STORAGE_HERE>
  servicebus-connection: <PASTE_BASE64_SERVICEBUS_HERE>
```

**Important**: Don't commit `secret.yaml` to git!

---

### Step 4: Update Deployment Image Name

Edit `k8s/deployment.yaml`:

```yaml
containers:
- name: consumer
  image: shivaacr123.azurecr.io/eventhub-consumer:v2  # <-- Your ACR name + v2 tag
```

---

### Step 5: Build and Push Docker Image

```bash
cd src/Consumer

# Login to your ACR
az acr login -n shivaacr123  # <-- Your ACR name

# Build image for Phase 2
docker build -t shivaacr123.azurecr.io/eventhub-consumer:v2 -f Dockerfile ..

# Push to ACR
docker push shivaacr123.azurecr.io/eventhub-consumer:v2
```

---

### Step 6: Deploy to AKS

```bash
# Make sure you're connected to your AKS cluster
kubectl config current-context

# Apply all configurations
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/deployment.yaml

# Wait for pod to be ready
kubectl get pods -w

# Check logs
kubectl logs -f deployment/eventhub-consumer
```

You should see:
```
Event Hubs Consumer Starting (Phase 2)...
Event Hub: messages
Storage Container: processed-messages
Service Bus Queue: retry-queue

Starting Event Hub processor...
Event Hub processor started
Starting Service Bus processor...
Service Bus processor started

Listening for messages... (Press Ctrl+C to stop)
```

---

### Step 7: Test the Full Flow

#### Terminal 1 - Watch Consumer Logs
```bash
kubectl logs -f deployment/eventhub-consumer
```

#### Terminal 2 - Run Producer
```bash
cd src/Producer

# Set your connection string and Event Hub name
export EVENTHUB_CONNECTION_STRING="Endpoint=sb://..."
export EVENTHUB_NAME="messages"

dotnet run
```

Select **Option 2** to send 10 messages.

---

### Step 8: Verify Results

#### Check Consumer Logs

You should see output like this:

```
========================================
EVENT HUB MESSAGE RECEIVED
  Partition:   0
  Message ID:  1
  Timestamp:   2026-01-04T12:34:56.789Z
  Body:        {"MessageId":"1","Timestamp":"2026-01-04T12:34:56.789Z",...}
========================================
✓ Stored to Blob: eventhub/2026-01-04/1_abc123.json

========================================
EVENT HUB MESSAGE RECEIVED
  Partition:   1
  Message ID:  5
  Timestamp:   2026-01-04T12:34:58.123Z
  Body:        {"MessageId":"5","Timestamp":"2026-01-04T12:34:58.123Z",...}
========================================
✓ Stored to Blob: eventhub/2026-01-04/5_def456.json
✓ Forwarded to Service Bus

========================================
SERVICE BUS MESSAGE RECEIVED
  Message ID:  5
  Body:        {"MessageId":"5","Timestamp":"2026-01-04T12:34:58.123Z",...}
========================================
✓ Stored to Blob: servicebus/2026-01-04/5_ghi789.json
```

**Key observations:**
- Message 5 was forwarded to Service Bus (because 5 % 5 == 0)
- Message 5 was then consumed from Service Bus
- Message 5 appears in both `eventhub/` and `servicebus/` folders

#### Check Azure Blob Storage (in Portal)

1. Go to your Storage Account → **Containers** → **processed-messages**
2. You should see folders:
   - `eventhub/2026-01-04/` - Contains all Event Hub messages
   - `servicebus/2026-01-04/` - Contains messages that went through Service Bus
3. Click on any `.json` file to download and view the message

#### Check Service Bus Queue (in Portal)

1. Go to your Service Bus Namespace → **Queues** → **retry-queue**
2. Look at **Active message count**:
   - Should be 0 (all messages processed)
3. Look at **Completed messages** in metrics
   - Should show messages were delivered and completed

---

## Message Forwarding Logic

The consumer forwards messages to Service Bus when:
1. `ShouldRetry` field is `true` in the message, OR
2. `messageId` is divisible by 5 (e.g., 5, 10, 15, 20, ...)

This simulates real-world scenarios where:
- Messages can flag themselves for retry
- System applies deterministic routing rules

---

## Project Structure

```
ShivaProject-Phase2/
├── README.md
├── PHASE2_TODO.md
├── ShivaProject.sln
├── src/
│   ├── Producer/              # Runs locally (same as Phase 1)
│   │   ├── Program.cs
│   │   ├── MessageGenerator.cs
│   │   ├── Producer.csproj
│   │   └── appsettings.json
│   └── Consumer/              # Runs in AKS (updated for Phase 2)
│       ├── Program.cs         # ← NEW: Storage + Service Bus
│       ├── Consumer.csproj    # ← NEW: Added packages
│       ├── Dockerfile
│       └── appsettings.json   # ← NEW: Storage + Service Bus config
└── k8s/
    ├── configmap.yaml         # ← NEW: Storage + Service Bus settings
    ├── secret.yaml.template   # ← NEW: 3 connection strings
    └── deployment.yaml        # ← NEW: 3 environment variables
```

---

## Key Concepts Explained

### EventProcessorClient vs EventHubConsumerClient
- **Phase 1**: Used `EventHubConsumerClient` (no checkpointing)
- **Phase 2**: Uses `EventProcessorClient` (with checkpointing to Blob Storage)
- **Why**: Checkpointing ensures we don't lose track of processed messages if the consumer restarts

### Blob Storage - Two Uses
1. **Checkpoints container**: Stores Event Hub partition offsets (managed by SDK)
2. **Processed-messages container**: Stores our actual processed message data
   - `eventhub/` folder: Messages from Event Hub
   - `servicebus/` folder: Messages from Service Bus

### Service Bus Queue
- **Purpose**: Retry/secondary processing path
- **How**: Consumer forwards selected messages from Event Hub → Service Bus
- **Then**: Same consumer process reads from Service Bus and processes again

### Connection Strings - Where They Come From
1. **Event Hubs**: Namespace → Shared access policies → RootManageSharedAccessKey → Primary Connection String
2. **Storage**: Storage Account → Access keys → key1 → Connection string
3. **Service Bus**: Namespace → Shared access policies → RootManageSharedAccessKey → Primary Connection String

### Kubernetes Secrets
- Base64-encoded (not encrypted!)
- Injected as environment variables into pods
- **Never commit to git** - Use template file instead

---

## Troubleshooting

### Consumer pod not starting
```bash
kubectl describe pod <pod-name>
kubectl logs <pod-name>
```
Common issues:
- Wrong connection strings in secret
- Image not found in ACR
- ACR not attached to AKS: `az aks update -n <aks> -g <rg> --attach-acr <acr>`

### No messages appearing in logs
- Make sure consumer is running BEFORE you send messages
- Event Hub processor reads from latest position
- Check Event Hub metrics in Azure Portal

### Messages not in Blob Storage
- Check container names match in code and portal
- Verify storage connection string is correct
- Check consumer logs for errors
- Look in Azure Portal → Storage Account → Containers → processed-messages

### Service Bus messages not being processed
- Verify queue name is `retry-queue`
- Check Service Bus connection string
- Look at queue metrics in Azure Portal

### Permission errors
- Make sure connection strings have proper permissions:
  - Event Hub: Listen, Send, Manage
  - Storage: Full access
  - Service Bus: Listen, Send, Manage

---

## Evidence to Collect (For Presentation)

### Screenshots to Take:

1. **Azure Portal - All Resources**
   - Show Event Hubs, Storage, Service Bus, ACR, AKS in one view

2. **Blob Storage with Messages**
   - Show `processed-messages` container with `eventhub/` and `servicebus/` folders
   - Open a JSON file to show message content

3. **Service Bus Queue Metrics**
   - Show completed message count
   - Show active messages = 0

4. **Consumer Logs**
   - Show Event Hub messages received
   - Show messages stored to Blob
   - Show messages forwarded to Service Bus
   - Show Service Bus messages processed

5. **Producer Output**
   - Show messages being sent

---

## Success Criteria

Phase 2 complete when you can demonstrate:

✅ Producer sends messages to Event Hubs  
✅ Consumer receives messages from Event Hubs  
✅ Consumer stores ALL messages to Blob Storage (`eventhub/` folder)  
✅ Consumer forwards selected messages to Service Bus (messageId % 5 == 0)  
✅ Consumer processes messages from Service Bus  
✅ Consumer stores Service Bus messages to Blob Storage (`servicebus/` folder)  
✅ You can show message files in Azure Portal  
✅ You can explain how authentication works (connection strings)  
✅ You can explain why secrets are not committed to git  

---

## What Changed from Phase 1 to Phase 2

### Code Changes
- **Consumer.csproj**: Added 3 new packages (EventProcessorClient, Storage, Service Bus)
- **Consumer/Program.cs**: Complete rewrite using EventProcessorClient + Storage + Service Bus
- **appsettings.json**: Added Storage and Service Bus sections

### Infrastructure Changes
- **New Azure Resources**: Storage Account, Service Bus Namespace + Queue
- **ConfigMap**: Added Storage container name and Service Bus queue name
- **Secret**: Now has 3 connection strings instead of 1
- **Deployment**: Added 2 new environment variables

### Behavior Changes
- Messages are now checkpointed (survives restarts)
- All messages stored to Blob Storage
- Selected messages go through Service Bus
- Two processing paths: Event Hubs → Storage AND Event Hubs → Service Bus → Storage

---

## Next Steps (Optional Stretch Phase)

Consider adding:
- **Liveness/Readiness probes** in deployment.yaml
- **Horizontal Pod Autoscaler (HPA)** for CPU-based scaling
- **Structured logging** with correlation IDs
- **Application Insights** for monitoring
- **Retry policies** in Azure SDK clients
- **Managed Identity** instead of connection strings

---

## Resources

- [Azure Event Hubs](https://docs.microsoft.com/azure/event-hubs/)
- [Azure Storage Blobs](https://docs.microsoft.com/azure/storage/blobs/)
- [Azure Service Bus](https://docs.microsoft.com/azure/service-bus-messaging/)
- [Azure Kubernetes Service](https://docs.microsoft.com/azure/aks/)
- [EventProcessorClient](https://docs.microsoft.com/dotnet/api/azure.messaging.eventhubs.processor.eventprocessorclient)
