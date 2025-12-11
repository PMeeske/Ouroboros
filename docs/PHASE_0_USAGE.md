# Phase 0 Usage Guide

## Quick Start

Phase 0 introduces three main capabilities to Ouroboros:

1. **Feature Flags** - Control evolutionary features
2. **DAG Operations** - Manage pipeline snapshots
3. **Global Metrics** - Monitor system evolution

## Feature Flags

### Configuration

Add to `appsettings.json`:

```json
{
  "FeatureFlags": {
    "Embodiment": false,
    "SelfModel": true,
    "Affect": false
  }
}
```

### Programmatic Usage

```csharp
using LangChainPipeline.Core.Configuration;

// Create with specific flags
var flags = new FeatureFlags 
{ 
    SelfModel = true,
    Affect = true 
};

// Check if enabled
if (flags.SelfModel)
{
    // Use self-model features
}

// Get all enabled features
var enabled = flags.GetEnabledFeatures();
// Returns: ["SelfModel", "Affect"]

// Helper methods
bool anyEnabled = flags.AnyEnabled();      // true
bool allEnabled = flags.AllEnabled();      // false

// Factory methods
var allOn = FeatureFlags.AllOn();   // All enabled
var allOff = FeatureFlags.AllOff(); // All disabled (default)
```

### Integration with PipelineConfiguration

```csharp
var config = new PipelineConfiguration
{
    Features = new FeatureFlags { SelfModel = true }
};

// Or load from configuration
var configuration = builder.Build();
var pipelineConfig = configuration.GetSection("Pipeline")
    .Get<PipelineConfiguration>();
    
if (pipelineConfig.Features.SelfModel)
{
    // Initialize self-model capabilities
}
```

## DAG Operations

### CLI Commands

All DAG operations are accessed through the `dag` verb:

```bash
dotnet run -- dag --command <subcommand> [options]
```

### Creating Snapshots

Capture the current state of pipeline branches:

```bash
# Create snapshot of a branch
dotnet run -- dag --command snapshot --branch main

# Export snapshot to file
dotnet run -- dag --command snapshot --output snapshot.json

# Create snapshot of specific branch and export
dotnet run -- dag --command snapshot --branch production --output prod-snapshot.json
```

**Output**:
```
=== Creating DAG Snapshot ===
✓ Created epoch 1 (ID: abc123...)
  Branches: 1
  Created: 2025-12-10 23:00:00 UTC
  Exported to: snapshot.json
```

### Viewing Snapshots

Display information about epochs and metrics:

```bash
# Show latest epoch and global metrics
dotnet run -- dag --command show

# Show specific epoch
dotnet run -- dag --command show --epoch 1

# Show in JSON format
dotnet run -- dag --command show --format json

# Verbose output
dotnet run -- dag --command show --verbose
```

**Output (Summary)**:
```
=== DAG Information ===
Global Metrics:
  Total Epochs: 3
  Total Branches: 2
  Total Events: 42
  Average Events per Branch: 21.00
  Last Epoch: 2025-12-10 23:00:00 UTC

Latest Epoch:
Epoch 3:
  ID: def456...
  Created: 2025-12-10 23:00:00 UTC
  Branches: 2
    - main: 25 events, 10 vectors
    - feature: 17 events, 5 vectors
```

### Replaying Snapshots

Replay a snapshot from a file:

```bash
# Basic replay
dotnet run -- dag --command replay --input snapshot.json

# Replay with verbose output
dotnet run -- dag --command replay --input snapshot.json --verbose
```

**Output**:
```
=== DAG Replay ===
Replaying epoch 1 from snapshot.json
  Branches: 1
  Created: 2025-12-10 23:00:00 UTC

  Branch: main
    Events: 25
    Vectors: 10
      - ReasoningStep at 22:58:31
      - ReasoningStep at 22:59:15
      ... and 23 more
```

### Validating Snapshots

Verify the integrity of all snapshots:

```bash
# Validate all epochs
dotnet run -- dag --command validate

# Validate with verbose output
dotnet run -- dag --command validate --verbose
```

**Output**:
```
=== DAG Validation ===
Total epochs: 3
Epoch 1, Branch 'main': Hash a1b2c3d4...
Epoch 2, Branch 'main': Hash e5f6g7h8...
Epoch 3, Branch 'main': Hash i9j0k1l2...
✓ All snapshots validated successfully
```

