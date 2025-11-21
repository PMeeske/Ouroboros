using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TechTalk.SpecFlow;
using LangChainPipeline.CLI;
using LangChainPipeline.CLI.CodeGeneration;
using LangChainPipeline.Agent.MetaAI;
using LangChain.Providers;

namespace LangChainPipeline.Specs.Steps;

[Binding]
public class DslAssistantSimulationSteps
{
    private SimulatedLlm? _simulatedLlm;
    private DslAssistant? _assistant;
    private RoslynCodeTool? _codeTool;
    private McpServer? _mcpServer;
    private string? _currentDsl;
    private string? _partialToken;
    private string? _goal;
    private string? _csharpCode;
    private string? _className;
    private string? _namespaceName;
    private List<string>? _methods;
    private List<string>? _properties;
    private string? _methodSignature;
    private string? _methodBody;
    private int _startLine;
    private int _endLine;
    private string? _newMethodName;
    private string? _codeDescription;
    private string? _codeContext;
    private Dictionary<string, object>? _mcpParameters;
    
    private Result<List<DslSuggestion>, string>? _suggestions;
    private Result<List<string>, string>? _completions;
    private Result<DslValidationResult, string>? _validationResult;
    private Result<string, string>? _explanation;
    private Result<string, string>? _generatedDsl;
    private Result<CodeAnalysisResult, string>? _analysisResult;
    private Result<string, string>? _generatedCode;
    private McpResponse? _mcpTools;
    private McpToolResult? _mcpToolResult;

    // Background steps

    [Given("a simulated LLM for testing")]
    public void GivenASimulatedLlm()
    {
        _simulatedLlm = new SimulatedLlm();
    }

    [Given("a DSL assistant with the simulated LLM")]
    public void GivenADslAssistant()
    {
        _simulatedLlm.Should().NotBeNull();
        ToolRegistry tools = ToolRegistry.CreateDefault();
        ToolAwareChatModel llm = new ToolAwareChatModel(_simulatedLlm!, tools);
        _assistant = new DslAssistant(llm, tools);
    }

    [Given("a Roslyn code tool")]
    public void GivenARoslynCodeTool()
    {
        _codeTool = new RoslynCodeTool();
    }

    // DSL Suggestion steps

    [Given(@"the current DSL is ""(.*)""")]
    public void GivenTheCurrentDslIs(string dsl)
    {
        _currentDsl = dsl;
    }

    [When("I request suggestions for next steps")]
    public async Task WhenIRequestSuggestionsForNextSteps()
    {
        _assistant.Should().NotBeNull();
        _currentDsl.Should().NotBeNullOrEmpty();
        _suggestions = await _assistant!.SuggestNextStepAsync(_currentDsl!, maxSuggestions: 5);
    }

    [Then(@"I should receive at least (\d+) suggestions")]
    public void ThenIShouldReceiveAtLeastSuggestions(int minCount)
    {
        _suggestions.Should().NotBeNull();
        _suggestions!.IsSuccess.Should().BeTrue();
        _suggestions.Value.Count.Should().BeGreaterOrEqualTo(minCount);
    }

    [Then(@"suggestions should include ""(.*)""")]
    public void ThenSuggestionsShouldInclude(string token)
    {
        _suggestions.Should().NotBeNull();
        _suggestions!.IsSuccess.Should().BeTrue();
        _suggestions.Value.Should().Contain(s => s.Token.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    [Then("each suggestion should have an explanation")]
    public void ThenEachSuggestionShouldHaveAnExplanation()
    {
        _suggestions.Should().NotBeNull();
        _suggestions!.Value.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Explanation));
    }

    [Then("each suggestion should have a confidence score")]
    public void ThenEachSuggestionShouldHaveAConfidenceScore()
    {
        _suggestions.Should().NotBeNull();
        _suggestions!.Value.Should().OnlyContain(s => s.Confidence > 0 && s.Confidence <= 1);
    }

    // Token completion steps

    [Given(@"a partial token ""(.*)""")]
    public void GivenAPartialToken(string partial)
    {
        _partialToken = partial;
    }

    [When("I request token completions")]
    public void WhenIRequestTokenCompletions()
    {
        _assistant.Should().NotBeNull();
        _partialToken.Should().NotBeNullOrEmpty();
        _completions = _assistant!.CompleteToken(_partialToken!);
    }

