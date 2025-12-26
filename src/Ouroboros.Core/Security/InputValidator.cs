// <copyright file="InputValidator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Security;

/// <summary>
/// Result of input validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether indicates whether the input is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the sanitized input value if validation succeeded.
    /// </summary>
    public string? SanitizedValue { get; init; }

    /// <summary>
    /// Gets list of validation errors if validation failed.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns></returns>
    public static ValidationResult Success(string sanitizedValue) =>
        new() { IsValid = true, SanitizedValue = sanitizedValue };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <returns></returns>
    public static ValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

/// <summary>
/// Input validator and sanitizer for protecting against injection attacks and malicious input.
/// </summary>
public class InputValidator
{
    private readonly ValidationOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="InputValidator"/> class.
    /// Initializes a new input validator with the specified options.
    /// </summary>
    public InputValidator(ValidationOptions? options = null)
    {
        this.options = options ?? ValidationOptions.Default;
    }

    /// <summary>
    /// Validates and sanitizes user input.
    /// </summary>
    /// <returns></returns>
    public ValidationResult ValidateAndSanitize(string input, ValidationContext context)
    {
        if (string.IsNullOrEmpty(input))
        {
            return context.AllowEmpty
                ? ValidationResult.Success(string.Empty)
                : ValidationResult.Failure("Input cannot be empty");
        }

        List<string> errors = new List<string>();

        // Check length
        if (input.Length > context.MaxLength)
        {
            errors.Add($"Input exceeds maximum length of {context.MaxLength} characters");
        }

        if (input.Length < context.MinLength)
        {
            errors.Add($"Input must be at least {context.MinLength} characters");
        }

        // Check for injection patterns if enabled
        if (this.options.CheckInjectionPatterns)
        {
            List<string> injectionErrors = this.CheckForInjectionPatterns(input);
            errors.AddRange(injectionErrors);
        }

        // Check for dangerous characters
        if (this.options.CheckDangerousCharacters)
        {
            List<string> charErrors = this.CheckForDangerousCharacters(input, context);
            errors.AddRange(charErrors);
        }

        if (errors.Any())
        {
            return ValidationResult.Failure(errors.ToArray());
        }

        // Sanitize the input
        string sanitized = this.SanitizeInput(input, context);

        return ValidationResult.Success(sanitized);
    }

    private List<string> CheckForInjectionPatterns(string input)
    {
        List<string> errors = new List<string>();
        string lowerInput = input.ToLowerInvariant();

        // SQL injection patterns
        string[] sqlPatterns =
        {
            "'; drop", "'; delete", "'; update", "'; insert",
            "union select", "exec(", "execute(",
            "' or '", "\" or \"", "or 1=1", "or '1'='1",
            "--", "/*", "*/",
        };

        if (sqlPatterns.Any(pattern => lowerInput.Contains(pattern)))
        {
            errors.Add("Input contains potential SQL injection pattern");
        }

        // Command injection patterns
        string[] commandPatterns =
        {
            "&&", "||", ";", "|", "`", "$(",
            "../", "..\\", "/etc/", "c:\\",
        };

        if (commandPatterns.Any(pattern => lowerInput.Contains(pattern.ToLowerInvariant())))
        {
            errors.Add("Input contains potential command injection pattern");
        }

        // Script injection patterns
        string[] scriptPatterns =
        {
            "<script", "javascript:", "onerror=", "onload=",
            "<iframe", "eval(", "expression(",
        };

        if (scriptPatterns.Any(pattern => lowerInput.Contains(pattern)))
        {
            errors.Add("Input contains potential script injection pattern");
        }

        return errors;
    }

    private List<string> CheckForDangerousCharacters(string input, ValidationContext context)
    {
        List<string> errors = new List<string>();

        // Check for null bytes
        if (input.Contains('\0'))
        {
            errors.Add("Input contains null bytes");
        }

        // Check for control characters (except allowed ones like newline, tab)
        List<char> controlChars = input.Where(c =>
            char.IsControl(c) &&
            c != '\n' && c != '\r' && c != '\t').ToList();

        if (controlChars.Any())
        {
            errors.Add($"Input contains {controlChars.Count} control character(s)");
        }

        // Check against custom blocked characters
        if (context.BlockedCharacters != null)
        {
            List<char> blockedFound = input.Where(c => context.BlockedCharacters.Contains(c)).ToList();
            if (blockedFound.Any())
            {
                errors.Add($"Input contains blocked character(s): {string.Join(", ", blockedFound.Distinct())}");
            }
        }

        return errors;
    }

    private string SanitizeInput(string input, ValidationContext context)
    {
        string result = input;

        // Trim whitespace if enabled
        if (context.TrimWhitespace)
        {
            result = result.Trim();
        }

        // Normalize line endings
        if (context.NormalizeLineEndings)
        {
            result = result.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        // Remove any remaining control characters (except newline, tab)
        if (this.options.RemoveControlCharacters)
        {
            result = new string(result.Where(c =>
                !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
        }

        // Escape HTML if needed
        if (context.EscapeHtml)
        {
            result = System.Net.WebUtility.HtmlEncode(result);
        }

        return result;
    }
}

/// <summary>
/// Validation context specifying rules for input validation.
/// </summary>
public class ValidationContext
{
    /// <summary>
    /// Gets or sets maximum allowed length of input.
    /// </summary>
    public int MaxLength { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets minimum required length of input.
    /// </summary>
    public int MinLength { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether allow empty input.
    /// </summary>
    public bool AllowEmpty { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether trim leading and trailing whitespace.
    /// </summary>
    public bool TrimWhitespace { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether normalize line endings to LF.
    /// </summary>
    public bool NormalizeLineEndings { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether escape HTML characters.
    /// </summary>
    public bool EscapeHtml { get; set; } = false;

    /// <summary>
    /// Gets or sets characters that are explicitly blocked.
    /// </summary>
    public HashSet<char>? BlockedCharacters { get; set; }

    /// <summary>
    /// Gets default validation context for general text input.
    /// </summary>
    public static ValidationContext Default => new();

    /// <summary>
    /// Gets strict validation context for sensitive operations.
    /// </summary>
    public static ValidationContext Strict => new()
    {
        MaxLength = 1000,
        EscapeHtml = true,
        BlockedCharacters = new HashSet<char> { '<', '>', '&', '"', '\'' },
    };

    /// <summary>
    /// Gets validation context for tool parameters.
    /// </summary>
    public static ValidationContext ToolParameter => new()
    {
        MaxLength = 5000,
        TrimWhitespace = true,
        NormalizeLineEndings = true,
    };
}

/// <summary>
/// Options for input validation.
/// </summary>
public class ValidationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether check for injection patterns (SQL, command, script).
    /// </summary>
    public bool CheckInjectionPatterns { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether check for dangerous characters.
    /// </summary>
    public bool CheckDangerousCharacters { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether remove control characters during sanitization.
    /// </summary>
    public bool RemoveControlCharacters { get; set; } = true;

    /// <summary>
    /// Gets default validation options.
    /// </summary>
    public static ValidationOptions Default => new();

    /// <summary>
    /// Gets lenient validation options (fewer checks).
    /// </summary>
    public static ValidationOptions Lenient => new()
    {
        CheckInjectionPatterns = false,
        CheckDangerousCharacters = true,
    };
}