### Retention Policy Evaluation

Evaluate which snapshots to keep based on retention policies:

```bash
# Age-based retention (dry run)
dotnet run -- dag --command retention --max-age-days 30 --dry-run

# Count-based retention
dotnet run -- dag --command retention --max-count 10

# Combined retention policy
dotnet run -- dag --command retention --max-age-days 7 --max-count 5

# Verbose output showing details
dotnet run -- dag --command retention --max-count 3 --dry-run --verbose
```

**Output**:
```
=== Retention Policy Evaluation (DRY RUN) ===
Retention Plan (DRY RUN): Keep 3 snapshots, Delete 2 snapshots

Snapshots to keep: 3
  ✓ main (2025-12-10 23:00:00)
  ✓ main (2025-12-10 22:00:00)
  ✓ main (2025-12-10 21:00:00)

Snapshots to delete: 2
  ✗ main (2025-12-10 20:00:00)
  ✗ main (2025-12-10 19:00:00)

Note: Actual deletion not implemented in this phase
```

## Programmatic Usage

### Hash Integrity

```csharp
using LangChainPipeline.Pipeline.Branches;

// Compute hash
var snapshot = await BranchSnapshot.Capture(branch);
var hash = BranchHash.ComputeHash(snapshot);
Console.WriteLine($"Hash: {hash}");

// Verify hash
var isValid = BranchHash.VerifyHash(snapshot, expectedHash);
if (!isValid)
{
    Console.WriteLine("Snapshot integrity violation detected!");
}

// Get snapshot with hash
var (snap, hashValue) = BranchHash.WithHash(snapshot);
```

### Retention Policies

```csharp
using LangChainPipeline.Pipeline.Branches;

// Create retention policy
var policy = RetentionPolicy.ByAge(TimeSpan.FromDays(30));
// or
var policy = RetentionPolicy.ByCount(10);
// or
var policy = RetentionPolicy.Combined(TimeSpan.FromDays(30), 10);

// Create snapshot metadata
var snapshots = new List<SnapshotMetadata>
{
    new SnapshotMetadata
    {
        Id = "snap1",
        BranchName = "main",
        CreatedAt = DateTime.UtcNow.AddDays(-40),
        Hash = "hash1",
        SizeBytes = 1024
    },
    // ... more snapshots
};

// Evaluate retention
var plan = RetentionEvaluator.Evaluate(snapshots, policy, dryRun: true);

Console.WriteLine(plan.GetSummary());
Console.WriteLine($"Keep: {plan.ToKeep.Count}");
Console.WriteLine($"Delete: {plan.ToDelete.Count}");

// Process retention plan
if (!plan.IsDryRun)
{
    foreach (var snapshot in plan.ToDelete)
    {
        // Delete snapshot
        await DeleteSnapshotAsync(snapshot.Id);
    }
}
```

### Global Projection Service

```csharp
using LangChainPipeline.Pipeline.Branches;

var service = new GlobalProjectionService();

// Create epoch
var branches = new[] { branch1, branch2 };
var metadata = new Dictionary<string, object>
{
    ["version"] = "1.0",
    ["environment"] = "production"
};

var result = await service.CreateEpochAsync(branches, metadata);
if (result.IsSuccess)
{
    var epoch = result.Value;
    Console.WriteLine($"Created epoch {epoch.EpochNumber}");
}

// Query epochs
var latest = service.GetLatestEpoch();
if (latest.IsSuccess)
{
    Console.WriteLine($"Latest: Epoch {latest.Value.EpochNumber}");
}

var specific = service.GetEpoch(epochNumber: 5);
var range = service.GetEpochsInRange(
    start: DateTime.UtcNow.AddDays(-7),
    end: DateTime.UtcNow
);

// Get metrics
var metricsResult = service.GetMetrics();
if (metricsResult.IsSuccess)
{
    var metrics = metricsResult.Value;
    Console.WriteLine($"Total Epochs: {metrics.TotalEpochs}");
    Console.WriteLine($"Total Branches: {metrics.TotalBranches}");
    Console.WriteLine($"Total Events: {metrics.TotalEvents}");
    Console.WriteLine($"Avg Events/Branch: {metrics.AverageEventsPerBranch:F2}");
}

// Clear all epochs (testing)
service.Clear();
```

