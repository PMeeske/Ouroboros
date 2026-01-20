Feature: Tool-Aware Chat Model
    As a developer
    I want a chat model that can detect and execute tool calls
    So that I can build AI systems that use tools

    Background:
        Given I have a tool registry

    Scenario: Generate with no tools returns text only
        Given I have a mock chat model that responds with "This is a plain response without tools."
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools using prompt "test prompt"
        Then the response text should be "This is a plain response without tools."
        And the tool executions should be empty

    Scenario: Generate with math tool executes tool
        Given I have a mock chat model that responds with "Let me calculate: [TOOL:math 2+2]"
        And I register the math tool
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools using prompt "Calculate 2+2"
        Then the response text should contain "[TOOL:math 2+2]"
        And the response text should contain "[TOOL-RESULT:math] 4"
        And the tool executions should have 1 execution
        And the first tool execution name should be "math"
        And the first tool execution arguments should be "2+2"
        And the first tool execution output should be "4"

    Scenario: Tool not found returns error
        Given I have a mock chat model that responds with "[TOOL:nonexistent some args]"
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools using prompt "test"
        Then the response text should contain "[TOOL-RESULT:nonexistent] error: tool not found"
        And the tool executions should be empty

    Scenario: Multiple tools execute all
        Given I have a mock chat model that responds with "First: [TOOL:math 5*3]\nThen: [TOOL:math 10-2]"
        And I register the math tool
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools using prompt "test"
        Then the tool executions should have 2 executions
        And tool execution 0 name should be "math"
        And tool execution 0 output should be "15"
        And tool execution 1 name should be "math"
        And tool execution 1 output should be "8"

    Scenario: Tool with no args handles gracefully
        Given I have a mock chat model that responds with "[TOOL:math]"
        And I register the math tool
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools using prompt "test"
        Then the response text should contain "[TOOL-RESULT:math]"
        And the tool executions should have 1 execution

    Scenario: Generate with tools result returns success
        Given I have a mock chat model that responds with "Response: [TOOL:math 7+8]"
        And I register the math tool
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools result using prompt "test"
        Then the result should be successful
        And the result text should contain "[TOOL-RESULT:math] 15"
        And the result tool executions should have 1 execution

    Scenario: Model throws returns failure
        Given I have a throwing chat model
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools result using prompt "test"
        Then the result should be a failure
        And the result error should contain "Tool-aware generation failed"

    Scenario: Tool throws captures error
        Given I have a mock chat model that responds with "[TOOL:throwing_tool test]"
        And I register the throwing tool
        And I have a tool-aware chat model with the mock model and registry
        When I generate with tools using prompt "test"
        Then the response text should contain "[TOOL-RESULT:throwing_tool] error:"
        And the tool executions should have 1 execution
        And the first tool execution output should contain "error:"

    Scenario: Cancellation requested propagates token
        Given I have a mock chat model that responds with "[TOOL:math 1+1]"
        And I register the math tool
        And I have a tool-aware chat model with the mock model and registry
        And I have a cancelled cancellation token
        When I generate with tools using prompt "test" with cancellation token
        Then an OperationCanceledException should be thrown
