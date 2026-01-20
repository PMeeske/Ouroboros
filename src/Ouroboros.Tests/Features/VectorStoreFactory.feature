Feature: Vector Store Factory
    As a developer
    I want to create vector stores using a factory pattern
    So that I can support multiple vector store backends

    Background:
        Given a fresh vector store factory context

    Scenario: Create in-memory vector store
        Given a configuration with type "InMemory"
        When I create a vector store
        Then the store should not be null
        And the store should be of type TrackedVectorStore

    Scenario: Create in-memory vector store with lowercase type
        Given a configuration with type "inmemory"
        When I create a vector store
        Then the store should not be null
        And the store should be of type TrackedVectorStore

    Scenario: Qdrant type without connection string throws exception
        Given a configuration with type "Qdrant" and no connection string
        When I attempt to create a vector store
        Then the vector store creation should throw InvalidOperationException
        And the error should mention connection string

    Scenario: Pinecone type without connection string throws exception
        Given a configuration with type "Pinecone" and no connection string
        When I attempt to create a vector store
        Then the vector store creation should throw InvalidOperationException
        And the error should mention connection string

    Scenario: Qdrant with connection string creates Qdrant vector store
        Given a configuration with type "Qdrant" and connection string "http://localhost:6333"
        When I create a vector store
        Then the store should be of type QdrantVectorStore

    Scenario: Pinecone with connection string throws not implemented
        Given a configuration with type "Pinecone" and connection string "https://pinecone-api.io"
        When I attempt to create a vector store
        Then it should throw NotImplementedException
        And the error should mention Pinecone implementation

    Scenario: Unsupported type throws exception
        Given a configuration with type "UnsupportedType"
        When I attempt to create a vector store
        Then it should throw NotSupportedException
        And the error should mention UnsupportedType

    Scenario: Create factory from pipeline configuration
        Given a pipeline configuration with vector store settings
        When I create a factory from the pipeline configuration
        Then the factory should not be null
        And creating a store should return TrackedVectorStore

    Scenario: Constructor with null config throws exception
        When I attempt to create a factory with null config
        Then the factory creation should throw ArgumentNullException
