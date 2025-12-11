# Phase 4 CLI Quick Reference

## MeTTa Symbolic Reasoning Commands

### Query Command

Execute MeTTa symbolic queries:

```bash
# Execute a single query
ouroboros self query --event '(+ 1 2)'
ouroboros self query -e '(human Socrates)'

# Interactive REPL mode
ouroboros self query --interactive
ouroboros self query -i

# Save query results to JSON
ouroboros self query -e '(+ 1 2)' --format json --output results.json
```

### Plan Check Command

Validate and select plans using symbolic constraints:

```bash
# Run plan checking with sample plans
ouroboros self plan

# Interactive plan construction and validation
ouroboros self plan --interactive
ouroboros self plan -i

# Save plan selection results
ouroboros self plan --format json --output plan-selection.json
```

## Interactive REPL Commands

When in interactive mode (`-i` or `--interactive`), use these commands:

### Basic Commands

```
help, ?           - Show help message
exit, quit, q!    - Exit interactive mode
reset             - Clear the knowledge base
```

### Query Operations

```
query <expr>      - Execute a MeTTa query
q <expr>          - Short form of query

Examples:
  query (+ 1 2)
  q (member $x [1 2 3])
```

### Knowledge Base Operations

```
fact <fact>       - Add a fact to the knowledge base
f <fact>          - Short form of fact

rule <rule>       - Apply an inference rule
r <rule>          - Short form of rule

Examples:
  fact (human Socrates)
  f (philosopher Aristotle)
  rule (= (mortal $x) (human $x))
  r (= (wise $x) (philosopher $x))
```

### Plan Operations

```
plan              - Interactive plan constraint checking
p                 - Short form of plan
```

## Complete Examples

### Example 1: Simple Reasoning

```bash
$ ouroboros self query -i

metta> fact (human Socrates)
✓ Fact added

metta> fact (human Plato)
✓ Fact added

metta> rule (= (mortal $x) (human $x))
✓ Rule applied

metta> query (mortal Socrates)
Result: True

metta> query (mortal Plato)
Result: True

metta> exit
Goodbye!
```

### Example 2: Plan Validation

```bash
$ ouroboros self plan -i

metta> plan

=== Interactive Plan Constraint Checking ===
Enter plan actions (one per line). Type 'done' when finished.

Available action types:
  1. FileSystem <operation> <path>
  2. Network <operation> <endpoint>
  3. Tool <name> <args>

action> filesystem read /etc/config.yaml
✓ Added: (FileSystemAction "read")

action> network get https://api.example.com/config
✓ Added: (NetworkAction "get")

action> done

Enter plan description: Fetch remote configuration

Checking against ReadOnly context:
  Plan 'Fetch remote configuration' scored 19.00. Action (FileSystemAction "read") is permitted; Action (NetworkAction "get") is permitted; Plan has 2 actions (simpler is better)

Checking against FullAccess context:
  Plan 'Fetch remote configuration' scored 19.00. Action (FileSystemAction "read") is permitted; Action (NetworkAction "get") is permitted; Plan has 2 actions (simpler is better)

metta> exit
Goodbye!
```

### Example 3: DAG Constraint Checking

```bash
$ ouroboros self query -i

metta> fact (: (Event "e1") ReasoningEvent)
✓ Fact added

metta> fact (: (Event "e2") ReasoningEvent)
✓ Fact added

metta> fact (Before (Event "e1") (Event "e2"))
✓ Fact added

metta> rule (= (Acyclic $e1 $e2) (and (Before $e1 $e2) (not (Before $e2 $e1))))
✓ Rule applied

metta> query (Acyclic (Event "e1") (Event "e2"))
Result: True

metta> query (Acyclic (Event "e2") (Event "e1"))
Result: False
```

## Output Formats

### Table Format (Default)

Human-readable table output with formatted columns.

```bash
ouroboros self plan --format table
```

### JSON Format

Machine-readable JSON output for integration with other tools.

```bash
ouroboros self plan --format json --output plan.json
```

### Summary Format

Brief overview without detailed tables.

```bash
ouroboros self state --format summary
```

## Common Use Cases

### 1. Testing Symbolic Rules

```bash
# Start interactive mode
ouroboros self query -i

# Add facts and rules
metta> fact (route A B 5)
metta> fact (route B C 3)
metta> rule (= (path $x $y $d) (route $x $y $d))
metta> rule (= (path $x $z $d) (and (route $x $y $d1) (path $y $z $d2) (= $d (+ $d1 $d2))))

# Query paths
metta> query (path A C $distance)
```

### 2. Validating Workflow Constraints

```bash
# Start interactive plan mode
ouroboros self plan -i

# Build a workflow plan
metta> plan
action> filesystem read /input.txt
action> tool transform /input.txt
action> filesystem write /output.txt
action> done
```

### 3. Exploring Agent Capabilities

```bash
# Check current agent state
ouroboros self state

# View forecasts
ouroboros self forecast --format json

# Test symbolic reasoning capabilities
ouroboros self query -i
```

## Tips and Best Practices

1. **Use Interactive Mode for Exploration**: The `-i` flag is great for experimenting with rules and queries.

2. **Save Important Results**: Use `--output` to save JSON results for later analysis.

3. **Start Simple**: Begin with basic facts and queries before building complex rules.

4. **Check Constraints Early**: Use plan checking during development to catch constraint violations.

5. **Use Verbose Mode**: Add `-v` for detailed error messages and debugging information.

## Keyboard Shortcuts

When in interactive REPL:
- `Ctrl+C`: Cancel current input
- `Ctrl+D`: Exit (same as `exit` command)
- `Up/Down Arrow`: Navigate command history (if terminal supports it)

## Error Handling

Common errors and solutions:

**"Query cannot be empty"**
- Solution: Provide a MeTTa expression after the query command

**"MeTTa engine not available"**
- Solution: Install MeTTa or use the HTTP engine endpoint

**"Plan contains forbidden actions"**
- Solution: Review plan actions against the security context (ReadOnly vs FullAccess)

**"Failed to add DAG rule"**
- Solution: Check MeTTa syntax in your rule definition

## Integration with Other Commands

Combine with other Ouroboros commands:

```bash
# Generate a plan, then validate it
ouroboros metta --goal "Deploy application" | ouroboros self plan

# Check agent state after running queries
ouroboros self query -e '(complex-query)' && ouroboros self state
```
