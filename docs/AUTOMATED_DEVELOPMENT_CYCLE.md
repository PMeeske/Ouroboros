# Automated Development Cycle - Quick Reference

## Overview

The **Copilot Automated Development Cycle** is a fully automated workflow that continuously analyzes your codebase, identifies improvement opportunities, generates tasks, and manages the development workflow with minimal human intervention.

## How It Works

```
┌─────────────────────────────────────────────────────────┐
│              Automated Development Cycle                 │
└─────────────────────────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ▼                               ▼
    Scheduled Run                   PR Merged Event
    (9 AM, 5 PM UTC)               (Automatic Trigger)
         │                               │
         └───────────────┬───────────────┘
                         ▼
              ┌─────────────────────┐
              │  Check PR Limit     │
              │  (Max 5 Open PRs)   │
              └─────────────────────┘
                         │
                    Yes / No
                         │
              ┌──────────┴──────────┐
              ▼                     ▼
        Can Proceed           Pause Cycle
              │               (Wait for PR merge)
              ▼
    ┌────────────────────┐
    │  Analyze Codebase  │
    │  - TODOs/FIXMEs    │
    │  - Missing docs    │
    │  - Test coverage   │
    │  - Error handling  │
    │  - Async patterns  │
    └────────────────────┘
              │
              ▼
    ┌────────────────────┐
    │ Generate Tasks     │
    │ (Max 3 per cycle)  │
    └────────────────────┘
              │
              ▼
    ┌────────────────────┐
    │ Create Issues      │
    │ + Assign @copilot  │
    └────────────────────┘
              │
              ▼
    ┌────────────────────┐
    │ Trigger Analysis   │
    │ (Issue Assistant)  │
    └────────────────────┘
              │
              ▼
    ┌────────────────────┐
    │ Update Status      │
    │ (Tracking Issue)   │
    └────────────────────┘
```

## Schedule

### Automatic Triggers

1. **Scheduled Runs**: Twice daily
   - 9:00 AM UTC (Start of European workday)
   - 5:00 PM UTC (Start of US workday)

2. **PR Merge Events**: 
   - Triggers automatically when a PR is merged to `main`
   - Only runs if < 5 open copilot PRs

### Manual Trigger

Use GitHub Actions workflow dispatch:
```
Actions → Copilot Automated Development Cycle → Run workflow
```

**Options**:
- `force`: Skip PR limit check (default: false)
- `max_tasks`: Number of tasks to create (default: 3)

## PR Limit Management

### Why Limit PRs?

- **Prevent Overwhelm**: Max 5 open PRs prevents too many concurrent reviews
- **Focus Reviews**: Encourages team to review and merge existing PRs
- **Quality Over Quantity**: Ensures attention to each PR

### How It Works

1. Counts open PRs with `copilot/` branch prefix
2. If count < 5: Proceeds with cycle
3. If count >= 5: Pauses cycle, updates tracking issue
4. Automatically resumes when PRs are merged

### Force Execution

To bypass PR limit (use sparingly):
```yaml
workflow_dispatch:
  inputs:
    force: true
```

## Task Generation

### Analysis Areas

1. **TODO/FIXME Comments** (High Priority)
   - Searches for: `TODO`, `FIXME`, `HACK`
   - Creates: Task to resolve technical debt

2. **Missing Documentation** (Medium Priority)
   - Finds: Public APIs without XML docs
   - Creates: Documentation task

3. **Test Coverage Gaps** (High Priority)
   - Identifies: Source files without tests
   - Creates: Testing task

4. **Error Handling** (Medium Priority)
   - Finds: Exception throws that could use Result<T>
   - Creates: Refactoring task

5. **Async Patterns** (High Priority)
   - Detects: `.Result`, `.Wait()` blocking calls
   - Creates: Bug fix task

### Task Prioritization

Tasks are created in priority order:
1. **High**: Bugs, performance issues, test coverage
2. **Medium**: Documentation, refactoring
3. **Low**: Style, minor improvements

### Max Tasks Per Cycle

- Default: **3 tasks**
- Prevents issue tracker from being overwhelmed
- Adjustable via workflow dispatch input

## Issue Assignment

### Automatic @copilot Assignment

All generated issues automatically:
1. Include `[Copilot]` prefix in title
2. Add `copilot-automated` label
3. Add `copilot-assist` label
4. Mention @copilot in issue body
5. Trigger issue assistant workflow

### Issue Templates

Generated issues follow this structure:
```markdown
## 🔧/📚/🧪/♻️/⚡ Task Type

Description of the issue with context

### Approach
1. Step-by-step implementation guide
2. ...

### Guidelines
- Relevant coding standards
- Testing requirements
- Documentation needs

---

🤖 **Automated Task**: This issue was automatically generated.
**@copilot** Please analyze this issue and provide implementation guidance.
```

## Monitoring

### Tracking Issue

A persistent tracking issue shows:
- Current cycle status (Active/Paused)
- Open PR count
- Last cycle execution time
- Next scheduled run
- Recent activity log

**Location**: Look for issue titled "🤖 Copilot Automated Development Cycle Status"

### Status Indicators

- 🟢 **Active**: Cycle running normally
- 🟡 **Paused**: Max PRs reached
- 🔴 **Error**: Workflow failure (check Actions logs)

### Labels

Find cycle-related items by label:
- `copilot-automated`: Auto-generated issues
- `copilot-assist`: Issues assigned to @copilot
- `copilot-cycle-tracker`: Status tracking issue

