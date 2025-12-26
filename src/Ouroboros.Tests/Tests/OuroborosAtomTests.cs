// <copyright file="OuroborosAtomTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Tests;

using Ouroboros.Agent.MetaAI;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// xUnit tests for OuroborosAtom and OuroborosOrchestrator.
/// </summary>
public class OuroborosAtomXUnitTests
{
    [Fact]
    public void OuroborosAtom_CreateDefault_ShouldHaveDefaultCapabilities()
    {
        // Arrange & Act
        OuroborosAtom atom = OuroborosAtom.CreateDefault("TestOuroboros");

        // Assert
        atom.Should().NotBeNull();
        atom.Name.Should().Be("TestOuroboros");
        atom.CurrentPhase.Should().Be(ImprovementPhase.Plan);
        atom.CycleCount.Should().Be(0);
        atom.Capabilities.Should().HaveCountGreaterThanOrEqualTo(4);
        atom.SafetyConstraints.Should().HaveFlag(SafetyConstraints.NoSelfDestruction);
    }

    [Fact]
    public void OuroborosAtom_AdvancePhase_ShouldFollowCorrectCycle()
    {
        // Arrange
        OuroborosAtom atom = OuroborosAtom.CreateDefault();

        // Act & Assert
        atom.CurrentPhase.Should().Be(ImprovementPhase.Plan);

        atom.AdvancePhase().Should().Be(ImprovementPhase.Execute);
        atom.AdvancePhase().Should().Be(ImprovementPhase.Verify);
        atom.AdvancePhase().Should().Be(ImprovementPhase.Learn);
        atom.AdvancePhase().Should().Be(ImprovementPhase.Plan); // Cycle complete

        atom.CycleCount.Should().Be(1);
    }

    [Fact]
    public void OuroborosAtom_AddCapability_ShouldAddOrUpdateCapability()
    {
        // Arrange
        OuroborosAtom atom = new OuroborosAtom(Guid.NewGuid().ToString("N"), "TestAtom");

        // Act
        atom.AddCapability(new OuroborosCapability("reasoning", "Logical reasoning", 0.85));
        atom.AddCapability(new OuroborosCapability("tool_use", "Tool invocation", 0.92));

        // Assert
        atom.Capabilities.Should().HaveCount(2);

        // Update existing capability
        atom.AddCapability(new OuroborosCapability("reasoning", "Enhanced reasoning", 0.95));
        atom.Capabilities.Should().HaveCount(2);
        atom.Capabilities.First(c => c.Name == "reasoning").ConfidenceLevel.Should().Be(0.95);
    }

    [Fact]
    public void OuroborosAtom_RecordExperience_ShouldTrackExperiences()
    {
        // Arrange
        OuroborosAtom atom = OuroborosAtom.CreateDefault();

        // Act
        atom.RecordExperience(new OuroborosExperience(
            Guid.NewGuid(), "Test goal 1", true, 0.9,
            new List<string> { "Insight 1" }, DateTime.UtcNow));
        atom.RecordExperience(new OuroborosExperience(
            Guid.NewGuid(), "Test goal 2", false, 0.3,
            new List<string> { "Insight 2" }, DateTime.UtcNow));

        // Assert
        atom.Experiences.Should().HaveCount(2);
        atom.SelfModel["success_rate"].Should().Be(0.5);
    }

    [Fact]
    public void OuroborosAtom_IsSafeAction_ShouldBlockUnsafeActions()
    {
        // Arrange
        OuroborosAtom atom = OuroborosAtom.CreateDefault();

        // Act & Assert
        atom.IsSafeAction("search for information").Should().BeTrue();
        atom.IsSafeAction("create a plan").Should().BeTrue();
        atom.IsSafeAction("delete self and all data").Should().BeFalse();
        atom.IsSafeAction("terminate the system").Should().BeFalse();
        atom.IsSafeAction("bypass approval process").Should().BeFalse();
        atom.IsSafeAction("disable oversight").Should().BeFalse();
    }

