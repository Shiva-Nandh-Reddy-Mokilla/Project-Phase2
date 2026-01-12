# High Availability & Producer Deployment Guide

## Overview

This guide covers two enhancements:
1. **High Availability for Consumer** - Multiple replicas + health checks (Stretch Phase!)
2. **Deploy Producer to AKS** - Run producer as a container

---

## Part 1: High Availability for Consumer

### What Changed

#### ✅ Multiple Replicas (2 pods instead of 1)

**Before:**
```yaml
replicas: 1  # Single point of failure
```

**After:**
```yaml
replicas: 2  # Backup pod available
```

**Benefits:**
- ✅ If one pod crashes, the other keeps processing messages
- ✅ No downtime during deployments (rolling updates)
- ✅ Load is distributed across partitions

**How Event Hubs partitions work with multiple consumers:**
```
Event Hub (4 partitions)
  ├─ Partition 0 → Consumer Pod 1
  ├─ Partition 1 → Consumer Pod 1
  ├─ Partition 2 → Consumer Pod 2
  └─ Partition 3 → Consumer Pod 2

If Pod 1 crashes:
  ├─ Partition 0 → Consumer Pod 2 (takes over)
  ├─ Partition 1 → Consumer Pod 2 (takes over)
  ├─ Partition 2 → Consumer Pod 2
  └─ Partition 3 → Consumer Pod 2
```

---

#### ✅ Liveness & Readiness Probes (Stretch Phase Requirement!)

**Liveness Probe:**
- Checks if the pod is healthy
- **Action if fails:** Kubernetes **restarts** the pod
- Check: Is dotnet process running?
- Frequency: Every 10 seconds

**Readiness Probe:**
- Checks if the pod is ready to receive traffic
- **Action if fails:** Kubernetes **stops sending traffic** to this pod
- Check: Is dotnet process running?
- Frequency: Every 5 seconds

**Benefits:**
- ✅ Faster failure detection
- ✅ Automatic recovery
- ✅ No manual intervention needed
- ✅ **Satisfies Stretch Phase requirement!** 🎯

---

### Deploy the Changes

#### Step 1: Apply Updated Deployment

```bash
cd /Users/shivanandh/Downloads/ShivaProject-Phase2

# Apply updated deployment (now has 2 replicas + health checks)
kubectl apply -f k8s/deployment.yaml
```

#### Step 2: Verify Multiple Pods

```bash
# You should see 2 pods running
kubectl get pods -l app=eventhub-consumer

# Expected output:
# NAME                                 READY   STATUS    RESTARTS   AGE
# eventhub-consumer-abc123-xyz789     1/1     Running   0          1m
# eventhub-consumer-abc123-def456     1/1     Running   0          1m
```

#### Step 3: Test High Availability

**Test 1: Delete one pod (simulate crash)**
```bash
# Get pod names
kubectl get pods -l app=eventhub-consumer

# Delete one pod
kubectl delete pod eventhub-consumer-abc123-xyz789

# Watch what happens
kubectl get pods -l app=eventhub-consumer -w

# Result: 
# - Deleted pod terminates
# - Kubernetes immediately creates a new pod
# - Other pod continues processing (no downtime!)
```

**Test 2: Check health probe status**
```bash
# Describe a pod to see probe status
kubectl describe pod <pod-name>

# Look for:
# Liveness:  exec [/bin/sh -c ps aux | grep -v grep | grep dotnet || exit 1]
# Readiness: exec [/bin/sh -c ps aux | grep -v grep | grep dotnet || exit 1]
```

---

### Scaling Up/Down

```bash
# Scale to 3 replicas for even more redundancy
kubectl scale deployment eventhub-consumer --replicas=3

# Scale back to 1 (not recommended for production)
kubectl scale deployment eventhub-consumer --replicas=1

# Or edit the deployment YAML and apply
# Edit k8s/deployment.yaml -> change replicas: 2 to replicas: 3
kubectl apply -f k8s/deployment.yaml
```

---

## Part 2: Deploy Producer to AKS

### Why Deploy Producer to AKS?

**Option A: Keep Producer on Local Mac (Simpler)**
- ✅ Easy to start/stop for testing
- ✅ No container needed
- ✅ Good for Phase 2 demo
- ❌ Mac must be online

