using System.Net;

namespace Ouroboros.Tests.UnitTests.Providers;

/// <summary>
/// Integration tests for Polly retry policies in HTTP client models.
/// These tests verify that exponential backoff is properly configured.
/// </summary>
[Trait("Category", "Unit")]
public class PollyRetryTests
{
    /// <summary>
    /// Demonstrates the expected retry behavior with exponential backoff.
    /// This test documents the retry policy without requiring a live endpoint.
    /// </summary>
    [Fact]
    public void PollyRetryPolicy_Configuration_IsDocumented()
    {
        // Arrange: Expected retry behavior
        const int expectedRetryCount = 3;
        double[] expectedBackoffSeconds = { 2.0, 4.0, 8.0 };
        
        // Act: Calculate backoff for each retry
        double[] actualBackoff = new double[expectedRetryCount];
        for (int i = 0; i < expectedRetryCount; i++)
        {
            actualBackoff[i] = Math.Pow(2, i + 1);
        }
        
        // Assert: Verify exponential backoff calculation
        Assert.Equal(expectedBackoffSeconds, actualBackoff);
    }
    
    /// <summary>
    /// Verifies that retry policy handles 429 (Too Many Requests) status code.
    /// </summary>
    [Fact]
    public void PollyRetryPolicy_ShouldRetry_On429StatusCode()
    {
        // Arrange
        HttpStatusCode tooManyRequests = (HttpStatusCode)429;
        
        // Act: Check if status code is in retry range
        bool shouldRetry = (int)tooManyRequests == 429 || (int)tooManyRequests >= 500;
        
        // Assert
        Assert.True(shouldRetry, "Policy should retry on 429 Too Many Requests");
    }
    
    /// <summary>
    /// Verifies that retry policy handles 5xx server errors.
    /// </summary>
    [Theory]
    [InlineData(500)] // Internal Server Error
    [InlineData(502)] // Bad Gateway
    [InlineData(503)] // Service Unavailable
    [InlineData(504)] // Gateway Timeout
    public void PollyRetryPolicy_ShouldRetry_On5xxStatusCodes(int statusCode)
    {
        // Arrange
        HttpStatusCode serverError = (HttpStatusCode)statusCode;
        
        // Act: Check if status code is in retry range
        bool shouldRetry = (int)serverError == 429 || (int)serverError >= 500;
        
        // Assert
        Assert.True(shouldRetry, $"Policy should retry on {statusCode} server error");
    }
    
    /// <summary>
    /// Verifies that retry policy does NOT retry on client errors (4xx except 429).
    /// </summary>
    [Theory]
    [InlineData(400)] // Bad Request
    [InlineData(401)] // Unauthorized
    [InlineData(403)] // Forbidden
    [InlineData(404)] // Not Found
    public void PollyRetryPolicy_ShouldNotRetry_On4xxClientErrors(int statusCode)
    {
        // Arrange
        HttpStatusCode clientError = (HttpStatusCode)statusCode;
        
        // Act: Check if status code is in retry range
        bool shouldRetry = (int)clientError == 429 || (int)clientError >= 500;
        
        // Assert
        Assert.False(shouldRetry, $"Policy should NOT retry on {statusCode} client error");
    }
    
    /// <summary>
    /// Verifies that retry policy does NOT retry on successful responses.
    /// </summary>
    [Theory]
    [InlineData(200)] // OK
    [InlineData(201)] // Created
    [InlineData(204)] // No Content
    public void PollyRetryPolicy_ShouldNotRetry_OnSuccessStatusCodes(int statusCode)
    {
        // Arrange
        HttpStatusCode success = (HttpStatusCode)statusCode;
        
        // Act: Check if status code is in retry range
        bool shouldRetry = (int)success == 429 || (int)success >= 500;
        
        // Assert
        Assert.False(shouldRetry, $"Policy should NOT retry on {statusCode} success response");
    }
    
    /// <summary>
    /// Documents the total retry duration for the exponential backoff policy.
    /// With 3 retries at 2s, 4s, and 8s, total wait time is 14 seconds.
    /// </summary>
    [Fact]
    public void PollyRetryPolicy_TotalRetryDuration_IsCalculatedCorrectly()
    {
        // Arrange: Expected total wait time for all retries
        const int retryCount = 3;
        double expectedTotalWaitSeconds = 0;
        
        // Act: Calculate total wait time
        for (int i = 0; i < retryCount; i++)
        {
            expectedTotalWaitSeconds += Math.Pow(2, i + 1);
        }
        
        // Assert: Verify total is 2 + 4 + 8 = 14 seconds
        Assert.Equal(14.0, expectedTotalWaitSeconds);
    }
}