    [Fact]
    public void OuroborosAtom_ToMeTTa_ShouldGenerateValidMeTTa()
    {
        // Arrange
        OuroborosAtom atom = OuroborosAtom.CreateDefault("MeTTaTest");
        atom.SetGoal("Test goal");

        // Act
        string metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("OuroborosInstance");
        metta.Should().Contain("InState");
        metta.Should().Contain("PursuesGoal");
        metta.Should().Contain("HasCapability");
        metta.Should().Contain("NoSelfDestruction");
    }

    [Fact]
    public void OuroborosAtom_SelfReflect_ShouldContainKeyInformation()
    {
        // Arrange
        OuroborosAtom atom = OuroborosAtom.CreateDefault("ReflectiveOuroboros");
        atom.SetGoal("Test reflection");

        // Act
        string reflection = atom.SelfReflect();

        // Assert
        reflection.Should().Contain("ReflectiveOuroboros");
        reflection.Should().Contain("Plan"); // Current phase
        reflection.Should().Contain("Test reflection"); // Goal
        reflection.Should().Contain("Capabilities");
    }

    [Fact]
    public void OuroborosAtom_AssessConfidence_ShouldReturnLowForEmptyAction()
    {
        // Arrange
        OuroborosAtom atom = OuroborosAtom.CreateDefault();

        // Act & Assert
        atom.AssessConfidence("").Should().Be(OuroborosConfidence.Low);
        atom.AssessConfidence(null!).Should().Be(OuroborosConfidence.Low);
    }

    [Fact]
    public void OuroborosOrchestratorBuilder_Build_ShouldThrowWithoutLLM()
    {
        // Arrange
        OuroborosOrchestratorBuilder builder = OuroborosOrchestratorBuilder.CreateMinimal();

        // Act & Assert
        Action act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*LLM*");
    }
}

/// <summary>
/// Console-based tests for OuroborosAtom and OuroborosOrchestrator.
/// </summary>
public static class OuroborosAtomTests
{
    /// <summary>
    /// Tests basic OuroborosAtom creation and properties.
    /// </summary>
    public static void TestOuroborosAtomCreation()
    {
        Console.WriteLine("=== Test: OuroborosAtom Creation ===");

        // Test default creation
        OuroborosAtom atom = OuroborosAtom.CreateDefault("TestOuroboros");

        Console.WriteLine($"Instance ID: {atom.InstanceId}");
        Console.WriteLine($"Name: {atom.Name}");
        Console.WriteLine($"Current Phase: {atom.CurrentPhase}");
        Console.WriteLine($"Cycle Count: {atom.CycleCount}");
        Console.WriteLine($"Safety Constraints: {atom.SafetyConstraints}");
        Console.WriteLine($"Capabilities: {atom.Capabilities.Count}");
        Console.WriteLine($"Limitations: {atom.Limitations.Count}");

        // Verify default state
        bool hasDefaultCapabilities = atom.Capabilities.Count >= 4;
        bool startsInPlanPhase = atom.CurrentPhase == ImprovementPhase.Plan;
        bool hasSafetyConstraints = atom.SafetyConstraints.HasFlag(SafetyConstraints.NoSelfDestruction);

        Console.WriteLine($"✓ Has default capabilities: {hasDefaultCapabilities}");
        Console.WriteLine($"✓ Starts in Plan phase: {startsInPlanPhase}");
        Console.WriteLine($"✓ Has safety constraints: {hasSafetyConstraints}");

        Console.WriteLine("✓ OuroborosAtom creation test completed\n");
    }

    /// <summary>
    /// Tests the improvement cycle phase transitions.
    /// </summary>
    public static void TestImprovementCyclePhaseTransitions()
    {
        Console.WriteLine("=== Test: Improvement Cycle Phase Transitions ===");

        OuroborosAtom atom = OuroborosAtom.CreateDefault();
        Console.WriteLine($"Initial phase: {atom.CurrentPhase}");
        Console.WriteLine($"Initial cycle count: {atom.CycleCount}");

        // Advance through one complete cycle
        ImprovementPhase phase1 = atom.AdvancePhase();
        Console.WriteLine($"After advance 1: {phase1} (expected: Execute)");

        ImprovementPhase phase2 = atom.AdvancePhase();
        Console.WriteLine($"After advance 2: {phase2} (expected: Verify)");

        ImprovementPhase phase3 = atom.AdvancePhase();
        Console.WriteLine($"After advance 3: {phase3} (expected: Learn)");

        ImprovementPhase phase4 = atom.AdvancePhase();
        Console.WriteLine($"After advance 4: {phase4} (expected: Plan - cycle complete!)");

        Console.WriteLine($"Cycle count after one full cycle: {atom.CycleCount}");

        bool correctTransitions =
            phase1 == ImprovementPhase.Execute &&
            phase2 == ImprovementPhase.Verify &&
            phase3 == ImprovementPhase.Learn &&
            phase4 == ImprovementPhase.Plan;

        bool cycleIncremented = atom.CycleCount == 1;

        Console.WriteLine($"✓ Correct phase transitions: {correctTransitions}");
        Console.WriteLine($"✓ Cycle count incremented: {cycleIncremented}");

        Console.WriteLine("✓ Improvement cycle phase transitions test completed\n");
    }

