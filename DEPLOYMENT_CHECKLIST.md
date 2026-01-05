# Phase 2 Deployment Checklist

Use this as a quick reference while deploying. Check off each item as you complete it.

---

## 📋 Pre-Deployment (Azure Portal)

### Storage Account
- [ ] Created Storage Account (e.g., `shivastorage123`)
- [ ] Created container: `processed-messages`
- [ ] Created container: `checkpoints`
- [ ] Copied Storage connection string
- [ ] Saved connection string somewhere safe

### Service Bus
- [ ] Created Service Bus Namespace (e.g., `shivaservicebus123`)
- [ ] Selected **Standard** pricing tier (not Basic!)
- [ ] Created queue: `retry-queue`
- [ ] Copied Service Bus connection string
- [ ] Saved connection string somewhere safe

### Event Hub (Verify)
- [ ] Have Event Hub connection string from Phase 1
- [ ] Know Event Hub name (e.g., `messages`)

---

## 📋 Configuration (Local Machine)

### Base64 Encode Connection Strings
```bash
echo -n "YOUR_EVENTHUB_CONNECTION_STRING" | base64
echo -n "YOUR_STORAGE_CONNECTION_STRING" | base64
echo -n "YOUR_SERVICEBUS_CONNECTION_STRING" | base64
```

- [ ] Encoded Event Hub connection string
- [ ] Encoded Storage connection string
- [ ] Encoded Service Bus connection string
- [ ] Saved all 3 base64 strings

### Update ConfigMap
Edit `k8s/configmap.yaml`:
- [ ] Updated `EVENTHUB_NAME` to your Event Hub name

### Create Secret
```bash
cp k8s/secret.yaml.template k8s/secret.yaml
```
Edit `k8s/secret.yaml`:
- [ ] Pasted base64 Event Hub connection string
- [ ] Pasted base64 Storage connection string
- [ ] Pasted base64 Service Bus connection string
- [ ] Verified no extra spaces or newlines in the values

### Update Deployment
Edit `k8s/deployment.yaml`:
- [ ] Updated image name to your ACR (e.g., `myacr.azurecr.io/eventhub-consumer:v2`)

---

## 📋 Build and Push (Local Machine)

```bash
cd src/Consumer
```

### Build Docker Image
- [ ] Logged into ACR: `az acr login -n <YOUR_ACR>`
- [ ] Built image: `docker build -t <YOUR_ACR>.azurecr.io/eventhub-consumer:v2 -f Dockerfile ..`
- [ ] Build succeeded (no errors)

### Push to ACR
- [ ] Pushed image: `docker push <YOUR_ACR>.azurecr.io/eventhub-consumer:v2`
- [ ] Push succeeded
- [ ] Verified image in ACR (optional): `az acr repository show -n <ACR> --repository eventhub-consumer`

---

## 📋 Deploy to AKS

### Connect to AKS
- [ ] Connected to cluster: `kubectl config current-context`
- [ ] Correct cluster displayed

### Apply Configurations
```bash
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secret.yaml
kubectl apply -f k8s/deployment.yaml
```

- [ ] Applied configmap (no errors)
- [ ] Applied secret (no errors)
- [ ] Applied deployment (no errors)

### Verify Deployment
- [ ] Checked pods: `kubectl get pods`
- [ ] Pod is in "Running" state (not CrashLoopBackOff or Error)
- [ ] Checked logs: `kubectl logs -f deployment/eventhub-consumer`
- [ ] Logs show:
  - "Event Hubs Consumer Starting (Phase 2)..."
  - "Event Hub processor started"
  - "Service Bus processor started"
  - "Listening for messages..."

---

## 📋 Testing

### Terminal 1 - Watch Logs
```bash
kubectl logs -f deployment/eventhub-consumer
```
- [ ] Logs are streaming (no errors)

