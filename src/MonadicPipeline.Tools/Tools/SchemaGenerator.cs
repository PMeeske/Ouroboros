// <copyright file="SchemaGenerator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

using System.Reflection;
using System.Text.Json.Serialization;

/// <summary>
/// Generates JSON schemas for types to support tool parameter validation.
/// </summary>
public static class SchemaGenerator
{
    /// <summary>
    /// Generates a JSON schema for the specified type.
    /// </summary>
    /// <param name="type">The type to generate a schema for.</param>
    /// <returns>A JSON schema string.</returns>
    public static string GenerateSchema(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var schema = new
        {
            type = "object",
            properties = properties.ToDictionary(
                p => p.Name,
                p => new
                {
                    type = MapType(p.PropertyType),
                    description = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? string.Empty,
                }),
            required = properties
                .Where(p => !IsNullableProperty(p))
                .Select(p => p.Name)
                .ToArray(),
        };

        return ToolJson.Serialize(schema);
    }

    private static string MapType(Type type)
    {
        // Handle nullable types
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
        {
            return "string";
        }

        if (underlyingType == typeof(int) || underlyingType == typeof(long))
        {
            return "integer";
        }

        if (underlyingType == typeof(float) || underlyingType == typeof(double) || underlyingType == typeof(decimal))
        {
            return "number";
        }

        if (underlyingType == typeof(bool))
        {
            return "boolean";
        }

        if (underlyingType.IsArray || (typeof(System.Collections.IEnumerable).IsAssignableFrom(underlyingType) && underlyingType != typeof(string)))
        {
            return "array";
        }

        return "object";
    }

    private static bool IsNullableProperty(PropertyInfo property)
    {
        Type type = property.PropertyType;
        
        // Check if it's a nullable value type (e.g., int?, double?)
        if (type.IsValueType)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        // For reference types, check nullability context
        // If the property has a default value or is an array, it's optional
        if (type.IsArray)
        {
            return true; // Arrays are optional by default
        }

        // Check if property has a default value initializer (approximation)
        // In practice, properties with = string.Empty or = [] are not truly nullable
        // but for JSON schema purposes, we want to mark them as required
        // We'll check the NullabilityInfo API if available
        var nullabilityInfo = new System.Reflection.NullabilityInfoContext().Create(property);
        return nullabilityInfo.WriteState == System.Reflection.NullabilityState.Nullable;
    }
}