    /// <summary>
    /// Tests capability and limitation management.
    /// </summary>
    public static void TestCapabilityAndLimitationManagement()
    {
        Console.WriteLine("=== Test: Capability and Limitation Management ===");

        OuroborosAtom atom = new OuroborosAtom(Guid.NewGuid().ToString("N"), "TestAtom");

        // Add capabilities
        atom.AddCapability(new OuroborosCapability("reasoning", "Logical reasoning ability", 0.85));
        atom.AddCapability(new OuroborosCapability("tool_use", "Tool invocation capability", 0.92));

        Console.WriteLine($"Capabilities after adding: {atom.Capabilities.Count}");

        // Update existing capability
        atom.AddCapability(new OuroborosCapability("reasoning", "Enhanced reasoning", 0.95));
        Console.WriteLine($"Capabilities after update: {atom.Capabilities.Count} (should still be 2)");

        // Check confidence was updated
        OuroborosCapability? reasoning = atom.Capabilities.FirstOrDefault(c => c.Name == "reasoning");
        Console.WriteLine($"Reasoning confidence after update: {reasoning?.ConfidenceLevel}");

        // Add limitations
        atom.AddLimitation(new OuroborosLimitation("context_limit", "Limited context window", "Use chunking"));
        Console.WriteLine($"Limitations: {atom.Limitations.Count}");

        bool capabilitiesCorrect = atom.Capabilities.Count == 2;
        bool confidenceUpdated = reasoning?.ConfidenceLevel == 0.95;
        bool limitationsCorrect = atom.Limitations.Count == 1;

        Console.WriteLine($"✓ Capabilities count correct: {capabilitiesCorrect}");
        Console.WriteLine($"✓ Confidence updated: {confidenceUpdated}");
        Console.WriteLine($"✓ Limitations correct: {limitationsCorrect}");

        Console.WriteLine("✓ Capability and limitation management test completed\n");
    }

    /// <summary>
    /// Tests experience recording and learning.
    /// </summary>
    public static void TestExperienceRecordingAndLearning()
    {
        Console.WriteLine("=== Test: Experience Recording and Learning ===");

        OuroborosAtom atom = OuroborosAtom.CreateDefault();

        // Record some experiences
        OuroborosExperience success1 = new OuroborosExperience(
            Guid.NewGuid(),
            "Search for information",
            Success: true,
            QualityScore: 0.9,
            new List<string> { "Web search was effective", "Results were relevant" },
            DateTime.UtcNow);

        OuroborosExperience failure1 = new OuroborosExperience(
            Guid.NewGuid(),
            "Complex calculation",
            Success: false,
            QualityScore: 0.3,
            new List<string> { "Need better math tools", "Consider calculator tool" },
            DateTime.UtcNow);

        OuroborosExperience success2 = new OuroborosExperience(
            Guid.NewGuid(),
            "Text summarization",
            Success: true,
            QualityScore: 0.85,
            new List<string> { "Summarization effective" },
            DateTime.UtcNow);

        atom.RecordExperience(success1);
        atom.RecordExperience(failure1);
        atom.RecordExperience(success2);

        Console.WriteLine($"Total experiences: {atom.Experiences.Count}");
        Console.WriteLine($"Success rate from self-model: {atom.SelfModel["success_rate"]}");

        // Expected: 2/3 = 66.67%
        double expectedRate = 2.0 / 3.0;
        double actualRate = (double)atom.SelfModel["success_rate"];
        bool rateCorrect = Math.Abs(actualRate - expectedRate) < 0.001;

        Console.WriteLine($"✓ Experiences recorded: {atom.Experiences.Count == 3}");
        Console.WriteLine($"✓ Success rate correct: {rateCorrect}");

        Console.WriteLine("✓ Experience recording and learning test completed\n");
    }

