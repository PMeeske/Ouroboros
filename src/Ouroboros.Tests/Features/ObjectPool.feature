Feature: Object Pool
    As a developer
    I want to use object pools for efficient resource management
    So that I can reduce allocations and garbage collection pressure

    Background:
        Given a fresh object pool context

    Scenario: Rent returns non-null object
        Given an object pool of StringBuilder with initial capacity 10
        When I rent an object
        Then the rented object should not be null

    Scenario: Return adds object back to pool
        Given an object pool of StringBuilder with initial capacity 10
        When I rent an object
        And I return the object
        And I rent another object
        Then the second rented object should be the same as the first

    Scenario: Reset action is invoked on return
        Given an object pool with reset action that clears StringBuilder
        When I rent an object
        And I append "test" to the StringBuilder
        And I return the object
        And I rent another object
        Then the StringBuilder should be empty

    Scenario: Pool respects max size limit
        Given an object pool with max size 2
        When I rent 3 objects
        And I return all 3 objects
        And I rent 3 more objects
        Then only 2 objects should be from the pool

    Scenario: Clear empties the pool
        Given an object pool of StringBuilder with initial capacity 10
        When I rent 5 objects
        And I return all objects
        And I clear the pool
        And I rent an object
        Then it should be a new object

    Scenario: RentDisposable returns object on dispose
        Given an object pool of StringBuilder with initial capacity 10
        When I rent with RentDisposable
        And I dispose the disposable
        And I rent another object
        Then the object should be from the pool

    Scenario: CommonPools StringBuilderPool works
        When I rent from CommonPools StringBuilderPool
        Then the StringBuilder should have capacity at least 256

    Scenario: CommonPools StringListPool works
        When I rent from CommonPools StringListPool
        Then the List should be empty

    Scenario: CommonPools StringDictionaryPool works
        When I rent from CommonPools StringDictionaryPool
        Then the Dictionary should be empty

    Scenario: CommonPools MemoryStreamPool works
        When I rent from CommonPools MemoryStreamPool
        Then the MemoryStream should have position 0

    Scenario: PooledHelpers GetPooledString builds string
        When I use GetPooledString to build "Hello World"
        Then the result should be "Hello World"

    Scenario: PooledHelpers GetPooledString returns builder to pool
        Given I track CommonPools StringBuilderPool size
        When I use GetPooledString to build "test"
        Then the pool size should not increase

    Scenario: Thread safety stress test
        Given an object pool of StringBuilder with max size 10
        When I run 100 parallel rent and return operations
        Then all operations should complete without exceptions

    Scenario: Multiple pools are independent
        Given two separate object pools
        When I rent from pool A and pool B
        Then the objects should be different instances
