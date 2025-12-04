# GitHub Copilot-like DSL Assistant

## Overview

The Ouroboros DSL Assistant provides GitHub Copilot-like intelligent code assistance for building pipelines using the CLI DSL, with full C# code creation, analysis, and refactoring capabilities powered by Roslyn and AI.

## Features

### 1. DSL Intelligence
- **Smart Suggestions**: Context-aware next-step suggestions based on current pipeline
- **Auto-Completion**: Fuzzy token completion with similarity matching
- **Validation**: Real-time DSL validation with automatic fix suggestions
- **Explanation**: Natural language explanations of what pipelines do
- **Building**: Generate complete DSL pipelines from high-level goals

### 2. Code Analysis & Generation
- **Roslyn Analysis**: Full code analysis with diagnostics and syntax trees
- **Class Creation**: Generate complete C# classes with structure
- **Method Addition**: Add methods to existing classes
- **Refactoring**: Rename symbols and extract methods
- **AI Generation**: Generate code from natural language descriptions
- **Custom Analyzers**:
  - Monadic pattern analyzer (checks Result<T> usage)
  - Async/await pattern analyzer (detects .Result/.Wait() blocking)
  - Documentation completeness analyzer

### 3. MCP Server Integration
- Model Context Protocol (MCP) server with 10 tools
- Standardized JSON schema-based interfaces
- IDE/editor integration ready
- Remote tool execution support

## Installation

The DSL Assistant is included in the Ouroboros.CLI project:

```bash
cd src/Ouroboros.CLI
dotnet build
```

## Usage

### Command Line Interface

The `assist` command provides multiple modes of operation:

```bash
# Suggest next DSL steps
dotnet run -- assist -m suggest -d "SetTopic('AI Ethics')"

# Complete partial token
dotnet run -- assist -m complete -p "UseD"

# Validate DSL
dotnet run -- assist -m validate -d "SetTopic('test') | UseDraft | UnknownToken"

# Explain DSL
dotnet run -- assist -m explain -d "SetTopic('AI') | UseDraft | UseCritique"

# Build DSL from goal
dotnet run -- assist -m build -g "Create a pipeline to analyze code quality"

# Analyze C# code
dotnet run -- assist -m analyze -d "public class Calculator { public int Add(int a, int b) { return a + b; } }"

# Generate code from description
dotnet run -- assist -m create -g "Create a Result<T> monad with Success and Failure factory methods"

# Start MCP server
dotnet run -- assist -m mcp

# Interactive REPL mode
dotnet run -- assist --interactive
```

### Interactive Mode

The interactive mode provides a REPL (Read-Eval-Print-Loop) interface:

```bash
dotnet run -- assist --interactive

> suggest SetTopic('AI')
Suggestions:
  • UseDraft: Generate an initial draft response...
  • UseCritique: Analyze and critique the current draft...

> complete Use
Completions: UseDraft, UseDir, UseCritique, UseImprove, UseIngest

> validate SetTopic('test') | UseDraft
Valid: True

> explain SetTopic('AI') | UseDraft | UseCritique
Explanation: This pipeline starts by setting the topic to AI, generates an initial draft...

> build Create a document analysis pipeline
Generated DSL: SetTopic('document analysis') | UseIngest | UseDraft | UseCritique | UseImprove

> analyze public class Test { public void Method() { } }
Valid: True
Classes: Test
Methods: 1

> create Create a class with async methods returning Result<T>
Generated code:
public class AsyncExample
{
    public async Task<Result<string, string>> ProcessAsync(string input)
    {
        // Implementation
    }
}

> help
Available commands:
  suggest <dsl>       - Suggest next DSL steps
  complete <partial>  - Complete partial token
  validate <dsl>      - Validate DSL syntax
  explain <dsl>       - Explain what DSL does
  build <goal>        - Build DSL from goal
  analyze <code>      - Analyze C# code
  create <desc>       - Create C# class from description
  help                - Show this help
  exit                - Exit interactive mode

> exit
```

### Programmatic Usage

