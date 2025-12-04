Feature: DSL Assistant Simulation Integration Tests
  As a developer using Ouroboros CLI
  I want AI-powered DSL assistance with code generation
  So that I can build pipelines efficiently like GitHub Copilot

  Background:
    Given a simulated LLM for testing
    And a DSL assistant with the simulated LLM
    And a Roslyn code tool

  Scenario: Suggest next DSL steps based on current pipeline
    Given the current DSL is "SetTopic('AI Ethics')"
    When I request suggestions for next steps
    Then I should receive at least 3 suggestions
    And suggestions should include "UseDraft"
    And suggestions should include "UseCritique"
    And each suggestion should have an explanation
    And each suggestion should have a confidence score

  Scenario: Complete partial DSL token
    Given a partial token "UseD"
    When I request token completions
    Then I should receive completions including "UseDraft"
    And I should receive completions including "UseDir"
    And completions should be case-insensitive

  Scenario: Validate valid DSL
    Given a valid DSL "SetTopic('test') | UseDraft | UseCritique"
    When I validate the DSL
    Then validation should succeed
    And there should be no errors
    And there should be no warnings

  Scenario: Validate invalid DSL with unknown token
    Given an invalid DSL "SetTopic('test') | UnknownToken | UseDraft"
    When I validate the DSL
    Then validation should fail
    And there should be an error about "UnknownToken"
    And suggestions should include similar valid tokens

  Scenario: Explain DSL pipeline in natural language
    Given a DSL pipeline "SetTopic('AI') | UseDraft | UseCritique | UseImprove"
    When I request an explanation
    Then I should receive a natural language explanation
    And the explanation should mention "draft"
    And the explanation should mention "critique"
    And the explanation should mention "improve"

  Scenario: Build DSL from natural language goal
    Given a goal "Create a pipeline to analyze code quality"
    When I request DSL generation from the goal
    Then I should receive a valid DSL pipeline
    And the DSL should start with "SetTopic" or "SetPrompt"
    And the DSL should contain the pipe operator "|"

  Scenario: Analyze C# code with Roslyn
    Given sample C# code with a class and method
    When I analyze the code
    Then analysis should succeed
    And I should get a list of classes found
    And I should get a list of methods found
    And I should get diagnostic information

  Scenario: Analyze C# code with syntax errors
    Given C# code with syntax errors
    When I analyze the code
    Then analysis should report invalid code
    And diagnostics should contain error messages
    And error messages should include line numbers

  Scenario: Create a new C# class with Roslyn
    Given a class name "TestPipeline"
    And a namespace "LangChainPipeline.Generated"
    And methods including "ExecuteAsync" and "ValidateAsync"
    And properties including "string Name" and "bool IsValid"
    When I generate the class
    Then I should receive valid C# code
    And the code should contain "public class TestPipeline"
    And the code should contain "namespace LangChainPipeline.Generated"
    And the code should contain "ExecuteAsync"
    And the code should contain "ValidateAsync"

  Scenario: Add method to existing C# class
    Given existing C# code with a class "Calculator"
    And a method signature "public int Add(int a, int b)"
    And a method body "return a + b;"
    When I add the method to the class
    Then I should receive updated C# code
    And the code should contain the new method
    And the code should be properly formatted

  Scenario: Rename symbol in C# code
    Given C# code with a variable "oldName"
    When I rename "oldName" to "newName"
    Then I should receive updated code
    And the code should not contain "oldName"
    And the code should contain "newName" in all occurrences

  Scenario: Extract method refactoring
    Given C# code with a method containing multiple statements
    And I select lines 5 to 8 for extraction
    And I provide a new method name "ExtractedMethod"
    When I perform extract method refactoring
    Then I should receive refactored code
    And the code should contain a new method "ExtractedMethod"
    And the original location should call "ExtractedMethod"

  Scenario: Generate code from natural language description
    Given a description "Create a Result<T> monad with Success and Failure methods"
    And context about Ouroboros conventions
    When I generate code from the description
    Then I should receive valid C# code
    And the code should compile without errors
    And the code should follow Result<T> pattern

  Scenario: Run custom analyzers on code
    Given C# code that blocks on async methods
    When I analyze the code with custom analyzers
    Then analyzer findings should include async pattern issues
    And findings should mention ".Result" or ".Wait()"

  Scenario: Detect missing XML documentation
    Given C# code with public methods
    And the methods lack XML documentation
    When I analyze the code with documentation analyzer
    Then findings should mention missing documentation
    And findings should list the undocumented members

  Scenario: MCP Server lists available tools
    Given an MCP server with DSL and code tools
    When I request the list of available tools
    Then I should receive at least 10 tools
    And tools should include "analyze_code"
    And tools should include "suggest_dsl_step"
    And tools should include "create_class"
    And each tool should have a name, description, and input schema

  Scenario: MCP Server executes DSL suggestion tool
    Given an MCP server
    And parameters with currentDsl "SetTopic('test')"
    When I execute the "suggest_dsl_step" tool
    Then execution should succeed
    And result should contain suggestions
    And suggestions should be in proper format

  Scenario: MCP Server executes code analysis tool
    Given an MCP server
    And parameters with C# code to analyze
    When I execute the "analyze_code" tool
    Then execution should succeed
    And result should contain analysis information
    And result should have isValid field
    And result should have diagnostics list

  Scenario: Interactive DSL assistant mode simulation
    Given an interactive DSL assistant session
    When I type "suggest SetTopic('AI')"
    Then I should see suggestions for next steps
    When I type "complete Use"
    Then I should see token completions starting with "Use"
    When I type "help"
    Then I should see available commands
    When I type "exit"
    Then the session should terminate

  Scenario: End-to-end DSL pipeline building with assistance
    Given I want to build a pipeline for "document analysis"
    When I ask the assistant to build a DSL
    Then I receive a suggested DSL pipeline
    When I validate the suggested DSL
    Then validation should succeed
    When I request an explanation of the DSL
    Then I understand what the pipeline does
    When I suggest improvements
    Then I receive enhanced DSL with additional steps

  Scenario: Code generation with validation cycle
    Given I describe "a class with async methods returning Result<T>"
    When I generate code from the description
    Then I receive C# code
    When I analyze the generated code
    Then the code should be valid
    And the code should follow monadic patterns
    When I ask for code explanation
    Then I understand the code structure and purpose
