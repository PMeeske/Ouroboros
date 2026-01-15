Feature: Stream Deduplicator
    As a developer
    I want to deduplicate vectors in streams
    So that I can avoid storing duplicate embeddings

    Background:
        Given a fresh stream deduplicator context

    Scenario: Create deduplicator with valid threshold
        When I create a deduplicator with threshold 0.95
        Then the deduplicator should not be null

    Scenario: Constructor rejects threshold below zero
        When I attempt to create a deduplicator with threshold -0.1
        Then it should throw ArgumentOutOfRangeException for threshold

    Scenario: Constructor rejects threshold above one
        When I attempt to create a deduplicator with threshold 1.5
        Then it should throw ArgumentOutOfRangeException for threshold

    Scenario: Constructor rejects zero max cache size
        When I attempt to create a deduplicator with max cache size 0
        Then it should throw ArgumentOutOfRangeException for maxCacheSize

    Scenario: Constructor rejects negative max cache size
        When I attempt to create a deduplicator with max cache size -10
        Then it should throw ArgumentOutOfRangeException for maxCacheSize

    Scenario: First vector is not a duplicate
        Given a deduplicator with threshold 0.95
        When I check if first vector [1, 0, 0] is duplicate
        Then it should not be a duplicate

    Scenario: Identical vectors are duplicates
        Given a deduplicator with threshold 0.95
        And I add vector [1, 0, 0] to cache
        When I check if vector [1, 0, 0] is duplicate
        Then it should be a duplicate

    Scenario: Similar vectors above threshold are duplicates
        Given a deduplicator with threshold 0.95
        And I add vector [1, 0, 0] to cache
        When I check if vector [0.99, 0.01, 0] is duplicate
        Then it should be a duplicate

    Scenario: Different vectors are not duplicates
        Given a deduplicator with threshold 0.95
        And I add vector [1, 0, 0] to cache
        When I check if vector [0, 1, 0] is duplicate
        Then it should not be a duplicate

    Scenario: LRU cache evicts oldest entries
        Given a deduplicator with threshold 0.95 and max cache size 2
        And I add vector [1, 0, 0] to cache
        And I add vector [0, 1, 0] to cache
        And I add vector [0, 0, 1] to cache
        When I check if vector [1, 0, 0] is duplicate
        Then it should not be a duplicate

    Scenario: Filter batch removes duplicates
        Given a deduplicator with threshold 0.95
        When I filter batch with vectors [1,0,0], [0.99,0.01,0], [0,1,0]
        Then the filtered batch should have 2 vectors
        And the filtered batch should contain [1,0,0]
        And the filtered batch should contain [0,1,0]

    Scenario: Filter batch with all duplicates returns one
        Given a deduplicator with threshold 0.95
        When I filter batch with vectors [1,0,0], [1,0,0], [1,0,0]
        Then the filtered batch should have 1 vector

    Scenario: Filter async stream removes duplicates
        Given a deduplicator with threshold 0.95
        When I filter async stream with vectors [1,0,0], [0.99,0.01,0], [0,1,0]
        Then the filtered stream should yield 2 vectors

    Scenario: Filter async stream handles empty stream
        Given a deduplicator with threshold 0.95
        When I filter empty async stream
        Then the filtered stream should yield 0 vectors

    Scenario: Filter async stream respects cancellation
        Given a deduplicator with threshold 0.95
        When I filter async stream with cancellation after 1 vector
        Then it should throw OperationCanceledException

    Scenario: Extension method Deduplicate creates deduplicator
        Given a stream of vectors [1,0,0], [0,1,0]
        When I call Deduplicate extension with threshold 0.95
        Then the result should have 2 vectors

    Scenario: Extension method Deduplicate removes duplicates
        Given a stream of vectors [1,0,0], [1,0,0], [0,1,0]
        When I call Deduplicate extension with threshold 0.95
        Then the result should have 2 vectors

    Scenario: Multiple batches maintain state
        Given a deduplicator with threshold 0.95
        When I filter batch with vectors [1,0,0]
        And I filter another batch with vectors [1,0,0], [0,1,0]
        Then the second batch should have 1 vector
