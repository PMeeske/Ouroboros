Feature: Distributed Tracing
    As a developer
    I want to trace operations with distributed tracing
    So that I can monitor and debug distributed systems

    Background:
        Given tracing is disabled initially

    Scenario: Start activity should create activity
        Given tracing is enabled
        When I start an activity named "test_operation"
        Then the activity should not be null
        And the activity operation name should be "test_operation"

    Scenario: Start activity with tags should add tags
        Given tracing is enabled
        And I have tags with key1 "value1" and key2 42
        When I start an activity named "test_operation" with tags
        Then the activity should not be null
        And the activity should have at least one tag

    Scenario: Record event should add event to activity
        Given tracing is enabled
        And I start an activity named "test_operation"
        When I record an event "test_event" with detail "info"
        Then the activity should have 1 event
        And the first event name should be "test_event"

    Scenario: Record exception should set error status
        Given tracing is enabled
        And I start an activity named "test_operation"
        When I record an exception with message "Test error"
        Then the activity status should be "Error"
        And the activity should have tag "exception.type"
        And the activity should have tag "exception.message" with value "Test error"

    Scenario: Set status should update activity status
        Given tracing is enabled
        And I start an activity named "test_operation"
        When I set activity status to "Ok" with description "Success"
        Then the activity status should be "Ok"

    Scenario: Add tag should add tag to activity
        Given tracing is enabled
        And I start an activity named "test_operation"
        When I add tag "custom_key" with value "custom_value"
        Then the activity should have tag "custom_key" with value "custom_value"

    Scenario: Get trace ID should return current trace ID
        Given tracing is enabled
        And I start an activity named "test_operation"
        When I get the trace ID
        Then the trace ID should not be null
        And the trace ID should not be empty

    Scenario: Get span ID should return current span ID
        Given tracing is enabled
        And I start an activity named "test_operation"
        When I get the span ID
        Then the span ID should not be null
        And the span ID should not be empty

    Scenario: Trace tool execution should create activity with tool tags
        Given tracing is enabled
        When I trace tool execution for "test_tool" with input "input data"
        Then the activity should not be null
        And the activity operation name should contain "test_tool"
        And the activity should have tag "tool.name" with value "test_tool"

    Scenario: Trace pipeline execution should create activity with pipeline tags
        Given tracing is enabled
        When I trace pipeline execution for "test_pipeline"
        Then the activity should not be null
        And the activity operation name should contain "test_pipeline"
        And the activity should have tag "pipeline.name" with value "test_pipeline"

    Scenario: Trace LLM request should create activity with LLM tags
        Given tracing is enabled
        When I trace LLM request for model "gpt-4" with max tokens 500
        Then the activity should not be null
        And the activity operation name should be "llm.request"
        And the activity should have tag "llm.model" with value "gpt-4"

    Scenario: Trace vector operation should create activity with vector tags
        Given tracing is enabled
        When I trace vector operation "search" with dimension 10
        Then the activity should not be null
        And the activity operation name should contain "search"
        And the activity should have tag "vector.operation" with value "search"

    Scenario: Complete LLM request should add completion tags
        Given tracing is enabled
        And I trace LLM request for model "gpt-4" with max tokens 500
        When I complete the LLM request with response length 1000 and token count 150
        Then the activity status should be "Ok"

    Scenario: Complete tool execution with success should set OK status
        Given tracing is enabled
        And I trace tool execution for "test_tool" with input "input"
        When I complete the tool execution successfully with output length 200
        Then the activity status should be "Ok"

    Scenario: Complete tool execution with failure should set error status
        Given tracing is enabled
        And I trace tool execution for "test_tool" with input "input"
        When I complete the tool execution with failure and output length 0
        Then the activity status should be "Error"

    Scenario: Nested activities should maintain parent-child relationship
        Given tracing is enabled
        When I start an activity named "parent"
        And I capture the parent activity ID
        And I start an activity named "child"
        Then the child activity parent ID should match the captured parent ID

    Scenario: Activity listener should receive callbacks
        Given tracing is enabled with activity callbacks
        When I start and complete an activity named "test"
        Then the started callback count should be 1
        And the stopped callback count should be 1

    Scenario: Disable tracing should stop creating activities
        Given tracing is enabled
        When I start an activity named "test1"
        Then the activity should not be null
        When I disable tracing
        And I start an activity named "test2"
        Then the second activity should be null
