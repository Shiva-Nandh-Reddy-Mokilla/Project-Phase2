# Event Hubs to AKS Consumer - Phase 2

## Overview

This project implements a message processing system using Azure Event Hubs, Azure Kubernetes Service (AKS), Azure Storage, and Azure Service Bus.

**Architecture Flow:**
```
Producer (AKS) → Event Hubs → Consumer (AKS) → Azure Storage (Blob)
                                    ↓
                              Service Bus Queue → Consumer → Azure Storage (Blob)
```

## Components

### Producer Application
- Sends messages to Azure Event Hubs every 2 seconds
- Runs as a containerized application in AKS
- Built with .NET 8.0

### Consumer Application
- Reads messages from Azure Event Hubs
- Stores processed messages to Azure Blob Storage
- Forwards messages (where messageId % 5 == 0) to Azure Service Bus Queue
- Processes Service Bus messages and stores them to Azure Blob Storage
- Built with .NET 8.0

## Azure Resources Created

### Via Azure Portal:
1. **Azure Kubernetes Service (AKS)** - `phase1-aks`
   - Region: West US
   - Node pool: 1 node

2. **Azure Event Hubs Namespace** - `MyNameSpace-1`
   - Event Hub: `my-event-hub`
   - Shared Access Policy: `phase1-policy`

3. **Azure Storage Account** - `shivastorage123`
   - Container: `checkpoints` (Event Hubs checkpointing)
   - Container: `processed-messages` (stores processed messages)

4. **Azure Service Bus Namespace** - `shivaservicebus123`
   - Queue: `retry-queue`

5. **Azure Container Registry** - `shivaacr123`
   - Stores Docker images for Consumer and Producer

## Project Structure

```
ShivaProject-Phase2/
├── src/
│   ├── Consumer/
│   │   ├── Program.cs              # Consumer application logic
│   │   ├── Consumer.csproj
│   │   ├── Dockerfile
│   │   └── appsettings.json
│   └── Producer/
│       ├── Program.cs              # Producer application logic
│       ├── MessageGenerator.cs
│       ├── Producer.csproj
│       ├── Dockerfile
│       └── appsettings.json
├── k8s/
│   ├── deployment.yaml             # Consumer deployment
│   ├── configmap.yaml              # Consumer config
│   ├── secret.yaml                 # Connection strings (not committed)
│   ├── secret.yaml.template        # Template for secrets
│   ├── producer-deployment.yaml    # Producer deployment
│   └── producer-configmap.yaml     # Producer config
└── .github/
    └── workflows/
        └── build-push.yml          # GitHub Actions CI/CD

```

## Setup Steps

### 1. Create Kubernetes Secrets

Encode your connection strings:
```bash
echo -n "YOUR_EVENTHUB_CONNECTION_STRING" | base64
echo -n "YOUR_STORAGE_CONNECTION_STRING" | base64
echo -n "YOUR_SERVICEBUS_CONNECTION_STRING" | base64
```

Create `k8s/secret.yaml` from template and add encoded values.

### 2. Configure kubectl

```bash
az aks get-credentials --resource-group EventHub-test --name phase1-aks
```

### 3. Deploy to AKS

```bash
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/producer-configmap.yaml
kubectl apply -f k8s/producer-deployment.yaml
```

### 4. Verify Deployment

```bash
kubectl get pods
kubectl logs deployment/eventhub-producer
kubectl logs deployment/eventhub-consumer
```

## Managing the Application

### View Running Pods
```bash
kubectl get pods
```

### View Logs
```bash
# Producer logs
kubectl logs deployment/eventhub-producer --tail=20

# Consumer logs
kubectl logs deployment/eventhub-consumer --tail=20

# Follow logs in real-time
kubectl logs -f deployment/eventhub-consumer
```

### Stop Producer
```bash
kubectl scale deployment eventhub-producer --replicas=0
```

### Restart Producer
```bash
kubectl scale deployment eventhub-producer --replicas=1
```