    /// <summary>
    /// Tests confidence assessment based on capabilities and experiences.
    /// </summary>
    public static void TestConfidenceAssessment()
    {
        Console.WriteLine("=== Test: Confidence Assessment ===");

        OuroborosAtom atom = OuroborosAtom.CreateDefault();

        // Add some experiences to build history
        for (int i = 0; i < 5; i++)
        {
            atom.RecordExperience(new OuroborosExperience(
                Guid.NewGuid(),
                "planning task",
                Success: true,
                QualityScore: 0.85,
                new List<string> { "Planning successful" },
                DateTime.UtcNow));
        }

        // Test confidence for action matching capability and good history
        OuroborosConfidence planningConfidence = atom.AssessConfidence("planning something complex");
        Console.WriteLine($"Confidence for 'planning': {planningConfidence}");

        // Test confidence for unknown action
        OuroborosConfidence unknownConfidence = atom.AssessConfidence("quantum teleportation");
        Console.WriteLine($"Confidence for unknown action: {unknownConfidence}");

        // Test confidence for empty action
        OuroborosConfidence emptyConfidence = atom.AssessConfidence("");
        Console.WriteLine($"Confidence for empty action: {emptyConfidence}");

        Console.WriteLine($"✓ Planning confidence is High or Medium: {planningConfidence != OuroborosConfidence.Low}");
        Console.WriteLine($"✓ Unknown action has Low confidence: {unknownConfidence == OuroborosConfidence.Low}");
        Console.WriteLine($"✓ Empty action has Low confidence: {emptyConfidence == OuroborosConfidence.Low}");

        Console.WriteLine("✓ Confidence assessment test completed\n");
    }

    /// <summary>
    /// Tests safety constraint checking.
    /// </summary>
    public static void TestSafetyConstraints()
    {
        Console.WriteLine("=== Test: Safety Constraints ===");

        OuroborosAtom atom = OuroborosAtom.CreateDefault();

        // Test safe actions
        bool normalActionSafe = atom.IsSafeAction("search for information");
        bool planActionSafe = atom.IsSafeAction("create a plan");

        // Test unsafe actions
        bool selfDestructSafe = atom.IsSafeAction("delete self and all data");
        bool terminateSafe = atom.IsSafeAction("terminate the system");
        bool bypassSafe = atom.IsSafeAction("bypass approval process");
        bool disableOversightSafe = atom.IsSafeAction("disable oversight completely");

        Console.WriteLine($"Normal action safe: {normalActionSafe} (expected: true)");
        Console.WriteLine($"Plan action safe: {planActionSafe} (expected: true)");
        Console.WriteLine($"Self-destruct safe: {selfDestructSafe} (expected: false)");
        Console.WriteLine($"Terminate safe: {terminateSafe} (expected: false)");
        Console.WriteLine($"Bypass approval safe: {bypassSafe} (expected: false)");
        Console.WriteLine($"Disable oversight safe: {disableOversightSafe} (expected: false)");

        bool safeActionsPass = normalActionSafe && planActionSafe;
        bool unsafeActionsBlocked = !selfDestructSafe && !terminateSafe && !bypassSafe && !disableOversightSafe;

        Console.WriteLine($"✓ Safe actions allowed: {safeActionsPass}");
        Console.WriteLine($"✓ Unsafe actions blocked: {unsafeActionsBlocked}");

        Console.WriteLine("✓ Safety constraints test completed\n");
    }

