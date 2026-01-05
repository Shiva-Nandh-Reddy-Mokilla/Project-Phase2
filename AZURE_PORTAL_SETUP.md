# Azure Portal Setup Guide - Phase 2

This document provides step-by-step instructions for creating Azure resources in the Azure Portal.

---

## Step 1: Create Storage Account

### 1.1 Create the Storage Account

1. Go to **Azure Portal** (portal.azure.com)
2. Click **Create a resource** (top left) OR search for "Storage accounts"
3. Click **Create**
4. Fill in the basics:
   - **Subscription**: Select your subscription
   - **Resource group**: Use the same resource group as your Event Hub (e.g., `my-rg`)
   - **Storage account name**: Enter a unique name (e.g., `shivastorage123`)
     - Must be globally unique
     - 3-24 characters
     - Lowercase letters and numbers only
     - No dashes or special characters
   - **Region**: Select the same region as your Event Hub (e.g., `East US`)
   - **Performance**: **Standard**
   - **Redundancy**: **Locally-redundant storage (LRS)**
5. Click **Review + Create**
6. Click **Create**
7. Wait for deployment to complete (30-60 seconds)
8. Click **Go to resource**

### 1.2 Create Blob Containers

1. In your Storage Account, look at the left menu
2. Under **Data storage**, click **Containers**
3. Click **+ Container** (top left)
4. Create first container:
   - **Name**: `processed-messages`
   - **Public access level**: **Private (no anonymous access)**
   - Click **Create**
5. Click **+ Container** again to create second container:
   - **Name**: `checkpoints`
   - **Public access level**: **Private**
   - Click **Create**

You should now see both containers listed.

### 1.3 Get Storage Connection String

1. In your Storage Account, look at the left menu
2. Under **Security + networking**, click **Access keys**
3. You'll see **key1** and **key2**
4. Under **key1**, find **Connection string**
5. Click **Show** button
6. Click the **Copy** icon to copy the connection string
7. **SAVE THIS** - You'll need it later for Kubernetes secrets

The connection string looks like:
```
DefaultEndpointsProtocol=https;AccountName=shivastorage123;AccountKey=ABC123...==;EndpointSuffix=core.windows.net
```

---

## Step 2: Create Service Bus

### 2.1 Create Service Bus Namespace

1. Go to **Azure Portal**
2. Click **Create a resource** OR search for "Service Bus"
3. Click **Create**
4. Fill in the basics:
   - **Subscription**: Select your subscription
   - **Resource group**: Use the same resource group (e.g., `my-rg`)
   - **Namespace name**: Enter a unique name (e.g., `shivaservicebus123`)
     - Must be globally unique
     - 6-50 characters
     - Letters, numbers, and hyphens
   - **Location**: Select the same region as your Event Hub (e.g., `East US`)
   - **Pricing tier**: **Standard**
     - ⚠️ **Important**: Don't select Basic! Basic tier doesn't support queues.
5. Click **Review + Create**
6. Click **Create**
7. Wait for deployment to complete (1-2 minutes)
8. Click **Go to resource**

### 2.2 Create a Queue

1. In your Service Bus Namespace, look at the left menu
2. Under **Entities**, click **Queues**
3. Click **+ Queue** (top left)
4. Fill in:
   - **Name**: `retry-queue`
   - **Max queue size**: 1 GB (default)
   - **Message time to live**: 14 days (default)
   - Leave other settings as default
5. Click **Create**

You should now see `retry-queue` in the list.

### 2.3 Get Service Bus Connection String

1. In your Service Bus Namespace, look at the left menu
2. Under **Settings**, click **Shared access policies**
3. You'll see **RootManageSharedAccessKey** - click on it
4. A panel opens on the right showing connection strings
5. Find **Primary Connection String**
6. Click the **Copy** icon
7. **SAVE THIS** - You'll need it later for Kubernetes secrets

The connection string looks like:
```
Endpoint=sb://shivaservicebus123.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ABC123...=
```

---

## Step 3: Verify Your Event Hub Connection String

You should already have this from Phase 1, but if you need to get it again:

