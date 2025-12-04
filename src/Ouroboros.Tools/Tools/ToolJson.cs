// <copyright file="ToolJson.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

using System.Text.Json;

/// <summary>
/// Provides JSON serialization utilities for tools.
/// </summary>
public static class ToolJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary>
    /// Deserializes JSON to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="JsonException">Thrown when JSON is invalid.</exception>
    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options)!;

    /// <summary>
    /// Serializes an object to JSON.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The JSON string representation.</returns>
    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, Options);
}
