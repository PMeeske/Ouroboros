using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace Ouroboros.Specs.Steps;

[Binding]
public class TrackedVectorStoreSteps
{
    private TrackedVectorStore? _store;
    private IReadOnlyCollection<Document>? _results;
    private List<Vector>? _allVectors;

    [Given("a fresh tracked vector store context")]
    public void GivenAFreshTrackedVectorStoreContext()
    {
        _store = null;
        _results = null;
        _allVectors = null;
    }

    [Given("a tracked vector store")]
    public void GivenATrackedVectorStore()
    {
        _store = new TrackedVectorStore();
    }

    [When("I add two test vectors")]
    public async Task WhenIAddTwoTestVectors()
    {
        _store.Should().NotBeNull();
        var vectors = new List<Vector>
        {
            new()
            {
                Id = "test1",
                Text = "This is a test document",
                Embedding = new[] { 1f, 0f, 0f },
                Metadata = new Dictionary<string, object> { ["type"] = "test" },
            },
            new()
            {
                Id = "test2",
                Text = "Another test document",
                Embedding = new[] { 0f, 1f, 0f },
                Metadata = new Dictionary<string, object> { ["type"] = "test" }
            },
        };

        await _store!.AddAsync(vectors);
    }

    [When(@"I add vectors about ""(.*)"" and ""(.*)""")]
    public async Task WhenIAddVectorsAbout(string topic1, string topic2)
    {
        _store.Should().NotBeNull();
        var vectors = new List<Vector>
        {
            new()
            {
                Id = "doc1",
                Text = "Machine learning is fascinating",
                Embedding = new[] { 1f, 0.5f, 0f },
                Metadata = new Dictionary<string, object> { ["category"] = "ai" },
            },
            new()
            {
                Id = "doc2",
                Text = "Cooking recipes are useful",
                Embedding = new[] { 0f, 0f, 1f },
                Metadata = new Dictionary<string, object> { ["category"] = "cooking" }
            },
        };

        await _store!.AddAsync(vectors);
    }

    [When(@"I query with embedding similar to ""(.*)""")]
    public async Task WhenIQueryWithEmbeddingSimilarTo(string topic)
    {
        _store.Should().NotBeNull();
        var queryEmbedding = new[] { 0.9f, 0.4f, 0.1f };
        _results = await _store!.GetSimilarDocumentsAsync(queryEmbedding, amount: 1);
    }

    [When("I add a test vector")]
    public async Task WhenIAddATestVector()
    {
        _store.Should().NotBeNull();
        var vector = new Vector
        {
            Id = "clear-test",
            Text = "Document to be cleared",
            Embedding = new[] { 1f, 1f, 1f },
        };

        await _store!.AddAsync(new[] { vector });
    }

    [When(@"I verify the store has (.*) vector")]
    public void WhenIVerifyTheStoreHasVector(int count)
    {
        _store.Should().NotBeNull();
        var vectors = _store!.GetAll().ToList();
        vectors.Should().HaveCount(count);
    }

    [When("I clear the store")]
    public async Task WhenIClearTheStore()
    {
        _store.Should().NotBeNull();
        await _store!.ClearAsync();
    }

    [When("I get all vectors")]
    public void WhenIGetAllVectors()
    {
        _store.Should().NotBeNull();
        _allVectors = _store!.GetAll().ToList();
    }

    [Then(@"the store should contain (.*) vectors")]
    public void ThenTheStoreShouldContainVectors(int count)
    {
        _store.Should().NotBeNull();
        var allVectors = _store!.GetAll().ToList();
        allVectors.Should().HaveCount(count);
    }

    [Then(@"the store should contain vector ""(.*)""")]
    public void ThenTheStoreShouldContainVector(string vectorId)
    {
        _store.Should().NotBeNull();
        var allVectors = _store!.GetAll().ToList();
        allVectors.Should().Contain(v => v.Id == vectorId);
    }

    [Then(@"the results should contain (.*) document")]
    public void ThenTheResultsShouldContainDocument(int count)
    {
        _results.Should().NotBeNull();
        _results!.Should().HaveCount(count);
    }

    [Then(@"the first result should contain ""(.*)""")]
    public void ThenTheFirstResultShouldContain(string expected)
    {
        _results.Should().NotBeNull();
        _results!.Should().NotBeEmpty();
        _results.First().PageContent.Should().Contain(expected);
    }

    [Then("the store should be empty")]
    public void ThenTheStoreShouldBeEmpty()
    {
        _store.Should().NotBeNull();
        var afterClear = _store!.GetAll().ToList();
        afterClear.Should().BeEmpty();
    }

    [Then("the result should be empty")]
    public void ThenTheResultShouldBeEmpty()
    {
        _allVectors.Should().NotBeNull();
        _allVectors!.Should().BeEmpty();
    }
}
