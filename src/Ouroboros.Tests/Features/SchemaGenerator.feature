Feature: Schema Generator
    As a developer
    I want to generate JSON schemas from C# types
    So that I can validate tool arguments and provide API documentation

    Background:
        Given a fresh schema generation context

    Scenario: Generate schema with null type throws exception
        When I attempt to generate a schema from a null type
        Then it should throw an ArgumentNullException

    Scenario: Generate schema from complex type
        Given a complex type with multiple properties
        When I generate the schema
        Then the schema should have type "object"
        And the schema should define property "Name" as "string"
        And the schema should define property "Count" as "integer"
        And the schema should define property "Optional" as "number"
        And the schema should define property "Tags" as "array"
        And the schema should mark "Name" as required
        And the schema should mark "Count" as required
        And the schema should not mark "Optional" as required
        And the schema should not mark "Tags" as required
        And the property "Count" should have JsonPropertyName "identifier"
