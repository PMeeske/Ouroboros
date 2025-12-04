# Copilot Development Loop Workflows

This directory contains GitHub Actions workflows that implement the automatic development loop with GitHub Copilot assistance.

## 🔄 Workflow Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   Development Loop Cycle                     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
              ┌───────────────────────────┐
              │   Developer Action        │
              │   • Opens PR              │
              │   • Creates Issue         │
              │   • Weekly Schedule       │
              └───────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
    ┌─────────┐         ┌─────────┐       ┌─────────┐
    │ PR      │         │ Issue   │       │ Weekly  │
    │ Review  │         │ Analysis│       │ Quality │
    └─────────┘         └─────────┘       └─────────┘
          │                   │                   │
          │                   │                   │
          ▼                   ▼                   ▼
    ┌─────────┐         ┌─────────┐       ┌─────────┐
    │ Pattern │         │ Context │       │ Metrics │
    │ Check   │         │ Search  │       │ Report  │
    └─────────┘         └─────────┘       └─────────┘
          │                   │                   │
          │                   │                   │
          ▼                   ▼                   ▼
    ┌─────────┐         ┌─────────┐       ┌─────────┐
    │ Comment │         │ Comment │       │ Issue   │
    │ on PR   │         │ on Issue│       │ Created │
    └─────────┘         └─────────┘       └─────────┘
          │                   │                   │
          └───────────────────┼───────────────────┘
                              ▼
                    ┌───────────────────┐
                    │ Developer Reviews │
                    │ & Takes Action    │
                    └───────────────────┘
```

## 📋 Workflows

### 1. `copilot-code-review.yml`

**Purpose**: Automated code review for pull requests

**Triggers**:
- `pull_request` events (opened, synchronize, reopened)
- Manual trigger via workflow_dispatch

**What it does**:
1. Checks out code
2. Identifies changed files
3. Analyzes code patterns:
   - Monadic error handling
   - Null safety with Option<T>
   - Async/await patterns
   - Documentation completeness
   - Namespace conventions
   - Immutability patterns
4. Runs build and collects warnings
5. Posts review as PR comment

**Permissions**:
- `contents: read` - Read repository
- `pull-requests: write` - Post comments
- `issues: write` - Update issue labels

**Example Comment**:
```markdown
## 🤖 GitHub Copilot Code Review

### Changed Files
- src/Core/NewFeature.cs

### Code Quality Suggestions

#### Functional Programming Patterns
- ⚠️ `NewFeature.cs`: Consider using `Result<T>` monad instead of throwing exceptions

#### Documentation
- 📝 `NewFeature.cs`: Add XML documentation comments for public APIs
```

---

### 2. `copilot-issue-assistant.yml`

**Purpose**: Provide implementation guidance for issues

**Triggers**:
- `issues` opened
- `issues` labeled with `copilot-assist`
- `issue_comment` mentioning `@copilot`
- Manual trigger with issue number

**What it does**:
1. Gets issue details
2. Classifies issue type (bug, feature, test, docs, refactor)
3. Searches codebase for relevant files
4. Generates implementation suggestions
5. Posts analysis as issue comment

**Permissions**:
- `contents: write` - Read repository
- `issues: write` - Post comments
- `pull-requests: write` - Create PRs if needed

**Issue Classification**:
- **Bug**: Contains "bug", "error", "fix", "crash", "fail"
- **Feature**: Contains "feature", "enhance", "add", "implement", "new"
- **Test**: Contains "test", "coverage", "spec"
- **Documentation**: Contains "doc", "documentation", "readme"
- **Refactor**: Contains "refactor", "improve", "optimize"

**Example Comment**:
```markdown
## 🤖 GitHub Copilot Issue Analysis

## 🔍 Codebase Context Analysis

### Related Files
- `src/Ouroboros.Core/Steps/Step.cs`
- `src/Ouroboros.Tests/Core/StepTests.cs`

## 💡 Implementation Suggestions

### Feature Implementation Approach

