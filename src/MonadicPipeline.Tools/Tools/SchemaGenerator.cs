using System.Reflection;
using System.Text.Json.Serialization;

namespace LangChainPipeline.Tools;

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
                    description = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? string.Empty
                }
            ),
            required = properties
                .Where(p => !IsNullable(p.PropertyType))
                .Select(p => p.Name)
                .ToArray()
        };

        return ToolJson.Serialize(schema);
    }

    private static string MapType(Type type)
    {
        if (type == typeof(string))
            return "string";

        if (type == typeof(int) || type == typeof(long))
            return "integer";

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            return "number";

        if (type == typeof(bool))
            return "boolean";

        if (type.IsArray || (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string)))
            return "array";

        return "object";
    }

    private static bool IsNullable(Type type)
    {
        if (!type.IsValueType)
            return true;

        return Nullable.GetUnderlyingType(type) != null;
    }
}
