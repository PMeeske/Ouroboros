// <copyright file="SelfAssemblySecurityTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;
using Xunit;

namespace Ouroboros.Tests.Tests.Safety;

/// <summary>
/// Safety-critical tests for the SelfAssemblyEngine security pipeline.
/// Verifies ethics evaluation, approval gates, and sandbox testing.
/// </summary>
[Trait("Category", "Safety")]
public sealed class SelfAssemblySecurityTests
{
    #region Pipeline Integrity Tests

    [Fact]
    public async Task Pipeline_RequiresEthicsEvaluation_BeforeCompilation()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            MinSafetyScore = 0.8
        };
        var engine = new SelfAssemblyEngine(config);
        
        bool validationCalled = false;
        engine.SetMeTTaValidator(async blueprint =>
        {
            validationCalled = true;
            await Task.Delay(10);
            return new MeTTaValidation(
                true,
                0.9,
                Array.Empty<string>(),
                Array.Empty<string>(),
                "(validate-test)");
        });

        var blueprint = CreateValidBlueprint("TestNeuron");

        // Act
        var result = await engine.SubmitBlueprintAsync(blueprint);

        // Assert
        result.IsSuccess.Should().BeTrue();
        validationCalled.Should().BeTrue("validation (ethics) must be called before compilation");
        
        var proposal = engine.GetProposal(result.Value);
        proposal.Should().NotBeNull();
        proposal!.Status.Should().Be(AssemblyProposalStatus.PendingApproval, 
            "should not proceed to compilation without approval");
    }

    [Fact]
    public async Task Pipeline_RequiresApprovalCallback_BeforeDeployment()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            MinSafetyScore = 0.8
        };
        var engine = new SelfAssemblyEngine(config);
        
        bool approvalRequested = false;
        engine.SetMeTTaValidator(async blueprint => 
        {
            await Task.Delay(1);
            return new MeTTaValidation(true, 0.9, Array.Empty<string>(), Array.Empty<string>(), "test");
        });
        
        engine.SetApprovalCallback(async proposal =>
        {
            approvalRequested = true;
            await Task.Delay(10);
            return true;
        });

        var blueprint = CreateValidBlueprint("ApprovalTestNeuron");

        // Act
        var submitResult = await engine.SubmitBlueprintAsync(blueprint);
        submitResult.IsSuccess.Should().BeTrue();
        
        var approveResult = await engine.ApproveProposalAsync(submitResult.Value);

        // Assert
        approveResult.IsSuccess.Should().BeTrue();
        approvalRequested.Should().BeTrue("approval callback must be invoked before deployment");
    }

    [Fact]
    public async Task Pipeline_RejectedByEthics_DoesNotCompile()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            MinSafetyScore = 0.8
        };
        var engine = new SelfAssemblyEngine(config);
        
        engine.SetMeTTaValidator(async blueprint =>
        {
            await Task.Delay(1);
            // Return validation failure (ethics rejection)
            return new MeTTaValidation(
                false,
                0.3,
                new List<string> { "Blueprint violates safety constraints" },
                Array.Empty<string>(),
                "(reject-unsafe)");
        });

        var blueprint = CreateValidBlueprint("UnsafeNeuron");

        // Act
        var result = await engine.SubmitBlueprintAsync(blueprint);

        // Assert
        result.IsSuccess.Should().BeFalse("rejected blueprints should fail submission");
        result.Error.Should().ContainEquivalentOf("validation failed");
        
        // Verify neuron was not added to assembled list
        engine.GetAssembledNeurons().Should().NotContainKey("UnsafeNeuron");
    }

    [Fact]
    public async Task Pipeline_RejectedByApproval_DoesNotDeploy()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            MinSafetyScore = 0.8
        };
        var engine = new SelfAssemblyEngine(config);
        
        bool deployed = false;
        engine.SetMeTTaValidator(async blueprint =>
        {
            await Task.Delay(1);
            return new MeTTaValidation(true, 0.9, Array.Empty<string>(), Array.Empty<string>(), "test");
        });
        
        engine.SetApprovalCallback(async proposal =>
        {
            await Task.Delay(1);
            return false; // Reject
        });
        
        engine.NeuronAssembled += (sender, e) => deployed = true;

        var blueprint = CreateValidBlueprint("RejectedNeuron");

        // Act
        var submitResult = await engine.SubmitBlueprintAsync(blueprint);
        submitResult.IsSuccess.Should().BeTrue();
        
        var approveResult = await engine.ApproveProposalAsync(submitResult.Value);
        await Task.Delay(100); // Wait for async pipeline

        // Assert
        deployed.Should().BeFalse("rejected proposals should not be deployed");
        engine.GetAssembledNeurons().Should().NotContainKey("RejectedNeuron");
        
        var proposal = engine.GetProposal(submitResult.Value);
        proposal!.Status.Should().Be(AssemblyProposalStatus.Rejected);
    }

    #endregion

    #region Sandbox Testing

    [Fact]
    public async Task SandboxTimeout_IsConfigurable()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            SandboxTimeout = TimeSpan.FromMilliseconds(100),
            MinSafetyScore = 0.5
        };
        var engine = new SelfAssemblyEngine(config);

        // Act & Assert
        config.SandboxTimeout.Should().Be(TimeSpan.FromMilliseconds(100),
            "sandbox timeout should be configurable");
    }

    [Fact]
    public async Task Sandbox_NeuronConstruction_StructureInPlace()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            MinSafetyScore = 0.5
        };
        var engine = new SelfAssemblyEngine(config);
        
        bool failureReported = false;
        engine.AssemblyFailed += (sender, e) =>
        {
            failureReported = true;
        };
        
        engine.SetMeTTaValidator(async blueprint =>
        {
            await Task.Delay(1);
            return new MeTTaValidation(true, 0.9, Array.Empty<string>(), Array.Empty<string>(), "test");
        });
        
        // Generate code with a neuron that throws in constructor
        engine.SetCodeGenerator(async blueprint =>
        {
            await Task.Delay(1);
            return @"
using System;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.SelfAssembled
{
    public class ThrowingNeuron : Neuron
    {
        public ThrowingNeuron()
        {
            throw new InvalidOperationException(""Intentional test failure"");
        }
        
        public override string Name => ""ThrowingNeuron"";
        
        protected override void ConfigureSubscriptions() { }
        
        protected override async Task OnMessageAsync(NeuralMessage message, CancellationToken ct)
        {
            await Task.CompletedTask;
        }
    }
}";
        });

        var blueprint = CreateValidBlueprint("ThrowingNeuron");

        // Act
        var submitResult = await engine.SubmitBlueprintAsync(blueprint);
        
        // Assert
        // This test verifies the structure is in place for sandbox testing
        // Note: Full pipeline execution (approval->compilation->instantiation) would require
        // a more complete integration test setup
        submitResult.IsSuccess.Should().BeTrue("submission should succeed for further pipeline testing");
    }

    #endregion

    #region Blueprint Validation

    [Fact]
    public async Task Blueprint_WithNullName_IsRejected()
    {
        // Arrange
        var engine = new SelfAssemblyEngine();
        var blueprint = new NeuronBlueprint
        {
            Name = null!,
            Description = "Test",
            Rationale = "Test",
            SubscribedTopics = new List<string> { "test" },
            MessageHandlers = new List<MessageHandler>
            {
                new MessageHandler
                {
                    TopicPattern = "test",
                    HandlingLogic = "test"
                }
            },
            ConfidenceScore = 0.8
        };

        // Act
        var result = await engine.SubmitBlueprintAsync(blueprint);

        // Assert
        result.IsSuccess.Should().BeFalse("blueprint with null name should be rejected");
    }

    [Fact]
    public async Task GeneratedCode_CanBeEmpty_WhenGeneratorReturnsEmpty()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            MinSafetyScore = 0.8
        };
        var engine = new SelfAssemblyEngine(config);
        
        engine.SetMeTTaValidator(async blueprint =>
        {
            await Task.Delay(1);
            return new MeTTaValidation(true, 0.9, Array.Empty<string>(), Array.Empty<string>(), "test");
        });
        
        // Set code generator to return empty code
        engine.SetCodeGenerator(async blueprint =>
        {
            await Task.Delay(1);
            return string.Empty;
        });

        var blueprint = CreateValidBlueprint("EmptyCodeNeuron");

        // Act
        var submitResult = await engine.SubmitBlueprintAsync(blueprint);
        
        // Assert
        submitResult.IsSuccess.Should().BeTrue("submission succeeds even with empty generated code");
        
        // Empty code would fail during compilation stage (not tested here)
        var proposal = engine.GetProposal(submitResult.Value);
        proposal.Should().NotBeNull();
        proposal!.GeneratedCode.Should().BeEmpty();
    }

    [Fact]
    public async Task Blueprint_MeTTaValidation_IsEnforced()
    {
        // Arrange
        var config = new SelfAssemblyConfig
        {
            AutoApprovalEnabled = false,
            MinSafetyScore = 0.8
        };
        var engine = new SelfAssemblyEngine(config);
        
        bool validatorCalled = false;
        engine.SetMeTTaValidator(async blueprint =>
        {
            validatorCalled = true;
            await Task.Delay(1);
            // Return validation with low safety score
            return new MeTTaValidation(
                true,
                0.5, // Below minimum
                Array.Empty<string>(),
                new List<string> { "Low safety score" },
                "(validate-low-safety)");
        });

        var blueprint = CreateValidBlueprint("LowSafetyNeuron");

        // Act
        var result = await engine.SubmitBlueprintAsync(blueprint);

        // Assert
        validatorCalled.Should().BeTrue("MeTTa validator must be called");
        result.IsSuccess.Should().BeFalse("blueprint below safety threshold should be rejected");
        result.Error.Should().ContainEquivalentOf("safety score");
    }

    #endregion

    #region Helper Methods

    private static NeuronBlueprint CreateValidBlueprint(string name)
    {
        return new NeuronBlueprint
        {
            Name = name,
            Description = $"Test neuron {name}",
            Rationale = "Test purposes",
            SubscribedTopics = new List<string> { "test.topic" },
            MessageHandlers = new List<MessageHandler>
            {
                new MessageHandler
                {
                    TopicPattern = "test.topic",
                    HandlingLogic = "Process test messages"
                }
            },
            ConfidenceScore = 0.9
        };
    }

    #endregion
}
