Feature: Retrieval Tool
    As a developer
    I want to search documents using semantic similarity
    So that I can retrieve relevant context for AI operations

    Background:
        Given a fresh retrieval tool context

    Scenario: Retrieve matching documents
        Given a vector store with documents about "AI" and "Baking"
        And a retrieval tool configured for the store
        When I search for "AI" with k=1
        Then the result should be successful
        And the result should contain "[Doc1]"
        And the result should contain "Machine learning"
        And the result should not contain "Doc2"

    Scenario: Search with no matching documents
        Given an empty vector store
        And a retrieval tool configured for the store
        When I search for "nothing" with k=2
        Then the result should be successful
        And the result should be "No relevant documents found."

    Scenario: Search with invalid JSON input
        Given an empty vector store
        And a retrieval tool configured for the store
        When I invoke the tool with invalid JSON "not-json"
        Then the result should be a failure
        And the error should contain "Search failed"

    Scenario: Truncate long document snippets
        Given a vector store with a very long document
        And a retrieval tool configured for the store
        When I search for "long" with k=1
        Then the result should be successful
        And the result should contain "[LongDoc]"
        And the result should contain "..."
        And the snippet should be truncated to 243 characters or less
