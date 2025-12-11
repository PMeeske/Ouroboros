// <copyright file="Phase5GovernanceExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Core.Monads;
using LangChainPipeline.Domain.Governance;
using LangChainPipeline.Examples;

namespace Ouroboros.Examples.Examples;

/// <summary>
/// Demonstrates Phase 5 Governance, Safety, and Ops features.
/// Shows policy management, maintenance scheduling, and human approval workflows.
/// </summary>
public static class Phase5GovernanceExample
{
    /// <summary>
    /// Runs the Phase 5 governance demonstration.
    /// </summary>
    public static async Task RunAsync()
    {
        ConsoleHelper.WriteHeader("Phase 5: Governance, Safety, and Ops Example");

        await DemonstratePolicyEngine();
        await DemonstrateMaintenanceScheduler();
        await DemonstrateApprovalWorkflow();

        ConsoleHelper.WriteSuccess("Phase 5 governance example completed!");
    }

    private static async Task DemonstratePolicyEngine()
    {
        ConsoleHelper.WriteSubHeader("1. Policy Engine Demo");

        // Create a policy engine
        var policyEngine = new PolicyEngine();

        // Define a resource quota policy
        var quotaPolicy = Policy.Create(
            "ResourceQuotaPolicy",
            "Enforces resource usage limits for AI operations")
        with
        {
            Priority = 1.0,
            Quotas = new List<ResourceQuota>
            {
                new()
                {
                    ResourceName = "cpu_cores",
                    MaxValue = 8.0,
                    CurrentValue = 6.0,
                    Unit = "cores"
                },
                new()
                {
                    ResourceName = "memory",
                    MaxValue = 16.0,
                    CurrentValue = 12.0,
                    Unit = "GB"
                },
                new()
                {
                    ResourceName = "requests_per_hour",
                    MaxValue = 1000.0,
                    CurrentValue = 850.0,
                    Unit = "requests",
                    TimeWindow = TimeSpan.FromHours(1)
                }
            }
        };

        // Register the policy
        var result = policyEngine.RegisterPolicy(quotaPolicy);
        Console.WriteLine($"✓ Registered policy: {result.Value.Name}");

        // Define a safety threshold policy
        var safetyPolicy = Policy.Create(
            "SafetyThresholdPolicy",
            "Ensures AI operations stay within safety bounds")
        with
        {
            Priority = 2.0, // Higher priority than resource policy
            Thresholds = new List<Threshold>
            {
                new()
                {
                    MetricName = "error_rate",
                    UpperBound = 0.05, // 5% error rate
                    Action = PolicyAction.Alert,
                    Severity = ThresholdSeverity.Warning
                },
                new()
                {
                    MetricName = "response_time_ms",
                    UpperBound = 5000, // 5 seconds
                    Action = PolicyAction.Throttle,
                    Severity = ThresholdSeverity.Error
                }
            }
        };

        policyEngine.RegisterPolicy(safetyPolicy);

        // Get all policies
        var policies = policyEngine.GetPolicies();
        Console.WriteLine($"✓ Total active policies: {policies.Count}");

        foreach (var policy in policies)
        {
            Console.WriteLine($"  [{policy.Priority:F1}] {policy.Name}");
            Console.WriteLine($"      Quotas: {policy.Quotas.Count}, Thresholds: {policy.Thresholds.Count}");
        }

        // Simulate policy evaluation
        var context = new
        {
            cpu_usage = 7.5,
            memory_usage = 14.0,
            requests = 950
        };

        var simulation = await policyEngine.SimulatePolicyAsync(quotaPolicy, context);
        if (simulation.IsSuccess)
        {
            Console.WriteLine($"\n✓ Policy Simulation:");
            Console.WriteLine($"  Compliant: {simulation.Value.EvaluationResult.IsCompliant}");
            Console.WriteLine($"  Would Block: {simulation.Value.WouldBlock}");
            Console.WriteLine($"  Violations: {simulation.Value.EvaluationResult.Violations.Count}");
        }

        // Get audit trail
        var auditTrail = policyEngine.GetAuditTrail(limit: 5);
        Console.WriteLine($"\n✓ Audit Trail (last {auditTrail.Count} entries):");
        foreach (var entry in auditTrail)
        {
            Console.WriteLine($"  [{entry.Timestamp:HH:mm:ss}] {entry.Action} on '{entry.Policy.Name}' by {entry.Actor}");
        }
    }

