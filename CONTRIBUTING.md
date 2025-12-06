# Contributing to Ouroboros

Thank you for your interest in contributing to Ouroboros! This document provides guidelines and instructions for contributing to the project.

## 🚀 Quick Start

1. **Fork the repository** on GitHub
2. **Clone your fork** locally
3. **Create a branch** for your changes
4. **Make your changes** following our coding standards
5. **Test your changes** thoroughly
6. **Submit a pull request**

## 📋 Code of Conduct

This project adheres to a [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## 🎯 How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include:

- A clear, descriptive title
- Detailed steps to reproduce the issue
- Expected vs. actual behavior
- Your environment (OS, .NET version, etc.)
- Screenshots or logs if applicable

Use the [Bug Report template](.github/ISSUE_TEMPLATE/bug_report.yml) when creating bug reports.

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, include:

- A clear, descriptive title
- Detailed description of the proposed enhancement
- Examples of how it would be used
- Why this enhancement would be useful

Use the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.yml) when suggesting enhancements.

### Pull Requests

1. **Follow the coding standards** described below
2. **Include tests** for new functionality
3. **Update documentation** as needed
4. **Keep commits focused** and write clear commit messages
5. **Link related issues** in your PR description

## 🏗️ Development Setup

### Prerequisites

- .NET 10.0 SDK or later
- Docker (for local testing with Ollama/Qdrant)
- Git

### Building the Project

```bash
# Clone the repository
git clone https://github.com/PMeeske/Ouroboros.git
cd Ouroboros

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

### Running Locally

```bash
# Start services with Docker Compose
docker-compose up -d

# Run the CLI
dotnet run --project src/Ouroboros.CLI

# Run the Web API
dotnet run --project src/Ouroboros.WebApi
```

## 🎨 Coding Standards

Ouroboros follows functional programming principles and C# best practices:

### Functional Programming Principles

- **Use monadic composition**: Leverage `Result<T>` and `Option<T>` for error handling
- **Prefer immutability**: Use immutable data structures and pure functions
- **Avoid side effects**: Keep functions pure when possible
- **Use Kleisli arrows**: Implement `Step<TInput, TOutput>` for composable operations

### C# Conventions

- **Nullable reference types**: Enable and use correctly
- **XML documentation**: Document all public APIs
- **StyleCop compliance**: Follow StyleCop rules (see `stylecop.json`)
- **Naming conventions**: Use PascalCase for types/methods, camelCase for parameters
- **Async/await**: Use properly for asynchronous operations

### Code Style Example

```csharp
/// <summary>
/// Creates a draft arrow that generates an initial response.
/// </summary>
/// <param name="llm">The language model for generation</param>
/// <param name="tools">Registry of available tools</param>
/// <param name="topic">The topic for draft generation</param>
/// <returns>A step that transforms a pipeline branch</returns>
public static Step<PipelineBranch, PipelineBranch> DraftArrow(
    ToolAwareChatModel llm, 
    ToolRegistry tools, 
    string topic) =>
    async branch =>
    {
        var result = await GenerateDraft(llm, tools, topic);
        return result.Match(
            success => branch.AddReasoning(success),
            error => branch.AddError(error));
    };
```

## 🧪 Testing Guidelines

- **Write tests for all new functionality**
- **Follow existing test patterns** in the `Tests/` directory
- **Use descriptive test names**: `Should_ReturnSuccess_When_InputIsValid`
- **Use FluentAssertions** for assertions
- **Mock external dependencies** (LLMs, vector stores, etc.)
- **Aim for high coverage**: Target 70%+ coverage

### Test Example

```csharp
[Fact]
public async Task Should_GenerateDraft_When_ValidInputProvided()
{
    // Arrange
    var branch = new PipelineBranch("test", store, dataSource);
    var llm = CreateMockLLM();
    var tools = CreateTestTools();
    
    // Act
    var result = await DraftArrow(llm, tools, "test topic")(branch);
    
    // Assert
    result.Events.OfType<ReasoningStep>()
        .Should().ContainSingle()
        .Which.State.Should().BeOfType<Draft>();
}
```

## 📚 Documentation

- **Update README.md** if you change functionality
- **Add XML documentation** to all public APIs
- **Update guides** in the `docs/` directory as needed
- **Include code examples** for new features

## 🔍 Code Review Process

1. All pull requests require at least one review
2. Automated checks must pass (build, tests, linting)
3. Address reviewer feedback promptly
4. Maintain a respectful, constructive dialogue

## 🎯 Production Release v1.0 Epic

We're working towards a production-ready v1.0 release! See [Epic #1](https://github.com/PMeeske/Ouroboros/issues/1) for the complete roadmap:

- Requirements & Scope Finalisation
- Code Quality & Architecture Audit
- Unit, Integration & Property Testing
- Continuous Integration / Continuous Delivery
- Security Hardening & Secrets Management
- Performance Benchmarking & Optimisation
- Documentation Overhaul
- Observability: Logging, Metrics & Alerts
- Deployment & Infrastructure as Code
- License, Legal & Third-Party Compliance
- Release Management & Versioning Strategy
- Contribution Guidelines & Community Health
- Example Apps, Tutorials & Samples
- Final Production Roll-out & Monitoring

## 💬 Community

- **GitHub Issues**: Bug reports and feature requests
- **GitHub Discussions**: Questions and community discussions
- **Pull Requests**: Code contributions

## 📝 Commit Message Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Examples:**
```
feat(pipeline): add support for parallel execution
fix(vectorstore): resolve memory leak in batch operations
docs(readme): update installation instructions
```

## 🏆 Recognition

Contributors are recognized in our README.md and release notes. Thank you for helping make Ouroboros better!

## ❓ Questions?

If you have questions about contributing, feel free to:
- Open a [GitHub Discussion](https://github.com/PMeeske/Ouroboros/discussions)
- Reach out in an existing issue or PR
- Check our [documentation](docs/README.md)

---

**Part of Epic [#1](https://github.com/PMeeske/Ouroboros/issues/1) - Production-ready Release v1.0**
