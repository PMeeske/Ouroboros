# Contributing to Ouroboros

First off, thank you for considering contributing to Ouroboros! 🎉

This document provides guidelines for contributing to the project. Following these guidelines helps communicate that you respect the time of the developers managing and developing this open source project.

## 📋 Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How Can I Contribute?](#how-can-i-contribute)
- [Development Setup](#development-setup)
- [Coding Standards](#coding-standards)
- [Commit Guidelines](#commit-guidelines)
- [Pull Request Process](#pull-request-process)
- [Testing Requirements](#testing-requirements)

## 📜 Code of Conduct

This project and everyone participating in it is governed by our [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.

## 🤝 How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues to avoid duplicates. When creating a bug report, include:

- **Clear title and description**
- **Steps to reproduce** the issue
- **Expected vs actual behavior**
- **Environment details** (OS, .NET version, etc.)
- **Code samples** or error messages

Use the bug report template when creating issues.

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear title** describing the enhancement
- **Provide detailed description** of the suggested enhancement
- **Explain why** this enhancement would be useful
- **List similar features** in other projects if applicable

### Pull Requests

1. **Fork the repository** and create your branch from `main`
2. **Make your changes** following our coding standards
3. **Add tests** for any new functionality
4. **Update documentation** as needed
5. **Ensure all tests pass** locally
6. **Submit a pull request**

## 🛠️ Development Setup

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Git](https://git-scm.com/)
- [Ollama](https://ollama.ai/) (optional, for local LLM testing)
- IDE: Visual Studio 2022, VS Code, or JetBrains Rider

### Setup Steps

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/Ouroboros.git
cd Ouroboros

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test

# Run examples (optional)
cd src/Ouroboros.Examples
dotnet run
```

### Development Workflow

1. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**
   - Follow the functional programming patterns established in the codebase
   - Use monadic error handling consistently
   - Add XML documentation for public APIs

3. **Test your changes**
   ```bash
   dotnet test
   ```

4. **Commit and push**
   ```bash
   git add .
   git commit -m "feat: your feature description"
   git push origin feature/your-feature-name
   ```

5. **Create Pull Request**

## 💻 Coding Standards

### Functional Programming Principles

Ouroboros follows functional programming paradigms:

- **Pure Functions**: Prefer functions without side effects
- **Immutability**: Use immutable data structures
- **Monadic Composition**: Use `Result<T>` and `Option<T>` for error handling
- **Type Safety**: Leverage the C# type system fully

### Code Style

We use `.editorconfig` for consistent formatting:

```csharp
// ✅ Good - Monadic error handling
public static async Task<Result<Draft>> GenerateDraft(string prompt, ToolRegistry tools)
{
    try 
    {
        var result = await llm.GenerateAsync(prompt);
        return Result<Draft>.Ok(new Draft(result));
    }
    catch (Exception ex)
    {
        return Result<Draft>.Error($"Draft generation failed: {ex.Message}");
    }
}

// ❌ Bad - Throwing exceptions in pipeline operations
public static async Task<Draft> GenerateDraft(string prompt, ToolRegistry tools)
{
    var result = await llm.GenerateAsync(prompt);
    return new Draft(result); // Can throw!
}
```

### Naming Conventions

- **Classes**: PascalCase (e.g., `PipelineBranch`)
- **Methods**: PascalCase (e.g., `GenerateDraft`)
- **Parameters**: camelCase (e.g., `toolRegistry`)
- **Private Fields**: camelCase with `_` prefix (e.g., `_vectorStore`)
- **Constants**: PascalCase (e.g., `MaxRetries`)

### Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Creates a draft arrow that generates an initial response using the provided LLM and tools.
/// </summary>
/// <param name="llm">The tool-aware language model for generation</param>
/// <param name="tools">Registry of available tools</param>
/// <param name="topic">The topic for draft generation</param>
/// <returns>A step that transforms a pipeline branch by adding a draft reasoning state</returns>
public static Step<PipelineBranch, PipelineBranch> DraftArrow(
    ToolAwareChatModel llm, ToolRegistry tools, string topic)
```

## 📝 Commit Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `perf`: Performance improvements
- `chore`: Maintenance tasks

### Examples

```bash
feat(pipeline): add iterative refinement loop
fix(math-tool): use culture-invariant number formatting
docs(readme): update deployment instructions
test(monads): add Result monad composition tests
```

## 🔄 Pull Request Process

1. **Update Documentation**
   - Update README.md if needed
   - Add/update XML documentation
   - Update CHANGELOG.md (if exists)

2. **Ensure Tests Pass**
   ```bash
   dotnet test
   ```

3. **Check Build Succeeds**
   ```bash
   dotnet build
   ```

4. **Follow PR Template**
   - Describe changes clearly
   - Reference related issues
   - Add screenshots for UI changes

5. **Code Review**
   - Address review comments
   - Keep PR scope focused
   - Squash commits if requested

6. **Merge Requirements**
   - All tests passing
   - No merge conflicts
   - At least one approval
   - All conversations resolved

## 🧪 Testing Requirements

### Unit Tests

- **Required** for all new functionality
- Use xUnit framework
- Follow AAA pattern (Arrange, Act, Assert)
- Use FluentAssertions for readable assertions

```csharp
[Fact]
public async Task Should_Generate_Draft_With_Valid_Input()
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

### Integration Tests

- Test end-to-end workflows
- Use real or realistic test doubles
- Document external dependencies

### Test Coverage

- Aim for >80% coverage for new code
- Focus on critical paths
- Test edge cases and error conditions

## 🎯 Areas for Contribution

We especially welcome contributions in:

- **Test Coverage**: Increasing coverage for CLI and Agent modules
- **Vector Stores**: Implementing Qdrant and Pinecone integrations
- **Documentation**: Examples, tutorials, and guides
- **Performance**: Optimizations and benchmarks
- **Tools**: New tool implementations
- **Bug Fixes**: Any bugs you encounter

## 📚 Additional Resources

- [.github/copilot-instructions.md](.github/copilot-instructions.md) - Development guidelines
- [DEPLOYMENT.md](DEPLOYMENT.md) - Deployment guide
- [TEST_COVERAGE_REPORT.md](TEST_COVERAGE_REPORT.md) - Coverage details
- [docs/](docs/) - Additional documentation

## 💬 Questions?

- Open a [Discussion](https://github.com/PMeeske/Ouroboros/discussions)
- Join our community chat (if available)
- Check existing [Issues](https://github.com/PMeeske/Ouroboros/issues)

## 🙏 Recognition

Contributors will be recognized in:
- GitHub contributors page
- Release notes
- Project acknowledgments

Thank you for contributing to Ouroboros! 🚀
