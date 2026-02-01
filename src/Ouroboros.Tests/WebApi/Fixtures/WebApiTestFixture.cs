// <copyright file="WebApiTestFixture.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.WebApi.Services;

namespace Ouroboros.Tests.WebApi.Fixtures;

/// <summary>
/// Custom WebApplicationFactory for integration testing of WebApi.
/// </summary>
public class WebApiTestFixture : WebApplicationFactory<Program>
{
    /// <summary>
    /// Configures the test web host.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with mock implementations
            services.AddSingleton<IPipelineService, MockPipelineService>();
            services.AddSingleton<ISelfModelService, MockSelfModelService>();
        });

        builder.UseEnvironment("Testing");
    }
}
