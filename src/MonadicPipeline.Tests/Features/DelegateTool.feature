Feature: Delegate Tool
    As a developer
    I want to create tools from delegate functions
    So that I can quickly wrap functionality in the tool interface

    Background:
        Given a fresh delegate tool context

    Scenario: Create tool with valid parameters
        When I create a delegate tool named "test" with description "description"
        Then the tool name should be "test"
        And the tool description should be "description"
        And the tool schema should be null

    Scenario: Create tool with schema
        When I create a delegate tool with schema
        Then the tool schema should be stored

    Scenario: Create tool with null name throws exception
        When I attempt to create a delegate tool with null name
        Then it should throw ArgumentNullException

    Scenario: Create tool with null description throws exception
        When I attempt to create a delegate tool with null description
        Then it should throw ArgumentNullException

    Scenario: Create tool with null executor throws exception
        When I attempt to create a delegate tool with null executor
        Then it should throw ArgumentNullException

    Scenario: InvokeAsync executes delegate
        Given a delegate tool that processes input
        When I invoke it with "input"
        Then the result should be successful
        And the result value should be "processed: input"

    Scenario: InvokeAsync with failure returns failure
        Given a delegate tool that fails
        When I invoke it with "input"
        Then the result should be a failure
        And the error message should be "error occurred"

    Scenario: InvokeAsync passes cancellation token
        Given a delegate tool that captures cancellation token
        When I invoke it with a cancellation token
        Then the cancellation token should be passed correctly

    Scenario: Create tool with async func
        When I create a delegate tool with async func
        Then the tool should be created successfully

    Scenario: InvokeAsync with async func returns success
        Given a delegate tool with async func
        When I invoke it with "data"
        Then the result should be successful
        And the result value should be "processed: data"

    Scenario: InvokeAsync with async func throwing exception returns failure
        Given a delegate tool with throwing async func
        When I invoke it with "input"
        Then the result should be a failure
        And the error message should contain "test error"

    Scenario: Create tool with sync func
        When I create a delegate tool with sync func
        Then the tool should be created successfully

    Scenario: InvokeAsync with sync func returns success
        Given a delegate tool with sync func
        When I invoke it with "data"
        Then the result should be successful
        And the result value should be "processed: data"

    Scenario: InvokeAsync with sync func throwing exception returns failure
        Given a delegate tool with throwing sync func
        When I invoke it with "input"
        Then the result should be a failure
        And the error message should contain "sync error"

    Scenario: FromJson creates tool with schema
        When I create a tool using FromJson
        Then the tool should have a schema

    Scenario: FromJson with valid JSON executes function
        Given a FromJson tool
        When I invoke it with valid JSON
        Then the result should be successful
        And the result should contain the parsed value

    Scenario: FromJson with invalid JSON returns failure
        Given a FromJson tool
        When I invoke it with invalid JSON
        Then the result should be a failure
        And the error message should contain "JSON parse failed"

    Scenario: FromJson when function throws returns failure
        Given a FromJson tool that throws
        When I invoke it with valid JSON
        Then the result should be a failure
        And the error message should contain "JSON parse failed"