### Terminal 2 - Run Producer
```bash
cd src/Producer
export EVENTHUB_CONNECTION_STRING="..."
export EVENTHUB_NAME="messages"
dotnet run
```
- [ ] Producer started successfully
- [ ] Selected option 2 (send 10 messages)
- [ ] Producer shows "Sent: 1, 2, 3..." messages

### Verify Consumer Logs
- [ ] Consumer logs show "EVENT HUB MESSAGE RECEIVED"
- [ ] Logs show "✓ Stored to Blob: eventhub/..."
- [ ] For message 5 or 10: Logs show "✓ Forwarded to Service Bus"
- [ ] Logs show "SERVICE BUS MESSAGE RECEIVED"
- [ ] Logs show "✓ Stored to Blob: servicebus/..."

---

## 📋 Verification (Azure Portal)

### Blob Storage
1. Go to Storage Account → Containers → `processed-messages`
2. Look for folders: `eventhub/` and `servicebus/`

- [ ] `eventhub/` folder exists
- [ ] `eventhub/YYYY-MM-DD/` folder has JSON files
- [ ] `servicebus/` folder exists
- [ ] `servicebus/YYYY-MM-DD/` folder has JSON files
- [ ] Downloaded a JSON file and verified it contains message data

### Service Bus Queue
1. Go to Service Bus Namespace → Queues → `retry-queue` → Overview

- [ ] **Active message count**: 0 (all processed)
- [ ] **Completed messages** (in metrics): > 0

---

## 📋 Evidence Collection (For Presentation)

### Screenshots to Take

- [ ] Azure Portal - All resources view (showing EH, Storage, SB, ACR, AKS)
- [ ] Storage Account - `processed-messages` container showing folders
- [ ] Storage Account - Downloaded JSON file contents
- [ ] Service Bus - Queue overview showing metrics
- [ ] Consumer logs - Event Hub messages
- [ ] Consumer logs - Service Bus messages
- [ ] Producer output - Messages sent

### Document Challenges
- [ ] Write down any issues you encountered
- [ ] Document how you fixed them
- [ ] Note any questions you have about the architecture

---

## ✅ Success Criteria

You're done when ALL of these are true:

- [ ] Consumer pod is running in AKS
- [ ] Producer can send messages to Event Hubs
- [ ] Consumer receives and logs Event Hub messages
- [ ] ALL messages stored to Blob Storage (`eventhub/` folder)
- [ ] Messages with ID % 5 == 0 forwarded to Service Bus
- [ ] Consumer processes Service Bus messages
- [ ] Service Bus messages stored to Blob Storage (`servicebus/` folder)
- [ ] Can view JSON files in Azure Portal
- [ ] Service Bus queue shows 0 active messages
- [ ] Have screenshots of everything working

---

## 🆘 Troubleshooting Quick Reference

### Pod won't start (CrashLoopBackOff)
```bash
kubectl describe pod <pod-name>
kubectl logs <pod-name>
```
**Common causes:**
- Wrong connection strings in secret
- Missing environment variables
- Image not found in ACR

### Can't pull image
```bash
az aks update -n <aks-name> -g <rg> --attach-acr <acr-name>
```

### No messages in Blob Storage
- Check storage connection string
- Verify container name is `processed-messages`
- Check consumer logs for errors

### Service Bus messages not processing
- Verify Service Bus connection string
- Check queue name is `retry-queue`
- Look at queue metrics in portal

---

## 🎯 Quick Commands Reference

### View logs
```bash
kubectl logs -f deployment/eventhub-consumer
```

### Check pod status
```bash
kubectl get pods
kubectl describe pod <pod-name>
```

### Restart deployment
```bash
kubectl rollout restart deployment/eventhub-consumer
```

### Delete and recreate
```bash
kubectl delete -f k8s/deployment.yaml
kubectl apply -f k8s/deployment.yaml
```

### View secret (debugging)
```bash
kubectl get secret azure-secrets -o yaml
```

### Decode secret value (debugging)
```bash
kubectl get secret azure-secrets -o jsonpath='{.data.storage-connection}' | base64 --decode
```

---

**Print this checklist and check off items as you go!** ✨

