using LangChainPipeline.Core.Monads;

namespace LangChainPipeline.CLI.Utilities;

/// <summary>
/// Provides parsing utilities for CLI argument strings into typed configurations.
/// </summary>
public static class ConfigParser
{
    /// <summary>
    /// Parses a pipe-delimited argument string into a configuration object.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration object.</typeparam>
    /// <param name="args">The argument string to parse.</param>
    /// <param name="defaults">The default configuration values.</param>
    /// <param name="builder">A function that builds the configuration from the parsed dictionary and defaults.</param>
    /// <returns>A Result containing the parsed configuration or an error.</returns>
    public static Result<TConfig> Parse<TConfig>(
        string? args,
        TConfig defaults,
        Func<Dictionary<string, string>, TConfig, Result<TConfig>> builder)
    {
        try
        {
            string raw = ParseString(args);
            Dictionary<string, string> dict = ParseKeyValueArgs(raw);
            return builder(dict, defaults);
        }
        catch (Exception ex)
        {
            return Result<TConfig>.Failure($"Configuration parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses pipe-delimited key-value pairs into a dictionary.
    /// </summary>
    /// <param name="raw">The raw string containing key-value pairs.</param>
    /// <returns>A dictionary of key-value pairs.</returns>
    public static Dictionary<string, string> ParseKeyValueArgs(string? raw)
    {
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return map;
        }

        foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int idx = part.IndexOf('=');
            if (idx > 0)
            {
                string key = part[..idx].Trim();
                string value = part[(idx + 1)..].Trim();
                map[key] = value;
            }
            else
            {
                map[part.Trim()] = "true";
            }
        }

        return map;
    }

    /// <summary>
    /// Removes surrounding quotes from a string argument.
    /// </summary>
    /// <param name="arg">The argument string to clean.</param>
    /// <returns>The cleaned string without surrounding quotes.</returns>
    public static string ParseString(string? arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return string.Empty;
        }

        string trimmed = arg.Trim();
        
        if ((trimmed.StartsWith('\'') && trimmed.EndsWith('\'')) ||
            (trimmed.StartsWith('"') && trimmed.EndsWith('"')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    /// <summary>
    /// Parses a boolean value from various string representations.
    /// </summary>
    /// <param name="raw">The raw string value.</param>
    /// <param name="defaultValue">The default value if parsing fails or input is empty.</param>
    /// <returns>The parsed boolean value.</returns>
    public static bool ParseBool(string? raw, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (bool.TryParse(raw, out bool parsed))
        {
            return parsed;
        }

        if (int.TryParse(raw, out int numeric))
        {
            return numeric != 0;
        }

        return raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("y", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("on", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("enable", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("enabled", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the first non-empty string from the provided values.
    /// </summary>
    /// <param name="values">The values to check.</param>
    /// <returns>The first non-empty string, or null if all are empty.</returns>
    public static string? ChooseFirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(static v => !string.IsNullOrWhiteSpace(v));
}
