// <copyright file="TapoRestClientTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers.Tapo;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Ouroboros.Core.Learning;
using Ouroboros.Providers.Tapo;
using Xunit;

/// <summary>
/// Comprehensive tests for the TapoRestClient class.
/// Tests authentication, device management, session handling,
/// and error scenarios.
/// </summary>
[Trait("Category", "Unit")]
public class TapoRestClientTests
{
    private readonly Mock<ILogger<TapoRestClient>> _mockLogger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TapoRestClientTests()
    {
        _mockLogger = new Mock<ILogger<TapoRestClient>>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidHttpClient_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new TapoRestClient(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_WithLogger_Succeeds()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        using var client = new TapoRestClient(httpClient, _mockLogger.Object);

        // Assert
        client.Should().NotBeNull();
    }

    #endregion

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WithValidPassword_ReturnsSuccessResult()
    {
        // Arrange
        const string sessionId = "test-session-id";
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, sessionId);
        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.LoginAsync("test-password");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(sessionId);
    }

    [Fact]
    public async Task LoginAsync_WithEmptyPassword_ReturnsFailureResult()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.LoginAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Password is required");
    }

    [Fact]
    public async Task LoginAsync_WithWhitespacePassword_ReturnsFailureResult()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.LoginAsync("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Password is required");
    }

    [Fact]
    public async Task LoginAsync_WithHttpError_ReturnsFailureResult()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.Unauthorized, "Unauthorized");
        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.LoginAsync("wrong-password");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Login failed");
    }

    [Fact]
    public async Task LoginAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);

        // Act
        Func<Task> act = async () => await client.LoginAsync("test-password", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion

    #region GetDevicesAsync Tests

    [Fact]
    public async Task GetDevicesAsync_WhenNotAuthenticated_ReturnsFailureResult()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.GetDevicesAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not authenticated");
    }

    [Fact]
    public async Task GetDevicesAsync_WithValidSession_ReturnsDeviceList()
    {
        // Arrange
        var devices = new List<TapoDevice>
        {
            new() { Name = "bulb1", DeviceType = TapoDeviceType.L530, IpAddress = "192.168.1.100" },
            new() { Name = "plug1", DeviceType = TapoDeviceType.P110, IpAddress = "192.168.1.101" }
        };
        var devicesJson = JsonSerializer.Serialize(devices, _jsonOptions);

        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, "session-id");
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath == "/devices"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(devicesJson, System.Text.Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);

        await client.LoginAsync("test-password");

        // Act
        var result = await client.GetDevicesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("bulb1");
        result.Value[1].Name.Should().Be("plug1");
    }

    #endregion

    #region GetActionsAsync Tests

    [Fact]
    public async Task GetActionsAsync_WhenNotAuthenticated_ReturnsFailureResult()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.GetActionsAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not authenticated");
    }

    [Fact]
    public async Task GetActionsAsync_WithValidSession_ReturnsActionList()
    {
        // Arrange
        var actions = new List<string> { "/l530/on", "/l530/off", "/p110/on" };
        var actionsJson = JsonSerializer.Serialize(actions, _jsonOptions);

        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, "session-id");
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath == "/actions"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(actionsJson, System.Text.Encoding.UTF8, "application/json")
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);

        await client.LoginAsync("test-password");

        // Act
        var result = await client.GetActionsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().Contain("/l530/on");
    }

    #endregion

    #region RefreshSessionAsync Tests

    [Fact]
    public async Task RefreshSessionAsync_WhenNotAuthenticated_ReturnsFailureResult()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.RefreshSessionAsync("test-device");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not authenticated");
    }

    [Fact]
    public async Task RefreshSessionAsync_WithEmptyDeviceName_ReturnsFailureResult()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, "session-id");
        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);
        await client.LoginAsync("test-password");

        // Act
        var result = await client.RefreshSessionAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Device name is required");
    }

    [Fact]
    public async Task RefreshSessionAsync_WithValidDevice_ReturnsSuccessResult()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, "session-id");
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.AbsolutePath == "/refresh-session" &&
                    req.RequestUri.Query.Contains("device=bulb1")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);
        await client.LoginAsync("test-password");

        // Act
        var result = await client.RefreshSessionAsync("bulb1");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region ReloadConfigAsync Tests

    [Fact]
    public async Task ReloadConfigAsync_WhenNotAuthenticated_ReturnsFailureResult()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Act
        var result = await client.ReloadConfigAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Not authenticated");
    }

    [Fact]
    public async Task ReloadConfigAsync_WithValidSession_ReturnsSuccessResult()
    {
        // Arrange
        var mockHandler = CreateMockHttpHandler(HttpStatusCode.OK, "session-id");
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.AbsolutePath == "/reload-config" &&
                    req.Method == HttpMethod.Post),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        using var httpClient = new HttpClient(mockHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };
        using var client = new TapoRestClient(httpClient);
        await client.LoginAsync("test-password");

        // Act
        var result = await client.ReloadConfigAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Device Operations Properties Tests

    [Fact]
    public void LightBulbs_Property_IsNotNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.LightBulbs.Should().NotBeNull();
    }

    [Fact]
    public void ColorLightBulbs_Property_IsNotNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.ColorLightBulbs.Should().NotBeNull();
    }

    [Fact]
    public void LightStrips_Property_IsNotNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.LightStrips.Should().NotBeNull();
    }

    [Fact]
    public void RgbicLightStrips_Property_IsNotNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.RgbicLightStrips.Should().NotBeNull();
    }

    [Fact]
    public void Plugs_Property_IsNotNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.Plugs.Should().NotBeNull();
    }

    [Fact]
    public void EnergyPlugs_Property_IsNotNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.EnergyPlugs.Should().NotBeNull();
    }

    [Fact]
    public void PowerStrips_Property_IsNotNull()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var client = new TapoRestClient(httpClient);

        // Assert
        client.PowerStrips.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static Mock<HttpMessageHandler> CreateMockHttpHandler(HttpStatusCode statusCode, string content)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.AbsolutePath == "/login"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        return mockHandler;
    }

    #endregion
}
