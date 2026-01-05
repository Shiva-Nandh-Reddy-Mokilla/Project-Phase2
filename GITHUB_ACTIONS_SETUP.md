# GitHub Actions Setup for Phase 2

This guide shows you how to deploy using GitHub Actions (same as Phase 1).

---

## ✅ What's Already Done

- ✅ GitHub Actions workflow created (`.github/workflows/deploy-consumer.yml`)
- ✅ Workflow configured for your AKS cluster: `phase1-aks`
- ✅ `.gitignore` configured to NOT commit secrets

---

## 🔐 Step 1: Set Up Azure Service Principal (If Not Already Done)

If you already have `AZURE_CREDENTIALS` secret in your GitHub repo from Phase 1, **skip to Step 2**.

If not, run this command to create Azure credentials for GitHub Actions:

```bash
# Get your subscription ID first
az account show --query id -o tsv

# Create service principal (replace YOUR_SUBSCRIPTION_ID)
az ad sp create-for-rbac --name "github-actions-shivaproject" \
  --role contributor \
  --scopes /subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/EventHub-test \
  --sdk-auth
```

This will output JSON like:
```json
{
  "clientId": "...",
  "clientSecret": "...",
  "subscriptionId": "...",
  "tenantId": "...",
  ...
}
```

**Copy the ENTIRE JSON output.**

---

## 🔐 Step 2: Add GitHub Secret

1. Go to your GitHub repository
2. Click **Settings** (top right)
3. In left sidebar: **Secrets and variables** → **Actions**
4. Click **New repository secret**
5. Name: `AZURE_CREDENTIALS`
6. Value: Paste the JSON from Step 1
7. Click **Add secret**

---

## 📤 Step 3: Commit and Push

```bash
cd /Users/shivanandh/Downloads/ShivaProject-Phase2

# Initialize git if not already (skip if already a git repo)
git init
git remote add origin YOUR_GITHUB_REPO_URL

# Stage all files (secret.yaml is already ignored)
git add .

# Commit Phase 2 changes
git commit -m "Phase 2: Add Storage and Service Bus integration"

# Push to GitHub
git push origin main
```

**Note**: If your default branch is `master` instead of `main`, use:
```bash
git push origin master
```

---

## 🎯 Step 4: Trigger GitHub Actions

### Option A: Automatic (Push Triggers It)
The workflow will automatically run when you push.

### Option B: Manual Trigger
1. Go to your GitHub repo → **Actions** tab
2. Click **Build and Deploy Consumer to AKS** workflow
3. Click **Run workflow** → **Run workflow**

---

## 👀 Step 5: Watch GitHub Actions Build

1. Go to your GitHub repo → **Actions** tab
2. Click on the running workflow
3. Watch the build and deploy process
4. All steps should turn green ✅

---

## ✅ Step 6: Verify Deployment

After GitHub Actions completes successfully:

```bash
# Connect to your AKS cluster
az aks get-credentials --resource-group EventHub-test --name phase1-aks

# Check pods
kubectl get pods

# Watch consumer logs
kubectl logs -f deployment/eventhub-consumer
```

You should see:
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

---

## 🧪 Step 7: Test End-to-End

**Terminal 1** - Watch logs:
```bash
kubectl logs -f deployment/eventhub-consumer
```

**Terminal 2** - Run producer:
```bash
cd src/Producer
export EVENTHUB_CONNECTION_STRING="Endpoint=sb://mynamespace-1.servicebus.windows.net/;SharedAccessKeyName=phase1-policy;SharedAccessKey=S3AHvrmH15wsumbx+L+ZWBekFlV062YK4+AEhDoHI2U="
export EVENTHUB_NAME="my-event-hub"
dotnet run
```

Select **option 2** to send 10 messages.

---

## 📸 Step 8: Verify in Azure Portal

### Check Blob Storage
1. Go to Azure Portal → **shivastorage123** → **Containers** → **processed-messages**
2. You should see folders:
   - `eventhub/2026-01-04/` with `.json` files
   - `servicebus/2026-01-04/` with `.json` files
3. Download a file to view the message

### Check Service Bus Queue
1. Go to Azure Portal → **shivaservicebus123** → **Queues** → **retry-queue**
2. Look at metrics:
   - **Active message count**: 0 (all processed)
   - **Completed messages**: > 0

---

## 🎉 Success Criteria

Phase 2 is complete when:
- ✅ GitHub Actions builds and deploys successfully (all green checks)
- ✅ Consumer pod runs in AKS
- ✅ Producer sends messages to Event Hubs
- ✅ Consumer logs show messages received from Event Hubs
- ✅ Messages stored in Blob Storage (`eventhub/` folder)
- ✅ Every 5th message forwarded to Service Bus
- ✅ Service Bus messages processed
- ✅ Service Bus messages stored in Blob Storage (`servicebus/` folder)
- ✅ Can view JSON files in Azure Portal

---

## 🆘 Troubleshooting

### GitHub Actions fails at "Azure Login"
- Make sure `AZURE_CREDENTIALS` secret is set correctly
- Verify the JSON has all required fields

### GitHub Actions fails at "Login to ACR"
- Ensure service principal has permission to access ACR
- Try re-creating the service principal with broader scope:
  ```bash
  az ad sp create-for-rbac --name "github-actions-shivaproject" \
    --role contributor \
    --scopes /subscriptions/YOUR_SUBSCRIPTION_ID \
    --sdk-auth
  ```

### GitHub Actions fails at "Deploy to AKS"
- The `k8s/secret.yaml` file should be ignored and NOT in git
- Secrets need to be created manually in AKS or stored as GitHub secrets
- Option: Convert connection strings to GitHub secrets and update workflow

### Pod crashes with "Missing configuration"
- Check that `k8s/secret.yaml` is applied manually:
  ```bash
  kubectl apply -f k8s/secret.yaml
  ```

---

## 💡 Alternative: Manual Secret Management

If GitHub Actions fails to deploy secrets, apply them manually:

```bash
# Connect to AKS
az aks get-credentials --resource-group EventHub-test --name phase1-aks

# Apply secret manually (once)
kubectl apply -f k8s/secret.yaml

# Then let GitHub Actions deploy the rest
```

---

**Ready? Follow the steps above and let me know if you hit any issues!** 🚀

