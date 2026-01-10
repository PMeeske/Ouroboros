// <copyright file="OuroborosConfiguration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Unified configuration for the entire Ouroboros system.
/// Supports loading from appsettings.json and environment variables.
/// </summary>
public sealed class OuroborosConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Ouroboros";

    /// <summary>Gets or sets episodic memory configuration.</summary>
    public EpisodicMemoryConfig EpisodicMemory { get; set; } = new();

    /// <summary>Gets or sets adapter learning configuration.</summary>
    public AdapterLearningConfig AdapterLearning { get; set; } = new();

    /// <summary>Gets or sets MeTTa reasoning configuration.</summary>
    public MeTTaConfig MeTTa { get; set; } = new();

    /// <summary>Gets or sets hierarchical planning configuration.</summary>
    public PlanningConfig Planning { get; set; } = new();

    /// <summary>Gets or sets reflection engine configuration.</summary>
    public ReflectionConfig Reflection { get; set; } = new();

    /// <summary>Gets or sets program synthesis configuration.</summary>
    public SynthesisConfig Synthesis { get; set; } = new();

    /// <summary>Gets or sets world model configuration.</summary>
    public WorldModelConfig WorldModel { get; set; } = new();

    /// <summary>Gets or sets multi-agent configuration.</summary>
    public MultiAgentConfig MultiAgent { get; set; } = new();

    /// <summary>Gets or sets causal reasoning configuration.</summary>
    public CausalConfig Causal { get; set; } = new();

    /// <summary>Gets or sets meta-learning configuration.</summary>
    public MetaLearningConfig MetaLearning { get; set; } = new();

    /// <summary>Gets or sets embodied simulation configuration.</summary>
    public EmbodiedConfig Embodied { get; set; } = new();

    /// <summary>Gets or sets consciousness configuration.</summary>
    public ConsciousnessConfig Consciousness { get; set; } = new();

    /// <summary>Gets or sets benchmark configuration.</summary>
    public BenchmarkConfig Benchmarks { get; set; } = new();

    /// <summary>Gets or sets cognitive loop configuration.</summary>
    public CognitiveLoopSettings CognitiveLoop { get; set; } = new();

    /// <summary>Gets or sets which features are enabled.</summary>
    public FeaturesConfig Features { get; set; } = new();

    /// <summary>
    /// Loads configuration from IConfiguration.
    /// </summary>
    public static OuroborosConfiguration Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        
        var config = new OuroborosConfiguration();
        configuration.GetSection(SectionName).Bind(config);
        return config;
    }

    /// <summary>
    /// Validates the configuration and returns validation errors if any.
    /// </summary>
    public List<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        Validator.TryValidateObject(this, context, results, true);

        // Validate nested configurations
        ValidateNested(EpisodicMemory, "EpisodicMemory", results);
        ValidateNested(AdapterLearning, "AdapterLearning", results);
        ValidateNested(MeTTa, "MeTTa", results);
        ValidateNested(Planning, "Planning", results);
        ValidateNested(Reflection, "Reflection", results);
        ValidateNested(Synthesis, "Synthesis", results);
        ValidateNested(WorldModel, "WorldModel", results);
        ValidateNested(MultiAgent, "MultiAgent", results);
        ValidateNested(Causal, "Causal", results);
        ValidateNested(MetaLearning, "MetaLearning", results);
        ValidateNested(Embodied, "Embodied", results);
        ValidateNested(Consciousness, "Consciousness", results);
        ValidateNested(Benchmarks, "Benchmarks", results);
        ValidateNested(CognitiveLoop, "CognitiveLoop", results);

        return results;
    }

    private static void ValidateNested(object obj, string memberName, List<ValidationResult> results)
    {
        var context = new ValidationContext(obj) { MemberName = memberName };
        var nestedResults = new List<ValidationResult>();
        Validator.TryValidateObject(obj, context, nestedResults, true);
        
        foreach (var result in nestedResults)
        {
            results.Add(new ValidationResult(
                $"{memberName}.{result.ErrorMessage}",
                result.MemberNames.Select(m => $"{memberName}.{m}")));
        }
    }
}

/// <summary>Episodic memory configuration.</summary>
public sealed class EpisodicMemoryConfig
{
    /// <summary>Gets or sets vector store connection string.</summary>
    [Required]
    [Url]
    public string VectorStoreConnectionString { get; set; } = "http://localhost:6333";

    /// <summary>Gets or sets maximum memory size.</summary>
    [Range(100, 1000000)]
    public int MaxMemorySize { get; set; } = 10000;

    /// <summary>Gets or sets consolidation interval in hours.</summary>
    [Range(1, 168)]
    public int ConsolidationIntervalHours { get; set; } = 24;

