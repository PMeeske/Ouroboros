// <copyright file="SchemaGeneratorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

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

    #region Type Mapping Tests

    private sealed class StringPropertyType
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void GenerateSchema_WithStringProperty_MapsToString()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(StringPropertyType));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement properties = document.RootElement.GetProperty("properties");
        properties.GetProperty(nameof(StringPropertyType.Value)).GetProperty("type").GetString().Should().Be("string");
    }

    private sealed class IntegerTypes
    {
        public int IntValue { get; set; }

        public long LongValue { get; set; }

        public int? NullableInt { get; set; }
    }

    [Fact]
    public void GenerateSchema_WithIntegerProperties_MapsToInteger()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(IntegerTypes));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement properties = document.RootElement.GetProperty("properties");
        properties.GetProperty(nameof(IntegerTypes.IntValue)).GetProperty("type").GetString().Should().Be("integer");
        properties.GetProperty(nameof(IntegerTypes.LongValue)).GetProperty("type").GetString().Should().Be("integer");
        properties.GetProperty(nameof(IntegerTypes.NullableInt)).GetProperty("type").GetString().Should().Be("integer");
    }

    private sealed class NumericTypes
    {
        public float FloatValue { get; set; }

        public double DoubleValue { get; set; }

        public decimal DecimalValue { get; set; }

        public double? NullableDouble { get; set; }
    }

    [Fact]
    public void GenerateSchema_WithNumericProperties_MapsToNumber()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(NumericTypes));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement properties = document.RootElement.GetProperty("properties");
        properties.GetProperty(nameof(NumericTypes.FloatValue)).GetProperty("type").GetString().Should().Be("number");
        properties.GetProperty(nameof(NumericTypes.DoubleValue)).GetProperty("type").GetString().Should().Be("number");
        properties.GetProperty(nameof(NumericTypes.DecimalValue)).GetProperty("type").GetString().Should().Be("number");
        properties.GetProperty(nameof(NumericTypes.NullableDouble)).GetProperty("type").GetString().Should().Be("number");
    }

    private sealed class BooleanType
    {
        public bool IsActive { get; set; }

        public bool? OptionalFlag { get; set; }
    }

    [Fact]
    public void GenerateSchema_WithBooleanProperty_MapsToBoolean()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(BooleanType));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement properties = document.RootElement.GetProperty("properties");
        properties.GetProperty(nameof(BooleanType.IsActive)).GetProperty("type").GetString().Should().Be("boolean");
        properties.GetProperty(nameof(BooleanType.OptionalFlag)).GetProperty("type").GetString().Should().Be("boolean");
    }

    private sealed class ArrayTypes
    {
        public string[] StringArray { get; set; } = [];

        public int[] IntArray { get; set; } = [];

        public List<string> StringList { get; set; } = [];
    }

    [Fact]
    public void GenerateSchema_WithArrayProperties_MapsToArray()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(ArrayTypes));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement properties = document.RootElement.GetProperty("properties");
        properties.GetProperty(nameof(ArrayTypes.StringArray)).GetProperty("type").GetString().Should().Be("array");
        properties.GetProperty(nameof(ArrayTypes.IntArray)).GetProperty("type").GetString().Should().Be("array");
        properties.GetProperty(nameof(ArrayTypes.StringList)).GetProperty("type").GetString().Should().Be("array");
    }

    private sealed class NestedType
    {
        public string Name { get; set; } = string.Empty;

        public InnerType Inner { get; set; } = new();
    }

    private sealed class InnerType
    {
        public int Value { get; set; }
    }

    [Fact]
    public void GenerateSchema_WithNestedObject_MapsToObject()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(NestedType));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement properties = document.RootElement.GetProperty("properties");
        properties.GetProperty(nameof(NestedType.Inner)).GetProperty("type").GetString().Should().Be("object");
    }

    #endregion

    #region Required Field Tests

    private sealed class NullableFields
    {
        public string? NullableString { get; set; }

        public int? NullableInt { get; set; }
    }

    [Fact]
    public void GenerateSchema_WithNullableValueType_MarksAsOptional()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(NullableFields));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        string[] required = document.RootElement.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToArray();

        required.Should().NotContain(nameof(NullableFields.NullableInt));
    }

    private sealed class RequiredFields
    {
        public string RequiredString { get; set; } = string.Empty;

        public int RequiredInt { get; set; }
    }

    [Fact]
    public void GenerateSchema_WithNonNullableFields_MarksAsRequired()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(RequiredFields));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        string[] required = document.RootElement.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToArray();

        required.Should().Contain(nameof(RequiredFields.RequiredString));
        required.Should().Contain(nameof(RequiredFields.RequiredInt));
    }

    #endregion

    #region Edge Cases

    private sealed class EmptyType
    {
    }

    [Fact]
    public void GenerateSchema_WithNoProperties_ProducesValidSchema()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(EmptyType));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("type").GetString().Should().Be("object");
        document.RootElement.GetProperty("properties").EnumerateObject().Should().BeEmpty();
    }

    private sealed class MultipleJsonPropertyNames
    {
        [JsonPropertyName("custom_name")]
        public string OriginalName { get; set; } = string.Empty;

        [JsonPropertyName("another_custom")]
        public int AnotherProperty { get; set; }
    }

    [Fact]
    public void GenerateSchema_WithJsonPropertyNameAttribute_UsesCustomName()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(MultipleJsonPropertyNames));

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement properties = document.RootElement.GetProperty("properties");

        // Schema should include the original property name
        properties.GetProperty(nameof(MultipleJsonPropertyNames.OriginalName)).Should().NotBeNull();

        // Description should contain the JSON property name
        string desc = properties.GetProperty(nameof(MultipleJsonPropertyNames.OriginalName))
            .GetProperty("description").GetString()!;
        desc.Should().Be("custom_name");
    }

    #endregion

    #region Schema Structure Tests

    [Fact]
    public void GenerateSchema_ProducesValidJsonSchema()
    {
        // Arrange
        var type = typeof(ComplexArgs);

        // Act
        string json = SchemaGenerator.GenerateSchema(type);

        // Assert
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        // Validate basic schema structure
        root.TryGetProperty("type", out _).Should().BeTrue();
        root.TryGetProperty("properties", out _).Should().BeTrue();
        root.TryGetProperty("required", out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateSchema_ProducesWellFormedJson()
    {
        // Act
        string json = SchemaGenerator.GenerateSchema(typeof(ComplexArgs));

        // Assert - Should not throw
        Action act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }

    #endregion
}
