Feature: Result Monad
    As a developer
    I want to handle computation results with explicit success or failure states
    So that I can write robust error-handling code following monadic principles

    Background:
        Given a fresh result monad context

    Scenario: Create successful result
        When I create a successful result with value 42
        Then the result should be successful
        And the result should not be a failure
        And the value should be 42

    Scenario: Create failed result
        When I create a failed result with error "error"
        Then the result should be a failure
        And the result should not be successful
        And the error should be "error"

    Scenario: Access value on failed result throws exception
        Given a failed result with error "error"
        When I attempt to access the value
        Then it should throw InvalidOperationException

    Scenario: Access error on successful result throws exception
        Given a successful result with value 42
        When I attempt to access the error
        Then it should throw InvalidOperationException

    Scenario: Map transforms successful result value
        Given a successful result with value 5
        When I map it with function that doubles the value
        Then the result should be successful
        And the value should be 10

    Scenario: Map preserves failed result error
        Given a failed result with error "error"
        When I map it with function that doubles the value
        Then the result should be a failure
        And the error should be "error"

    Scenario: Bind applies function to successful result
        Given a successful result with value 5
        When I bind it with function that doubles and wraps the value
        Then the result should be successful
        And the value should be 10

    Scenario: Bind preserves failed result error
        Given a failed result with error "error"
        When I bind it with function that doubles and wraps the value
        Then the result should be a failure
        And the error should be "error"

    Scenario: Bind chains operations
        Given a successful result with value 5
        When I bind it to double and then add 3
        Then the result should be successful
        And the value should be 13

    Scenario: Bind short-circuits on first error
        Given a successful result with value 5
        When I bind it to fail with "first error" and then add 3
        Then the result should be a failure
        And the error should be "first error"

    Scenario: MapError transforms error type
        Given a failed result with error "error"
        When I map the error to its length
        Then the result should be a failure
        And the error length should be 5

    Scenario: MapError preserves success value
        Given a successful result with value 42
        When I map the error to its length
        Then the result should be successful
        And the value should be 42

    Scenario: Match executes success function on success
        Given a successful result with value 42
        When I match it with formatters
        Then the output should be "Success: 42"

    Scenario: Match executes failure function on failure
        Given a failed result with error "error"
        When I match it with formatters
        Then the output should be "Error: error"

    Scenario Outline: Map correctly transforms different values
        Given a successful result with value <input>
        When I map it with function that doubles the value
        Then the value should be <expected>

        Examples:
            | input | expected |
            | 5     | 10       |
            | 0     | 0        |
            | -3    | -6       |

    Scenario: Result follows monadic left identity law
        Given a value 42
        When I test the left identity law with toString function
        Then both sides should be equal

    Scenario: Result follows monadic associativity law
        Given a successful result with value 5
        When I test the associativity law with double and add3 functions
        Then both sides should be equal