    private static async Task DemonstrateMaintenanceScheduler()
    {
        ConsoleHelper.WriteSubHeader("2. Maintenance Scheduler Demo");

        var scheduler = new MaintenanceScheduler();

        // Create a compaction task
        var compactionTask = MaintenanceScheduler.CreateCompactionTask(
            "Daily DAG Compaction",
            TimeSpan.FromHours(24),
            async ct =>
            {
                // Simulate compaction work
                await Task.Delay(50, ct);
                return Result<CompactionResult>.Success(new CompactionResult
                {
                    SnapshotsCompacted = 15,
                    BytesSaved = 1024 * 1024 * 50, // 50 MB
                    CompactedAt = DateTime.UtcNow
                });
            });

        scheduler.ScheduleTask(compactionTask);
        Console.WriteLine($"✓ Scheduled task: {compactionTask.Name}");
        Console.WriteLine($"  Frequency: Every {compactionTask.Schedule.TotalHours} hours");

        // Execute the compaction task manually
        var execution = await scheduler.ExecuteTaskAsync(compactionTask);
        if (execution.IsSuccess)
        {
            var exec = execution.Value;
            Console.WriteLine($"\n✓ Executed compaction:");
            Console.WriteLine($"  Status: {exec.Status}");
            Console.WriteLine($"  Duration: {(exec.CompletedAt - exec.StartedAt)?.TotalMilliseconds:F0}ms");

            if (exec.Metadata.TryGetValue("result", out var resultObj) && resultObj is CompactionResult compactionResult)
            {
                Console.WriteLine($"  Snapshots Compacted: {compactionResult.SnapshotsCompacted}");
                Console.WriteLine($"  Space Saved: {compactionResult.BytesSaved / (1024 * 1024)} MB");
            }
        }

        // Create an anomaly detection task
        var anomalyTask = MaintenanceScheduler.CreateAnomalyDetectionTask(
            "Hourly Anomaly Detection",
            TimeSpan.FromHours(1),
            async ct =>
            {
                await Task.Delay(30, ct);

                var anomalies = new List<AnomalyAlert>();

                // Simulate detecting an anomaly
                if (DateTime.UtcNow.Second % 2 == 0)
                {
                    anomalies.Add(new AnomalyAlert
                    {
                        MetricName = "dag_depth",
                        Description = "DAG depth exceeds expected range",
                        Severity = AlertSeverity.Warning,
                        ExpectedValue = "< 100 levels",
                        ObservedValue = "125 levels",
                        DetectedAt = DateTime.UtcNow
                    });
                }

                return Result<AnomalyDetectionResult>.Success(new AnomalyDetectionResult
                {
                    Anomalies = anomalies
                });
            });

        var anomalyExecution = await scheduler.ExecuteTaskAsync(anomalyTask);
        if (anomalyExecution.IsSuccess && anomalyExecution.Value.Metadata.TryGetValue("result", out var anomalyResultObj)
            && anomalyResultObj is AnomalyDetectionResult anomalyResult)
        {
            Console.WriteLine($"\n✓ Anomaly Detection:");
            Console.WriteLine($"  Anomalies Found: {anomalyResult.Anomalies.Count}");

            foreach (var anomaly in anomalyResult.Anomalies)
            {
                scheduler.CreateAlert(anomaly);
                Console.WriteLine($"  [{anomaly.Severity}] {anomaly.MetricName}: {anomaly.Description}");
                Console.WriteLine($"    Expected: {anomaly.ExpectedValue}, Observed: {anomaly.ObservedValue}");
            }
        }

        // Get execution history
        var history = scheduler.GetHistory(limit: 5);
        Console.WriteLine($"\n✓ Maintenance History ({history.Count} executions):");
        foreach (var exec in history)
        {
            var duration = exec.CompletedAt.HasValue
                ? (exec.CompletedAt.Value - exec.StartedAt).TotalMilliseconds
                : 0;
            Console.WriteLine($"  [{exec.Status}] {exec.Task.Name} - {duration:F0}ms");
        }
    }

