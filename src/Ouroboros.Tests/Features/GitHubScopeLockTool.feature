Feature: GitHub Scope Lock Tool
    As a developer
    I want to use the GitHub Scope Lock Tool
    So that I can manage scope-locked issues in GitHub

    Background:
        Given I have a GitHub token "test-token"
        And I have a repository owner "test-owner"
        And I have a repository name "test-repo"

    Scenario: Constructor with valid parameters creates instance
        When I create a GitHubScopeLockTool with the given parameters
        Then the tool should not be null

    Scenario: Name returns correct value
        Given I create a GitHubScopeLockTool with the given parameters
        When I get the tool name
        Then the name should be "github_scope_lock"

    Scenario: Description contains relevant keywords
        Given I create a GitHubScopeLockTool with the given parameters
        When I get the tool description
        Then the description should not be empty
        And the description should contain "scope"
        And the description should contain "lock"
        And the description should contain "scope-locked"

    Scenario: JSON schema returns valid schema
        Given I create a GitHubScopeLockTool with the given parameters
        When I get the tool JSON schema
        Then the schema should not be empty
        And the schema should contain "IssueNumber"

    Scenario: Invoke with invalid JSON returns failure
        Given I create a GitHubScopeLockTool with the given parameters
        When I invoke the tool with "invalid-json"
        Then the result should be a failure
        And the error should contain "failed"

    Scenario: Invoke with empty JSON returns failure
        Given I create a GitHubScopeLockTool with the given parameters
        When I invoke the tool with "{}"
        Then the result should be a failure

    Scenario: GitHubScopeLockArgs can be instantiated with all properties
        When I create GitHubScopeLockArgs with issue number 138 and milestone "v1.0"
        Then the args issue number should be 138
        And the args milestone should be "v1.0"

    Scenario: GitHubScopeLockArgs milestone is optional
        When I create GitHubScopeLockArgs with issue number 138 and no milestone
        Then the args issue number should be 138
        And the args milestone should be null