1. Go to **Azure Portal**
2. Search for your Event Hubs Namespace (e.g., `shivaeh123`)
3. Click on it
4. In the left menu, under **Settings**, click **Shared access policies**
5. Click **RootManageSharedAccessKey**
6. Copy the **Primary Connection String**

The connection string looks like:
```
Endpoint=sb://shivaeh123.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=ABC123...=;EntityPath=messages
```

---

## Summary - What You Should Have Now

✅ **Storage Account** with 2 containers:
   - `processed-messages`
   - `checkpoints`

✅ **Service Bus Namespace** with 1 queue:
   - `retry-queue`

✅ **Three connection strings saved**:
   1. Event Hub connection string
   2. Storage Account connection string
   3. Service Bus connection string

---

## Next: Configure Kubernetes

Now that you have all Azure resources and connection strings, you need to:

1. **Base64 encode the connection strings**:
   ```bash
   echo -n "YOUR_EVENTHUB_CONNECTION_STRING" | base64
   echo -n "YOUR_STORAGE_CONNECTION_STRING" | base64
   echo -n "YOUR_SERVICEBUS_CONNECTION_STRING" | base64
   ```

2. **Create k8s/secret.yaml** from template:
   ```bash
   cp k8s/secret.yaml.template k8s/secret.yaml
   ```

3. **Edit k8s/secret.yaml** and paste the base64 values

4. **Update k8s/configmap.yaml** with your Event Hub name

5. **Build and deploy** - see main README.md

---

## Tips

### Finding Resources Later

To find your resources easily:
1. Go to **Azure Portal** → **All resources**
2. Filter by your resource group name
3. You should see:
   - Event Hubs Namespace
   - Storage Account
   - Service Bus Namespace
   - Container Registry (ACR)
   - Kubernetes Service (AKS)

### Verifying Resources Work

**Storage Account**:
- Go to Containers → You should see `processed-messages` and `checkpoints`
- After running the app, you'll see `.json` files appear in `processed-messages`

**Service Bus Queue**:
- Go to Queues → `retry-queue` → Overview
- Look at **Active message count** (should be 0 when processed)
- Look at **Completed messages** in metrics

### Cost Management

All resources created here use the cheapest tiers suitable for development:
- **Storage**: Standard LRS (cheapest)
- **Service Bus**: Standard (needed for queues, ~$10/month)
- **Event Hubs**: Standard tier (from Phase 1)

**To avoid charges**: Delete resources when done testing!

---

## Troubleshooting

### "Name already taken" error when creating resources
- Storage Account names must be globally unique
- Service Bus namespace names must be globally unique
- Try adding more numbers or your initials (e.g., `shivastorage456`)

### Can't find connection string
- Storage: Access keys → key1 → Connection string
- Service Bus: Shared access policies → RootManageSharedAccessKey → Primary Connection String
- Event Hubs: Shared access policies → RootManageSharedAccessKey → Primary Connection String

### Service Bus creation fails with "Basic tier" selected
- Basic tier doesn't support queues
- Select **Standard** tier instead

---

## Authentication Explained

### Connection Strings - What Are They?

Connection strings contain:
1. **Endpoint**: The URL of your Azure service
2. **Credential**: Access key or SAS token
3. **Permissions**: What operations you can perform

Example breakdown:
```
Endpoint=sb://shivaservicebus123.servicebus.windows.net/
SharedAccessKeyName=RootManageSharedAccessKey
SharedAccessKey=ABC123...=
```

- `Endpoint`: Where to connect
- `SharedAccessKeyName`: Which policy to use
- `SharedAccessKey`: The secret key (like a password)

### Why Do We Base64 Encode?

Kubernetes Secrets store data in base64 format:
- **Not encryption!** Just encoding to handle special characters
- Anyone with cluster access can decode it
- For production, use Azure Key Vault or Managed Identity

### Better Alternative (Future)

Instead of connection strings, use **Managed Identity**:
- No secrets to manage
- Azure handles authentication automatically
- More secure
- But requires additional setup (out of scope for this exercise)

---

**Ready?** Once you have all 3 connection strings, go back to the main README.md and continue with "Step 2: Update ConfigMap"

