namespace LangChainPipeline.Core.Monads;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// Implements the Result monad for robust error handling.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public readonly struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;
    private readonly bool _isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents success.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents failure.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the success value (only valid when IsSuccess is true).
    /// </summary>
    public TValue Value => _isSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of a failed Result");

    /// <summary>
    /// Gets the error value (only valid when IsFailure is true).
    /// </summary>
    public TError Error => !_isSuccess ? _error! : throw new InvalidOperationException("Cannot access Error of a successful Result");

    private Result(TValue value)
    {
        _value = value;
        _error = default;
        _isSuccess = true;
    }

    private Result(TError error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
    }

    /// <summary>
    /// Creates a successful Result with the given value.
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <returns>A successful Result.</returns>
    public static Result<TValue, TError> Success(TValue value) => new(value);

    /// <summary>
    /// Creates a failed Result with the given error.
    /// </summary>
    /// <param name="error">The error value.</param>
    /// <returns>A failed Result.</returns>
    public static Result<TValue, TError> Failure(TError error) => new(error);

    /// <summary>
    /// Monadic bind operation. Applies a function that returns a Result to the wrapped value.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="func">Function to apply if this Result is successful.</param>
    /// <returns>The result of the function, or the original error if this Result failed.</returns>
    public Result<TResult, TError> Bind<TResult>(Func<TValue, Result<TResult, TError>> func)
    {
        return IsSuccess ? func(_value!) : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>
    /// Functor map operation. Transforms the wrapped value if the Result is successful.
    /// </summary>
    /// <typeparam name="TResult">The type of the result value.</typeparam>
    /// <param name="func">Function to apply to the wrapped value.</param>
    /// <returns>A Result containing the transformed value, or the original error.</returns>
    public Result<TResult, TError> Map<TResult>(Func<TValue, TResult> func)
    {
        return IsSuccess ? Result<TResult, TError>.Success(func(_value!)) : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>
    /// Transforms the error type while preserving the success value.
    /// </summary>
    /// <typeparam name="TNewError">The new error type.</typeparam>
    /// <param name="func">Function to transform the error.</param>
    /// <returns>A Result with the transformed error type.</returns>
    public Result<TValue, TNewError> MapError<TNewError>(Func<TError, TNewError> func)
    {
        return IsSuccess ? Result<TValue, TNewError>.Success(_value!) : Result<TValue, TNewError>.Failure(func(_error!));
    }

    /// <summary>
    /// Executes one of two functions based on whether the Result is successful or failed.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="onSuccess">Function to execute if Result is successful.</param>
    /// <param name="onFailure">Function to execute if Result failed.</param>
    /// <returns>The result of the appropriate function.</returns>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Executes one of two actions based on whether the Result is successful or failed.
    /// </summary>
    /// <param name="onSuccess">Action to execute if Result is successful.</param>
    /// <param name="onFailure">Action to execute if Result failed.</param>
    public void Match(Action<TValue> onSuccess, Action<TError> onFailure)
    {
        if (IsSuccess)
            onSuccess(_value!);
        else
            onFailure(_error!);
    }

    /// <summary>
    /// Returns the success value or a default value if the Result failed.
    /// </summary>
    /// <param name="defaultValue">The default value to return on failure.</param>
    /// <returns>The success value or the default value.</returns>
    public TValue GetValueOrDefault(TValue defaultValue)
    {
        return IsSuccess ? _value! : defaultValue;
    }

    /// <summary>
    /// Converts a Result to an Option, discarding error information.
    /// </summary>
    /// <returns>Some(value) if successful, None if failed.</returns>
    public Option<TValue> ToOption()
    {
        return IsSuccess ? Option<TValue>.Some(_value!) : Option<TValue>.None();
    }

    /// <summary>
    /// Implicit conversion from success value to Result.
    /// </summary>
    public static implicit operator Result<TValue, TError>(TValue value) => Success(value);

    /// <summary>
    /// Returns a string representation of the Result.
    /// </summary>
    public override string ToString()
    {
        return IsSuccess ? $"Success({_value})" : $"Failure({_error})";
    }

    /// <summary>
    /// Determines equality between two Results.
    /// </summary>
    public bool Equals(Result<TValue, TError> other)
    {
        if (IsSuccess != other.IsSuccess) return false;

        return IsSuccess
            ? EqualityComparer<TValue>.Default.Equals(_value, other._value)
            : EqualityComparer<TError>.Default.Equals(_error, other._error);
    }

    /// <summary>
    /// Determines equality with an object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Result<TValue, TError> other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this Result.
    /// </summary>
    public override int GetHashCode()
    {
        return IsSuccess
            ? HashCode.Combine(_isSuccess, _value)
            : HashCode.Combine(_isSuccess, _error);
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Result<TValue, TError> left, Result<TValue, TError> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Result<TValue, TError> left, Result<TValue, TError> right) => !left.Equals(right);
}

/// <summary>
/// Convenience type for Results with string errors.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
public readonly struct Result<TValue> : IEquatable<Result<TValue>>
{
    private readonly Result<TValue, string> _inner;

    private Result(Result<TValue, string> inner) => _inner = inner;

    /// <summary>
    /// Gets a value indicating whether this result represents success.
    /// </summary>
    public bool IsSuccess => _inner.IsSuccess;

    /// <summary>
    /// Gets a value indicating whether this result represents failure.
    /// </summary>
    public bool IsFailure => _inner.IsFailure;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    public TValue Value => _inner.Value;

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Error => _inner.Error;

    /// <summary>
    /// Creates a successful Result.
    /// </summary>
    public static Result<TValue> Success(TValue value) => new(Result<TValue, string>.Success(value));

    /// <summary>
    /// Creates a failed Result.
    /// </summary>
    public static Result<TValue> Failure(string error) => new(Result<TValue, string>.Failure(error));

    /// <summary>
    /// Monadic bind operation.
    /// </summary>
    public Result<TResult> Bind<TResult>(Func<TValue, Result<TResult>> func)
    {
        return new(_inner.Bind(v => func(v)._inner));
    }

    /// <summary>
    /// Functor map operation.
    /// </summary>
    public Result<TResult> Map<TResult>(Func<TValue, TResult> func)
    {
        return new(_inner.Map(func));
    }

    /// <summary>
    /// Pattern matching.
    /// </summary>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<string, TResult> onFailure)
    {
        return _inner.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// Pattern matching with actions.
    /// </summary>
    public void Match(Action<TValue> onSuccess, Action<string> onFailure)
    {
        _inner.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// Gets value or default.
    /// </summary>
    public TValue GetValueOrDefault(TValue defaultValue) => _inner.GetValueOrDefault(defaultValue);

    /// <summary>
    /// Converts to Option.
    /// </summary>
    public Option<TValue> ToOption() => _inner.ToOption();

    /// <summary>
    /// Implicit conversion from value.
    /// </summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>
    /// String representation.
    /// </summary>
    public override string ToString() => _inner.ToString();

    /// <summary>
    /// Equality comparison.
    /// </summary>
    public bool Equals(Result<TValue> other) => _inner.Equals(other._inner);

    /// <summary>
    /// Object equality.
    /// </summary>
    public override bool Equals(object? obj) => obj is Result<TValue> other && Equals(other);

    /// <summary>
    /// Hash code.
    /// </summary>
    public override int GetHashCode() => _inner.GetHashCode();

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Result<TValue> left, Result<TValue> right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Result<TValue> left, Result<TValue> right) => !left.Equals(right);
}