## Common Workflows

### Workflow 1: Regular Snapshot Creation

Create snapshots at regular intervals:

```bash
# Hourly cron job
0 * * * * cd /app && dotnet run -- dag --command snapshot --output /snapshots/hourly-$(date +\%Y\%m\%d-\%H).json

# Daily snapshot
0 0 * * * cd /app && dotnet run -- dag --command snapshot --output /snapshots/daily-$(date +\%Y\%m\%d).json
```

### Workflow 2: Retention Management

Weekly retention policy evaluation:

```bash
#!/bin/bash
# weekly-retention.sh

# Dry run first to preview
dotnet run -- dag --command retention \
  --max-age-days 30 \
  --max-count 50 \
  --dry-run \
  --verbose > retention-preview.txt

# Review preview
cat retention-preview.txt

# Execute (when ready to enable deletion in future phases)
# dotnet run -- dag --command retention \
#   --max-age-days 30 \
#   --max-count 50
```

### Workflow 3: Snapshot Validation

Daily integrity checks:

```bash
#!/bin/bash
# daily-validation.sh

echo "=== Daily Snapshot Validation ===" | tee validation.log
dotnet run -- dag --command validate --verbose | tee -a validation.log

# Check exit code
if [ $? -eq 0 ]; then
    echo "✓ Validation passed" | tee -a validation.log
else
    echo "✗ Validation failed - check validation.log" | tee -a validation.log
    # Send alert
    mail -s "Snapshot Validation Failed" admin@example.com < validation.log
fi
```

### Workflow 4: Disaster Recovery

Export and backup critical snapshots:

```bash
#!/bin/bash
# backup-snapshots.sh

BACKUP_DIR="/backups/ouroboros/$(date +%Y%m%d)"
mkdir -p "$BACKUP_DIR"

# Export latest snapshot
dotnet run -- dag --command snapshot --output "$BACKUP_DIR/latest.json"

# Show metrics for records
dotnet run -- dag --command show --format json > "$BACKUP_DIR/metrics.json"

# Validate
dotnet run -- dag --command validate > "$BACKUP_DIR/validation.txt"

# Compress
tar -czf "$BACKUP_DIR.tar.gz" "$BACKUP_DIR"
```

## Best Practices

### 1. Enable Feature Flags Gradually

Start with one feature at a time:

```json
{
  "FeatureFlags": {
    "Embodiment": false,
    "SelfModel": true,    // ← Start here
    "Affect": false
  }
}
```

Test thoroughly before enabling additional features.

### 2. Use Dry-Run Mode

Always test retention policies with `--dry-run` first:

```bash
# Preview changes
dotnet run -- dag --command retention --max-count 10 --dry-run --verbose

# Review output carefully

# Execute when confident
# dotnet run -- dag --command retention --max-count 10
```

### 3. Regular Snapshots

Create snapshots at meaningful intervals:
- **Hourly**: For active development
- **Daily**: For production systems
- **On-demand**: Before major changes

### 4. Monitor Metrics

Track system evolution over time:

```bash
# Daily metrics report
dotnet run -- dag --command show > daily-metrics.txt
```

### 5. Validate Regularly

Run integrity checks frequently:

```bash
# As part of CI/CD
dotnet run -- dag --command validate || exit 1
```

## Troubleshooting

### Issue: "No epochs available"

**Cause**: No snapshots have been created yet.

**Solution**:
```bash
dotnet run -- dag --command snapshot --branch main
```

### Issue: Hash mismatch

**Cause**: Snapshot has been modified or corrupted.

**Solution**: Check snapshot file integrity or recreate from source.

### Issue: Retention deletes too much

**Cause**: Policy is too aggressive.

**Solution**: Use dry-run to preview, adjust `--max-age-days` or `--max-count`.

## Next Steps

- Review [Architecture Documentation](PHASE_0_ARCHITECTURE.md)
- Explore Phase 1 features (when available)
- Contribute to Phase 0 enhancements

## Support

For issues or questions:
- Check [Troubleshooting Guide](../TROUBLESHOOTING.md)
- Open an issue on GitHub
- Review test cases in `src/Ouroboros.Tests/Tests/`
