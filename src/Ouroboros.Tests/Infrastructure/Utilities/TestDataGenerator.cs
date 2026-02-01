// <copyright file="TestDataGenerator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Bogus;
using Ouroboros.WebApi.Models;

namespace Ouroboros.Tests.Infrastructure.Utilities;

/// <summary>
/// Generates realistic test data using Bogus library.
/// </summary>
public static class TestDataGenerator
{
    private static readonly string[] Models = new[] { "llama3", "deepseek-coder:33b", "mistral:7b", "qwen2.5:7b" };
    private static readonly string[] Topics = new[] { "AI", "Machine Learning", "Functional Programming", "Software Architecture", "DevOps" };

    /// <summary>
    /// Generates a random AskRequest with realistic data.
    /// </summary>
    public static AskRequest GenerateAskRequest()
    {
        var faker = new Faker<AskRequest>()
            .CustomInstantiator(f => new AskRequest
            {
                Question = f.Lorem.Sentence(10),
            })
            .RuleFor(r => r.Model, f => f.PickRandom(Models))
            .RuleFor(r => r.UseRag, f => f.Random.Bool(0.3f))
            .RuleFor(r => r.Agent, f => f.Random.Bool(0.2f))
            .RuleFor(r => r.Temperature, f => f.Random.Float(0, 1))
            .RuleFor(r => r.MaxTokens, f => f.Random.Int(100, 2000));

        return faker.Generate();
    }

    /// <summary>
    /// Generates a random PipelineRequest with realistic data.
    /// </summary>
    public static PipelineRequest GeneratePipelineRequest()
    {
        var faker = new Faker<PipelineRequest>()
            .CustomInstantiator(f => new PipelineRequest
            {
                Dsl = $"SetTopic('{f.PickRandom(Topics)}') | UseDraft | UseCritique",
            })
            .RuleFor(r => r.Model, f => f.PickRandom(Models))
            .RuleFor(r => r.Debug, f => f.Random.Bool(0.1f))
            .RuleFor(r => r.Temperature, f => f.Random.Float(0, 1))
            .RuleFor(r => r.MaxTokens, f => f.Random.Int(100, 2000));

        return faker.Generate();
    }

    /// <summary>
    /// Generates a realistic question string.
    /// </summary>
    public static string GenerateQuestion()
    {
        var faker = new Faker();
        return faker.PickRandom(
            "What is artificial intelligence?",
            "Explain functional programming concepts.",
            "How does event sourcing work?",
            "What are the benefits of CQRS?",
            "Describe the actor model.",
            "What is a monad in functional programming?",
            faker.Lorem.Sentence(10));
    }

    /// <summary>
    /// Generates a realistic DSL expression.
    /// </summary>
    public static string GenerateDsl()
    {
        var faker = new Faker();
        var topic = faker.PickRandom(Topics);
        var steps = faker.PickRandom(
            $"SetTopic('{topic}') | UseDraft",
            $"SetTopic('{topic}') | UseDraft | UseCritique",
            $"SetTopic('{topic}') | UseDraft | UseCritique | UseImprove",
            $"SetTopic('{topic}') | UseDraft | UseRefine");

        return steps;
    }
}
