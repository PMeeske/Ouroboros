namespace LangChainPipeline.Core.Security;

/// <summary>
/// Result of input validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Indicates whether the input is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// The sanitized input value if validation succeeded.
    /// </summary>
    public string? SanitizedValue { get; init; }

    /// <summary>
    /// List of validation errors if validation failed.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success(string sanitizedValue) =>
        new() { IsValid = true, SanitizedValue = sanitizedValue };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

/// <summary>
/// Input validator and sanitizer for protecting against injection attacks and malicious input.
/// </summary>
public class InputValidator
{
    private readonly ValidationOptions _options;

    /// <summary>
    /// Initializes a new input validator with the specified options.
    /// </summary>
    public InputValidator(ValidationOptions? options = null)
    {
        _options = options ?? ValidationOptions.Default;
    }

    /// <summary>
    /// Validates and sanitizes user input.
    /// </summary>
    public ValidationResult ValidateAndSanitize(string input, ValidationContext context)
    {
        if (string.IsNullOrEmpty(input))
        {
            return context.AllowEmpty
                ? ValidationResult.Success(string.Empty)
                : ValidationResult.Failure("Input cannot be empty");
        }

        var errors = new List<string>();

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
        if (_options.CheckInjectionPatterns)
        {
            var injectionErrors = CheckForInjectionPatterns(input);
            errors.AddRange(injectionErrors);
        }

        // Check for dangerous characters
        if (_options.CheckDangerousCharacters)
        {
            var charErrors = CheckForDangerousCharacters(input, context);
            errors.AddRange(charErrors);
        }

        if (errors.Any())
        {
            return ValidationResult.Failure(errors.ToArray());
        }

        // Sanitize the input
        var sanitized = SanitizeInput(input, context);

        return ValidationResult.Success(sanitized);
    }

    private List<string> CheckForInjectionPatterns(string input)
    {
        var errors = new List<string>();
        var lowerInput = input.ToLowerInvariant();

        // SQL injection patterns
        string[] sqlPatterns =
        {
            "'; drop", "'; delete", "'; update", "'; insert",
            "union select", "exec(", "execute(",
            "' or '", "\" or \"", "or 1=1", "or '1'='1",
            "--", "/*", "*/"
        };

        if (sqlPatterns.Any(pattern => lowerInput.Contains(pattern)))
        {
            errors.Add("Input contains potential SQL injection pattern");
        }

        // Command injection patterns
        string[] commandPatterns =
        {
            "&&", "||", ";", "|", "`", "$(",
            "../", "..\\", "/etc/", "c:\\"
        };

        if (commandPatterns.Any(pattern => lowerInput.Contains(pattern.ToLowerInvariant())))
        {
            errors.Add("Input contains potential command injection pattern");
        }

        // Script injection patterns
        string[] scriptPatterns =
        {
            "<script", "javascript:", "onerror=", "onload=",
            "<iframe", "eval(", "expression("
        };

        if (scriptPatterns.Any(pattern => lowerInput.Contains(pattern)))
        {
            errors.Add("Input contains potential script injection pattern");
        }

        return errors;
    }

    private List<string> CheckForDangerousCharacters(string input, ValidationContext context)
    {
        var errors = new List<string>();

        // Check for null bytes
        if (input.Contains('\0'))
        {
            errors.Add("Input contains null bytes");
        }

        // Check for control characters (except allowed ones like newline, tab)
        var controlChars = input.Where(c =>
            char.IsControl(c) &&
            c != '\n' && c != '\r' && c != '\t').ToList();

        if (controlChars.Any())
        {
            errors.Add($"Input contains {controlChars.Count} control character(s)");
        }

        // Check against custom blocked characters
        if (context.BlockedCharacters != null)
        {
            var blockedFound = input.Where(c => context.BlockedCharacters.Contains(c)).ToList();
            if (blockedFound.Any())
            {
                errors.Add($"Input contains blocked character(s): {string.Join(", ", blockedFound.Distinct())}");
            }
        }

        return errors;
    }

    private string SanitizeInput(string input, ValidationContext context)
    {
        var result = input;

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
        if (_options.RemoveControlCharacters)
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
    /// Maximum allowed length of input.
    /// </summary>
    public int MaxLength { get; set; } = 10_000;

    /// <summary>
    /// Minimum required length of input.
    /// </summary>
    public int MinLength { get; set; } = 0;

    /// <summary>
    /// Allow empty input.
    /// </summary>
    public bool AllowEmpty { get; set; } = false;

    /// <summary>
    /// Trim leading and trailing whitespace.
    /// </summary>
    public bool TrimWhitespace { get; set; } = true;

    /// <summary>
    /// Normalize line endings to LF.
    /// </summary>
    public bool NormalizeLineEndings { get; set; } = true;

    /// <summary>
    /// Escape HTML characters.
    /// </summary>
    public bool EscapeHtml { get; set; } = false;

    /// <summary>
    /// Characters that are explicitly blocked.
    /// </summary>
    public HashSet<char>? BlockedCharacters { get; set; }

    /// <summary>
    /// Default validation context for general text input.
    /// </summary>
    public static ValidationContext Default => new();

    /// <summary>
    /// Strict validation context for sensitive operations.
    /// </summary>
    public static ValidationContext Strict => new()
    {
        MaxLength = 1000,
        EscapeHtml = true,
        BlockedCharacters = new HashSet<char> { '<', '>', '&', '"', '\'' }
    };

    /// <summary>
    /// Validation context for tool parameters.
    /// </summary>
    public static ValidationContext ToolParameter => new()
    {
        MaxLength = 5000,
        TrimWhitespace = true,
        NormalizeLineEndings = true
    };
}

/// <summary>
/// Options for input validation.
/// </summary>
public class ValidationOptions
{
    /// <summary>
    /// Check for injection patterns (SQL, command, script).
    /// </summary>
    public bool CheckInjectionPatterns { get; set; } = true;

    /// <summary>
    /// Check for dangerous characters.
    /// </summary>
    public bool CheckDangerousCharacters { get; set; } = true;

    /// <summary>
    /// Remove control characters during sanitization.
    /// </summary>
    public bool RemoveControlCharacters { get; set; } = true;

    /// <summary>
    /// Default validation options.
    /// </summary>
    public static ValidationOptions Default => new();

    /// <summary>
    /// Lenient validation options (fewer checks).
    /// </summary>
    public static ValidationOptions Lenient => new()
    {
        CheckInjectionPatterns = false,
        CheckDangerousCharacters = true
    };
}