    /// <summary>Gets or sets whether auto-consolidation is enabled.</summary>
    public bool EnableAutoConsolidation { get; set; } = true;
}

/// <summary>Adapter learning configuration.</summary>
public sealed class AdapterLearningConfig
{
    /// <summary>Gets or sets rank dimension.</summary>
    [Range(1, 64)]
    public int RankDimension { get; set; } = 8;

    /// <summary>Gets or sets learning rate.</summary>
    [Range(0.0001, 0.1)]
    public double LearningRate { get; set; } = 0.001;

    /// <summary>Gets or sets maximum adapters.</summary>
    [Range(1, 100)]
    public int MaxAdapters { get; set; } = 10;

    /// <summary>Gets or sets whether pruning is enabled.</summary>
    public bool EnablePruning { get; set; } = true;
}

/// <summary>MeTTa reasoning configuration.</summary>
public sealed class MeTTaConfig
{
    /// <summary>Gets or sets MeTTa executable path.</summary>
    [Required]
    public string ExecutablePath { get; set; } = "./metta";

    /// <summary>Gets or sets maximum inference steps.</summary>
    [Range(10, 10000)]
    public int MaxInferenceSteps { get; set; } = 100;

    /// <summary>Gets or sets whether type checking is enabled.</summary>
    public bool EnableTypeChecking { get; set; } = true;

    /// <summary>Gets or sets whether abduction is enabled.</summary>
    public bool EnableAbduction { get; set; } = true;
}

/// <summary>Planning configuration.</summary>
public sealed class PlanningConfig
{
    /// <summary>Gets or sets maximum planning depth.</summary>
    [Range(1, 100)]
    public int MaxPlanningDepth { get; set; } = 10;

    /// <summary>Gets or sets maximum planning time in seconds.</summary>
    [Range(1, 300)]
    public int MaxPlanningTimeSeconds { get; set; } = 30;

    /// <summary>Gets or sets whether HTN is enabled.</summary>
    public bool EnableHTN { get; set; } = true;

    /// <summary>Gets or sets whether temporal planning is enabled.</summary>
    public bool EnableTemporalPlanning { get; set; } = true;
}

/// <summary>Reflection configuration.</summary>
public sealed class ReflectionConfig
{
    /// <summary>Gets or sets analysis window in hours.</summary>
    [Range(1, 168)]
    public int AnalysisWindowHours { get; set; } = 24;

    /// <summary>Gets or sets minimum episodes for analysis.</summary>
    [Range(1, 1000)]
    public int MinEpisodesForAnalysis { get; set; } = 10;

    /// <summary>Gets or sets whether auto-improvement is enabled.</summary>
    public bool EnableAutoImprovement { get; set; } = true;
}

/// <summary>Synthesis configuration.</summary>
public sealed class SynthesisConfig
{
    /// <summary>Gets or sets maximum synthesis time in seconds.</summary>
    [Range(10, 600)]
    public int MaxSynthesisTimeSeconds { get; set; } = 60;

    /// <summary>Gets or sets whether library learning is enabled.</summary>
    public bool EnableLibraryLearning { get; set; } = true;

    /// <summary>Gets or sets maximum program complexity.</summary>
    [Range(10, 10000)]
    public int MaxProgramComplexity { get; set; } = 100;
}

/// <summary>World model configuration.</summary>
public sealed class WorldModelConfig
{
    /// <summary>Gets or sets model architecture.</summary>
    [Required]
    public string ModelArchitecture { get; set; } = "transformer";

    /// <summary>Gets or sets maximum model complexity.</summary>
    [Range(1000, 10000000)]
    public int MaxModelComplexity { get; set; } = 1000000;

    /// <summary>Gets or sets whether imagination planning is enabled.</summary>
    public bool EnableImaginationPlanning { get; set; } = true;
}

/// <summary>Multi-agent configuration.</summary>
public sealed class MultiAgentConfig
{
    /// <summary>Gets or sets maximum agents.</summary>
    [Range(1, 100)]
    public int MaxAgents { get; set; } = 10;

    /// <summary>Gets or sets consensus protocol.</summary>
    [Required]
    public string ConsensusProtocol { get; set; } = "raft";

    /// <summary>Gets or sets whether knowledge sharing is enabled.</summary>
    public bool EnableKnowledgeSharing { get; set; } = true;
}

/// <summary>Causal reasoning configuration.</summary>
public sealed class CausalConfig
{
    /// <summary>Gets or sets discovery algorithm.</summary>
    [Required]
    public string DiscoveryAlgorithm { get; set; } = "PC";

    /// <summary>Gets or sets maximum causal complexity.</summary>
    [Range(10, 1000)]
    public int MaxCausalComplexity { get; set; } = 100;

