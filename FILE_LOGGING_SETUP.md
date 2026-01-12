# File-Based Logging in AKS (NOT RECOMMENDED)

## ⚠️ Warning
This approach is NOT cloud-native and has significant drawbacks:
- Complex setup
- Performance overhead
- Costs extra for Azure File Share
- Hard to query/analyze logs
- Better alternatives exist (Console logs, Application Insights)

## If You Still Want It...

### Architecture

```
┌─────────────────────────────────────────┐
│ AKS Pod                                 │
│                                         │
│ /mnt/logs/app.log  ← Mounted volume    │
│      ↓                                  │
│      └─ Writes to Azure File Share     │
└─────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────┐
│ Azure File Share (Persistent Storage)   │
│                                         │
│ shivastorage123/logs/app.log           │
│                                         │
│ Access: Azure Portal → Storage Account │
└─────────────────────────────────────────┘
```

---

## Step 1: Create Azure File Share

### Via Azure Portal:

1. Go to: **Storage Accounts** → **shivastorage123**
2. Left menu → **File shares**
3. Click **+ File share**
4. Name: `applogs`
5. Tier: **Transaction optimized**
6. Click **Create**

### Get Access Key:

1. Storage Account → **Access keys**
2. Copy **key1** (you already have this)

---

## Step 2: Create Kubernetes Secret for Storage

```bash
# Create secret with storage account credentials
kubectl create secret generic azure-fileshare-secret \
  --from-literal=azurestorageaccountname=shivastorage123 \
  --from-literal=azurestorageaccountkey=<YOUR_STORAGE_KEY>
```

---

## Step 3: Create PersistentVolume and PersistentVolumeClaim

Create file: `k8s/persistent-volume.yaml`

```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: applogs-pv
spec:
  capacity:
    storage: 5Gi
  accessModes:
    - ReadWriteMany
  azureFile:
    secretName: azure-fileshare-secret
    shareName: applogs
    readOnly: false
  mountOptions:
    - dir_mode=0777
    - file_mode=0777
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: applogs-pvc
  namespace: default
spec:
  accessModes:
    - ReadWriteMany
  resources:
    requests:
      storage: 5Gi
```

Apply:
```bash
kubectl apply -f k8s/persistent-volume.yaml
```

---

## Step 4: Update Deployment to Mount Volume

Edit `k8s/deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: eventhub-consumer
spec:
  template:
    spec:
      containers:
      - name: consumer
        image: shivaacr123.azurecr.io/eventhub-consumer:v2
        # ... existing env vars ...
        volumeMounts:
        - name: logs-volume
          mountPath: /app/logs  # ← Where to mount in container
      volumes:
      - name: logs-volume
        persistentVolumeClaim:
          claimName: applogs-pvc
```

---

## Step 5: Update C# Code to Write to File

Edit `src/Consumer/Program.cs`:

```csharp
// At the top of Main method
var logFilePath = "/app/logs/app.log";
var logDirectory = Path.GetDirectoryName(logFilePath);

// Create directory if it doesn't exist
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

// Helper method to log to both console and file
void LogMessage(string message)
{
    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
    var logLine = $"[{timestamp}] {message}";
    
    // Console (for kubectl logs and Azure Portal)
    Console.WriteLine(logLine);
    
    // File (persistent storage)
    try
    {
        File.AppendAllText(logFilePath, logLine + Environment.NewLine);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to write to log file: {ex.Message}");
    }
}

// Replace Console.WriteLine with LogMessage
processor.ProcessEventAsync += async (args) =>
{
    var messageId = body["messageId"]?.ToString();
    LogMessage($"✓ Received: {messageId}");
    // ... rest of processing ...
};
```

---

## Step 6: Access Logs

### Via Azure Portal:
1. Go to **Storage Account** → **shivastorage123**
2. Click **File shares** → **applogs**
3. Click **app.log**
4. Click **Download** to view

### Via kubectl:
```bash
kubectl exec -it <pod-name> -- tail -f /app/logs/app.log
```

---

## Costs

- Azure File Share: ~$0.10/GB/month
- Transactions: ~$0.10 per 10,000 transactions
- Estimated: **$2-5/month** for basic logging

---

## Drawbacks

❌ **Performance**: File I/O is slower than console  
❌ **Complexity**: More moving parts to maintain  
❌ **Single file**: All pods write to same file (potential conflicts)  
❌ **No structure**: Plain text is hard to query  
❌ **Costs money**: Storage + transactions  

---

## Better Alternative: Structured Console Logs

Instead of file logging, enhance your console logs:

```csharp
using System.Text.Json;

void LogStructured(string eventType, object data)
{
    var logEntry = new
    {
        timestamp = DateTime.UtcNow,
        level = "INFO",
        eventType = eventType,
        data = data
    };
    
    Console.WriteLine(JsonSerializer.Serialize(logEntry));
}

// Usage:
LogStructured("message_received", new { messageId, source = "EventHub" });
LogStructured("stored_to_blob", new { messageId, blobName });
LogStructured("forwarded_to_servicebus", new { messageId });
```

**Benefits:**
- ✅ Free (no extra storage costs)
- ✅ Works with Azure Log Analytics
- ✅ Easy to parse and query
- ✅ **Satisfies Stretch Phase "Structured Logging" requirement!** 🎯

---

## Recommendation

**For learning/Phase 2:** Keep using console logs (what you have now)  
**For Stretch Phase:** Add structured JSON logging to console  
**For production:** Use Azure Application Insights  
**Avoid:** File-based logging in Kubernetes