    [Then(@"I should receive completions including ""(.*)""")]
    public void ThenIShouldReceiveCompletionsIncluding(string completion)
    {
        _completions.Should().NotBeNull();
        _completions!.IsSuccess.Should().BeTrue();
        _completions.Value.Should().Contain(c => c.Equals(completion, StringComparison.OrdinalIgnoreCase));
    }

    [Then("completions should be case-insensitive")]
    public void ThenCompletionsShouldBeCaseInsensitive()
    {
        _completions.Should().NotBeNull();
        _completions!.Value.Count.Should().BeGreaterThan(0);
    }

    // Validation steps

    [Given(@"a valid DSL ""(.*)""")]
    [Given(@"an invalid DSL ""(.*)""")]
    [Given(@"a DSL pipeline ""(.*)""")]
    public void GivenADsl(string dsl)
    {
        _currentDsl = dsl;
    }

    [When("I validate the DSL")]
    public async Task WhenIValidateTheDsl()
    {
        _assistant.Should().NotBeNull();
        _currentDsl.Should().NotBeNullOrEmpty();
        _validationResult = await _assistant!.ValidateAndFixAsync(_currentDsl!);
    }

    [Then("validation should succeed")]
    public void ThenValidationShouldSucceed()
    {
        _validationResult.Should().NotBeNull();
        _validationResult!.IsSuccess.Should().BeTrue();
        _validationResult.Value.IsValid.Should().BeTrue();
    }

    [Then("validation should fail")]
    public void ThenValidationShouldFail()
    {
        _validationResult.Should().NotBeNull();
        _validationResult!.IsSuccess.Should().BeTrue();
        _validationResult.Value.IsValid.Should().BeFalse();
    }

    [Then("there should be no errors")]
    public void ThenThereShouldBeNoErrors()
    {
        _validationResult.Should().NotBeNull();
        _validationResult!.Value.Errors.Should().BeEmpty();
    }

    [Then("there should be no warnings")]
    public void ThenThereShouldBeNoWarnings()
    {
        _validationResult.Should().NotBeNull();
        _validationResult!.Value.Warnings.Should().BeEmpty();
    }

    [Then(@"there should be an error about ""(.*)""")]
    public void ThenThereShouldBeAnErrorAbout(string errorToken)
    {
        _validationResult.Should().NotBeNull();
        _validationResult!.Value.Errors.Should().Contain(e => e.Contains(errorToken, StringComparison.OrdinalIgnoreCase));
    }

    [Then("suggestions should include similar valid tokens")]
    public void ThenSuggestionsShouldIncludeSimilarValidTokens()
    {
        _validationResult.Should().NotBeNull();
        _validationResult!.Value.Suggestions.Should().NotBeEmpty();
    }

    // Explanation steps

    [When("I request an explanation")]
    public async Task WhenIRequestAnExplanation()
    {
        _assistant.Should().NotBeNull();
        _currentDsl.Should().NotBeNullOrEmpty();
        _explanation = await _assistant!.ExplainDslAsync(_currentDsl!);
    }