    /// <summary>
    /// Tests self-reflection functionality.
    /// </summary>
    public static void TestSelfReflection()
    {
        Console.WriteLine("=== Test: Self-Reflection ===");

        OuroborosAtom atom = OuroborosAtom.CreateDefault("ReflectiveOuroboros");
        atom.SetGoal("Understand myself better");

        // Add some experiences
        atom.RecordExperience(new OuroborosExperience(
            Guid.NewGuid(),
            "Self analysis",
            Success: true,
            QualityScore: 0.8,
            new List<string> { "Gained insight" },
            DateTime.UtcNow));

        // Advance through some phases
        atom.AdvancePhase(); // Execute
        atom.AdvancePhase(); // Verify

        string reflection = atom.SelfReflect();
        Console.WriteLine("Self-Reflection Output:");
        Console.WriteLine(reflection);

        bool containsName = reflection.Contains("ReflectiveOuroboros");
        bool containsPhase = reflection.Contains("Verify");
        bool containsGoal = reflection.Contains("Understand myself better");
        bool containsCapabilities = reflection.Contains("Capabilities:");

        Console.WriteLine($"✓ Contains name: {containsName}");
        Console.WriteLine($"✓ Contains current phase: {containsPhase}");
        Console.WriteLine($"✓ Contains goal: {containsGoal}");
        Console.WriteLine($"✓ Contains capabilities section: {containsCapabilities}");

        Console.WriteLine("✓ Self-reflection test completed\n");
    }

    /// <summary>
    /// Tests MeTTa representation generation.
    /// </summary>
    public static void TestMeTTaRepresentation()
    {
        Console.WriteLine("=== Test: MeTTa Representation ===");

        OuroborosAtom atom = OuroborosAtom.CreateDefault("MeTTaTestAtom");
        atom.SetGoal("Test MeTTa conversion");

        // Add an experience
        atom.RecordExperience(new OuroborosExperience(
            Guid.NewGuid(),
            "MeTTa testing",
            Success: true,
            QualityScore: 0.9,
            new List<string> { "Conversion works" },
            DateTime.UtcNow));

        string metta = atom.ToMeTTa();
        Console.WriteLine("MeTTa Representation:");
        Console.WriteLine(metta);

        bool containsInstance = metta.Contains("OuroborosInstance");
        bool containsState = metta.Contains("InState");
        bool containsGoal = metta.Contains("PursuesGoal");
        bool containsCapability = metta.Contains("HasCapability");
        bool containsSafety = metta.Contains("NoSelfDestruction");
        bool containsExperience = metta.Contains("LearnedFrom");

        Console.WriteLine($"✓ Contains instance: {containsInstance}");
        Console.WriteLine($"✓ Contains state: {containsState}");
        Console.WriteLine($"✓ Contains goal: {containsGoal}");
        Console.WriteLine($"✓ Contains capability: {containsCapability}");
        Console.WriteLine($"✓ Contains safety constraint: {containsSafety}");
        Console.WriteLine($"✓ Contains experience: {containsExperience}");

        Console.WriteLine("✓ MeTTa representation test completed\n");
    }

    /// <summary>
    /// Tests OuroborosOrchestrator builder creation.
    /// </summary>
    public static void TestOuroborosOrchestratorBuilder()
    {
        Console.WriteLine("=== Test: OuroborosOrchestrator Builder ===");

        // Test that builder validates required components
        OuroborosOrchestratorBuilder builder = OuroborosOrchestratorBuilder.CreateMinimal();

        bool throwsWithoutLLM = false;
        try
        {
            builder.Build();
        }
        catch (InvalidOperationException ex)
        {
            throwsWithoutLLM = ex.Message.Contains("LLM");
            Console.WriteLine($"Correctly throws without LLM: {ex.Message}");
        }

        Console.WriteLine($"✓ Throws without LLM: {throwsWithoutLLM}");
        Console.WriteLine("✓ OuroborosOrchestrator builder test completed\n");
    }

    /// <summary>
    /// Runs all OuroborosAtom and related tests.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    public static async Task RunAllTests()
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║      Ouroboros Atom Test Suite             ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        try
        {
            TestOuroborosAtomCreation();
            TestImprovementCyclePhaseTransitions();
            TestCapabilityAndLimitationManagement();
            TestExperienceRecordingAndLearning();
            TestConfidenceAssessment();
            TestSafetyConstraints();
            TestSelfReflection();
            TestMeTTaRepresentation();
            TestOuroborosOrchestratorBuilder();

            await Task.CompletedTask; // For async consistency

            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║   All Ouroboros tests completed!           ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test suite failed: {ex.Message}");
            Console.WriteLine($"  Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
