Feature: Monadic tool composition
  Compose tools using Then/OrElse/Map to model Kleisli-style workflows.

  Background:
    Given a fresh tool composition context

  Scenario: Then chains on success
    Given a primary tool that succeeds with "hello"
    And a secondary tool that appends " world" and succeeds
    When I chain the tools with Then and execute with input "ignored"
    Then the result should be success with "hello world"

  Scenario: Then short-circuits on failure
    Given a primary tool that fails with error "boom"
    And a secondary tool that appends " world" and succeeds
    When I chain the tools with Then and execute with input "ignored"
    Then the result should be failure with error "boom"

  Scenario: OrElse uses fallback on failure
    Given a primary tool that fails with error "first failed"
    And a fallback tool that succeeds with "fallback"
    When I compose the tools with OrElse and execute with input "ignored"
    Then the result should be success with "fallback"

  Scenario: Map transforms successful result
    Given a primary tool that succeeds with "value"
    And a mapping that uppercases the result
    When I map the tool result and execute with input "ignored"
    Then the result should be success with "VALUE"
