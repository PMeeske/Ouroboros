# GitHub Actions Workflow Resilience Guide

## Overview

This document describes the resilience mechanisms implemented across all GitHub Actions workflows in the Ouroboros project. These mechanisms ensure workflows handle transient failures gracefully, timeout appropriately, and don't block deployments due to non-critical failures.

## Resilience Mechanisms

### 1. Automatic Retry Logic

All workflows use the `nick-fields/retry-action@v3` for operations prone to transient failures:

#### Network Operations
- **NuGet Package Restore**: 3 attempts, 30s wait
- **Docker Registry Operations**: Built-in retry in docker/build-push-action
- **Ollama Model Downloads**: 3 attempts, 60s wait (large files)
- **MAUI Workload Installation**: 3 attempts, 30s wait

#### Infrastructure Operations
- **Kubectl Commands**: 3 attempts, 5-15s wait
- **Terraform Init**: 3 attempts, 30s wait
- **Kubernetes Cluster Connection**: 3 attempts, 10s wait

#### Test Operations
- **Test Execution**: 2 attempts, 10s wait
- **Integration Tests**: 2 attempts, 10-15s wait

### 2. Timeout Management

#### Job-Level Timeouts
Prevents runaway workflows from consuming resources indefinitely:

| Workflow | Job | Timeout |
|----------|-----|---------|
| dotnet-coverage | test-coverage | 30 min |
| dotnet-coverage | benchmark-tests | 45 min |
| ionos-deploy | infrastructure | 15 min |
| ionos-deploy | test | 20 min |
| ionos-deploy | build-and-push | 60 min |
| ionos-deploy | deploy | 30 min |
| android-build | build-android | 30 min |
| terraform-infrastructure | terraform | 45 min |
| terraform-infrastructure | notify | 5 min |
| terraform-tests | validate | 15 min |
| terraform-tests | module-tests | 20 min |
| terraform-tests | environment-tests | 20 min |
| terraform-tests | run-test-suite | 20 min |
| terraform-tests | security-scan | 15 min |
| terraform-tests | summary | 5 min |
| ollama-integration-test | ollama-integration | 30 min |
| copilot-automated-development-cycle | check-pr-limit | 10 min |
| copilot-automated-development-cycle | analyze-and-generate-tasks | 30 min |
| copilot-automated-development-cycle | create-improvement-issues | 20 min |
| copilot-agent-solver | dispatch-copilot-agent | 30 min |

#### Step-Level Timeouts
Critical long-running operations have individual timeouts:

- Checkout: 5 min
- Docker builds: 30 min
- Terraform operations: 5-30 min depending on action
- Kubectl operations: 3-10 min
- Test runs: 15-20 min
- Copilot operations: 15 min

### 3. Non-Critical Failure Handling

Operations marked with `continue-on-error: true` won't block workflow completion:

#### Observability Operations
- ✅ Artifact uploads
- ✅ Test result publishing
- ✅ Code coverage reports
- ✅ PR comments
- ✅ Benchmark result uploads

#### Notification Operations
- ✅ Email notifications
- ✅ Slack notifications (if added)
- ✅ Status updates

#### Optional Operations
- ✅ Security scan warnings
- ✅ Code coverage summary
- ✅ Terraform output uploads
- ✅ Cleanup operations

### 4. Concurrency Controls

Prevents race conditions and resource conflicts:

#### Deployment Workflows (No Cancellation)
```yaml
concurrency:
  group: ionos-deployment-${{ github.ref }}
  cancel-in-progress: false  # Complete deployments, never cancel
```

- **ionos-deploy.yml**: Per-branch deployment serialization
- **terraform-infrastructure.yml**: Per-environment serialization

#### Test/Build Workflows (With Cancellation)
```yaml
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true  # Cancel old runs to save resources
```

- **dotnet-coverage.yml**: Cancel outdated test runs
- **android-build.yml**: Cancel outdated builds
- **terraform-tests.yml**: Cancel outdated test runs
- **ollama-integration-test.yml**: Cancel outdated integration tests

### 5. Error Recovery & Cleanup

#### Deployment Cleanup
On deployment failures:
- Remove sensitive kubeconfig files
- Log recent Kubernetes events for debugging
- Preserve deployment state for manual intervention

#### Resource Cleanup
```yaml
- name: Cleanup on failure
  if: failure()
  continue-on-error: true
  run: |
    rm -f kubeconfig.yaml
    kubectl get events -n ${{ env.NAMESPACE }} --sort-by='.lastTimestamp' | tail -20
```

