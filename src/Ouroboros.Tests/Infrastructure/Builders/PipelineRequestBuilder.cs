// <copyright file="PipelineRequestBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.WebApi.Models;

namespace Ouroboros.Tests.Infrastructure.Builders;

/// <summary>
/// Builder for creating PipelineRequest test data with fluent API.
/// </summary>
public class PipelineRequestBuilder
{
    private string _dsl = "SetTopic('AI') | UseDraft";
    private string? _model = "llama3";
    private bool _debug = false;
    private float? _temperature = null;
    private int? _maxTokens = null;
    private string? _endpoint = null;
    private string? _apiKey = null;

    /// <summary>
    /// Sets the DSL expression.
    /// </summary>
    public PipelineRequestBuilder WithDsl(string dsl)
    {
        _dsl = dsl;
        return this;
    }

    /// <summary>
    /// Sets the model name.
    /// </summary>
    public PipelineRequestBuilder WithModel(string model)
    {
        _model = model;
        return this;
    }

    /// <summary>
    /// Enables debug mode.
    /// </summary>
    public PipelineRequestBuilder WithDebug()
    {
        _debug = true;
        return this;
    }

    /// <summary>
    /// Sets the temperature.
    /// </summary>
    public PipelineRequestBuilder WithTemperature(float temperature)
    {
        _temperature = temperature;
        return this;
    }

    /// <summary>
    /// Sets the max tokens.
    /// </summary>
    public PipelineRequestBuilder WithMaxTokens(int maxTokens)
    {
        _maxTokens = maxTokens;
        return this;
    }

    /// <summary>
    /// Sets the remote endpoint.
    /// </summary>
    public PipelineRequestBuilder WithEndpoint(string endpoint, string? apiKey = null)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
        return this;
    }

    /// <summary>
    /// Builds the PipelineRequest instance.
    /// </summary>
    public PipelineRequest Build() => new PipelineRequest
    {
        Dsl = _dsl,
        Model = _model,
        Debug = _debug,
        Temperature = _temperature,
        MaxTokens = _maxTokens,
        Endpoint = _endpoint,
        ApiKey = _apiKey,
    };

    /// <summary>
    /// Implicit conversion to PipelineRequest.
    /// </summary>
    public static implicit operator PipelineRequest(PipelineRequestBuilder builder) => builder.Build();
}
