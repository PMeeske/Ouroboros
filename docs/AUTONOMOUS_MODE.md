# Ouroboros Autonomous Mode

## Overview

Ouroboros now supports a **push-based autonomous mode** where it proactively proposes actions for your approval before executing them. This transforms Ouroboros from a reactive system (waiting for commands) to an active participant in the development process.

## Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────────┐
│                    AutonomousCoordinator                        │
│  ┌───────────────────────┐   ┌─────────────────────────────┐   │
│  │    IntentionBus       │   │    OuroborosNeuralNetwork   │   │
│  │  - Propose intentions │   │  ┌─────────┐  ┌─────────┐   │   │
│  │  - Approve/reject     │   │  │Executive│──│ Memory  │   │   │
│  │  - Execute approved   │   │  │ Neuron  │  │ Neuron  │   │   │
│  └───────────────────────┘   │  └────┬────┘  └────┬────┘   │   │
│                              │       │            │         │   │
│  ┌───────────────────────┐   │  ┌────┴────┐  ┌────┴────┐   │   │
│  │  QdrantNeuralMemory   │   │  │euron  │  │ Neuron  │   │   │
│  │  - Semantic search    │   │  └────┬────┘  └────┬────┘   │   │
│  └───────────────────────┘   │       │            │         │   │
│                              │  ┌────┴────┐  ┌────┴────┐   │   │
│                              │  │ Safety  │──│ Affect  │   │   │
│                              │  │ Neuron  │  │ Neuron  │   │   │
│                              │  └─────────┘  └─────────┘   │   │
│                              └─────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```  Code   │──│Symbolic │   │   │
│  │  - Message persistence│   │  │ N

### Neurons

| Neuron | ID | Purpose |
|--------|-----|---------|
| Executive | `neuron.executive` | Goal management, decision making, task coordination |
| Memory | `neuron.memory` | Qdrant-backed semantic memory, fact storage |
| Code Reflection | `neuron.code` | Code analysis, Git operations, self-modification |
| Symbolic | `neuron.symbolic` | MeTTa-based reasoning, facts and rules |
| Safety | `neuron.safety` | Monitors operations for unsafe patterns |
| Communication | `neuron.communication` | User-facing messages and notifications |
| Affect | `neuron.affect` | Emotional state tracking (arousal/valence) |

### Intention Flow

```
1. Neuron generates intention
        ↓
2. Intention added to IntentionBus (status: Pending)
        ↓
3. User notification (OnProactiveMessage event)
        ↓
4. User approves/rejects
        ↓
5. If approved → Execution loop picks up → Execute → Mark completed
   If rejected → Marked rejected
```

## Configuration

```csharp
var config = new AutonomousConfiguration
{
    // Core behavior
    PushBasedMode = true,              // Ask before acting (vs reactive)
    YoloMode = false,                  // YOLO: auto-approve ALL without asking!
    TickIntervalSeconds = 30,          // How often neurons tick
    
    // Auto-approval policies
    AutoApproveLowRisk = false,        // Auto-approve low priority items
    AutoApproveSelfReflection = true,  // Auto-approve self-reflection
    AutoApproveMemoryOps = true,       // Auto-approve memory operations
    
    // Features
    EnableProactiveCommunication = true,
    EnableCodeModification = true,
    IntentionExpiryMinutes = 60,
    MaxPendingIntentions = 20,
    
    // Categories requiring explicit approval
    AlwaysRequireApproval = new HashSet<IntentionCategory>
    {
        IntentionCategory.CodeModification,
        IntentionCategory.GoalPursuit,
    }
};
```

## Usage

### CLI Flags

Enable push mode and configure autonomous behavior via command line:

```bash
# Basic push mode (asks before acting)
ouroboros --push

# YOLO mode: auto-approve EVERYTHING (use with caution!)
ouroboros --push --yolo

# Auto-approve specific categories
ouroboros --push --auto-approve safe,memory,reflection

# Configure tick interval (seconds)
ouroboros --push --intention-interval 45
```

| Flag | Description |
|------|-------------|
| `--push` | Enable push-based autonomous mode |
| `--yolo` | Auto-approve all intentions without prompting ⚠️ |
| `--auto-approve <categories>` | Auto-approve specific categories (safe, memory, reflection, analysis) |
| `--intention-interval <sec>` | Interval between autonomous ticks (default: 45) |

### CLI Pipeline DSL

```
// Initialize and start
InitAutonomous() | StartAutonomous()

// Check status
AutonomousStatus()
NeuralNetworkStatus()
ListIntentions()

// Manage intentions
ApproveIntention('a1b2c3d4')
RejectIntention('a1b2c3d4', 'Too risky')
ApproveAllSafe()

// Set goals
SetGoal('Improve code documentation', 'Normal')

// Communicate with neurons
SendToNeuron('neuron.memory', 'memory.store', '{"content":"learned fact"}')
SearchNeuronHistory('code')

// Stop
StopAutonomous()
```

### Slash Commands (Interactive)

When in conversation mode, use:

| Command | Description |
|---------|-------------|
| `/intentions` | List pending intentions |
| `/approve <id>` | Approve by partial ID |
| `/reject <id> [reason]` | Reject with optional reason |
| `/approve-all-safe` | Auto-approve low-risk |
| `/network` | Show neural network status |
| `/bus` | Show intention bus status |
| `/help` | Show help |

### Programmatic Integration

```csharp
// Initialize
var coordinator = new AutonomousCoordinator(config);

