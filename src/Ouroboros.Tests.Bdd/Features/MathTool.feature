Feature: Math Tool
    As a developer
    I want to evaluate arithmetic expressions using a math tool
    So that I can perform calculations within the pipeline

    Background:
        Given a fresh math tool context

    Scenario: Tool name is math
        When I create a math tool
        Then the tool name should be "math"

    Scenario: Tool has valid description
        When I create a math tool
        Then the tool description should mention arithmetic

    Scenario: Tool schema is null
        When I create a math tool
        Then the tool schema should be null

    Scenario: Simple addition
        Given a math tool
        When I evaluate "2+2"
        Then the result should be successful
        And the result value should be "4"

    Scenario: Multiplication
        Given a math tool
        When I evaluate "5*3"
        Then the result should be successful
        And the result value should be "15"

    Scenario: Complex expression with order of operations
        Given a math tool
        When I evaluate "2+2*5"
        Then the result should be successful
        And the result value should be "12"

    Scenario: Expression with parentheses
        Given a math tool
        When I evaluate "(10-5)/2"
        Then the result should be successful
        And the result value should be "2.5"

    Scenario: Empty string returns failure
        Given a math tool
        When I evaluate empty string
        Then the result should be a failure
        And the error should mention empty

    Scenario: Whitespace returns failure
        Given a math tool
        When I evaluate whitespace
        Then the result should be a failure
        And the error should mention empty

    Scenario: Invalid expression returns failure
        Given a math tool
        When I evaluate "invalid"
        Then the result should be a failure
        And the error should contain "Math evaluation failed"

    Scenario Outline: Various arithmetic operations
        Given a math tool
        When I evaluate "<expression>"
        Then the result should be successful
        And the result value should be "<expected>"

        Examples:
            | expression | expected |
            | 1+1        | 2        |
            | 10-5       | 5        |
            | 3*4        | 12       |
            | 20/4       | 5        |
