# Contributing to Copilot Development Loop

Thank you for your interest in improving the Ouroboros Copilot Development Loop!

## 🎯 Overview

The Copilot Development Loop consists of three GitHub Actions workflows that provide automated code review, issue analysis, and continuous improvement suggestions. Contributions to these workflows help improve code quality for the entire project.

## 🔧 How to Contribute

### Improving Analysis Patterns

The workflows use pattern matching to identify code quality issues. You can enhance these patterns:

**Example: Adding a new pattern check**

Edit `.github/workflows/copilot-code-review.yml`:

```yaml
- name: Check for new pattern
  run: |
    for file in ${{ steps.changed-files.outputs.all_changed_files }}; do
      if [[ "$file" == *.cs ]]; then
        # Check for your pattern
        if grep -q "YOUR_PATTERN" "$file" 2>/dev/null; then
          echo "- ⚠️ \`$file\`: Your suggestion here" >> review-summary.md
        fi
      fi
    done
```

### Adding New Workflow Features

1. **Fork the repository**
2. **Create a feature branch**: `git checkout -b feature/copilot-enhancement`
3. **Make your changes** to workflow files
4. **Test locally** using [act](https://github.com/nektos/act) (GitHub Actions local runner)
5. **Update documentation** in `docs/COPILOT_DEVELOPMENT_LOOP.md`
6. **Submit a pull request**

### Testing Your Changes

Before submitting a PR, validate your changes:

```bash
# 1. Validate YAML syntax
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/copilot-code-review.yml'))"

# 2. Run the test suite
./scripts/test-copilot-workflows.sh

# 3. Test locally with act (if installed)
act pull_request -W .github/workflows/copilot-code-review.yml
```

## 📋 Contribution Ideas

### High Priority

- [ ] Improve pattern detection accuracy
- [ ] Add support for additional programming patterns
- [ ] Reduce false positive rate
- [ ] Enhance error messages

### Medium Priority

- [ ] Add metrics collection for workflow effectiveness
- [ ] Implement machine learning for pattern detection
- [ ] Create workflow for automatic PR creation
- [ ] Add integration with external tools

### Nice to Have

- [ ] Visual reports and dashboards
- [ ] Custom rule engine
- [ ] Performance optimizations
- [ ] Multi-language support

## 🎨 Code Style for Workflows

Follow these conventions when modifying workflows:

### YAML Formatting

```yaml
# Use 2-space indentation
name: Workflow Name

on:
  trigger_type:
    branches: [ main ]

jobs:
  job-name:
    name: Descriptive Job Name
    runs-on: ubuntu-latest
    
    steps:
    - name: Descriptive step name
      run: |
        echo "Multi-line commands"
        echo "Should be indented properly"
```

### Shell Scripts in Workflows

```yaml
- name: Analysis step
  run: |
    # Use clear variable names
    FILE_PATH="${{ steps.context.outputs.file }}"
    
    # Check conditions explicitly
    if [ -f "$FILE_PATH" ]; then
      echo "Processing $FILE_PATH"
    fi
    
    # Handle errors
    command || echo "Warning: command failed"
```

### Comments

```yaml
- name: Complex step
  run: |
    # Explain WHY, not WHAT
    # This checks for monadic error handling because it's a core pattern
    if grep -q "Result<" "$file"; then
      echo "Good pattern detected"
    fi
```

## 🧪 Testing Guidelines

### Unit Testing

For workflow logic, create test files in `/tmp` during testing:

```yaml
- name: Test pattern detection
  run: |
    # Create test file
    cat > /tmp/test.cs << 'EOF'
    public class Test {
        public void Method() {
            throw new Exception();
        }
    }
    EOF
    
    # Test detection
    if grep -q "throw new" /tmp/test.cs; then
      echo "✓ Pattern detected correctly"
    else
      echo "✗ Pattern not detected"
      exit 1
    fi
```

### Integration Testing

Test the full workflow on your fork:

1. Create a test branch
2. Make a test commit
3. Trigger the workflow
4. Verify the output

## 📝 Documentation Requirements

When adding new features, update:

1. **Workflow file itself**: Add inline comments
2. **README_COPILOT.md**: Update workflow descriptions
3. **COPILOT_DEVELOPMENT_LOOP.md**: Add usage examples
4. **COPILOT_QUICKSTART.md**: Update if user-facing changes

### Documentation Example

```markdown
## New Feature: Pattern X Detection

### What it does
Detects pattern X in code and suggests improvement Y.

### Example
**Before:**
\`\`\`csharp
// Bad pattern
\`\`\`

**After:**
\`\`\`csharp
// Good pattern
\`\`\`

### Configuration
Enable in workflow with:
\`\`\`yaml
detect_pattern_x: true
\`\`\`
```

## 🐛 Bug Reports

Found a bug in the workflows?

1. **Search existing issues** to avoid duplicates
2. **Create a new issue** with label `copilot-workflow-bug`
3. **Include**:
   - Workflow name
   - Expected behavior
   - Actual behavior
   - Workflow run URL
   - Relevant logs

### Bug Report Template

```markdown
**Workflow**: copilot-code-review.yml

**Expected**: Pattern X should be detected

**Actual**: Pattern X not detected

**Run URL**: https://github.com/.../actions/runs/...

**Logs**:
\`\`\`
[Include relevant logs]
\`\`\`

**Steps to Reproduce**:
1. Create PR with file containing X
2. Wait for workflow
3. Check comments
```

## 🌟 Feature Requests

Want to suggest a new feature?

1. **Open an issue** with label `copilot-workflow-enhancement`
2. **Describe the use case**: What problem does it solve?
3. **Propose a solution**: How should it work?
4. **Consider impact**: How does it affect existing workflows?

### Feature Request Template

```markdown
**Use Case**: As a developer, I want to...

**Problem**: Currently, the workflow doesn't...

**Proposed Solution**: Add a step that...

**Impact**: 
- Existing workflows: No breaking changes
- Performance: Adds ~30s to workflow time
- Users: Opt-in via configuration

**Example**:
\`\`\`yaml
# Configuration
\`\`\`
```

## 🔄 Pull Request Process

1. **Create PR** against `main` branch
2. **Fill out PR template** completely
3. **Link related issues** using keywords (Fixes #123)
4. **Wait for review**: The Copilot Code Review workflow will analyze your PR!
5. **Address feedback** from both automated review and maintainers
6. **Update documentation** as needed
7. **Merge** after approval

### PR Checklist

- [ ] YAML files are valid (run test script)
- [ ] Documentation updated
- [ ] Changes tested locally
- [ ] No breaking changes (or documented if necessary)
- [ ] Examples provided for new features
- [ ] Error handling included

## 🤝 Code Review Guidelines

When reviewing PRs for Copilot workflows:

### What to Check

- **Correctness**: Does the logic work as intended?
- **Performance**: Will it slow down workflows significantly?
- **Security**: Are there any security implications?
- **Maintainability**: Is the code clear and well-documented?
- **Compatibility**: Does it work with existing workflows?

### Providing Feedback

- Be constructive and specific
- Suggest improvements, don't just criticize
- Reference documentation when relevant
- Test the changes if possible

## 📊 Metrics and Success Criteria

Good contributions to Copilot workflows should:

- ✅ Improve detection accuracy
- ✅ Reduce false positives
- ✅ Provide clear, actionable feedback
- ✅ Execute in reasonable time (<2 minutes)
- ✅ Be well-documented
- ✅ Include tests

## 🎓 Learning Resources

### GitHub Actions

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Workflow Syntax](https://docs.github.com/en/actions/reference/workflow-syntax-for-github-actions)
- [GitHub Actions Toolkit](https://github.com/actions/toolkit)

### Pattern Matching

- [grep Tutorial](https://www.gnu.org/software/grep/manual/grep.html)
- [Regular Expressions](https://regex101.com/)
- [Shell Scripting Guide](https://www.shellscript.sh/)

### Functional Programming Patterns

- [Ouroboros Copilot Instructions](copilot-instructions.md)
- [Functional Programming in C#](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/)

## 💬 Community

### Getting Help

- **Questions**: Open an issue with label `question`
- **Discussions**: Use GitHub Discussions
- **Real-time**: Check if there's a community chat

### Staying Updated

- **Watch** the repository for updates
- **Star** to show support
- **Follow** release notes

## 🙏 Recognition

Contributors to Copilot workflows are recognized:

- Listed in PR descriptions
- Mentioned in release notes
- Credited in documentation

## 📜 License

By contributing, you agree that your contributions will be licensed under the same license as the project.

---

**Thank you for contributing to Ouroboros Copilot Development Loop!** 🚀

Your improvements help the entire development community write better code.