    /// <summary>Gets or sets whether counterfactuals are enabled.</summary>
    public bool EnableCounterfactuals { get; set; } = true;
}

/// <summary>Meta-learning configuration.</summary>
public sealed class MetaLearningConfig
{
    /// <summary>Gets or sets algorithm name.</summary>
    [Required]
    public string Algorithm { get; set; } = "MAML";

    /// <summary>Gets or sets meta-learning steps.</summary>
    [Range(1, 100)]
    public int MetaLearningSteps { get; set; } = 5;

    /// <summary>Gets or sets meta-learning rate.</summary>
    [Range(0.0001, 0.1)]
    public double MetaLearningRate { get; set; } = 0.001;
}

/// <summary>Embodied simulation configuration.</summary>
public sealed class EmbodiedConfig
{
    /// <summary>Gets or sets environment name.</summary>
    [Required]
    public string Environment { get; set; } = "gym";

    /// <summary>Gets or sets whether physics simulation is enabled.</summary>
    public bool EnablePhysicsSimulation { get; set; } = true;

    /// <summary>Gets or sets maximum simulation steps.</summary>
    [Range(100, 100000)]
    public int MaxSimulationSteps { get; set; } = 1000;
}

/// <summary>Consciousness configuration.</summary>
public sealed class ConsciousnessConfig
{
    /// <summary>Gets or sets maximum workspace size.</summary>
    [Range(10, 1000)]
    public int MaxWorkspaceSize { get; set; } = 100;

    /// <summary>Gets or sets item lifetime in minutes.</summary>
    [Range(1, 1440)]
    public int ItemLifetimeMinutes { get; set; } = 60;

    /// <summary>Gets or sets whether metacognition is enabled.</summary>
    public bool EnableMetacognition { get; set; } = true;
}

/// <summary>Benchmark configuration.</summary>
public sealed class BenchmarkConfig
{
    /// <summary>Gets or sets enabled benchmarks.</summary>
    public List<string> EnabledBenchmarks { get; set; } = new();

    /// <summary>Gets or sets benchmark timeout in seconds.</summary>
    [Range(10, 3600)]
    public int BenchmarkTimeoutSeconds { get; set; } = 300;
}

/// <summary>Cognitive loop configuration settings.</summary>
public sealed class CognitiveLoopSettings
{
    /// <summary>Gets or sets cycle interval in milliseconds.</summary>
    [Range(100, 60000)]
    public int CycleIntervalMs { get; set; } = 1000;

    /// <summary>Gets or sets maximum concurrent goals.</summary>
    [Range(1, 100)]
    public int MaxConcurrentGoals { get; set; } = 5;

    /// <summary>Gets or sets whether autonomous learning is enabled.</summary>
    public bool EnableAutonomousLearning { get; set; } = true;
}

/// <summary>Feature flags configuration.</summary>
public sealed class FeaturesConfig
{
    /// <summary>Gets or sets whether episodic memory is enabled.</summary>
    public bool EnableEpisodicMemory { get; set; } = true;

    /// <summary>Gets or sets whether adapter learning is enabled.</summary>
    public bool EnableAdapterLearning { get; set; } = true;

    /// <summary>Gets or sets whether MeTTa reasoning is enabled.</summary>
    public bool EnableMeTTa { get; set; } = true;

    /// <summary>Gets or sets whether hierarchical planning is enabled.</summary>
    public bool EnablePlanning { get; set; } = true;

    /// <summary>Gets or sets whether reflection is enabled.</summary>
    public bool EnableReflection { get; set; } = true;

    /// <summary>Gets or sets whether program synthesis is enabled.</summary>
    public bool EnableSynthesis { get; set; } = false;

    /// <summary>Gets or sets whether world model is enabled.</summary>
    public bool EnableWorldModel { get; set; } = false;

    /// <summary>Gets or sets whether multi-agent is enabled.</summary>
    public bool EnableMultiAgent { get; set; } = false;

    /// <summary>Gets or sets whether causal reasoning is enabled.</summary>
    public bool EnableCausal { get; set; } = true;

    /// <summary>Gets or sets whether meta-learning is enabled.</summary>
    public bool EnableMetaLearning { get; set; } = false;

    /// <summary>Gets or sets whether embodied simulation is enabled.</summary>
    public bool EnableEmbodied { get; set; } = false;

    /// <summary>Gets or sets whether consciousness is enabled.</summary>
    public bool EnableConsciousness { get; set; } = true;

    /// <summary>Gets or sets whether benchmarks are enabled.</summary>
    public bool EnableBenchmarks { get; set; } = true;

    /// <summary>Gets or sets whether cognitive loop is enabled.</summary>
    public bool EnableCognitiveLoop { get; set; } = false;
}
