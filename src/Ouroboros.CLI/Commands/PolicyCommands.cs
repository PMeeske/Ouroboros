using System.Text.Json;
using Ouroboros.Domain.Governance;
using Ouroboros.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Policy management commands for governance and safety.
/// Phase 5: Governance, Safety, and Ops.
/// </summary>
public static class PolicyCommands
{
    private static readonly PolicyEngine _policyEngine = new();

    /// <summary>
    /// Executes a policy command based on the provided options.
    /// </summary>
    public static async Task RunPolicyAsync(PolicyOptions options)
    {
        try
        {
            var command = options.Command.ToLowerInvariant();
            await (command switch
            {
                "list" => ExecuteListAsync(options),
                "create" => ExecuteCreateAsync(options),
                "simulate" => ExecuteSimulateAsync(options),
                "enforce" => ExecuteEnforceAsync(options),
                "audit" => ExecuteAuditAsync(options),
                "approve" => ExecuteApproveAsync(options),
                _ => PrintErrorAsync($"Unknown policy command: {options.Command}. Valid commands: list, create, simulate, enforce, audit, approve")
            });
        }
        catch (Exception ex)
        {
            PrintError($"Policy operation failed: {ex.Message}");
            if (options.Verbose)
            {
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    private static Task ExecuteListAsync(PolicyOptions options)
    {
        Console.WriteLine("=== Registered Policies ===\n");

        var policies = _policyEngine.GetPolicies(activeOnly: false);

        if (policies.Count == 0)
        {
            Console.WriteLine("No policies registered.");
            return Task.CompletedTask;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(policies, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
        }
        else if (options.Format.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            PrintPoliciesTable(policies, options.Verbose);
        }
        else
        {
            PrintPoliciesSummary(policies);
        }

        return Task.CompletedTask;
    }

    private static Task ExecuteCreateAsync(PolicyOptions options)
    {
        Console.WriteLine("=== Creating Policy ===\n");

        Policy policy;

        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            // Load from file
            if (!File.Exists(options.FilePath))
            {
                PrintError($"Policy file not found: {options.FilePath}");
                return Task.CompletedTask;
            }

            var json = File.ReadAllText(options.FilePath);
            var loadedPolicy = JsonSerializer.Deserialize<Policy>(json);
            
            if (loadedPolicy == null)
            {
                PrintError("Failed to deserialize policy from file");
                return Task.CompletedTask;
            }

            policy = loadedPolicy;
        }
        else
        {
            // Create simple policy from command line args
            if (string.IsNullOrWhiteSpace(options.Name))
            {
                PrintError("Policy name is required (use --name)");
                return Task.CompletedTask;
            }

            policy = Policy.Create(
                options.Name,
                options.Description ?? "Policy created via CLI");
        }

        var result = _policyEngine.RegisterPolicy(policy);

        if (result.IsSuccess)
        {
            Console.WriteLine($"✓ Policy '{policy.Name}' created successfully");
            Console.WriteLine($"  ID: {policy.Id}");
            Console.WriteLine($"  Priority: {policy.Priority}");
            Console.WriteLine($"  Rules: {policy.Rules.Count}");
            Console.WriteLine($"  Quotas: {policy.Quotas.Count}");
            Console.WriteLine($"  Thresholds: {policy.Thresholds.Count}");
            Console.WriteLine($"  Approval Gates: {policy.ApprovalGates.Count}");
        }
        else
        {
            PrintError($"Failed to create policy: {result.Error}");
        }

        return Task.CompletedTask;
    }

    private static async Task ExecuteSimulateAsync(PolicyOptions options)
    {
        Console.WriteLine("=== Simulating Policy ===\n");

        if (string.IsNullOrWhiteSpace(options.PolicyId))
        {
            PrintError("Policy ID is required for simulation (use --policy-id)");
            return;
        }

        if (!Guid.TryParse(options.PolicyId, out var policyId))
        {
            PrintError($"Invalid policy ID: {options.PolicyId}");
            return;
        }

        var policies = _policyEngine.GetPolicies(activeOnly: false);
        var policy = policies.FirstOrDefault(p => p.Id == policyId);

        if (policy == null)
        {
            PrintError($"Policy not found: {policyId}");
            return;
        }

        // Create a test context
        var context = new
        {
            timestamp = DateTime.UtcNow,
            user = "cli_user",
            operation = "test_operation"
        };

        var result = await _policyEngine.SimulatePolicyAsync(policy, context);

        if (result.IsSuccess)
        {
            var simulation = result.Value;
            Console.WriteLine($"Policy: {simulation.Policy.Name}");
            Console.WriteLine($"Compliant: {simulation.EvaluationResult.IsCompliant}");
            Console.WriteLine($"Would Block: {simulation.WouldBlock}");
            Console.WriteLine($"Violations: {simulation.EvaluationResult.Violations.Count}");

            if (simulation.EvaluationResult.Violations.Count > 0)
            {
                Console.WriteLine("\nViolations:");
                foreach (var violation in simulation.EvaluationResult.Violations)
                {
                    Console.WriteLine($"  [{violation.Severity}] {violation.Message}");
                    Console.WriteLine($"    Action: {violation.RecommendedAction}");
                }
            }

            if (simulation.RequiredApprovals.Count > 0)
            {
                Console.WriteLine($"\nRequired Approvals: {simulation.RequiredApprovals.Count}");
                foreach (var gate in simulation.RequiredApprovals)
                {
                    Console.WriteLine($"  - {gate.Name}: {gate.MinimumApprovals} approval(s) required");
                }
            }
        }
        else
        {
            PrintError($"Simulation failed: {result.Error}");
        }
    }

    private static async Task ExecuteEnforceAsync(PolicyOptions options)
    {
        Console.WriteLine("=== Enforcing Policies ===\n");

        // Create a test context
        var context = new
        {
            timestamp = DateTime.UtcNow,
            user = "cli_user",
            operation = "enforce_test"
        };

        var result = await _policyEngine.EnforcePoliciesAsync(context);

        if (result.IsSuccess)
        {
            var enforcement = result.Value;
            Console.WriteLine($"Evaluations: {enforcement.Evaluations.Count}");
            Console.WriteLine($"Total Violations: {enforcement.Evaluations.Sum(e => e.Violations.Count)}");
            Console.WriteLine($"Blocked: {enforcement.IsBlocked}");
            Console.WriteLine($"Actions Required: {enforcement.ActionsRequired.Count}");
            Console.WriteLine($"Approvals Required: {enforcement.ApprovalsRequired.Count}");

            if (enforcement.ActionsRequired.Count > 0)
            {
                Console.WriteLine("\nRequired Actions:");
                foreach (var action in enforcement.ActionsRequired.Distinct())
                {
                    var count = enforcement.ActionsRequired.Count(a => a == action);
                    Console.WriteLine($"  - {action}: {count}");
                }
            }

            if (enforcement.ApprovalsRequired.Count > 0)
            {
                Console.WriteLine("\nApproval Requests Created:");
                foreach (var request in enforcement.ApprovalsRequired)
                {
                    Console.WriteLine($"  ID: {request.Id}");
                    Console.WriteLine($"  Operation: {request.OperationDescription}");
                    Console.WriteLine($"  Deadline: {request.Deadline:yyyy-MM-dd HH:mm:ss} UTC");
                    Console.WriteLine();
                }
            }
        }
        else
        {
            PrintError($"Enforcement failed: {result.Error}");
        }
    }

    private static async Task ExecuteAuditAsync(PolicyOptions options)
    {
        Console.WriteLine("=== Policy Audit Trail ===\n");

        DateTime? since = null;
        if (!string.IsNullOrWhiteSpace(options.Since))
        {
            if (DateTime.TryParse(options.Since, out var sinceDate))
            {
                since = sinceDate;
            }
        }

        var auditTrail = _policyEngine.GetAuditTrail(options.Limit, since);

        if (auditTrail.Count == 0)
        {
            Console.WriteLine("No audit entries found.");
            return;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.Serialize(auditTrail, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);

            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                await File.WriteAllTextAsync(options.OutputPath, json);
                Console.WriteLine($"\n✓ Audit trail exported to: {options.OutputPath}");
            }
        }
        else
        {
            PrintAuditTrailTable(auditTrail, options.Verbose);
        }
    }

    private static Task ExecuteApproveAsync(PolicyOptions options)
    {
        Console.WriteLine("=== Approval Management ===\n");

        if (string.IsNullOrWhiteSpace(options.ApprovalId))
        {
            // List pending approvals
            var pending = _policyEngine.GetPendingApprovals();
            
            if (pending.Count == 0)
            {
                Console.WriteLine("No pending approval requests.");
                return Task.CompletedTask;
            }

            Console.WriteLine($"Pending Approvals: {pending.Count}\n");
            foreach (var request in pending)
            {
                TimeSpan timeUntilDeadline = request.Deadline - DateTime.UtcNow;
                string urgency = timeUntilDeadline.TotalHours < 24 ? "⚠️ URGENT" : "";

                Console.WriteLine($"ID: {request.Id}");
                Console.WriteLine($"Operation: {request.OperationDescription}");
                Console.WriteLine($"Required Approvals: {request.Gate.MinimumApprovals}");
                Console.WriteLine($"Current Approvals: {request.Approvals.Count}");
                Console.WriteLine($"Deadline: {request.Deadline:yyyy-MM-dd HH:mm:ss} UTC {urgency}");
                Console.WriteLine();
            }

            return Task.CompletedTask;
        }

        // Submit an approval
        if (!Guid.TryParse(options.ApprovalId, out var requestId))
        {
            PrintError($"Invalid approval ID: {options.ApprovalId}");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(options.Decision))
        {
            PrintError("Decision is required (use --decision: approve|reject|request-info)");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(options.ApproverId))
        {
            PrintError("Approver ID is required (use --approver)");
            return Task.CompletedTask;
        }

        var decision = options.Decision.ToLowerInvariant() switch
        {
            "approve" => ApprovalDecision.Approve,
            "reject" => ApprovalDecision.Reject,
            "request-info" => ApprovalDecision.RequestInfo,
            _ => ApprovalDecision.Reject
        };

        var approval = new Approval
        {
            ApproverId = options.ApproverId,
            Decision = decision,
            Comments = options.Comments
        };

        var result = _policyEngine.SubmitApproval(requestId, approval);

        if (result.IsSuccess)
        {
            var updated = result.Value;
            Console.WriteLine($"✓ Approval submitted successfully");
            Console.WriteLine($"  Request ID: {updated.Id}");
            Console.WriteLine($"  New Status: {updated.Status}");
            Console.WriteLine($"  Total Approvals: {updated.Approvals.Count}");
            
            if (updated.IsApproved)
            {
                Console.WriteLine($"  ✓ Request is now APPROVED");
            }
        }
        else
        {
            PrintError($"Failed to submit approval: {result.Error}");
        }

        return Task.CompletedTask;
    }

    private static void PrintPoliciesTable(IReadOnlyList<Policy> policies, bool verbose)
    {
        Console.WriteLine($"Total Policies: {policies.Count}\n");
        
        foreach (var policy in policies)
        {
            string status = policy.IsActive ? "✓ ACTIVE" : "  INACTIVE";
            Console.WriteLine($"[{policy.Priority:F1}] {status} {policy.Name}");
            Console.WriteLine($"  ID: {policy.Id}");
            Console.WriteLine($"  Description: {policy.Description}");
            Console.WriteLine($"  Rules: {policy.Rules.Count}, Quotas: {policy.Quotas.Count}, Thresholds: {policy.Thresholds.Count}, Gates: {policy.ApprovalGates.Count}");
            Console.WriteLine($"  Created: {policy.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            
            if (verbose)
            {
                if (policy.Rules.Count > 0)
                {
                    Console.WriteLine("  Rules:");
                    foreach (var rule in policy.Rules)
                    {
                        Console.WriteLine($"    - {rule.Name}: {rule.Condition} → {rule.Action}");
                    }
                }

                if (policy.Quotas.Count > 0)
                {
                    Console.WriteLine("  Quotas:");
                    foreach (var quota in policy.Quotas)
                    {
                        Console.WriteLine($"    - {quota.ResourceName}: {quota.CurrentValue}/{quota.MaxValue} {quota.Unit} ({quota.UtilizationPercent:F0}%)");
                    }
                }
            }
            
            Console.WriteLine();
        }
    }

    private static void PrintPoliciesSummary(IReadOnlyList<Policy> policies)
    {
        var active = policies.Count(p => p.IsActive);
        Console.WriteLine($"Total: {policies.Count} policies ({active} active, {policies.Count - active} inactive)");
        Console.WriteLine($"Total Rules: {policies.Sum(p => p.Rules.Count)}");
        Console.WriteLine($"Total Quotas: {policies.Sum(p => p.Quotas.Count)}");
        Console.WriteLine($"Total Thresholds: {policies.Sum(p => p.Thresholds.Count)}");
        Console.WriteLine($"Total Approval Gates: {policies.Sum(p => p.ApprovalGates.Count)}");
    }

    private static void PrintAuditTrailTable(IReadOnlyList<PolicyAuditEntry> entries, bool verbose)
    {
        Console.WriteLine($"Audit Entries: {entries.Count}\n");

        foreach (var entry in entries)
        {
            Console.WriteLine($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.Action} by {entry.Actor}");
            Console.WriteLine($"  Policy: {entry.Policy.Name}");
            
            if (entry.EvaluationResult != null)
            {
                Console.WriteLine($"  Compliant: {entry.EvaluationResult.IsCompliant}");
                Console.WriteLine($"  Violations: {entry.EvaluationResult.Violations.Count}");
            }

            if (entry.ApprovalRequest != null)
            {
                Console.WriteLine($"  Approval: {entry.ApprovalRequest.Status}");
            }

            if (verbose && entry.Metadata.Count > 0)
            {
                Console.WriteLine("  Metadata:");
                foreach (var kvp in entry.Metadata)
                {
                    Console.WriteLine($"    {kvp.Key}: {kvp.Value}");
                }
            }

            Console.WriteLine();
        }
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"✗ {message}");
        Console.ResetColor();
    }

    private static Task PrintErrorAsync(string message)
    {
        PrintError(message);
        return Task.CompletedTask;
    }
}