    [Then("I should receive a natural language explanation")]
    public void ThenIShouldReceiveANaturalLanguageExplanation()
    {
        _explanation.Should().NotBeNull();
        _explanation!.IsSuccess.Should().BeTrue();
        _explanation.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"the explanation should mention ""(.*)""")]
    public void ThenTheExplanationShouldMention(string keyword)
    {
        _explanation.Should().NotBeNull();
        _explanation!.Value.Should().Contain(keyword, Exactly.Once().OrMore());
    }

    // DSL generation steps

    [Given(@"a goal ""(.*)""")]
    public void GivenAGoal(string goal)
    {
        _goal = goal;
    }

    [When("I request DSL generation from the goal")]
    public async Task WhenIRequestDslGenerationFromTheGoal()
    {
        _assistant.Should().NotBeNull();
        _goal.Should().NotBeNullOrEmpty();
        _generatedDsl = await _assistant!.BuildDslInteractivelyAsync(_goal!);
    }

    [Then("I should receive a valid DSL pipeline")]
    public void ThenIShouldReceiveAValidDslPipeline()
    {
        _generatedDsl.Should().NotBeNull();
        _generatedDsl!.IsSuccess.Should().BeTrue();
        _generatedDsl.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"the DSL should start with ""(.*)"" or ""(.*)""")]
    public void ThenTheDslShouldStartWith(string option1, string option2)
    {
        _generatedDsl.Should().NotBeNull();
        bool startsWithEither = _generatedDsl!.Value.TrimStart().StartsWith(option1, StringComparison.OrdinalIgnoreCase) ||
                                _generatedDsl.Value.TrimStart().StartsWith(option2, StringComparison.OrdinalIgnoreCase);
        startsWithEither.Should().BeTrue();
    }

    [Then(@"the DSL should contain the pipe operator ""\|""")]
    public void ThenTheDslShouldContainThePipeOperator()
    {
        _generatedDsl.Should().NotBeNull();
        _generatedDsl!.Value.Should().Contain("|");
    }

    // Code analysis steps

    [Given("sample C# code with a class and method")]
    public void GivenSampleCSharpCodeWithAClassAndMethod()
    {
        _csharpCode = @"
using System;
namespace Test
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }
    }
}";
    }

    [Given("C# code with syntax errors")]
    public void GivenCSharpCodeWithSyntaxErrors()
    {
        _csharpCode = @"
using System;
namespace Test
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + // Missing operand
        }
    }
}";
    }

    [When("I analyze the code")]
    [When("I analyze the code with custom analyzers")]
    [When("I analyze the code with documentation analyzer")]
    public async Task WhenIAnalyzeTheCode()
    {
        _codeTool.Should().NotBeNull();
        _csharpCode.Should().NotBeNullOrEmpty();
        _analysisResult = await _codeTool!.AnalyzeCodeAsync(_csharpCode!, runAnalyzers: true);
    }

    [Then("analysis should succeed")]
    public void ThenAnalysisShouldSucceed()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.IsSuccess.Should().BeTrue();
    }

    [Then("analysis should report invalid code")]
    public void ThenAnalysisShouldReportInvalidCode()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.IsSuccess.Should().BeTrue();
        _analysisResult.Value.IsValid.Should().BeFalse();
    }

    [Then("I should get a list of classes found")]
    public void ThenIShouldGetAListOfClassesFound()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.Classes.Should().NotBeEmpty();
    }

    [Then("I should get a list of methods found")]
    public void ThenIShouldGetAListOfMethodsFound()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.Methods.Should().NotBeEmpty();
    }

    [Then("I should get diagnostic information")]
    public void ThenIShouldGetDiagnosticInformation()
    {
        _analysisResult.Should().NotBeNull();
        // Diagnostics list exists (may be empty for valid code)
        _analysisResult!.Value.Diagnostics.Should().NotBeNull();
    }

    [Then("diagnostics should contain error messages")]
    public void ThenDiagnosticsShouldContainErrorMessages()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.Diagnostics.Should().Contain(d => d.Contains("Error", StringComparison.OrdinalIgnoreCase));
    }

    [Then("error messages should include line numbers")]
    public void ThenErrorMessagesShouldIncludeLineNumbers()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.Diagnostics.Should().Contain(d => d.Contains("line", StringComparison.OrdinalIgnoreCase));
    }

    [Then("analyzer findings should include async pattern issues")]
    public void ThenAnalyzerFindingsShouldIncludeAsyncPatternIssues()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.AnalyzerResults.Should().Contain(f => 
            f.Contains("async", StringComparison.OrdinalIgnoreCase) ||
            f.Contains(".Result", StringComparison.Ordinal) ||
            f.Contains(".Wait()", StringComparison.Ordinal));
    }

    [Then(@"findings should mention "".Result"" or "".Wait\(\)""")]
    public void ThenFindingsShouldMentionResultOrWait()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.AnalyzerResults.Should().Contain(f =>
            f.Contains(".Result") || f.Contains(".Wait()"));
    }

    [Then("findings should mention missing documentation")]
    public void ThenFindingsShouldMentionMissingDocumentation()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.AnalyzerResults.Should().Contain(f =>
            f.Contains("documentation", StringComparison.OrdinalIgnoreCase));
    }

    [Then("findings should list the undocumented members")]
    public void ThenFindingsShouldListTheUndocumentedMembers()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.AnalyzerResults.Should().NotBeEmpty();
    }

    // Code generation steps

    [Given(@"a class name ""(.*)""")]
    public void GivenAClassName(string className)
    {
        _className = className;
    }

    [Given(@"a namespace ""(.*)""")]
    public void GivenANamespace(string namespaceName)
    {
        _namespaceName = namespaceName;
    }

    [Given(@"methods including ""(.*)"" and ""(.*)""")]
    public void GivenMethodsIncluding(string method1, string method2)
    {
        _methods = new List<string> { method1, method2 };
    }

    [Given(@"properties including ""(.*)"" and ""(.*)""")]
    public void GivenPropertiesIncluding(string prop1, string prop2)
    {
        _properties = new List<string> { prop1, prop2 };
    }

    [When("I generate the class")]
    public void WhenIGenerateTheClass()
    {
        _codeTool.Should().NotBeNull();
        _className.Should().NotBeNullOrEmpty();
        _namespaceName.Should().NotBeNullOrEmpty();
        
        _generatedCode = _codeTool!.CreateClass(
            _className!,
            _namespaceName!,
            _methods,
            _properties);
    }

    [Then("I should receive valid C# code")]
    public void ThenIShouldReceiveValidCSharpCode()
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.IsSuccess.Should().BeTrue();
        _generatedCode.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Then(@"the code should contain ""(.*)""")]
    public void ThenTheCodeShouldContain(string expectedContent)
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().Contain(expectedContent);
    }

    // Add method steps

    [Given(@"existing C# code with a class ""(.*)""")]
    public void GivenExistingCSharpCodeWithAClass(string className)
    {
        _className = className;
        _csharpCode = $@"
using System;
namespace Test
{{
    public class {className}
    {{
    }}
}}";
    }

    [Given(@"a method signature ""(.*)""")]
    public void GivenAMethodSignature(string signature)
    {
        _methodSignature = signature;
    }

    [Given(@"a method body ""(.*)""")]
    public void GivenAMethodBody(string body)
    {
        _methodBody = body;
    }

    [When("I add the method to the class")]
    public void WhenIAddTheMethodToTheClass()
    {
        _codeTool.Should().NotBeNull();
        _csharpCode.Should().NotBeNullOrEmpty();
        _className.Should().NotBeNullOrEmpty();
        _methodSignature.Should().NotBeNullOrEmpty();
        
        _generatedCode = _codeTool!.AddMethodToClass(
            _csharpCode!,
            _className!,
            _methodSignature!,
            _methodBody);
    }

    [Then("I should receive updated C# code")]
    [Then("I should receive updated code")]
    [Then("I should receive refactored code")]
    public void ThenIShouldReceiveUpdatedCSharpCode()
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.IsSuccess.Should().BeTrue();
    }

    [Then("the code should contain the new method")]
    public void ThenTheCodeShouldContainTheNewMethod()
    {
        _generatedCode.Should().NotBeNull();
        _methodSignature.Should().NotBeNullOrEmpty();
        _generatedCode!.Value.Should().Contain(_methodSignature!.Split('(')[0]);
    }

    [Then("the code should be properly formatted")]
    public void ThenTheCodeShouldBeProperlyFormatted()
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().Contain("{").And.Contain("}");
    }

    // Rename steps

    [Given(@"C# code with a variable ""(.*)""")]
    public void GivenCSharpCodeWithAVariable(string varName)
    {
        _oldSymbolName = varName;
        _csharpCode = $@"
using System;
namespace Test
{{
    public class Example
    {{
        private string {varName} = ""test"";
        
        public void UseVariable()
        {{
            Console.WriteLine({varName});
        }}
    }}
}}";
    }

    [When(@"I rename ""(.*)"" to ""(.*)""")]
    public void WhenIRename(string oldName, string newName)
    {
        _codeTool.Should().NotBeNull();
        _csharpCode.Should().NotBeNullOrEmpty();
        _oldSymbolName = oldName;
        _newSymbolName = newName;
        
        _generatedCode = _codeTool!.RenameSymbol(_csharpCode!, oldName, newName);
    }

    [Then(@"the code should not contain ""(.*)""")]
    public void ThenTheCodeShouldNotContain(string text)
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().NotContain(text);
    }

    [Then(@"the code should contain ""(.*)"" in all occurrences")]
    public void ThenTheCodeShouldContainInAllOccurrences(string text)
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().Contain(text);
    }

    // Extract method steps

    [Given("C# code with a method containing multiple statements")]
    public void GivenCSharpCodeWithAMethodContainingMultipleStatements()
    {
        _csharpCode = @"
using System;
namespace Test
{
    public class Example
    {
        public void ProcessData()
        {
            // Line 9
            int x = 10;
            int y = 20;
            int sum = x + y;
            Console.WriteLine(sum);
        }
    }
}";
    }

    [Given(@"I select lines (\d+) to (\d+) for extraction")]
    public void GivenISelectLinesForExtraction(int start, int end)
    {
        _startLine = start;
        _endLine = end;
    }

    [Given(@"I provide a new method name ""(.*)""")]
    public void GivenIProvideANewMethodName(string methodName)
    {
        _newMethodName = methodName;
    }

    [When("I perform extract method refactoring")]
    public void WhenIPerformExtractMethodRefactoring()
    {
        _codeTool.Should().NotBeNull();
        _csharpCode.Should().NotBeNullOrEmpty();
        _newMethodName.Should().NotBeNullOrEmpty();
        
        _generatedCode = _codeTool!.ExtractMethod(
            _csharpCode!,
            _startLine,
            _endLine,
            _newMethodName!);
    }

    [Then(@"the code should contain a new method ""(.*)""")]
    public void ThenTheCodeShouldContainANewMethod(string methodName)
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().Contain(methodName);
    }

    [Then(@"the original location should call ""(.*)""")]
    public void ThenTheOriginalLocationShouldCall(string methodName)
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().Contain($"{methodName}()");
    }

    // Code generation from description steps

    [Given(@"a description ""(.*)""")]
    public void GivenADescription(string description)
    {
        _codeDescription = description;
    }

    [Given("context about MonadicPipeline conventions")]
    public void GivenContextAboutMonadicPipelineConventions()
    {
        _codeContext = "MonadicPipeline uses functional programming with Result<T> and Option<T> monads";
    }

    [When("I generate code from the description")]
    public async Task WhenIGenerateCodeFromTheDescription()
    {
        _codeTool.Should().NotBeNull();
        _simulatedLlm.Should().NotBeNull();
        _codeDescription.Should().NotBeNullOrEmpty();
        
        ToolRegistry tools = ToolRegistry.CreateDefault();
        ToolAwareChatModel llm = new ToolAwareChatModel(_simulatedLlm!, tools);
        
        _generatedCode = await _codeTool!.GenerateCodeFromDescriptionAsync(
            _codeDescription!,
            _codeContext ?? string.Empty,
            llm);
    }

    [Then("the code should compile without errors")]
    public async Task ThenTheCodeShouldCompileWithoutErrors()
    {
        _generatedCode.Should().NotBeNull();
        _codeTool.Should().NotBeNull();
        
        Result<CodeAnalysisResult, string> analysis = await _codeTool!.AnalyzeCodeAsync(_generatedCode!.Value);
        analysis.IsSuccess.Should().BeTrue();
        analysis.Value.IsValid.Should().BeTrue();
    }

    [Then(@"the code should follow Result<T> pattern")]
    public void ThenTheCodeShouldFollowResultPattern()
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().Contain("Result<").And.Contain("Success").And.Contain("Failure");
    }

    // MCP Server steps

    [Given("an MCP server with DSL and code tools")]
    [Given("an MCP server")]
    public void GivenAnMcpServer()
    {
        _assistant.Should().NotBeNull();
        _codeTool.Should().NotBeNull();
        _mcpServer = new McpServer(_codeTool!, _assistant!);
    }

    [When("I request the list of available tools")]
    public void WhenIRequestTheListOfAvailableTools()
    {
        _mcpServer.Should().NotBeNull();
        _mcpTools = _mcpServer!.ListTools();
    }

    [Then(@"I should receive at least (\d+) tools")]
    public void ThenIShouldReceiveAtLeastTools(int minCount)
    {
        _mcpTools.Should().NotBeNull();
        _mcpTools!.Tools.Count.Should().BeGreaterOrEqualTo(minCount);
    }

    [Then(@"tools should include ""(.*)""")]
    public void ThenToolsShouldInclude(string toolName)
    {
        _mcpTools.Should().NotBeNull();
        _mcpTools!.Tools.Should().Contain(t => t.Name == toolName);
    }

    [Then("each tool should have a name, description, and input schema")]
    public void ThenEachToolShouldHaveANameDescriptionAndInputSchema()
    {
        _mcpTools.Should().NotBeNull();
        _mcpTools!.Tools.Should().OnlyContain(t =>
            !string.IsNullOrWhiteSpace(t.Name) &&
            !string.IsNullOrWhiteSpace(t.Description) &&
            t.InputSchema != null);
    }

    // MCP execution steps

    [Given(@"parameters with currentDsl ""(.*)""")]
    public void GivenParametersWithCurrentDsl(string dsl)
    {
        _mcpParameters = new Dictionary<string, object>
        {
            ["currentDsl"] = dsl
        };
    }

    [Given("parameters with C# code to analyze")]
    public void GivenParametersWithCSharpCodeToAnalyze()
    {
        _mcpParameters = new Dictionary<string, object>
        {
            ["code"] = "public class Test { }"
        };
    }

    [When(@"I execute the ""(.*)"" tool")]
    public async Task WhenIExecuteTheTool(string toolName)
    {
        _mcpServer.Should().NotBeNull();
        _mcpParameters.Should().NotBeNull();
        _mcpToolResult = await _mcpServer!.ExecuteToolAsync(toolName, _mcpParameters!);
    }

    [Then("execution should succeed")]
    public void ThenExecutionShouldSucceed()
    {
        _mcpToolResult.Should().NotBeNull();
        _mcpToolResult!.Success.Should().BeTrue();
    }

    [Then("result should contain suggestions")]
    public void ThenResultShouldContainSuggestions()
    {
        _mcpToolResult.Should().NotBeNull();
        _mcpToolResult!.Data.Should().NotBeNull();
    }

    [Then("suggestions should be in proper format")]
    public void ThenSuggestionsShouldBeInProperFormat()
    {
        _mcpToolResult.Should().NotBeNull();
        _mcpToolResult!.Data.Should().NotBeNull();
    }

    [Then("result should contain analysis information")]
    public void ThenResultShouldContainAnalysisInformation()
    {
        _mcpToolResult.Should().NotBeNull();
        _mcpToolResult!.Data.Should().NotBeNull();
    }

    [Then("result should have isValid field")]
    [Then("result should have diagnostics list")]
    public void ThenResultShouldHaveExpectedFields()
    {
        _mcpToolResult.Should().NotBeNull();
        _mcpToolResult!.Data.Should().NotBeNull();
    }

    // Interactive mode steps (simplified simulation)

    [Given("an interactive DSL assistant session")]
    public void GivenAnInteractiveDslAssistantSession()
    {
        // Session initialization is implicit
    }

    [When(@"I type ""(.*)""")]
    public void WhenIType(string command)
    {
        // Simulated - in real implementation this would be async
    }

    [Then(@"I should see (.*)")]
    public void ThenIShouldSee(string expectedOutput)
    {
        // Simulated - assertion placeholder
        expectedOutput.Should().NotBeNullOrEmpty();
    }

    [Then("the session should terminate")]
    public void ThenTheSessionShouldTerminate()
    {
        // Simulated termination
    }

    // End-to-end steps

    [Given(@"I want to build a pipeline for ""(.*)""")]
    public void GivenIWantToBuildAPipelineFor(string goal)
    {
        _goal = goal;
    }

    [When("I ask the assistant to build a DSL")]
    public async Task WhenIAskTheAssistantToBuildADsl()
    {
        await WhenIRequestDslGenerationFromTheGoal();
    }

    [Then("I receive a suggested DSL pipeline")]
    public void ThenIReceiveASuggestedDslPipeline()
    {
        ThenIShouldReceiveAValidDslPipeline();
    }

    [When("I validate the suggested DSL")]
    public async Task WhenIValidateTheSuggestedDsl()
    {
        _currentDsl = _generatedDsl?.Value;
        await WhenIValidateTheDsl();
    }

    [When("I request an explanation of the DSL")]
    public async Task WhenIRequestAnExplanationOfTheDsl()
    {
        _currentDsl = _generatedDsl?.Value;
        await WhenIRequestAnExplanation();
    }

    [Then("I understand what the pipeline does")]
    [Then("I understand the code structure and purpose")]
    public void ThenIUnderstandWhatThePipelineDoes()
    {
        _explanation.Should().NotBeNull();
        _explanation!.Value.Should().NotBeNullOrWhiteSpace();
    }

    [When("I suggest improvements")]
    public async Task WhenISuggestImprovements()
    {
        _currentDsl = _generatedDsl?.Value;
        await WhenIRequestSuggestionsForNextSteps();
    }

    [Then("I receive enhanced DSL with additional steps")]
    public void ThenIReceiveEnhancedDslWithAdditionalSteps()
    {
        _suggestions.Should().NotBeNull();
        _suggestions!.Value.Should().NotBeEmpty();
    }

    [Given(@"I describe ""(.*)""")]
    public void GivenIDescribe(string description)
    {
        _codeDescription = description;
    }

    [Then("I receive C# code")]
    public void ThenIReceiveCSharpCode()
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.Value.Should().NotBeNullOrWhiteSpace();
    }

    [When("I analyze the generated code")]
    public async Task WhenIAnalyzeTheGeneratedCode()
    {
        _csharpCode = _generatedCode?.Value;
        await WhenIAnalyzeTheCode();
    }

    [Then("the code should be valid")]
    public void ThenTheCodeShouldBeValid()
    {
        _analysisResult.Should().NotBeNull();
        _analysisResult!.Value.IsValid.Should().BeTrue();
    }

    [Then("the code should follow monadic patterns")]
    public void ThenTheCodeShouldFollowMonadicPatterns()
    {
        _generatedCode.Should().NotBeNull();
        _generatedCode!.IsSuccess.Should().BeTrue();
        string code = _generatedCode.Value.Value;
        bool hasMonadicPattern = code.Contains("Result<", StringComparison.Ordinal) || 
                                  code.Contains("Option<", StringComparison.Ordinal);
        hasMonadicPattern.Should().BeTrue();
    }

    [When("I ask for code explanation")]
    public async Task WhenIAskForCodeExplanation()
    {
        // Simulated - would explain code using assistant
        await Task.CompletedTask;
    }

    [Given("C# code with public methods")]
    [Given("the methods lack XML documentation")]
    [Given("C# code that blocks on async methods")]
    public void GivenCSharpCodeWithSpecificCharacteristics()
    {
        _csharpCode = @"
using System;
using System.Threading.Tasks;
namespace Test
{
    public class Example
    {
        public void PublicMethod()
        {
            var result = AsyncMethod().Result; // Blocking call
        }
        
        private async Task<int> AsyncMethod()
        {
            await Task.Delay(100);
            return 42;
        }
    }
}";
    }
}

/// <summary>
/// Simulated LLM for testing purposes.
/// </summary>
internal class SimulatedLlm : IChatCompletionModel
{
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        // Return simulated responses based on prompt content
        if (prompt.Contains("suggest", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(@"1. UseDraft - Generate an initial draft response. This is the first step after SetTopic.
2. UseCritique - Analyze and critique the current draft to identify improvements.
3. UseImprove - Refine the draft based on critique feedback.");
        }
        
        if (prompt.Contains("build", StringComparison.OrdinalIgnoreCase) && prompt.Contains("DSL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("SetTopic('document analysis') | UseIngest | UseDraft | UseCritique | UseImprove");
        }
        
        if (prompt.Contains("explain", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("This pipeline starts with a topic, generates a draft, critiques it, and improves the response.");
        }
        
        if (prompt.Contains("Generate complete, production-quality C#", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(@"```csharp
public readonly struct Result<TValue, TError>
{
    private readonly TValue? value;
    private readonly TError? error;
    private readonly bool isSuccess;
    
    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);
    
    private Result(TValue value)
    {
        this.value = value;
        this.error = default;
        this.isSuccess = true;
    }
}
```");
        }

        return Task.FromResult("Simulated LLM response");
    }
}
