using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LangChainPipeline.Core.Steps;

/// <summary>
/// Custom exception thrown when a step execution fails.
/// Provides detailed context about the step failure including the step type,
/// input parameters, and underlying exception.
/// </summary>
[Serializable]
[SuppressMessage("Design", "CA1032:Implement standard exception constructors")]
public class StepExecutionException : Exception
{
    /// <summary>
    /// Gets the type of the step that failed.
    /// </summary>
    public Type StepType { get; }

    /// <summary>
    /// Gets the input value that was being processed when the failure occurred.
    /// This may be null if the input could not be captured or was not available.
    /// </summary>
    public object? InputValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StepExecutionException"/> class.
    /// </summary>
    /// <param name="stepType">The type of the step that failed.</param>
    /// <param name="inputValue">The input value being processed when the failure occurred.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StepExecutionException(Type stepType, object? inputValue, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StepType = stepType ?? throw new ArgumentNullException(nameof(stepType));
        InputValue = inputValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StepExecutionException"/> class
    /// with a default message format.
    /// </summary>
    /// <param name="stepType">The type of the step that failed.</param>
    /// <param name="inputValue">The input value being processed when the failure occurred.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StepExecutionException(Type stepType, object? inputValue, Exception? innerException = null)
        : this(stepType, inputValue, $"Step execution failed for step of type '{stepType.Name}' with input: {inputValue}", innerException)
    {
    }

    /// <summary>
    /// Gets a message that describes the current exception.
    /// </summary>
    public override string Message
    {
        get
        {
            var baseMessage = base.Message;
            return $"StepExecutionException in {StepType.Name}: {baseMessage}\nInput: {InputValue}";
        }
    }
}

/// <summary>
/// Interface representing a computation step that transforms input of type <typeparamref name="TIn"/>
/// to output of type <typeparamref name="TOut"/> with enhanced error handling capabilities.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides a more robust alternative to the <see cref="Step{TIn, TOut}"/> delegate
/// by incorporating explicit error handling through the <see cref="TryExecuteAsync"/> method.
/// </para>
/// <para>
/// The covariant type parameters (<c>out TOut</c>) allow for more flexible type relationships
/// when working with step compositions and hierarchies.
/// </para>
/// </remarks>
/// <typeparam name="TIn">The contravariant input type.</typeparam>
/// <typeparam name="TOut">The output type.</typeparam>
public interface IStep<in TIn, TOut>
{
    /// <summary>
    /// Attempts to execute the step asynchronously with enhanced error handling.
    /// </summary>
    /// <param name="input">The input value to process.</param>
    /// <returns>
    /// A <see cref="ValueTask"/> that completes with a <see cref="StepResult{TOut}"/> containing
    /// either the successful result or detailed error information.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides a non-throwing alternative to the standard execution pattern,
    /// allowing callers to handle errors without try-catch blocks. The method should not
    /// throw exceptions under normal operation - all errors should be captured in the
    /// returned <see cref="StepResult{TOut}"/>.
    /// </para>
    /// <para>
    /// Implementations should ensure thread safety and proper resource management.
    /// </para>
    /// </remarks>
    ValueTask<StepResult<TOut>> TryExecuteAsync(TIn input);

    /// <summary>
    /// Executes the step asynchronously with traditional exception throwing behavior.
    /// </summary>
    /// <param name="input">The input value to process.</param>
    /// <returns>A task that represents the asynchronous operation and contains the result.</returns>
    /// <exception cref="StepExecutionException">
    /// Thrown when the step execution fails. The exception contains detailed context
    /// about the failure including the step type and input value.
    /// </exception>
    /// <remarks>
    /// This method provides compatibility with existing code that expects exception-based
    /// error handling. For new code, consider using <see cref="TryExecuteAsync"/> for
    /// more granular error control.
    /// </remarks>
    async Task<TOut> ExecuteAsync(TIn input)
    {
        var result = await TryExecuteAsync(input).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return result.Value!;
        }

        throw new StepExecutionException(
            GetType(),
            input,
            result.ErrorMessage ?? "Step execution failed",
            result.Exception);
    }
}

/// <summary>
/// Represents the result of a step execution attempt, containing either a successful result
/// or detailed error information.
/// </summary>
/// <typeparam name="T">The type of the result value.</typeparam>
public readonly struct StepResult<T>
{
    private readonly T? _value;
    private readonly string? _errorMessage;
    private readonly Exception? _exception;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepResult{T}"/> struct representing a successful result.
    /// </summary>
    /// <param name="value">The successful result value.</param>
    public StepResult(T value)
    {
        _value = value;
        _errorMessage = null;
        _exception = null;
        IsSuccess = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StepResult{T}"/> struct representing a failed result.
    /// </summary>
    /// <param name="errorMessage">A descriptive error message.</param>
    /// <param name="exception">The exception that caused the failure, if any.</param>
    public StepResult(string errorMessage, Exception? exception = null)
    {
        _value = default;
        _errorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        _exception = exception;
        IsSuccess = false;
    }

    /// <summary>
    /// Gets a value indicating whether the step execution was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the result value if the execution was successful.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result represents a failure.</exception>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of a failed result.");

    /// <summary>
    /// Gets the error message if the execution failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result represents a success.</exception>
    public string ErrorMessage => !IsSuccess ? _errorMessage! : throw new InvalidOperationException("Cannot access ErrorMessage of a successful result.");

    /// <summary>
    /// Gets the exception that caused the failure, if any.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result represents a success.</exception>
    public Exception? Exception => !IsSuccess ? _exception : throw new InvalidOperationException("Cannot access Exception of a successful result.");

    /// <summary>
    /// Creates a successful <see cref="StepResult{T}"/> with the specified value.
    /// </summary>
    /// <param name="value">The successful result value.</param>
    /// <returns>A successful step result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StepResult<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed <see cref="StepResult{T}"/> with the specified error message.
    /// </summary>
    /// <param name="errorMessage">A descriptive error message.</param>
    /// <returns>A failed step result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StepResult<T> Failure(string errorMessage) => new(errorMessage);

    /// <summary>
    /// Creates a failed <see cref="StepResult{T}"/> with the specified error message and exception.
    /// </summary>
    /// <param name="errorMessage">A descriptive error message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed step result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StepResult<T> Failure(string errorMessage, Exception exception) => new(errorMessage, exception);

    /// <summary>
    /// Implicitly converts a value to a successful <see cref="StepResult{T}"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator StepResult<T>(T value) => new(value);

    /// <summary>
    /// Deconstructs the step result into its components.
    /// </summary>
    /// <param name="isSuccess">Output parameter indicating whether the result was successful.</param>
    /// <param name="value">Output parameter containing the value if successful.</param>
    /// <param name="errorMessage">Output parameter containing the error message if failed.</param>
    public void Deconstruct(out bool isSuccess, out T? value, out string? errorMessage)
    {
        isSuccess = IsSuccess;
        value = IsSuccess ? _value : default;
        errorMessage = !IsSuccess ? _errorMessage : null;
    }
}
