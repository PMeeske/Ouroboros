// <copyright file="ApiResponseAssertions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Ouroboros.WebApi.Models;

namespace Ouroboros.Tests.Infrastructure.Assertions;

/// <summary>
/// Custom FluentAssertions extensions for ApiResponse{T}.
/// </summary>
public static class ApiResponseAssertionsExtensions
{
    /// <summary>
    /// Returns an ApiResponseAssertionsContext for the given ApiResponse.
    /// </summary>
    public static ApiResponseAssertionsContext<T> Should<T>(this ApiResponse<T> response)
        => new ApiResponseAssertionsContext<T>(response);
}

/// <summary>
/// Assertions context for ApiResponse{T}.
/// </summary>
public class ApiResponseAssertionsContext<T>
{
    private readonly ApiResponse<T> _subject;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiResponseAssertionsContext{T}"/> class.
    /// </summary>
    public ApiResponseAssertionsContext(ApiResponse<T> subject)
    {
        _subject = subject;
    }

    /// <summary>
    /// Asserts that the API response indicates success.
    /// </summary>
    public AndConstraint<ApiResponseAssertionsContext<T>> BeSuccessful(string because = "", params object[] becauseArgs)
    {
        _subject.Success.Should().BeTrue(because, becauseArgs);
        return new AndConstraint<ApiResponseAssertionsContext<T>>(this);
    }

    /// <summary>
    /// Asserts that the API response indicates failure.
    /// </summary>
    public AndConstraint<ApiResponseAssertionsContext<T>> BeFailed(string because = "", params object[] becauseArgs)
    {
        _subject.Success.Should().BeFalse(because, becauseArgs);
        return new AndConstraint<ApiResponseAssertionsContext<T>>(this);
    }

    /// <summary>
    /// Asserts that the API response has the expected data.
    /// </summary>
    public AndConstraint<ApiResponseAssertionsContext<T>> HaveData(T expectedData, string because = "", params object[] becauseArgs)
    {
        _subject.Data.Should().NotBeNull(because, becauseArgs);
        _subject.Data.Should().Be(expectedData, because, becauseArgs);
        return new AndConstraint<ApiResponseAssertionsContext<T>>(this);
    }

    /// <summary>
    /// Asserts that the API response has non-null data.
    /// </summary>
    public AndConstraint<ApiResponseAssertionsContext<T>> HaveData(string because = "", params object[] becauseArgs)
    {
        _subject.Data.Should().NotBeNull(because, becauseArgs);
        return new AndConstraint<ApiResponseAssertionsContext<T>>(this);
    }

    /// <summary>
    /// Asserts that the API response has the expected error message.
    /// </summary>
    public AndConstraint<ApiResponseAssertionsContext<T>> HaveError(string expectedError, string because = "", params object[] becauseArgs)
    {
        _subject.Error.Should().NotBeNull(because, becauseArgs);
        _subject.Error.Should().Contain(expectedError, because, becauseArgs);
        return new AndConstraint<ApiResponseAssertionsContext<T>>(this);
    }

    /// <summary>
    /// Asserts that the API response has an error.
    /// </summary>
    public AndConstraint<ApiResponseAssertionsContext<T>> HaveError(string because = "", params object[] becauseArgs)
    {
        _subject.Error.Should().NotBeNull(because, becauseArgs);
        return new AndConstraint<ApiResponseAssertionsContext<T>>(this);
    }

    /// <summary>
    /// Asserts that the API response execution time is less than the specified duration.
    /// </summary>
    public AndConstraint<ApiResponseAssertionsContext<T>> HaveExecutionTimeLessThan(long milliseconds, string because = "", params object[] becauseArgs)
    {
        _subject.ExecutionTimeMs.Should().HaveValue(because, becauseArgs);
        _subject.ExecutionTimeMs.Should().BeLessThan(milliseconds, because, becauseArgs);
        return new AndConstraint<ApiResponseAssertionsContext<T>>(this);
    }
}
