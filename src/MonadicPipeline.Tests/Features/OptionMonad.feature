Feature: Option Monad
    As a developer
    I want to handle optional values safely without null reference exceptions
    So that I can write robust null-safe code following monadic principles

    Background:
        Given a fresh option monad context

    Scenario: Create Some with value
        When I create Some with value 42
        Then the option should have a value
        And the value should be 42

    Scenario: Create None
        When I create None for strings
        Then the option should not have a value

    Scenario: Construct with null creates None
        When I construct an option with null
        Then the option should not have a value

    Scenario: Construct with value creates Some
        When I construct an option with value "test"
        Then the option should have a value
        And the string value should be "test"

    Scenario: Map transforms Some value
        Given Some with value 5
        When I map it with function that doubles the value
        Then the option should have a value
        And the value should be 10

    Scenario: Bind applies function to Some
        Given Some with value 5
        When I bind it with function that doubles and wraps the value
        Then the option should have a value
        And the value should be 10

    Scenario: Bind chains operations
        Given Some with value 5
        When I bind it to double and then add 3
        Then the option should have a value
        And the value should be 13

    Scenario: Bind short-circuits on None
        Given Some with string value "test"
        When I bind it to None and then append " value"
        Then the option should not have a value

    Scenario: Match executes Some function on Some
        Given Some with value 42
        When I match it with formatters
        Then the output should be "Value: 42"

    Scenario: Match returns default on None
        Given None for strings
        When I match it with formatters and default "No value"
        Then the output should be "No value"

    Scenario: MatchAction executes Some action on Some
        Given Some with value 42
        When I match it with actions
        Then the Some action should have been called
        And the captured value should be 42

    Scenario: MatchAction executes None action on None
        Given None for strings
        When I match it with actions
        Then the None action should have been called

    Scenario: GetValueOrDefault returns value on Some
        Given Some with value 42
        When I get value or default 0
        Then the returned value should be 42

    Scenario: GetValueOrDefault returns default on None
        Given None for integers
        When I get value or default 100
        Then the returned value should be 100

    Scenario Outline: Map correctly transforms different values
        Given Some with value <input>
        When I map it with function that doubles the value
        Then the value should be <expected>

        Examples:
            | input | expected |
            | 5     | 10       |
            | 0     | 0        |
            | -3    | -6       |

    Scenario: Option follows monadic left identity law
        Given a value 42
        When I test the option left identity law with toString function
        Then both option sides should be equal

    Scenario: Option follows monadic associativity law
        Given Some with value 5
        When I test the option associativity law with double and add3 functions
        Then both option sides should be equal