```csharp
using LangChainPipeline.CLI;
using LangChainPipeline.CLI.CodeGeneration;
using LangChainPipeline.Agent.MetaAI;

// Initialize
var provider = new OllamaProvider();
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var tools = ToolRegistry.CreateDefault();
var llm = new ToolAwareChatModel(chatModel, tools);

// Create assistant
var assistant = new DslAssistant(llm, tools);

// Suggest next steps
var suggestions = await assistant.SuggestNextStepAsync("SetTopic('AI')");
suggestions.Match(
    list => {
        foreach (var s in list)
            Console.WriteLine($"{s.Token}: {s.Explanation}");
    },
    error => Console.WriteLine($"Error: {error}"));

// Complete token
var completions = assistant.CompleteToken("UseD");
completions.Match(
    list => Console.WriteLine($"Completions: {string.Join(", ", list)}"),
    error => Console.WriteLine($"Error: {error}"));

// Validate DSL
var validation = await assistant.ValidateAndFixAsync("SetTopic('test') | UseDraft");
validation.Match(
    result => {
        Console.WriteLine($"Valid: {result.IsValid}");
        if (result.FixedDsl != null)
            Console.WriteLine($"Suggested fix: {result.FixedDsl}");
    },
    error => Console.WriteLine($"Error: {error}"));

// Explain DSL
var explanation = await assistant.ExplainDslAsync("SetTopic('AI') | UseDraft | UseCritique");
explanation.Match(
    text => Console.WriteLine($"Explanation: {text}"),
    error => Console.WriteLine($"Error: {error}"));

// Build DSL from goal
var dsl = await assistant.BuildDslInteractivelyAsync("Analyze code quality");
dsl.Match(
    text => Console.WriteLine($"Generated DSL: {text}"),
    error => Console.WriteLine($"Error: {error}"));
```

### Roslyn Code Tool

```csharp
using LangChainPipeline.CLI.CodeGeneration;

var codeTool = new RoslynCodeTool();

// Analyze code
var code = "public class Calculator { public int Add(int a, int b) { return a + b; } }";
var analysis = await codeTool.AnalyzeCodeAsync(code, runAnalyzers: true);
analysis.Match(
    result => {
        Console.WriteLine($"Valid: {result.IsValid}");
        Console.WriteLine($"Classes: {string.Join(", ", result.Classes)}");
        Console.WriteLine($"Methods: {result.Methods.Count}");
        foreach (var diagnostic in result.Diagnostics)
            Console.WriteLine($"  {diagnostic}");
        foreach (var finding in result.AnalyzerResults)
            Console.WriteLine($"  Analyzer: {finding}");
    },
    error => Console.WriteLine($"Error: {error}"));

// Create a class
var classResult = codeTool.CreateClass(
    className: "PipelineProcessor",
    namespaceName: "LangChainPipeline.Generated",
    methods: new[] { "public async Task<Result<string, string>> ExecuteAsync(string input)" },
    properties: new[] { "string Name", "bool IsEnabled" });

classResult.Match(
    code => Console.WriteLine(code),
    error => Console.WriteLine($"Error: {error}"));

// Add method to existing class
var addResult = codeTool.AddMethodToClass(
    code: existingCode,
    className: "Calculator",
    methodSignature: "public int Multiply(int a, int b)",
    methodBody: "return a * b;");

// Rename symbol
var renameResult = codeTool.RenameSymbol(code, "oldName", "newName");

// Extract method
var extractResult = codeTool.ExtractMethod(code, startLine: 5, endLine: 8, newMethodName: "ExtractedMethod");

// Generate code from description
var generateResult = await codeTool.GenerateCodeFromDescriptionAsync(
    description: "Create a Result<T> monad with Success and Failure methods",
    codeContext: "Ouroboros functional programming project",
    llm: llm);
```

### MCP Server

```csharp
using LangChainPipeline.CLI.CodeGeneration;

var mcpServer = new McpServer(codeTool, assistant);

// List available tools
var tools = mcpServer.ListTools();
foreach (var tool in tools.Tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// Execute a tool
var parameters = new Dictionary<string, object>
{
    ["currentDsl"] = "SetTopic('test')",
    ["maxSuggestions"] = 5
};

var result = await mcpServer.ExecuteToolAsync("suggest_dsl_step", parameters);
if (result.Success)
{
    Console.WriteLine($"Result: {result.Data}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

## Available MCP Tools

1. **analyze_code** - Analyze C# code with Roslyn and custom analyzers
2. **create_class** - Generate a new C# class with specified structure
3. **add_method** - Add a method to an existing class
4. **rename_symbol** - Rename a symbol throughout the code
5. **extract_method** - Extract code into a new method
6. **suggest_dsl_step** - Suggest next DSL pipeline step
7. **complete_token** - Complete partial DSL token
8. **validate_dsl** - Validate DSL syntax and suggest fixes
9. **explain_dsl** - Explain DSL pipeline in natural language
10. **build_dsl** - Build DSL pipeline from high-level goal

## Custom Analyzers

### Monadic Pattern Analyzer
Checks for proper use of Result<T> monads:
```csharp
// ✓ Good
public async Task<Result<string, string>> ProcessAsync(string input)
{
    return Result<string, string>.Success(processed);
}

