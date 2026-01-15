// <copyright file="SchemaGeneratorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Tests for the SchemaGenerator utility.
/// </summary>
[Trait("Category", "Unit")]
public class SchemaGeneratorTests
{
    private sealed class ComplexArgs
    {
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("identifier")]
        public int Count { get; set; }

        public double? Optional { get; set; }

        public string[] Tags { get; set; } = [];
    }

    [Fact]
    public void GenerateSchema_WithNullType_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => SchemaGenerator.GenerateSchema(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GenerateSchema_WithComplexType_ProducesExpectedSchema()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(ComplexArgs));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        root.GetProperty("type").GetString().Should().Be("object");

        JsonElement properties = root.GetProperty("properties");
        properties.GetProperty(nameof(ComplexArgs.Name)).GetProperty("type").GetString().Should().Be("string");
        properties.GetProperty(nameof(ComplexArgs.Count)).GetProperty("type").GetString().Should().Be("integer");
        properties.GetProperty(nameof(ComplexArgs.Count)).GetProperty("description").GetString().Should().Be("identifier");
        properties.GetProperty(nameof(ComplexArgs.Optional)).GetProperty("type").GetString().Should().Be("number");
        properties.GetProperty(nameof(ComplexArgs.Tags)).GetProperty("type").GetString().Should().Be("array");

        string[] required = root.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToArray();
        required.Should().Contain(nameof(ComplexArgs.Name));
        required.Should().Contain(nameof(ComplexArgs.Count));
        required.Should().NotContain(nameof(ComplexArgs.Optional));
        required.Should().NotContain(nameof(ComplexArgs.Tags));
    }
}
