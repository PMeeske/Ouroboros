# MonadicPipeline Dockerfile
# Multi-stage build for optimized production image

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY MonadicPipeline.sln .
COPY src/MonadicPipeline.CLI/MonadicPipeline.CLI.csproj src/MonadicPipeline.CLI/
COPY src/MonadicPipeline.Core/MonadicPipeline.Core.csproj src/MonadicPipeline.Core/
COPY src/MonadicPipeline.Domain/MonadicPipeline.Domain.csproj src/MonadicPipeline.Domain/
COPY src/MonadicPipeline.Pipeline/MonadicPipeline.Pipeline.csproj src/MonadicPipeline.Pipeline/
COPY src/MonadicPipeline.Tools/MonadicPipeline.Tools.csproj src/MonadicPipeline.Tools/
COPY src/MonadicPipeline.Providers/MonadicPipeline.Providers.csproj src/MonadicPipeline.Providers/
COPY src/MonadicPipeline.Agent/MonadicPipeline.Agent.csproj src/MonadicPipeline.Agent/
COPY src/MonadicPipeline.WebApi/MonadicPipeline.WebApi.csproj src/MonadicPipeline.WebApi/
COPY src/MonadicPipeline.Tests/MonadicPipeline.Tests.csproj src/MonadicPipeline.Tests/
COPY src/MonadicPipeline.Examples/MonadicPipeline.Examples.csproj src/MonadicPipeline.Examples/
COPY src/MonadicPipeline.Benchmarks/MonadicPipeline.Benchmarks.csproj src/MonadicPipeline.Benchmarks/

# Restore dependencies
RUN dotnet restore src/MonadicPipeline.CLI/MonadicPipeline.CLI.csproj

# Copy source code
COPY . .

# Build and publish
WORKDIR /src/src/MonadicPipeline.CLI
RUN dotnet publish -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Install dependencies for MeTTa support (optional)
RUN apt-get update && apt-get install -y \
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Copy configuration files
COPY appsettings.json .
COPY appsettings.Production.json .

# Create logs directory
RUN mkdir -p /app/logs

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_ENVIRONMENT=Production

# Expose health check port (if needed for future enhancements)
EXPOSE 8080

# Set entrypoint
ENTRYPOINT ["dotnet", "LangChainPipeline.dll"]
