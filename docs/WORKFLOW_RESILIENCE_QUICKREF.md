# Workflow Resilience Quick Reference

## 🚀 Quick Start

All Ouroboros workflows now have built-in resilience mechanisms. This guide helps you understand and maintain them.

## 📋 At a Glance

### What Changed?
- ✅ Automatic retry for flaky operations
- ✅ Timeout protection (no more hung workflows)
- ✅ Non-critical failures won't block deployments
- ✅ Concurrent runs properly managed
- ✅ Better error messages and debugging

### Which Workflows?
All 8 workflows updated:
- `dotnet-coverage.yml`
- `ionos-deploy.yml`
- `android-build.yml`
- `terraform-infrastructure.yml`
- `terraform-tests.yml`
- `ollama-integration-test.yml`
- `copilot-automated-development-cycle.yml`
- `copilot-agent-solver.yml`

## 🔄 Retry Pattern

**When operations fail due to network issues, they automatically retry!**

```yaml
- name: Restore dependencies
  uses: nick-fields/retry-action@v3
  with:
    timeout_minutes: 10
    max_attempts: 3
    retry_wait_seconds: 30
    command: dotnet restore
```

**Operations with retry:**
- NuGet restore
- Docker push/pull
- kubectl commands
- Terraform init
- Ollama model downloads
- MAUI workload installs

## ⏱️ Timeout Protection

**Workflows won't hang forever!**

Every job and long-running step has a timeout:
- Tests: 20-30 minutes
- Builds: 15-30 minutes
- Deployments: 30 minutes
- Infrastructure: 45 minutes

## 🛡️ Non-Critical Failures

**These operations won't block your workflow:**
- ✅ Uploading artifacts
- ✅ Sending email notifications
- ✅ Posting PR comments
- ✅ Uploading code coverage
- ✅ Security scan warnings

**These still fail the workflow (as they should):**
- ❌ Build failures
- ❌ Test failures
- ❌ Deployment errors
- ❌ Required validations

## 🔒 Concurrency Control

**Workflows won't step on each other's toes!**

**Deployments (no cancellation):**
- ionos-deploy
- terraform-infrastructure

**Tests/Builds (cancel old runs):**
- dotnet-coverage
- android-build
- terraform-tests
- ollama-integration-test

## 🐛 Debugging Failures

### Step 1: Check Job Summary
- Look for ✅/❌ status
- Check which step failed
- Note if it was a timeout

### Step 2: Review Step Logs
- Scroll to the failed step
- Look for retry attempts
- Check error messages

### Step 3: Download Artifacts
- Test results (if available)
- Coverage reports
- Build logs
- Kubernetes events (in deploy logs)

### Step 4: Common Issues

**Timeout occurred?**
→ Operation took longer than expected. May need timeout increase.

**Retry exhausted?**
→ Persistent failure, not transient. Needs investigation.

**Deployment failed?**
→ Check kubectl events in logs for cluster issues.

**Artifact upload failed?**
→ Non-critical, workflow still succeeds. Check storage quota.

## 🔧 Adding Resilience to New Steps

### Template: Retry Pattern
```yaml
- name: My Network Operation
  uses: nick-fields/retry-action@v3
  with:
    timeout_minutes: 10
    max_attempts: 3
    retry_wait_seconds: 30
    command: |
      your-command-here
      can-be-multiline
```

### Template: Non-Critical Operation
```yaml
- name: Upload Reports
  uses: some-action@v1
  continue-on-error: true  # Won't fail workflow
  with:
    # ... action config
```

### Template: Timeout Protection
```yaml
- name: Long Running Operation
  run: |
    # your commands
  timeout-minutes: 20  # Adjust as needed
```

## 📞 Need Help?

**Full Documentation:** `docs/WORKFLOW_RESILIENCE.md`

**Common Questions:**

**Q: Why did my workflow retry 3 times?**
A: Transient network failure. Automatic retry resolved it.

**Q: Workflow timed out. Is this bad?**
A: Possibly. Check logs to see if it's a performance issue or timeout too short.

**Q: Artifact upload failed but workflow succeeded?**
A: Expected! Artifact uploads are non-critical.

**Q: Can I adjust retry/timeout values?**
A: Yes! Edit the workflow file and adjust parameters.

**Q: How do I test my changes to workflows?**
A: Use `workflow_dispatch` trigger or push to a test branch.

## 🎯 Best Practices

✅ **DO:**
- Trust the retry mechanism for transient failures
- Check logs when operations timeout
- Add `continue-on-error` only for observability
- Use concurrency controls for state-modifying operations

❌ **DON'T:**
- Remove retry from network operations
- Set timeouts too low (causes false failures)
- Add `continue-on-error` to critical steps
- Ignore persistent retry failures

## 📊 Monitoring

**Key Metrics:**
- Workflow success rate should improve
- Transient failures self-heal via retry
- No workflows should hang indefinitely
- Deployments should never conflict

**Review Monthly:**
- Are timeouts appropriate?
- Are retry rates normal?
- Any persistent failures?

## 🔗 Related Documentation

- Main Guide: `docs/WORKFLOW_RESILIENCE.md`
- GitHub Actions: https://docs.github.com/en/actions
- Retry Action: https://github.com/nick-fields/retry-action

---

**Last Updated:** 2025-01-13
**Version:** 1.0
