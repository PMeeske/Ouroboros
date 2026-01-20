using LangChain.Databases;

namespace Ouroboros.Specs.Steps;

[Binding]
[Scope(Feature = "Retrieval Tool")]
public class RetrievalToolSteps
{
    private TrackedVectorStore? _store;
    private RetrievalTool? _tool;
    private Result<string, string>? _result;

    private sealed class FakeEmbeddingModel : IEmbeddingModel
    {
        private readonly Func<string, float[]> factory;

        public FakeEmbeddingModel(Func<string, float[]> factory)
        {
            this.factory = factory;
        }

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(this.factory(input));
        }
    }

    [Given("a fresh retrieval tool context")]
    public void GivenAFreshRetrievalToolContext()
    {
        _store = null;
        _tool = null;
        _result = null;
    }

    [Given(@"a vector store with documents about ""(.*)"" and ""(.*)""")]
    public async Task GivenAVectorStoreWithDocumentsAbout(string topic1, string topic2)
    {
        _store = new TrackedVectorStore();
        Vector[] vectors =
        [
            new Vector
            {
                Id = "doc1",
                Text = "Machine learning is fascinating",
                Embedding = new[] { 1f, 0f, 0f },
                Metadata = new Dictionary<string, object> { ["name"] = "Doc1" },
            },
            new Vector
            {
                Id = "doc2",
                Text = "Baking cakes requires patience",
                Embedding = new[] { 0f, 1f, 0f },
                Metadata = new Dictionary<string, object> { ["name"] = "Doc2" },
            },
        ];
        await _store.AddAsync(vectors);
    }

    [Given("an empty vector store")]
    public void GivenAnEmptyVectorStore()
    {
        _store = new TrackedVectorStore();
    }

    [Given("a vector store with a very long document")]
    public async Task GivenAVectorStoreWithAVeryLongDocument()
    {
        _store = new TrackedVectorStore();
        string longText = new string('a', 300);
        Vector vector = new Vector
        {
            Id = "long",
            Text = longText,
            Embedding = new[] { 1f, 0f, 0f },
            Metadata = new Dictionary<string, object> { ["name"] = "LongDoc" },
        };
        await _store.AddAsync(new[] { vector });
    }

    [Given("a retrieval tool configured for the store")]
    public void GivenARetrievalToolConfiguredForTheStore()
    {
        _store.Should().NotBeNull();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        _tool = new RetrievalTool(_store!, embeddings);
    }

    [When(@"I search for ""(.*)"" with k=(.*)")]
    public async Task WhenISearchForWithK(string query, int k)
    {
        _tool.Should().NotBeNull();
        string input = ToolJson.Serialize(new RetrievalArgs { Q = query, K = k });
        _result = await _tool!.InvokeAsync(input);
    }

    [When(@"I invoke the tool with invalid JSON ""(.*)""")]
    public async Task WhenIInvokeTheToolWithInvalidJson(string invalidJson)
    {
        _tool.Should().NotBeNull();
        _result = await _tool!.InvokeAsync(invalidJson);
    }

    [Then("the result should be successful")]
    public void ThenTheResultShouldBeSuccessful()
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
    }

    [Then("the result should be a failure")]
    public void ThenTheResultShouldBeAFailure()
    {
        _result.Should().NotBeNull();
        _result.Value.IsFailure.Should().BeTrue();
    }

    [Then(@"the result should contain ""(.*)""")]
    public void ThenTheResultShouldContain(string expected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.Should().Contain(expected);
    }

    [Then(@"the result should not contain ""(.*)""")]
    public void ThenTheResultShouldNotContain(string notExpected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.Should().NotContain(notExpected);
    }

    [Then(@"the result should be ""(.*)""")]
    public void ThenTheResultShouldBe(string expected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
        _result.Value.Value.Should().Be(expected);
    }

    [Then(@"the error should contain ""(.*)""")]
    public void ThenTheErrorShouldContain(string expected)
    {
        _result.Should().NotBeNull();
        _result.Value.IsFailure.Should().BeTrue();
        _result.Value.Error.Should().Contain(expected);
    }

    [Then("the snippet should be truncated to 243 characters or less")]
    public void ThenTheSnippetShouldBeTruncated()
    {
        _result.Should().NotBeNull();
        _result.Value.IsSuccess.Should().BeTrue();
        string resultValue = _result.Value.Value;
        int startIndex = resultValue.IndexOf("]", StringComparison.Ordinal) + 2;
        string snippet = resultValue[startIndex..];
        snippet.Length.Should().BeLessThanOrEqualTo(243);
    }
}
