Feature: Android Behavior
    As an Android developer
    I want the app to handle initialization failures gracefully
    So that users never see a purple screen crash

    Background:
        Given a fresh Android behavior context

    # Android Services Integration Tests
    Scenario: CLI Executor successful initialization allows command execution
        Given a CLI executor configured to succeed
        When I initialize with database path "/tmp/test.db"
        Then the executor should be initialized
        When I execute command "help"
        Then the command result should contain "Executed: help"

    Scenario: CLI Executor failed initialization throws exception
        Given a CLI executor configured to fail initialization
        When I attempt to initialize with database path "/tmp/test.db"
        Then it should throw InvalidOperationException with message "CliExecutor initialization failed"

    Scenario: CLI Executor database failure allows fallback without database
        Given a CLI executor configured to fail with database
        When I attempt to initialize with database path "/tmp/test.db"
        Then it should throw InvalidOperationException
        When I initialize without database
        Then the executor should be initialized
        And I should be able to execute commands

    Scenario: CLI Executor command execution without initialization fails
        Given an uninitialized CLI executor
        When I attempt to execute command "help"
        Then it should throw InvalidOperationException with message "Executor not initialized"

    # Android Activity Lifecycle Tests
    Scenario: Activity normal lifecycle renders UI
        Given a test main page activity
        When I call OnCreate
        And I call OnStart
        And I call OnResume
        Then the activity state should be Resumed
        And the UI should be rendered
        And the output should contain "MonadicPipeline CLI v1.0"
        And the output should contain ">"
        And the CLI executor should be initialized

    Scenario: Activity initialization failure still renders UI
        Given a test main page activity configured to fail initialization
        When I call OnCreate
        And I call OnStart
        And I call OnResume
        Then the activity state should be Resumed
        And the UI should be rendered
        And the output should contain "MonadicPipeline CLI v1.0"
        And the output should contain "⚠ Initialization error"
        And the output should contain ">"
        And the CLI executor should be initialized via fallback

    Scenario: Activity background and foreground maintains state
        Given a resumed activity with initial output
        When I call OnPause
        And I call OnStop
        And I call OnStart
        And I call OnResume
        Then the activity state should be Resumed
        And the output should match the initial output
        And the UI should be rendered

    Scenario: Activity execute command in resumed state succeeds
        Given a resumed activity
        When I execute command "help"
        Then the command result should contain "Executed: help"

    Scenario: Activity execute command not resumed throws exception
        Given an activity in Started state
        When I attempt to execute command "help"
        Then it should throw InvalidOperationException with message "Activity must be in Resumed state"

    Scenario: Activity destroy cleans up resources
        Given a resumed activity
        When I call OnPause
        And I call OnStop
        And I call OnDestroy
        Then the activity state should be Destroyed
        And the UI should not be rendered
        And the CLI executor should not be initialized

    Scenario: Activity configuration change recreates activity
        Given a resumed activity
        When I simulate configuration change
        Then the new activity state should be Resumed
        And the new activity UI should be rendered
        And the new activity CLI executor should be initialized

    # Purple Screen BDD Tests
    Scenario: Database failure when app starts shows error not purple screen
        Given database initialization will fail with "SQLite Error: unable to open database file"
        When the app starts
        Then the UI should render
        And the output should contain "MonadicPipeline CLI v1.0"
        And the output should contain "⚠ Initialization error"
        And the output should contain "SQLite Error"
        And the output should contain ">"

    Scenario: All services healthy provides full functionality
        Given all services are healthy
        When the app starts
        Then full functionality should be available
        And the output should not contain "⚠"
        And the output should contain "MonadicPipeline CLI v1.0"
        And the output should contain ">"

    Scenario: Partial service failure enables degraded mode
        Given primary service fails but fallback succeeds
        When the app starts
        Then degraded mode should work
        And the CLI executor should be available via fallback
        And the output should contain "⚠ Initialization error"
        And the output should contain ">"

    Scenario Outline: Various initialization errors are displayed
        Given initialization will fail with "<error_message>"
        When the app starts
        Then the specific error "<error_message>" should be shown to user
        And the output should contain "MonadicPipeline CLI v1.0"
        And the output should contain ">"

        Examples:
            | error_message                            |
            | SQLite Error: unable to open database file |
            | Permission denied                        |
            | Network unavailable                      |
            | Service initialization timeout           |

    Scenario: Purple screen bug fix ensures UI always renders
        Given the purple screen bug is fixed
        When initialization throws an exception
        Then the UI must render
        And the error must be shown to user
        And the app should not show purple screen
