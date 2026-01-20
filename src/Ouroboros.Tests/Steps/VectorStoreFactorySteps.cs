using Ouroboros.Core.Configuration;

namespace Ouroboros.Specs.Steps;

[Binding]
public class VectorStoreFactorySteps
{
    private VectorStoreConfiguration? _config;
    private PipelineConfiguration? _pipelineConfig;
    private VectorStoreFactory? _factory;
    private IVectorStore? _store;
    private Exception? _thrownException;

    [Given("a fresh vector store factory context")]
    public void GivenAFreshVectorStoreFactoryContext()
    {
        _config = null;
        _pipelineConfig = null;
        _factory = null;
        _store = null;
        _thrownException = null;
    }

    [Given(@"^a configuration with type ""([^""]+)""$")]
    public void GivenAConfigurationWithType(string type)
    {
        _config = new VectorStoreConfiguration { Type = type };
    }

    [Given(@"^a configuration with type ""([^""]+)"" and no connection string$")]
    public void GivenAConfigurationWithTypeAndNoConnectionString(string type)
    {
        _config = new VectorStoreConfiguration
        {
            Type = type,
            ConnectionString = null,
        };
    }

    [Given(@"^a configuration with type ""([^""]+)"" and connection string ""([^""]+)""$")]
    public void GivenAConfigurationWithTypeAndConnectionString(string type, string connectionString)
    {
        _config = new VectorStoreConfiguration
        {
            Type = type,
            ConnectionString = connectionString,
        };
    }

    [Given("a pipeline configuration with vector store settings")]
    public void GivenAPipelineConfigurationWithVectorStoreSettings()
    {
        _pipelineConfig = new PipelineConfiguration
        {
            VectorStore = new VectorStoreConfiguration
            {
                Type = "InMemory",
                BatchSize = 200
            },
        };
    }

    [When("I create a vector store")]
    public void WhenICreateAVectorStore()
    {
        _config.Should().NotBeNull();
        _factory = new VectorStoreFactory(_config!);
        _store = _factory.Create();
    }

    [When("I attempt to create a vector store")]
    public void WhenIAttemptToCreateAVectorStore()
    {
        try
        {
            _config.Should().NotBeNull();
            _factory = new VectorStoreFactory(_config!);
            _store = _factory.Create();
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I create a factory from the pipeline configuration")]
    public void WhenICreateAFactoryFromThePipelineConfiguration()
    {
        _pipelineConfig.Should().NotBeNull();
        _factory = _pipelineConfig!.CreateVectorStoreFactory();
    }

    [When("I attempt to create a factory with null config")]
    public void WhenIAttemptToCreateAFactoryWithNullConfig()
    {
        try
        {
            _factory = new VectorStoreFactory(null!);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [Then("the store should not be null")]
    public void ThenTheStoreShouldNotBeNull()
    {
        _store.Should().NotBeNull();
    }

    [Then("the store should be of type TrackedVectorStore")]
    public void ThenTheStoreShouldBeOfTypeTrackedVectorStore()
    {
        _store.Should().BeOfType<TrackedVectorStore>();
    }

    [Then("the store should be of type QdrantVectorStore")]
    public void ThenTheStoreShouldBeOfTypeQdrantVectorStore()
    {
        _store.Should().NotBeNull();
        _store.Should().BeOfType<QdrantVectorStore>();
    }

    [Then("it should throw NotImplementedException")]
    public void ThenItShouldThrowNotImplementedException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<NotImplementedException>();
    }

    [Then("it should throw NotSupportedException")]
    public void ThenItShouldThrowNotSupportedException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<NotSupportedException>();
    }

    [Then("the vector store creation should throw InvalidOperationException")]
    public void ThenTheVectorStoreCreationShouldThrowInvalidOperationException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<InvalidOperationException>();
    }

    [Then("the error should mention connection string")]
    public void ThenTheErrorShouldMentionConnectionString()
    {
        _thrownException.Should().NotBeNull();
        _thrownException!.Message.Should().Contain("Connection string is required");
    }

    // Qdrant is implemented, so no error expectation step is needed anymore.

    [Then("the error should mention Pinecone implementation")]
    public void ThenTheErrorShouldMentionPineconeImplementation()
    {
        _thrownException.Should().NotBeNull();
        _thrownException!.Message.Should().Contain("Pinecone vector store implementation");
    }

    [Then("the factory creation should throw ArgumentNullException")]
    public void ThenTheFactoryCreationShouldThrowArgumentNullException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<ArgumentNullException>();
    }

    [Then("the error should mention UnsupportedType")]
    public void ThenTheErrorShouldMentionUnsupportedType()
    {
        _thrownException.Should().NotBeNull();
        _thrownException!.Message.Should().Contain("UnsupportedType");
        _thrownException.Message.Should().Contain("is not supported");
    }

    [Then("the factory should not be null")]
    public void ThenTheFactoryShouldNotBeNull()
    {
        _factory.Should().NotBeNull();
    }

    [Then("creating a store should return TrackedVectorStore")]
    public void ThenCreatingAStoreShouldReturnTrackedVectorStore()
    {
        _factory.Should().NotBeNull();
        var store = _factory!.Create();
        store.Should().BeOfType<TrackedVectorStore>();
    }
}
