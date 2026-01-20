Feature: Tool Registry
    As a developer
    I want to register and retrieve tools in an immutable registry
    So that I can manage tool collections safely in a functional programming style

    Background:
        Given a fresh tool registry context

    Scenario: Create empty registry
        When I create a new tool registry
        Then the registry count should be 0
        And the registry should have no tools

    Scenario: Add tool to registry
        Given an empty tool registry
        When I add a tool named "test-tool"
        Then the new registry count should be 1
        And the new registry should contain "test-tool"

    Scenario: WithTool returns new instance
        Given an empty tool registry
        When I add a tool named "test-tool"
        Then a new registry instance should be returned
        And the original registry should remain empty
        And the new registry count should be 1

    Scenario: WithTool with null throws exception
        Given an empty tool registry
        When I attempt to add a null tool
        Then it should throw ArgumentNullException

    Scenario: Chain multiple tools
        Given an empty tool registry
        When I chain tools "tool1", "tool2", and "tool3"
        Then the registry count should be 3
        And the registry should contain "tool1"
        And the registry should contain "tool2"
        And the registry should contain "tool3"

    Scenario: Replace existing tool
        Given an empty tool registry
        When I add a tool named "my-tool" with description "First description"
        And I add another tool named "my-tool" with description "Second description"
        Then the registry count should be 1
        And the tool "my-tool" should have description "Second description"

    Scenario: GetTool returns Some for existing tool
        Given a registry with tool "test-tool"
        When I get tool "test-tool"
        Then the tool option should have a value
        And the tool should be the same instance

    Scenario: GetTool returns None for non-existent tool
        Given an empty tool registry
        When I get tool "non-existent"
        Then the tool option should not have a value

    Scenario: GetTool with null throws exception
        Given an empty tool registry
        When I attempt to get a tool with null name
        Then it should throw ArgumentNullException

    Scenario Outline: GetTool is case insensitive
        Given a registry with tool "Test-Tool"
        When I get tool "<search_name>"
        Then the tool option should have a value
        And the tool should be the same instance

        Examples:
            | search_name |
            | test-tool   |
            | TEST-TOOL   |
            | Test-Tool   |

    Scenario: Get returns tool for existing name
        Given a registry with tool "test-tool"
        When I use Get for "test-tool"
        Then the tool should be returned

    Scenario: Get returns null for non-existent tool
        Given an empty tool registry
        When I use Get for "non-existent"
        Then null should be returned

    Scenario: Contains returns true for existing tool
        Given a registry with tool "test-tool"
        When I check if it contains "test-tool"
        Then the result should be true

    Scenario: Contains returns false for non-existent tool
        Given an empty tool registry
        When I check if it contains "non-existent"
        Then the result should be false

    Scenario: All returns all registered tools
        Given an empty tool registry
        When I add tools "tool1", "tool2", and "tool3"
        Then All should return 3 tools
        And All should contain the tool instances

    Scenario: Count reflects registry size
        Given an empty tool registry
        Then the registry count should be 0
        When I add a tool named "tool1"
        Then the new registry count should be 1
        When I add a tool named "tool2" to the new registry
        Then the newest registry count should be 2

    Scenario: SafeExportSchemas with tools returns JSON
        Given an empty tool registry
        When I add a tool "tool1" with schema
        And I add a tool "tool2" without schema
        And I export schemas
        Then the result should be successful
        And the export should contain "tool1"
        And the export should contain "tool2"

    Scenario: SafeExportSchemas with empty registry returns empty array
        Given an empty tool registry
        When I export schemas
        Then the result should be successful
        And the export should contain "[]"

    Scenario: Registry maintains immutability
        Given an empty tool registry
        When I add "tool1" creating registry1
        And I add "tool2" to registry1 creating registry2
        Then the original registry count should be 0
        And registry1 count should be 1
        And registry2 count should be 2
        And original registry should not contain "tool1" or "tool2"
        And registry1 should contain "tool1" but not "tool2"
        And registry2 should contain both "tool1" and "tool2"
