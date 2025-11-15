#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#if NET8_0_OR_GREATER


// Guard ASP.NET Core references so this file only compiles when ASP.NET is available
#if HAS_ASPNET
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
#endif

namespace LangChainPipeline.Interop.AspNet;

/// <summary>
/// ASP.NET Core helpers to wrap middleware/configuration into Step-based composition.
/// This matches the requested API:
/// - Use(Func{RequestDelegate, RequestDelegate})
/// - Use(Func{IApplicationBuilder, IApplicationBuilder})
/// and lets you compose via StepDefinition with the | operator when TOut is preserved.
/// </summary>
public static class ApplicationStepExtensions
{
#if HAS_ASPNET
    /// <summary>
    /// Wrap standard ASP.NET Core middleware delegate.
    /// Equivalent to app => app.Use(middleware)
    /// </summary>
    public static Step<IApplicationBuilder, IApplicationBuilder> Use(
        Func<RequestDelegate, RequestDelegate> middleware)
        => new(async app =>
        {
            app.Use(middleware);
            return await Task.FromResult(app);
        });

    /// <summary>
    /// Wrap configuration function that returns the mutated builder.
    /// </summary>
    public static Step<IApplicationBuilder, IApplicationBuilder> Use(
        Func<IApplicationBuilder, IApplicationBuilder> configure)
        => new(async app => await Task.FromResult(configure(app)));
#else
    // Placeholders so the file compiles in non-ASP.NET projects. They throw if invoked without ASP.NET types.
    public static Step<object, object> Use(Func<Delegate, Delegate> _)
        => new(_ => Task.FromException<object>(new NotSupportedException("ASP.NET Core types are not referenced in this project.")));

    public static Step<object, object> Use(Func<object, object> _)
        => new(_ => Task.FromException<object>(new NotSupportedException("ASP.NET Core types are not referenced in this project.")));
#endif
}
#endif