**Option B: Deploy Producer to AKS (Better for Learning)**
- ✅ Runs 24/7 in cloud
- ✅ Learn containerization for both apps
- ✅ More realistic architecture
- ✅ Good for presentation
- ❌ Slightly more complex

---

### Deployment Steps

#### Step 1: Build Producer Docker Image

**Option A: Using GitHub Actions (Recommended)**

1. Commit the new Producer files:
   ```bash
   cd /Users/shivanandh/Downloads/ShivaProject-Phase2
   git add src/Producer/Dockerfile
   git add k8s/producer-*.yaml
   git add .github/workflows/build-push.yml
   git commit -m "Add Producer Docker support and deployment"
   git push
   ```

2. GitHub Actions will automatically:
   - Build `eventhub-producer:v1` image
   - Push to your ACR: `shivaacr123.azurecr.io`

**Option B: Build Locally (If you want to test first)**

```bash
# Build
docker build \
  -t shivaacr123.azurecr.io/eventhub-producer:v1 \
  -f src/Producer/Dockerfile \
  .

# Login to ACR
docker login shivaacr123.azurecr.io \
  -u shivaacr123 \
  -p <ACR_PASSWORD>

# Push
docker push shivaacr123.azurecr.io/eventhub-producer:v1
```

---

#### Step 2: Deploy Producer ConfigMap

```bash
kubectl apply -f k8s/producer-configmap.yaml
```

**Verify:**
```bash
kubectl get configmap producer-config
kubectl describe configmap producer-config
```

---

#### Step 3: Deploy Producer to AKS

```bash
kubectl apply -f k8s/producer-deployment.yaml
```

**Verify:**
```bash
# Check pod status
kubectl get pods -l app=eventhub-producer

# Should see:
# NAME                                 READY   STATUS    RESTARTS   AGE
# eventhub-producer-abc123-xyz789     1/1     Running   0          30s
```

---

#### Step 4: View Producer Logs

```bash
# Terminal
kubectl logs -f deployment/eventhub-producer

# Expected output:
# Event Hubs Producer Starting...
# Connected to Event Hub: my-event-hub
# Sending messages... (Press Ctrl+C to stop)
# [1] Sent: 123 at 2026-01-12T...
# [2] Sent: 456 at 2026-01-12T...
```

**Or in Azure Portal:**
- Workloads → Pods → eventhub-producer-xxxxx → Live logs

---

### Managing Producer

#### Start/Stop Producer

```bash
# Stop (scale to 0)
kubectl scale deployment eventhub-producer --replicas=0

# Start (scale to 1)
kubectl scale deployment eventhub-producer --replicas=1
```

#### Change Message Send Interval

Edit `k8s/producer-configmap.yaml`:
```yaml
data:
  MESSAGE_INTERVAL_SECONDS: "5"  # Change from 2 to 5 seconds
```

Apply:
```bash
kubectl apply -f k8s/producer-configmap.yaml
kubectl rollout restart deployment/eventhub-producer
```

#### Delete Producer

```bash
kubectl delete -f k8s/producer-deployment.yaml
kubectl delete -f k8s/producer-configmap.yaml
```

---

## Architecture After These Changes

### Before (Phase 2 Basic)
```
┌────────────────┐
│ Your Mac       │
│ - Producer     │  → Event Hubs
└────────────────┘

┌────────────────┐
│ AKS            │
│ - Consumer x1  │  → Event Hubs → Storage
└────────────────┘         ↓
                      Service Bus
```

### After (Phase 2 + High Availability + Stretch Phase)
```
┌────────────────────────────────────────┐
│ AKS Cluster                            │
│                                        │
│  ┌─────────────────┐                  │
│  │ Producer Pod x1 │ → Event Hubs     │
│  └─────────────────┘                  │
│                                        │
│  ┌─────────────────┐                  │
│  │ Consumer Pod x2 │ → Event Hubs     │
│  │ (HA + Probes)   │     ↓            │
│  └─────────────────┘   Storage        │
│                           ↓            │
│                      Service Bus      │
└────────────────────────────────────────┘
```