1. **Define interface**: Create clean API following functional patterns
2. **Implement core logic**: Use monadic composition and Kleisli arrows
3. **Add tests**: Write comprehensive unit and integration tests
```

---

### 3. `copilot-automated-development-cycle.yml`

**Purpose**: Automated development cycle with task generation and copilot assignment

**Triggers**:
- `schedule`: Twice daily at 9 AM and 5 PM UTC (`0 9,17 * * *`)
- `pull_request` closed on main branch
- Manual trigger with customizable options

**What it does**:
1. Checks PR limit (max 5 open copilot PRs)
2. Analyzes codebase for improvement opportunities:
   - TODO/FIXME comments
   - Missing documentation
   - Test coverage gaps
   - Error handling improvements
   - Async/await issues
3. Generates prioritized improvement tasks
4. Creates GitHub issues for selected tasks
5. **🎭 Assigns copilot to issues via Playwright UI automation** (NEW - for both new and unassigned issues)
6. Captures screenshots for debugging and uploads as artifacts
7. Falls back to API if UI automation fails
8. Updates cycle status tracking issue

**New Feature: Playwright-Based Assignment** 🎭
- Uses browser automation instead of GitHub API
- Interacts with GitHub UI like a human would
- Captures screenshots at each step for debugging
- Uploads artifacts for troubleshooting
- Automatically falls back to API if UI fails
- See [Playwright Assignment Guide](../../docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md)

**New Feature: Unassigned Issues Assignment**
- Scans for open issues without assignees
- Identifies copilot-relevant issues by:
  - Labels: `copilot-assist`, `copilot-automated`, `continuous-improvement`
  - Title: Contains `[Copilot]`
- Assignment mechanism: See Playwright-Based Assignment above
- Adds notification comment with `@copilot` mention
- Reports count of newly assigned issues

**Permissions**:
- `contents: write` - Read repository
- `issues: write` - Create and update issues
- `pull-requests: write` - Create PRs

**Configuration Options**:
- `force`: Bypass PR limit (default: false)
- `max_tasks`: Maximum tasks to create (default: 3)
- `assign_unassigned`: Enable unassigned issue assignment (default: true)

**Example Task Issue**:
```markdown
## 🔧 Code Maintenance Task

The following TODO/FIXME comments need attention:

[List of TODOs]

### Approach
1. Review each TODO/FIXME comment
2. Either implement the required changes or convert to proper issues
3. Remove completed TODOs
4. Update code with proper implementations

---

🤖 **GitHub Copilot** has been automatically assigned to this issue.

@copilot Please analyze this issue and provide implementation guidance.
```

---

### 4. `copilot-continuous-improvement.yml`

**Purpose**: Weekly code quality analysis and improvement suggestions

**Triggers**:
- `schedule`: Every Monday at 9 AM UTC (`0 9 * * 1`)
- Manual trigger with scope selection

**What it does**:
1. Builds the project
2. Analyzes code metrics:
   - Lines of code
   - Number of files
   - Test file count
3. Scans for code smells:
   - Large methods (>50 lines)
   - TODO/FIXME comments
   - Missing documentation
4. Analyzes test coverage
5. Reviews security patterns:
   - Hardcoded credentials
   - SQL injection risks
6. Provides architectural recommendations:
   - Error handling patterns
   - Async/await usage
7. Creates/updates improvement issue

**Permissions**:
- `contents: write` - Read repository
- `issues: write` - Create improvement issues
- `pull-requests: write` - Create improvement PRs

**Scope Options**:
- `full` - Analyze entire codebase
- `core` - Core modules only
- `tests` - Test files only
- `docs` - Documentation only

**Example Report**:
```markdown
## 📊 Code Quality Metrics

### Codebase Statistics

| Metric | Count |
|--------|-------|
| C# Files | 245 |
| Total Lines | 28,543 |
| Test Files | 67 |

### 🔍 Potential Improvements Detected

#### TODO Comments
- src/Core/Feature.cs:42: TODO: Optimize this algorithm

### 🧪 Test Coverage Analysis
Line Coverage: 78.4%

### 🏗️ Architectural Recommendations