    private static async Task DemonstrateApprovalWorkflow()
    {
        ConsoleHelper.WriteSubHeader("3. Human Approval Workflow Demo");

        var policyEngine = new PolicyEngine();

        // Create a policy with an approval gate
        var criticalOpsPolicy = Policy.Create(
            "CriticalOperationsPolicy",
            "Requires human approval for critical AI operations")
        with
        {
            ApprovalGates = new List<ApprovalGate>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "Production Deployment Gate",
                    Condition = "deployment_target == 'production'",
                    RequiredApprovers = new[] { "admin", "lead_engineer" },
                    MinimumApprovals = 2,
                    ApprovalTimeout = TimeSpan.FromHours(24),
                    TimeoutAction = ApprovalTimeoutAction.Block
                }
            },
            Rules = new List<PolicyRule>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "ProductionDeploymentRule",
                    Condition = "deployment_target == 'production'",
                    Action = PolicyAction.RequireApproval
                }
            }
        };

        policyEngine.RegisterPolicy(criticalOpsPolicy);
        Console.WriteLine($"✓ Created policy with approval gate: {criticalOpsPolicy.Name}");

        // Register custom condition evaluator
        policyEngine.RegisterConditionEvaluator("deployment_target == 'production'", ctx =>
        {
            dynamic context = ctx;
            return context.deployment_target == "production";
        });

        // Simulate a deployment that requires approval
        var deploymentContext = new
        {
            deployment_target = "production",
            version = "1.0.0",
            timestamp = DateTime.UtcNow
        };

        var enforcement = await policyEngine.EnforcePoliciesAsync(deploymentContext);
        if (enforcement.IsSuccess)
        {
            Console.WriteLine($"\n✓ Policy Enforcement:");
            Console.WriteLine($"  Operations Blocked: {enforcement.Value.IsBlocked}");
            Console.WriteLine($"  Approvals Required: {enforcement.Value.ApprovalsRequired.Count}");

            foreach (var approvalRequest in enforcement.Value.ApprovalsRequired)
            {
                Console.WriteLine($"\n  Approval Request: {approvalRequest.Id}");
                Console.WriteLine($"    Operation: {approvalRequest.OperationDescription}");
                Console.WriteLine($"    Min Approvals: {approvalRequest.Gate.MinimumApprovals}");
                Console.WriteLine($"    Deadline: {approvalRequest.Deadline:yyyy-MM-dd HH:mm:ss}");

                // Simulate approvals
                var approval1 = new Approval
                {
                    ApproverId = "admin",
                    Decision = ApprovalDecision.Approve,
                    Comments = "Deployment approved - all checks passed"
                };

                var result1 = policyEngine.SubmitApproval(approvalRequest.Id, approval1);
                Console.WriteLine($"\n  ✓ Approval from {approval1.ApproverId}: {result1.Value.Status}");

                var approval2 = new Approval
                {
                    ApproverId = "lead_engineer",
                    Decision = ApprovalDecision.Approve,
                    Comments = "Technical review complete, LGTM"
                };

                var result2 = policyEngine.SubmitApproval(approvalRequest.Id, approval2);
                Console.WriteLine($"  ✓ Approval from {approval2.ApproverId}: {result2.Value.Status}");

                if (result2.Value.IsApproved)
                {
                    Console.WriteLine($"\n  ✓✓ DEPLOYMENT APPROVED - All requirements met!");
                }
            }
        }

        // Show pending approvals
        var pendingApprovals = policyEngine.GetPendingApprovals();
        Console.WriteLine($"\n✓ Pending Approvals: {pendingApprovals.Count}");
    }
}
