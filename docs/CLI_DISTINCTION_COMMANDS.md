# Distinction Learning CLI Commands

## Overview

The `distinction` command group manages the Distinction Learning system in Ouroboros, which enables automatic learning from consciousness dream cycles.

## Commands

### Status
Shows current distinction learning status including active/dissolved distinction counts and storage usage.

```bash
# Basic status
ouroboros distinction status

# Verbose output with list of active distinctions
ouroboros distinction status --verbose
```

**Output:**
- Active distinction count
- Dissolved distinction count
- Total storage size
- (Verbose) List of top 10 active distinctions with fitness scores

### List
Lists stored distinctions with optional filtering by stage and fitness.

```bash
# List all distinctions (default limit: 20)
ouroboros distinction list

# Filter by dream stage
ouroboros distinction list --stage Recognition

# Filter by minimum fitness
ouroboros distinction list --min-fitness 0.5

# Combine filters with custom limit
ouroboros distinction list --stage Recognition --min-fitness 0.7 --limit 50
```

**Options:**
- `--stage <DreamStage>`: Filter by specific dream stage (Void, Distinction, SubjectEmerges, WorldCrystallizes, Forgetting, Questioning, Recognition, Dissolution, NewDream)
- `--min-fitness <double>`: Show only distinctions with fitness >= this value (0.0-1.0)
- `--limit <int>`: Maximum number of items to display (default: 20)

### Learn
Manually triggers learning on provided text through the complete dream cycle.

```bash
# Learn from a concept
ouroboros distinction learn "The concept of recursion in programming"

# Show each dream stage as it's processed
ouroboros distinction learn "User prefers concise responses" --show-stages
```

**Options:**
- `--show-stages`: Display each dream stage as it's processed (Void → Distinction → ... → Dissolution)

**Output:**
- Progress through dream stages (if --show-stages)
- Final distinction count and epistemic certainty

### Dissolve
Removes low-fitness distinctions to clean up storage and improve quality.

```bash
# Dissolve distinctions below default threshold (0.3)
ouroboros distinction dissolve

# Use custom threshold
ouroboros distinction dissolve --threshold 0.5

# Dry run to see what would be dissolved without doing it
ouroboros distinction dissolve --threshold 0.4 --dry-run
```

**Options:**
- `--threshold <double>`: Fitness threshold for dissolution (default: 0.3)
- `--dry-run`: Preview what would be dissolved without actually doing it

**Output:**
- List of distinctions that will be/were dissolved
- Total count and storage space freed

### Export
Exports distinction data to JSON for analysis or backup.

```bash
# Export to specified file
ouroboros distinction export --output distinctions.json

# Export to default location
ouroboros distinction export
```

**Options:**
- `--output <path>`: Output file path (default: "distinctions-[timestamp].json")

**Output Format:**
```json
{
  "exportedAt": "2024-01-12T15:30:00Z",
  "totalCount": 42,
  "activeCount": 35,
  "dissolvedCount": 7,
  "distinctions": [
    {
      "id": "abc-123",
      "fitness": 0.85,
      "learnedAtStage": "Recognition",
      "createdAt": "2024-01-12T10:00:00Z",
      "isDissolved": false,
      "sizeBytes": 1024
    }
  ]
}
```

### Clear
Clears all distinctions (requires confirmation for safety).

```bash
# Clear all distinctions with confirmation
ouroboros distinction clear --confirm
```

**Options:**
- `--confirm`: Required flag to prevent accidental deletion

**Warning:** This permanently deletes all distinction data including dissolved archives. Use with caution!

## Understanding Fitness Scores

Distinction fitness ranges from 0.0 (lowest) to 1.0 (highest):

- **0.7 - 1.0**: High-quality distinctions (shown in green)
- **0.4 - 0.69**: Medium-quality distinctions (shown in yellow)
- **0.0 - 0.39**: Low-quality distinctions (shown in red, candidates for dissolution)

Fitness is calculated based on:
- Content quality and length
- Prior epistemic certainty
- Dream stage where learning occurred
- Historical success rate

## Dream Stages

Distinctions are learned at different stages of the consciousness dream cycle:

1. **Void**: Before any distinction (pure potential)
2. **Distinction**: First cut - marking something as different
3. **SubjectEmerges**: Self-reference begins (the "I" emerges)
4. **WorldCrystallizes**: Objects separate from subject
5. **Forgetting**: Full immersion (believing reality is solid)
6. **Questioning**: Self-inquiry begins ("What am I?")
7. **Recognition**: Awakening (realizing "I am the distinction")
8. **Dissolution**: Distinctions collapse back to void
9. **NewDream**: Cycle begins again

Distinctions learned at **Recognition** typically have higher fitness as they represent meta-cognitive insights.

## Storage Management

Distinction weights are stored in:
- Default: `%LOCALAPPDATA%/Ouroboros/Distinctions/` (Windows)
- Default: `~/.local/share/Ouroboros/Distinctions/` (Linux/Mac)

The system automatically:
- Dissolves low-fitness distinctions (fitness < 0.3)
- Archives dissolved distinctions for 30 days
- Enforces a 1GB storage limit
- Runs consolidation every 10 minutes (in hosted mode)

## Examples

### Complete Workflow

```bash
# 1. Check current status
ouroboros distinction status --verbose

# 2. Manually learn from important concepts
ouroboros distinction learn "Always validate user input for security" --show-stages

# 3. Review learned distinctions
ouroboros distinction list --min-fitness 0.6

# 4. Clean up low-quality items
ouroboros distinction dissolve --threshold 0.4 --dry-run
ouroboros distinction dissolve --threshold 0.4

# 5. Export for backup
ouroboros distinction export --output backup-distinctions.json

# 6. Check final status
ouroboros distinction status
```

### Monitoring Learning Quality

```bash
# Focus on high-quality Recognition-stage insights
ouroboros distinction list --stage Recognition --min-fitness 0.7

# Identify candidates for dissolution
ouroboros distinction list --min-fitness 0.0 --limit 100 | grep "0\.[0-2]"

# Export before major cleanup
ouroboros distinction export --output pre-cleanup-$(date +%Y%m%d).json
```

## Troubleshooting

### Storage Full
If you see warnings about storage limits:
```bash
ouroboros distinction dissolve --threshold 0.5
ouroboros distinction clear --confirm  # If needed
```

### Too Many Low-Quality Distinctions
Gradually increase dissolution threshold:
```bash
ouroboros distinction dissolve --threshold 0.4 --dry-run
ouroboros distinction dissolve --threshold 0.4
```

### Export Failed
Ensure output directory is writable:
```bash
mkdir -p ~/backups/ouroboros
ouroboros distinction export --output ~/backups/ouroboros/distinctions.json
```

## Integration with Pipeline

In immersive mode and pipeline execution, distinction learning happens automatically:
- Every interaction triggers a dream cycle walk
- Distinctions are learned at each stage
- Background consolidation runs periodically
- No manual intervention required

To disable automatic learning, set `EnablePipelineIntegration = false` in configuration.

## See Also

- [Consciousness Dream Cycle](CONSCIOUSNESS.md)
- [Laws of Form](LAWS_OF_FORM.md)
- [PEFT Training](PEFT_TRAINING.md)