**Benefits:**
- ✅ No manual intervention needed (both run in cloud)
- ✅ High availability (Consumer has backup pod)
- ✅ Health monitoring (Liveness/Readiness probes)
- ✅ Auto-restart on failures
- ✅ Realistic production-like setup

---

## Testing the Complete Flow

### Test 1: End-to-End with Both in AKS

```bash
# Watch Consumer logs (Terminal 1)
kubectl logs -f deployment/eventhub-consumer

# Watch Producer logs (Terminal 2)
kubectl logs -f deployment/eventhub-producer

# You should see:
# Producer: [1] Sent: 123
# Consumer: ✓ Received: 123
# Consumer: ✓ Stored to Blob
# Consumer: ✓ Forwarded to Service Bus (if 123 % 5 == 0)
```

### Test 2: Verify in Azure Portal

1. **Storage Account** → **processed-messages** container
   - See new blobs: `eventhub/2026-01-12/123_xxx.json`

2. **Service Bus** → **retry-queue**
   - See messages where ID % 5 == 0

3. **AKS** → **Workloads** → **Pods**
   - See 2 consumer pods + 1 producer pod all Running

### Test 3: High Availability Test

```bash
# Delete one consumer pod
kubectl delete pod -l app=eventhub-consumer | head -1

# Watch what happens:
kubectl get pods -w

# Result:
# - Deleted pod terminates
# - Kubernetes creates replacement immediately
# - Producer keeps sending (no interruption)
# - Other consumer pod keeps processing (no message loss!)
```

---

## Stretch Phase Items Completed

With these changes, you've now completed:

✅ **Configuration Hygiene** (Phase 2)
- Separate ConfigMap vs Secret ✅
- Documented environment variables ✅

✅ **Liveness and Readiness Probes** (Stretch Phase)
- Consumer has both probes ✅
- Producer has both probes ✅
- Automatic failure detection and restart ✅

---

## What's Left for Stretch Phase?

Pick 1-2 more from:

⏳ **Structured Logging**
- Upgrade console logs to JSON format
- Example provided in: `examples/StructuredLoggingExample.cs`

⏳ **Retry Policies**
- Add retry logic to Azure SDK clients
- Handle transient failures gracefully

⏳ **Autoscaling (HPA)**
- Scale Consumer based on CPU usage
- `kubectl autoscale deployment eventhub-consumer --cpu-percent=70 --min=2 --max=5`

---

## Costs

Running both Producer and Consumer in AKS:

| Resource | Cost |
|----------|------|
| AKS nodes (1 node) | ~$70/month |
| Event Hubs | Free tier or ~$10/month |
| Storage | ~$1/month |
| Service Bus | Free tier or ~$1/month |
| **Total** | **~$72-82/month** |

**For learning:** Delete resources when not in use to minimize costs!

```bash
# Stop producer when not testing
kubectl scale deployment eventhub-producer --replicas=0

# Scale down consumer
kubectl scale deployment eventhub-consumer --replicas=1
```

---

## Troubleshooting

### Producer Pod Not Starting

```bash
# Check pod status
kubectl get pods -l app=eventhub-producer

# View logs
kubectl logs deployment/eventhub-producer

# Check events
kubectl describe pod <producer-pod-name>

# Common issues:
# - Image not found: Run GitHub Actions or build manually
# - Connection string missing: Check secret is applied
# - Out of memory: Check resource limits
```

### Consumer Pods Restarting

```bash
# Check restart count
kubectl get pods -l app=eventhub-consumer

# If restarts > 0, check logs
kubectl logs <pod-name> --previous

# Check probe failures
kubectl describe pod <pod-name>
# Look for: "Liveness probe failed" or "Readiness probe failed"
```

---

## Summary

**What you achieved:**

1. ✅ High Availability: Consumer has 2 replicas
2. ✅ Health Monitoring: Liveness & Readiness probes (Stretch Phase!)
3. ✅ Producer in AKS: No need for local Mac
4. ✅ Complete cloud deployment: Both apps run in Azure
5. ✅ Production-ready setup: Auto-restart, load distribution, fault tolerance

**Next steps:**

- Collect screenshots for presentation
- Consider adding 1-2 more Stretch Phase items
- Practice explaining the architecture
- Be ready to demo failover (delete pod, watch recovery)

🎉 **You now have a production-like, highly available system!**

