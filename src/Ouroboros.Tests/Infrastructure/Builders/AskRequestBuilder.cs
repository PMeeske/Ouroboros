// <copyright file="AskRequestBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.WebApi.Models;

namespace Ouroboros.Tests.Infrastructure.Builders;

/// <summary>
/// Builder for creating AskRequest test data with fluent API.
/// </summary>
public class AskRequestBuilder
{
    private string _question = "What is artificial intelligence?";
    private bool _useRag = false;
    private string? _sourcePath = null;
    private string? _model = "llama3";
    private bool _agent = false;
    private float? _temperature = null;
    private int? _maxTokens = null;
    private string? _endpoint = null;
    private string? _apiKey = null;

    /// <summary>
    /// Sets the question.
    /// </summary>
    public AskRequestBuilder WithQuestion(string question)
    {
        _question = question;
        return this;
    }

    /// <summary>
    /// Enables RAG mode.
    /// </summary>
    public AskRequestBuilder WithRag(string? sourcePath = null)
    {
        _useRag = true;
        _sourcePath = sourcePath;
        return this;
    }

    /// <summary>
    /// Sets the model name.
    /// </summary>
    public AskRequestBuilder WithModel(string model)
    {
        _model = model;
        return this;
    }

    /// <summary>
    /// Enables agent mode.
    /// </summary>
    public AskRequestBuilder WithAgent()
    {
        _agent = true;
        return this;
    }

    /// <summary>
    /// Sets the temperature.
    /// </summary>
    public AskRequestBuilder WithTemperature(float temperature)
    {
        _temperature = temperature;
        return this;
    }

    /// <summary>
    /// Sets the max tokens.
    /// </summary>
    public AskRequestBuilder WithMaxTokens(int maxTokens)
    {
        _maxTokens = maxTokens;
        return this;
    }

    /// <summary>
    /// Sets the remote endpoint.
    /// </summary>
    public AskRequestBuilder WithEndpoint(string endpoint, string? apiKey = null)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Builds the AskRequest instance.
    /// </summary>
    public AskRequest Build() => new AskRequest
    {
        Question = _question,
        UseRag = _useRag,
        SourcePath = _sourcePath,
        Model = _model,
        Agent = _agent,
        Temperature = _temperature,
        MaxTokens = _maxTokens,
        Endpoint = _endpoint,
        ApiKey = _apiKey,
    };

    /// <summary>
    /// Implicit conversion to AskRequest.
    /// </summary>
    public static implicit operator AskRequest(AskRequestBuilder builder) => builder.Build();
}