// ✗ Bad
public async Task<string> ProcessAsync(string input)
{
    return processed; // No error handling
}
```

### Async/Await Pattern Analyzer
Detects blocking async calls:
```csharp
// ✓ Good
var result = await AsyncMethod();

// ✗ Bad
var result = AsyncMethod().Result; // Blocking!
var result2 = AsyncMethod().Wait(); // Blocking!
```

### Documentation Completeness Analyzer
Checks for missing XML documentation:
```csharp
// ✓ Good
/// <summary>
/// Processes the input data.
/// </summary>
public void Process() { }

// ✗ Bad
public void Process() { } // Missing documentation
```

## Configuration

### Model Configuration
```bash
# Use specific model
dotnet run -- assist -m suggest -d "..." --model llama3

# Use remote endpoint
dotnet run -- assist -m suggest -d "..." \
  --endpoint https://api.ollama.com \
  --api-key your-key \
  --endpoint-type ollama-cloud

# Adjust generation parameters
dotnet run -- assist -m create -g "..." \
  --temperature 0.7 \
  --max-tokens 2000 \
  --timeout 120
```

### Environment Variables
```bash
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-api-key"
export CHAT_ENDPOINT_TYPE="ollama-cloud"
export MONADIC_DEBUG="1"
```

## Testing

Comprehensive simulation integration tests are included:

```bash
# Run all tests
cd src/Ouroboros.Tests
dotnet test

# Run specific feature
dotnet test --filter "FullyQualifiedName~DslAssistantSimulation"
```

Test coverage includes:
- 43 integration test scenarios
- DSL suggestions and completion
- Validation and explanation
- Code analysis and generation
- Refactoring operations
- Custom analyzers
- MCP server functionality
- End-to-end workflows

## Architecture

```
┌─────────────────────────────────────────────────────┐
│         GitHub Copilot-like DSL Assistant            │
└─────────────────────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
   DslAssistant    RoslynCodeTool   McpServer
        │               │               │
        ├─ Suggestions  ├─ Analysis    ├─ 10 Tools
        ├─ Completion   ├─ Generation  ├─ JSON Schema
        ├─ Validation   ├─ Refactoring ├─ Execution
        ├─ Explanation  ├─ Analyzers   └─ Protocol
        └─ Building     └─ Generators
                        
              All using Result<T> Monads
```

## Best Practices

### Error Handling
Always use Result<T> monads:
```csharp
var result = await assistant.SuggestNextStepAsync(dsl);
result.Match(
    suggestions => HandleSuccess(suggestions),
    error => HandleError(error));
```

### Resource Management
Dispose of resources properly:
```csharp
using var codeTool = new RoslynCodeTool();
var analysis = await codeTool.AnalyzeCodeAsync(code);
```

### Context Awareness
Provide rich context for better suggestions:
```csharp
var context = "Ouroboros uses functional programming with Result<T> monads";
var code = await codeTool.GenerateCodeFromDescriptionAsync(description, context, llm);
```

## Troubleshooting

### Common Issues

**Issue**: Build errors with Roslyn packages
```bash
# Solution: Restore packages
dotnet restore
dotnet build
```

**Issue**: LLM connection errors
```bash
# Solution: Check Ollama is running
ollama serve

# Or use remote endpoint
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-key"
```

**Issue**: Slow code generation
```bash
# Solution: Use faster model or adjust timeout
dotnet run -- assist -m create --model phi3:mini --timeout 60
```

## Future Enhancements

- [ ] IDE extensions (VS Code, Visual Studio)
- [ ] Real-time code completion in editors
- [ ] Multi-file refactoring
- [ ] Project-wide analysis
- [ ] Code smell detection
- [ ] Performance optimization suggestions
- [ ] Security vulnerability scanning

## Contributing

Contributions welcome! Please follow:
- Functional programming principles
- Result<T> monad usage
- Comprehensive testing
- XML documentation

## License

See repository LICENSE file.

## Related Documentation

- [Ouroboros README](../../README.md)
- [CLI Documentation](../Ouroboros.CLI/README.md)
- [Agent Architecture](../Ouroboros.Agent/README.md)
- [Test Specifications](../Ouroboros.Tests/README.md)
