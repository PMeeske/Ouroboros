Feature: Tracked Vector Store
    As a developer
    I want to store and retrieve vectors with similarity search
    So that I can implement semantic search functionality

    Background:
        Given a fresh tracked vector store context

    Scenario: Add vectors to store
        Given a tracked vector store
        When I add two test vectors
        Then the store should contain 2 vectors
        And the store should contain vector "test1"
        And the store should contain vector "test2"

    Scenario: Get similar documents returns most similar
        Given a tracked vector store
        When I add vectors about "ai" and "cooking"
        And I query with embedding similar to "ai"
        Then the results should contain 1 document
        And the first result should contain "Machine learning"

    Scenario: Clear removes all vectors
        Given a tracked vector store
        When I add a test vector
        And I verify the store has 1 vector
        And I clear the store
        Then the store should be empty

    Scenario: GetAll returns empty collection initially
        Given a tracked vector store
        When I get all vectors
        Then the result should be empty
