Feature: Event Store
    As a developer
    I want to store and retrieve pipeline events
    So that I can replay branch executions and maintain event history

    Background:
        Given a fresh event store context

    Scenario: Append events to new branch stores events and returns version
        Given a new branch "test-branch"
        And 2 test events
        When I append events to the branch
        Then the version should be 1
        And the events should be stored

    Scenario: Get events from existing branch returns all events
        Given a branch "test-branch" with 3 stored events
        When I get all events from the branch
        Then I should receive 3 events

    Scenario: Get events from non-existent branch returns empty list
        Given a non-existent branch "missing-branch"
        When I get all events from the branch
        Then I should receive 0 events

    Scenario: Get events with from version returns events from that version
        Given a branch "test-branch" with 4 stored events
        When I get events from version 2
        Then I should receive 2 events

    Scenario: Get version from existing branch returns current version
        Given a branch "test-branch" with 3 stored events
        When I get the current version
        Then the version should be 2

    Scenario: Get version from non-existent branch returns -1
        Given a non-existent branch "missing-branch"
        When I get the current version
        Then the version should be -1

    Scenario: Append events increments version correctly
        Given a branch "test-branch" with 2 stored events
        When I append 3 more events
        Then the version should be 4

    Scenario: Create snapshot stores branch state
        Given a branch "test-branch" with 5 stored events
        When I create a snapshot at version 4
        Then the snapshot should be stored
        And retrieving the snapshot should return version 4

    Scenario: Get snapshot from non-existent branch returns null
        Given a non-existent branch "missing-branch"
        When I get the latest snapshot
        Then the snapshot should be null

    Scenario: Multiple branches maintain separate event streams
        Given a branch "branch-1" with 2 stored events
        And a branch "branch-2" with 3 stored events
        When I get all events from "branch-1"
        Then I should receive 2 events
        When I get all events from "branch-2"
        Then I should receive 3 events

    Scenario: Event ordering is preserved
        Given a branch "test-branch"
        When I append events with IDs "evt-1", "evt-2", "evt-3"
        And I get all events from the branch
        Then the events should be in order "evt-1", "evt-2", "evt-3"