### Stop Consumer
```bash
kubectl scale deployment eventhub-consumer --replicas=0
```

## Configuration

### Environment Variables

**Consumer:**
- `EVENTHUB_CONNECTION_STRING` - Event Hubs connection string
- `EVENTHUB_NAME` - Event Hub name
- `STORAGE_CONNECTION_STRING` - Azure Storage connection string
- `SERVICEBUS_CONNECTION_STRING` - Service Bus connection string

**Producer:**
- `EVENTHUB_CONNECTION_STRING` - Event Hubs connection string
- `EVENTHUB_NAME` - Event Hub name
- `MESSAGE_INTERVAL_SECONDS` - Message send interval (default: 2)

### Connection String Format

**Event Hub:**
```
Endpoint=sb://NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=POLICY;SharedAccessKey=KEY
```

**Storage:**
```
DefaultEndpointsProtocol=https;AccountName=ACCOUNT;AccountKey=KEY;EndpointSuffix=core.windows.net
```

**Service Bus:**
```
Endpoint=sb://NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=KEY
```

## CI/CD with GitHub Actions

The project uses GitHub Actions to build Docker images:

1. Code is pushed to GitHub
2. GitHub Actions builds Docker images for Consumer and Producer
3. Images are pushed to Azure Container Registry
4. Kubernetes deployments are manually updated to use new images

## Verifying Message Flow

### Check Azure Storage
Via Azure Portal:
1. Go to Storage Account: `shivastorage123`
2. Navigate to Containers > `processed-messages`
3. Check folders:
   - `eventhub/YYYY-MM-DD/` - Messages from Event Hubs
   - `servicebus/YYYY-MM-DD/` - Messages from Service Bus

### Check Service Bus Queue
Via Azure Portal:
1. Go to Service Bus: `shivaservicebus123`
2. Navigate to Queues > `retry-queue`
3. View message count and inspect messages

### View in AKS
Via Azure Portal:
1. Go to Kubernetes service: `phase1-aks`
2. Navigate to Workloads > Pods
3. Click on a pod > Live logs

## Phase 2 Requirements Met

- Event Hubs to AKS Consumer: Complete
- Consumer stores to Azure Storage: Complete
- Consumer forwards to Service Bus: Complete (messageId % 5 == 0)
- Consumer processes Service Bus messages: Complete
- Service Bus messages stored to Storage: Complete

## Stretch Phase Items Implemented

1. **Configuration Hygiene**
   - Separate ConfigMaps for non-sensitive configuration
   - Separate Secrets for connection strings
   - Documented environment variables

2. **Structured Logging**
   - Consistent log format with clear sections
   - Message IDs and timestamps
   - JSON message bodies
   - Action indicators for tracking

## Troubleshooting

### Pods Not Starting
```bash
kubectl describe pod POD_NAME
kubectl logs POD_NAME
```

### Connection Issues
Verify secrets are correctly encoded:
```bash
kubectl get secret azure-secrets -o jsonpath='{.data.eventhub-connection}' | base64 --decode
```

### Update Secrets
```bash
kubectl delete secret azure-secrets
kubectl apply -f k8s/secret.yaml
kubectl delete pod -l app=eventhub-consumer
```

## Clean Up

### Stop Applications
```bash
kubectl scale deployment eventhub-producer --replicas=0
kubectl scale deployment eventhub-consumer --replicas=0
```

### Delete Deployments
```bash
kubectl delete -f k8s/deployment.yaml
kubectl delete -f k8s/producer-deployment.yaml
```

### Delete Azure Resources
Via Azure Portal, delete the resource group `EventHub-test` to remove all resources.

## Technology Stack

- .NET 8.0
- Azure Event Hubs
- Azure Kubernetes Service (AKS)
- Azure Storage (Blob)
- Azure Service Bus
- Azure Container Registry
- Docker
- Kubernetes
- GitHub Actions
