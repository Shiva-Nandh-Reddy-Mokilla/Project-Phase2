# Phase 1: Azure Event Hubs with AKS

## Overview

This project demonstrates message processing using Azure Event Hubs and Azure Kubernetes Service.

**What it does:**
- Producer sends test messages to Azure Event Hubs (runs locally)
- Consumer reads messages from Event Hubs and logs them (runs in AKS)

**Phase 1 Scope:**
- Simple Event Hubs producer and consumer
- No Blob Storage checkpointing
- No Service Bus
- No autoscaling
- Just the basics: send and receive messages


## Architecture

```
Producer (Local Console) --> Azure Event Hubs --> Consumer (AKS Console App)
```

---

## Prerequisites

### Azure Resources
- Event Hubs Namespace + Event Hub
- Container Registry (ACR)
- AKS Cluster

### Local Tools
- .NET 8 SDK
- Docker Desktop
- kubectl
- Azure CLI

---

## Setup

### 1. Create Azure Resources

```bash
# Variables
RG="my-rg"
LOCATION="eastus"
EH_NS="myeventhub123"
EH_NAME="messages"
ACR="myacr123"
AKS="myaks"

# Create Event Hubs
az eventhubs namespace create -n $EH_NS -g $RG -l $LOCATION --sku Standard
az eventhubs eventhub create -n $EH_NAME --namespace-name $EH_NS -g $RG --partition-count 2

# Get connection string
az eventhubs namespace authorization-rule keys list \
  --namespace-name $EH_NS -g $RG \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv

# Create AKS and ACR
az acr create -n $ACR -g $RG --sku Basic
az aks create -n $AKS -g $RG --node-count 1 --node-vm-size Standard_B2s --generate-ssh-keys
az aks update -n $AKS -g $RG --attach-acr $ACR
az aks get-credentials -n $AKS -g $RG
```

### 2. Run Producer Locally

```bash
cd src/Producer

# Option 1: Use appsettings.Development.json
# Edit appsettings.Development.json and add your connection string
export DOTNET_ENVIRONMENT=Development
dotnet run

# Option 2: Use environment variables
export EVENTHUB_CONNECTION_STRING="your-connection-string"
export EVENTHUB_NAME="messages"
dotnet run
```

Select option 2 to send 10 messages.

### 3. Deploy Consumer to AKS

```bash
# Build and push Docker image
cd src/Consumer
az acr login -n $ACR
docker build -t $ACR.azurecr.io/eventhub-consumer:v1 -f Dockerfile ..
docker push $ACR.azurecr.io/eventhub-consumer:v1

# Update k8s/configmap.yaml with your Event Hub name
# Update k8s/deployment.yaml with your ACR name

# Create secret with base64-encoded connection string
echo -n "your-eventhub-connection-string" | base64
# Copy the output

# Create k8s/secret.yaml from template and paste the base64 value
cp k8s/secret.yaml.template k8s/secret.yaml
# Edit secret.yaml and replace <BASE64_ENCODED_CONNECTION_STRING>

# Deploy to AKS
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/deployment.yaml

# Verify deployment
kubectl get pods
kubectl logs -f deployment/eventhub-consumer
```

### 4. Test End-to-End

Terminal 1 - Watch consumer logs:
```bash
kubectl logs -f deployment/eventhub-consumer
```

Terminal 2 - Send messages:
```bash
cd src/Producer
export DOTNET_ENVIRONMENT=Development
dotnet run
# Select option 2 to send 10 messages
```

You should see messages appearing in the consumer logs.

---

## Project Structure

```
ShivaProject/
├── README.md
├── azure-config.template.sh
├── ShivaProject.sln
├── src/
│   ├── Producer/                  # Runs locally
│   │   ├── Program.cs
│   │   ├── MessageGenerator.cs
│   │   ├── Producer.csproj
│   │   └── appsettings.json
│   └── Consumer/                  # Runs in AKS
│       ├── Program.cs
│       ├── Consumer.csproj
│       ├── Dockerfile
│       └── appsettings.json
└── k8s/
    ├── configmap.yaml
    ├── secret.yaml.template
    └── deployment.yaml
```

---

## Troubleshooting

### Producer
- Verify connection string and Event Hub name are set
- Check connection string has Send permissions

### Consumer
- Check pod logs: `kubectl logs <pod-name>`
- Verify connection string in secret: `kubectl get secret azure-secrets -o yaml`
- Ensure ACR is attached to AKS: `az aks check-acr -n $AKS -g $RG --acr $ACR`
- Check pod status: `kubectl describe pod <pod-name>`

### No Messages Appearing
- Verify Producer sends to correct Event Hub
- Consumer reads from EventPosition.Latest (only new messages)
- Send messages AFTER consumer is running
- Check Event Hub metrics in Azure Portal

---

## Key Concepts

**Event Hubs**: Azure messaging service for event streaming

**EventHubConsumerClient**: Simple client for reading messages (no checkpointing)

**Kubernetes Deployment**: Runs containerized apps in AKS

**ConfigMap vs Secret**:
- ConfigMap: Non-sensitive config (Event Hub name)
- Secret: Sensitive data (connection string)

---

## Phase 1 vs Phase 2

**Phase 1 (Current):**
- Producer sends messages
- Consumer reads and logs messages
- No checkpointing
- No Blob Storage
- No Service Bus

**Phase 2 (Future):**
- Add checkpointing with Blob Storage
- Forward messages to Service Bus
- Add autoscaling
- Add monitoring

---

## Success Criteria

Phase 1 complete when:
1. Producer sends messages to Event Hubs
2. Consumer pod runs in AKS
3. Consumer logs show received messages
4. You can explain the architecture

---

## Resources

- [Azure Event Hubs](https://docs.microsoft.com/azure/event-hubs/)
- [Azure Kubernetes Service](https://docs.microsoft.com/azure/aks/)
- [EventHubConsumerClient](https://docs.microsoft.com/dotnet/api/azure.messaging.eventhubs.consumer.eventhubconsumerclient)