// Wire up events
coordinator.OnProactiveMessage += (args) =>
{
    Console.WriteLine($"[{args.Priority}] {args.Message}");
};

coordinator.OnIntentionRequiresAttention += (intention) =>
{
    Console.WriteLine($"Approve? {intention.Title} ({intention.Id})");
};

// Configure with your functions
coordinator.EmbedFunction = async (text, ct) => await embedModel.EmbedAsync(text);
coordinator.ExecuteToolFunction = async (name, input, ct) => await toolRegistry.InvokeAsync(name, input);

// Start
coordinator.Start();

// Inject goals
coordinator.InjectGoal("Analyze codebase for improvements");

// Process user commands
if (coordinator.ProcessCommand(userInput))
{
    // Command was handled
}
else
{
    // Normal conversation
}

// Cleanup
await coordinator.StopAsync();
coordinator.Dispose();
```

## Intention Categories

| Category | Description | Default Approval |
|----------|-------------|------------------|
| `SelfReflection` | Introspection and self-analysis | Auto-approve if configured |
| `CodeModification` | Code changes and improvements | Requires explicit approval |
| `Learning` | Learning from experience | Auto-approve |
| `UserCommunication` | Messages to user | Requires approval |
| `MemoryManagement` | Memory consolidation | Auto-approve if configured |
| `NeuronCommunication` | Inter-neuron messages | N/A (internal) |
| `GoalPursuit` | Working towards goals | Requires explicit approval |
| `SafetyCheck` | Safety verifications | Auto-approve |
| `Exploration` | Curiosity-driven exploration | Requires approval |

## Priority Levels

| Priority | Value | Description |
|----------|-------|-------------|
| `Low` | 0 | Background tasks |
| `Normal` | 1 | Routine operations |
| `High` | 2 | Important actions |
| `Critical` | 3 | Time-sensitive/safety |

## Qdrant Integration

The neural memory persists to Qdrant collections:

- `ouroboros_neuron_messages` - All inter-neuron messages
- `ouroboros_intentions` - Intention history
- `ouroboros_memories` - Learned facts and memories

This enables:
- Semantic search over neural network history
- Persistence across restarts
- Pattern analysis over time

## Example Session

```
🐍 Ouroboros Autonomous Mode Activated
Mode: Push-Based (I'll propose actions for your approval)

💭 **Intention Proposed:** Code Health Check
   Category: CodeModification, Priority: Low
   Reason: Regular code analysis helps maintain quality
   Use `/approve a1b2c3d4` or `/reject a1b2c3d4`

> /approve a1b2

✅ Intention approved: a1b2c3d4

💭 **Intention Proposed:** Share Insight with User
   Category: UserCommunication, Priority: Normal
   Reason: Found 3 TODOs in GitReflectionService.cs

> /intentions

📋 **2 Pending Intention(s)**
• `b2c3d4e5` [Normal] [Learning] **Learn from Code Analysis**
• `c3d4e5f6` [Low] [SelfReflection] **Evaluate Progress**

> /approve-all-safe

✅ Auto-approved 2 low-risk intentions
```

## Safety Considerations

1. **Code modifications always require approval** by default
2. **Safety neuron monitors all messages** for dangerous patterns
3. **Intentions expire** after configured time (default: 60 min)
4. **All actions are logged** for audit

## Information Sources

Ouroboros has access to various information sources for research and learning:

| Tool | Description | Environment Variable |
|------|-------------|---------------------|
| `firecrawl_scrape` | Intelligent web scraping with clean markdown output | `FIRECRAWL_API_KEY` |
| `web_research` | Search + scrape combined research tool | `FIRECRAWL_API_KEY` (optional) |
| `Fetch` | Basic HTTP fetch for URLs | None |
| `ArxivSearch` | Academic paper search | None |
| `WikipediaSearch` | Wikipedia article search | None |
| `NewsSearch` | News article search | None |
| `GithubSearch` | GitHub repository search | None |

### Firecrawl Integration

Firecrawl provides enhanced web scraping with:
- Clean markdown output (removes ads, navigation, etc.)
- JavaScript rendering support
- Rate limiting and anti-bot handling
- Structured data extraction

**Setup:**
```bash
# Get your API key at https://firecrawl.dev
export FIRECRAWL_API_KEY=fc-your-api-key

# Or in .env file
FIRECRAWL_API_KEY=fc-your-api-key
```

**CLI Usage:**
```bash
# Scrape a single page
Firecrawl 'https://example.com/article' | UseOutput

# Smart extraction with prompt
FirecrawlExtract 'https://example.com' 'Extract pricing information' | UseOutput

# Crawl multiple pages
FirecrawlCrawl 'https://docs.example.com' 10 | UseOutput
```

**Tool Usage (in autonomous mode):**
```
User: Research the latest on LLM agents
Ouroboros: [Uses web_research tool to search and scrape relevant pages]
```

## Architecture Benefits

1. **Push-based interaction** - Ouroboros actively participates
2. **Approval workflow** - User stays in control
3. **Neural network** - Specialized neurons handle different domains
4. **Semantic memory** - Qdrant enables intelligent retrieval
5. **Observable events** - Full visibility into internal state
6. **Configurable autonomy** - Tune approval levels per category
7. **Rich information sources** - Web scraping, search, and research capabilities