## Best Practices

### When to Use Retry

✅ **Use retry for:**
- Network operations (downloads, API calls)
- Package manager operations
- Cluster operations
- External service calls

❌ **Don't use retry for:**
- Unit tests (fix the test instead)
- Code compilation
- Local file operations
- Deterministic failures

### When to Use Continue-on-Error

✅ **Use continue-on-error for:**
- Notifications
- Artifact uploads
- Optional reporting
- Cleanup operations

❌ **Don't use continue-on-error for:**
- Core build steps
- Deployment operations
- Security validations
- Required tests

### Timeout Guidelines

- **Checkout/Setup**: 5 minutes
- **Package Restore**: 10 minutes
- **Build**: 15-20 minutes
- **Test**: 20-30 minutes
- **Docker Build**: 30 minutes
- **Deployment**: 30 minutes
- **Infrastructure**: 45 minutes

## Monitoring & Debugging

### Workflow Logs
All workflows provide detailed logging:
- Step execution times
- Retry attempts
- Failure context
- Resource status

### Artifacts
Preserved for debugging:
- Test results (30 days)
- Coverage reports (30 days)
- Build logs (7 days)
- Terraform outputs (30 days)
- Kubernetes events (in logs)

### Failure Analysis
When workflows fail:
1. Check job summary for high-level status
2. Review step logs for specific errors
3. Check artifacts for detailed reports
4. Review Kubernetes events for deployment issues
5. Check retry attempts to identify transient vs. persistent failures

## Configuration Examples

### Basic Retry Pattern
```yaml
- name: Operation with retry
  uses: nick-fields/retry-action@v3
  with:
    timeout_minutes: 10
    max_attempts: 3
    retry_wait_seconds: 30
    command: your-command-here
```

### Conditional Retry
```yaml
- name: Critical operation
  uses: nick-fields/retry-action@v3
  with:
    timeout_minutes: 5
    max_attempts: 3
    retry_wait_seconds: 10
    command: kubectl apply -f deployment.yaml
```

### Non-Critical Operation
```yaml
- name: Upload artifacts
  uses: actions/upload-artifact@v4
  continue-on-error: true
  with:
    name: test-results
    path: results/
```

### Cleanup on Failure
```yaml
- name: Cleanup
  if: failure()
  continue-on-error: true
  run: |
    # Cleanup commands
    rm -f sensitive-file
    echo "Cleanup completed"
```

## Testing Resilience

### Simulating Failures

#### Network Failures
Test retry logic by temporarily blocking network:
```bash
# In workflow, add random failure simulation
if [ $RANDOM -lt 10000 ]; then exit 1; fi
```

#### Timeout Testing
Verify timeouts work:
```yaml
- name: Long operation
  run: sleep 600  # Should timeout
  timeout-minutes: 5
```

#### Concurrency Testing
Push multiple commits rapidly to test concurrency controls.

## Troubleshooting

### Common Issues

#### Retry Not Working
- Verify `nick-fields/retry-action@v3` version
- Check command exit codes
- Ensure timeout is longer than operation

#### Timeout Too Short
- Review historical run times
- Add 2x buffer for safety
- Consider network variability

#### Concurrency Conflicts
- Verify concurrency group matches pattern
- Check if cancel-in-progress is appropriate
- Review GitHub Actions run queue

## Maintenance

### Regular Reviews
- Monthly: Review timeout values vs. actual execution times
- Quarterly: Analyze retry success rates
- Annually: Update retry action versions

### Metrics to Track
- Workflow success rate
- Retry success rate
- Average execution time
- Timeout frequency
- Artifact upload success rate

## References

- [GitHub Actions Workflow Syntax](https://docs.github.com/en/actions/using-workflows/workflow-syntax-for-github-actions)
- [Retry Action Documentation](https://github.com/nick-fields/retry-action)
- [Best Practices for CI/CD](https://docs.github.com/en/actions/guides/about-continuous-integration)
- [Managing Concurrency](https://docs.github.com/en/actions/using-jobs/using-concurrency)

## Change Log

### 2025-01-13
- Initial implementation of comprehensive resilience mechanisms
- Added retry logic to all network operations
- Implemented timeout management across all workflows
- Added concurrency controls
- Enhanced error handling and cleanup
- Made notifications non-blocking
