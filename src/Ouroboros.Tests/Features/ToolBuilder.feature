Feature: Tool Builder
    As a developer
    I want to compose tools using builder patterns
    So that I can create sophisticated tool chains and conditional logic

    Background:
        Given a fresh tool builder context

    Scenario: Chain tools sequentially
        Given a chain of "uppercase" and "exclaim" tools
        When I invoke the chain with "monad"
        Then the result should be successful
        And the result value should be "MONAD!"

    Scenario: Chain stops when tool fails
        Given a chain with a failing first tool and a second tool
        When I invoke the chain with "input"
        Then the result should be a failure
        And the error message should be "fail"
        And the second tool should not have been invoked

    Scenario: Chain handles cancellation token
        Given a simple chain tool
        When I invoke it with a cancelled token
        Then the result should be a failure
        And the error message should be "Operation cancelled"

    Scenario: First success returns first successful result
        Given a FirstSuccess tool with "fail", "ok", and "skip" tools
        When I invoke it with "input"
        Then the result should be successful
        And the result value should be "input-ok"

    Scenario: First success returns failure when all fail
        Given a FirstSuccess tool where all tools fail
        When I invoke it with "input"
        Then the result should be a failure
        And the error message should be "All tools failed"

    Scenario: Conditional tool selects based on input
        Given a conditional tool that selects based on input
        When I invoke it with "upper"
        Then the result should be successful
        And the result value should be "UPPER"

    Scenario: Conditional tool selects lowercase
        Given a conditional tool that selects based on input
        When I invoke it with "lower"
        Then the result should be successful
        And the result value should be "lower"

    Scenario: Conditional tool handles selector exception
        Given a conditional tool with a throwing selector
        When I invoke it with "anything"
        Then the result should be a failure
        And the error message should contain "Tool selection failed"
        And the error message should contain "boom"