## Configuration

### Adjust Schedule

Edit `.github/workflows/copilot-automated-development-cycle.yml`:

```yaml
on:
  schedule:
    # Change to your preferred times (UTC)
    - cron: '0 9,17 * * *'  # 9 AM and 5 PM
```

Common schedules:
- `'0 */4 * * *'` - Every 4 hours
- `'0 0 * * *'` - Once daily at midnight
- `'0 9 * * 1-5'` - Weekdays at 9 AM

### Change Max Tasks

Edit workflow file or use dispatch input:
```yaml
workflow_dispatch:
  inputs:
    max_tasks:
      default: 3  # Change this number
```

### Adjust PR Limit

Edit the workflow file (line ~48):
```javascript
const maxPRs = 5;  // Change this number
```

**Recommendation**: Keep between 3-7 for best results

## Disabling the Cycle

### Temporary Disable

1. Go to repository Settings
2. Navigate to Actions → General
3. Find "Copilot Automated Development Cycle"
4. Click "Disable workflow"

### Permanent Removal

Delete the workflow file:
```bash
rm .github/workflows/copilot-automated-development-cycle.yml
```

## Troubleshooting

### Cycle Not Running

**Check**:
1. GitHub Actions enabled in repository settings
2. Workflow file has correct permissions
3. No workflow run failures in Actions tab

**Fix**:
- Review Actions logs for errors
- Manually trigger to test
- Check branch protection rules

### Too Many Issues Created

**Solutions**:
1. Reduce `max_tasks` input
2. Adjust schedule to run less frequently
3. Review and close duplicate issues
4. Temporarily disable workflow

### PR Limit Always Reached

**Solutions**:
1. Review and merge existing PRs faster
2. Increase max PR limit (not recommended > 7)
3. Use force flag for important cycles

### Tasks Not Relevant

**Solutions**:
1. Customize analysis patterns in workflow
2. Add `.gitignore` patterns for analysis directories
3. Modify task generation logic
4. Add custom filtering rules

## Best Practices

### 1. Regular PR Review

- Review copilot PRs within 1-2 days
- Merge small PRs quickly
- Request changes clearly

### 2. Task Triage

- Review auto-generated issues weekly
- Close duplicates promptly
- Update task priorities as needed

### 3. Cycle Monitoring

- Check tracking issue weekly
- Monitor task completion rate
- Adjust schedule based on team capacity

### 4. Quality Over Speed

- Don't force cycles unnecessarily
- Let PR limit mechanism work
- Focus on quality implementations

## Integration with Other Workflows

### Works With

1. **Issue Assistant**: Analyzes auto-generated issues
2. **Code Review**: Reviews PRs from implemented tasks
3. **Continuous Improvement**: Weekly quality reports

### Workflow Coordination

```
Automated Cycle → Creates Issues
       ↓
Issue Assistant → Provides Guidance
       ↓
Developer → Creates PR
       ↓
Code Review → Reviews Changes
       ↓
PR Merged → Triggers New Cycle
```

## Metrics and Benefits

### Time Savings

- **Manual Analysis**: 2-3 hours/week → **Automated**: 0 hours
- **Task Creation**: 1 hour/week → **Automated**: 0 hours
- **Issue Triage**: 30 min/week → **Assisted**: 10 min/week

**Total Savings**: ~4 hours/week per team

### Quality Improvements

- Consistent code quality checks
- No forgotten TODO comments
- Improved test coverage
- Better documentation
- Modern error handling

### Developer Experience

- Clear, actionable tasks
- Implementation guidance included
- Reduced decision fatigue
- Focus on coding, not planning

## Advanced Usage

### Custom Task Types

Add custom analysis in workflow file:
```javascript
// Custom analysis example
if (fs.existsSync('custom-analysis.txt')) {
  const customIssues = fs.readFileSync('custom-analysis.txt', 'utf8');
  tasks.push({
    priority: 'high',
    type: 'custom',
    title: 'Custom improvement task',
    body: customIssues,
    labels: ['custom', 'enhancement']
  });
}
```

### Integration with CI/CD

Trigger cycle after successful deployments:
```yaml
on:
  workflow_run:
    workflows: ["Deploy to Production"]
    types: [completed]
```

### Webhook Integration

Use workflow outputs with webhooks:
```yaml
- name: Notify Slack
  run: |
    curl -X POST $SLACK_WEBHOOK \
      -d '{"text":"Cycle completed: ${{ needs.analyze.outputs.tasks_count }} tasks created"}'
```

## FAQs

**Q: Can I customize the task descriptions?**  
A: Yes, edit the task generation logic in the workflow file.

**Q: Does this work with private repositories?**  
A: Yes, with appropriate GitHub Actions permissions.

**Q: What if I want different schedules for different task types?**  
A: Create separate workflow files with different schedules and filters.

**Q: Can this create PRs automatically?**  
A: Currently creates issues only. PR automation can be added as future enhancement.

**Q: How do I exclude certain directories?**  
A: Modify the `find` commands in the analysis steps to exclude paths.

---

## Related Documentation

- [Main Development Loop Guide](COPILOT_DEVELOPMENT_LOOP.md)
- [Contributing Guidelines](../CONTRIBUTING.md)
- [GitHub Actions Documentation](https://docs.github.com/actions)

---

**MonadicPipeline**: Continuous improvement through automation 🚀
