# Phase 2 Implementation Checklist

This project was copied from Phase 1. Below is what needs to be added for Phase 2.

## Phase 2 Requirements

**Goal:** Consumer processes messages, stores results in Azure Storage, and forwards certain messages to Service Bus.

### What Phase 2 Adds:
1. **Azure Blob Storage** - Store processed Event Hub messages
2. **Azure Service Bus Queue** - Retry/secondary processing path
3. **Checkpointing** - Use EventProcessorClient (instead of EventHubConsumerClient)
4. **Message Processing Logic** - Decide which messages to forward to Service Bus

---

## Azure Resources to Create

```bash
# Storage Account for checkpointing and storing messages
STORAGE_ACCOUNT="mystorage123"
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group <YOUR_RG> \
  --location eastus \
  --sku Standard_LRS

# Blob Container for processed messages
az storage container create \
  --name processed-messages \
  --account-name $STORAGE_ACCOUNT

# Service Bus Namespace
SERVICEBUS_NS="myservicebus123"
az servicebus namespace create \
  --name $SERVICEBUS_NS \
  --resource-group <YOUR_RG> \
  --location eastus \
  --sku Standard

# Service Bus Queue
az servicebus queue create \
  --name retry-queue \
  --namespace-name $SERVICEBUS_NS \
  --resource-group <YOUR_RG>
```

---

## Code Changes Needed

### 1. Consumer.csproj - Add Phase 2 Packages

Already has:
- Azure.Messaging.EventHubs
- Microsoft.Extensions.Configuration

Add:
- Azure.Messaging.EventHubs.Processor (for checkpointing)
- Azure.Storage.Blobs (for storing messages)
- Azure.Messaging.ServiceBus (for forwarding to Service Bus)

### 2. Consumer/Program.cs - Replace EventHubConsumerClient

**Phase 1 (current):**
- Uses `EventHubConsumerClient` (no checkpointing)

**Phase 2 (needs):**
- Use `EventProcessorClient` (with checkpointing)
- Read from Blob Storage for checkpoint persistence
- Process messages and store in Blob Storage
- Forward specific messages to Service Bus

### 3. Add Processing Logic

Decide which messages to forward to Service Bus:
- Example: Messages with errors
- Example: Messages matching certain rules
- Example: Random sampling for testing

### 4. Configuration Updates

**appsettings.json** - Add:
```json
{
  "EventHub": {
    "ConsumerGroup": "$Default",
    "EventHubName": null,
    "ConnectionString": null
  },
  "Storage": {
    "ConnectionString": null,
    "ContainerName": "processed-messages"
  },
  "ServiceBus": {
    "ConnectionString": null,
    "QueueName": "retry-queue"
  }
}
```

**Environment Variables** needed:
- EVENTHUB_CONNECTION_STRING (already have)
- EVENTHUB_NAME (already have)
- STORAGE_CONNECTION_STRING (new)
- SERVICEBUS_CONNECTION_STRING (new)

### 5. Kubernetes - Update ConfigMap and Secret

**k8s/configmap.yaml** - Add:
```yaml
data:
  EVENTHUB_NAME: "my-event-hub"
  STORAGE_CONTAINER: "processed-messages"
  SERVICEBUS_QUEUE: "retry-queue"
```

**k8s/secret.yaml.template** - Add:
```yaml
data:
  eventhub-connection: <BASE64_ENCODED>
  storage-connection: <BASE64_ENCODED>  # NEW
  servicebus-connection: <BASE64_ENCODED>  # NEW
```

**k8s/deployment.yaml** - Add environment variables:
```yaml
env:
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

---

## Testing Phase 2

1. **Run Producer** - Send messages to Event Hubs (same as Phase 1)
2. **Check Consumer Logs** - Should see:
   - Messages received from Event Hubs
   - Messages stored to Blob Storage
   - Selected messages forwarded to Service Bus
3. **Verify Blob Storage** - Check Azure Portal for stored JSON files
4. **Verify Service Bus** - Check queue message count in Azure Portal

---

## Evidence to Collect

- Screenshot: Blob Storage container with stored messages
- Screenshot: Service Bus queue showing message count
- Screenshot: Consumer logs showing all 3 operations
- Screenshot: Azure Portal showing all resources

---

## Next Steps

1. Create Azure Storage Account and Service Bus
2. Update Consumer.csproj with new packages
3. Rewrite Consumer/Program.cs with EventProcessorClient
4. Update Kubernetes configs
5. Rebuild Docker image and redeploy
6. Test end-to-end flow

---

**Phase 1 (completed):** Event Hubs → Consumer → Logs ✅
**Phase 2 (to build):** Event Hubs → Consumer → Storage + Service Bus → Logs