✅ Good functional error handling pattern with `Result<T>` monads
```

---

## 🎯 Usage Guide

### For Developers

**Getting Code Review**:
1. Create a pull request
2. Wait ~1 minute for workflow to complete
3. Review the automated comment on your PR
4. Address any suggestions before merging

**Getting Issue Guidance**:
1. Create an issue (automatic analysis)
2. OR add `copilot-assist` label to existing issue
3. OR mention `@copilot` in a comment
4. Review the implementation suggestions

**Automatic Copilot Assignment**:
- Copilot is automatically assigned to unassigned issues that have:
  - `copilot-assist`, `copilot-automated`, or `continuous-improvement` labels
  - `[Copilot]` prefix in the title
- Runs automatically twice daily with the development cycle
- Can be manually triggered via workflow_dispatch

**Checking Quality Reports**:
1. Look for issues labeled `continuous-improvement`
2. Review the weekly metrics and suggestions
3. Create PRs to address high-priority items

### For Maintainers

**Customize Review Patterns**:
Edit `copilot-code-review.yml` to add/remove checks

**Adjust Schedule**:
Modify the cron expression in workflows:
- `copilot-continuous-improvement.yml` - Weekly quality reports
- `copilot-automated-development-cycle.yml` - Twice daily task generation

**Configure Thresholds**:
Update the analysis parameters in workflow files

**Control Unassigned Issue Assignment**:
Disable automatic copilot assignment by setting:
```yaml
assign_unassigned: false
```
in workflow_dispatch manual trigger

---

## 🔧 Configuration

### Required Permissions

All workflows require these repository permissions:
- Actions: Read and write
- Contents: Read
- Issues: Write
- Pull requests: Write

### Secrets

No secrets required - workflows use `GITHUB_TOKEN` automatically.

### Branch Protection

Works with branch protection rules. Reviews are informational and don't block merges.

---

## 📊 Metrics & Monitoring

### Workflow Success Rate

Monitor in **Actions** tab:
- Check for failed workflows
- Review workflow logs
- Ensure timely completion

### Analysis Quality

Evaluate based on:
- Relevance of suggestions
- False positive rate
- Developer satisfaction

### Usage Patterns

Track:
- Number of PRs reviewed
- Issues analyzed
- Improvement items completed

---

## 🐛 Troubleshooting

### Workflow Not Running

**Check**:
1. GitHub Actions enabled in repository settings
2. Workflow file syntax is valid (YAML)
3. Permissions are correctly set
4. Branch matches trigger conditions

**Debug**:
```bash
# Validate YAML locally
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/copilot-code-review.yml'))"
```

### Comments Not Posted

**Check**:
1. Workflow completed successfully
2. `pull-requests: write` permission enabled
3. No branch protection blocking
4. Check workflow logs for errors

### Analysis Inaccurate

**Fix**:
1. Update analysis patterns in workflow
2. Improve detection logic
3. Add project-specific rules
4. Update `.github/copilot-instructions.md`

---

## 🚀 Future Enhancements

Planned improvements:
- [ ] Integration with GitHub Copilot Chat API
- [ ] ML-based suggestion ranking
- [ ] Automatic PR creation for fixes
- [ ] Custom rule engine
- [ ] Performance analysis
- [ ] Dependency vulnerability scanning

---

## 📚 Related Documentation

- [Full Documentation](../../docs/COPILOT_DEVELOPMENT_LOOP.md)
- [Automated Development Cycle](../../docs/AUTOMATED_DEVELOPMENT_CYCLE.md)
- [Playwright Copilot Assignment](../../docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md) 🎭
- [Quick Start Guide](../COPILOT_QUICKSTART.md)
- [Copilot Instructions](../copilot-instructions.md)
- [Main Workflows README](README.md)

---

## 🤝 Contributing

To improve these workflows:

1. Test changes locally with `act` (GitHub Actions local runner)
2. Validate YAML syntax
3. Update documentation
4. Submit PR with description of changes

---

**Last Updated**: January 2025  
**Maintained By**: Adaptive Systems Inc.
