using Ouroboros.Infrastructure.FeatureEngineering;

namespace Ouroboros.Specs.Steps;

[Binding]
public class StreamDeduplicatorSteps
{
    private StreamDeduplicator? _deduplicator;
    private Exception? _thrownException;
    private bool _isDuplicate;
    private List<float[]>? _filteredBatch;
    private List<float[]>? _filteredStream;
    private IAsyncEnumerable<float[]>? _streamSource;

    [Given("a fresh stream deduplicator context")]
    public void GivenAFreshStreamDeduplicatorContext()
    {
        _deduplicator = null;
        _thrownException = null;
        _isDuplicate = false;
        _filteredBatch = null;
        _filteredStream = null;
        _streamSource = null;
    }

    [Given("a deduplicator with threshold {double}")]
    public void GivenADeduplicatorWithThreshold(double threshold)
    {
        _deduplicator = new StreamDeduplicator((float)threshold);
    }

    [Given("a deduplicator with threshold {double} and max cache size {int}")]
    public void GivenADeduplicatorWithThresholdAndMaxCacheSize(double threshold, int maxCacheSize)
    {
        _deduplicator = new StreamDeduplicator((float)threshold, maxCacheSize);
    }

    [Given("I add vector [{float}, {float}, {float}] to cache")]
    public void GivenIAddVectorToCache(float x, float y, float z)
    {
        _deduplicator.Should().NotBeNull();
        _deduplicator!.IsDuplicate(new float[] { x, y, z });
    }

    [Given("a stream of vectors [{float},{float},{float}], [{float},{float},{float}]")]
    public void GivenAStreamOfVectorsTwoVectors(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        _streamSource = CreateAsyncEnumerable(new float[] { x1, y1, z1 }, new float[] { x2, y2, z2 });
    }

    [Given("a stream of vectors [{float},{float},{float}], [{float},{float},{float}], [{float},{float},{float}]")]
    public void GivenAStreamOfVectorsThreeVectors(float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3)
    {
        _streamSource = CreateAsyncEnumerable(new float[] { x1, y1, z1 }, new float[] { x2, y2, z2 }, new float[] { x3, y3, z3 });
    }

    [When("I create a deduplicator with threshold {double}")]
    public void WhenICreateADeduplicatorWithThreshold(double threshold)
    {
        _deduplicator = new StreamDeduplicator((float)threshold);
    }

    [When("I attempt to create a deduplicator with threshold {double}")]
    public void WhenIAttemptToCreateADeduplicatorWithThreshold(double threshold)
    {
        try
        {
            _deduplicator = new StreamDeduplicator((float)threshold);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I attempt to create a deduplicator with max cache size {int}")]
    public void WhenIAttemptToCreateADeduplicatorWithMaxCacheSize(int maxCacheSize)
    {
        try
        {
            _deduplicator = new StreamDeduplicator(0.95f, maxCacheSize);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I check if first vector [{float}, {float}, {float}] is duplicate")]
    [When("I check if vector [{float}, {float}, {float}] is duplicate")]
    public void WhenICheckIfVectorIsDuplicate(float x, float y, float z)
    {
        _deduplicator.Should().NotBeNull();
        _isDuplicate = _deduplicator!.IsDuplicate(new float[] { x, y, z });
    }

    [When("I filter batch with vectors [{float},{float},{float}], [{float},{float},{float}], [{float},{float},{float}]")]
    public void WhenIFilterBatchWithVectorsThree(float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3)
    {
        _deduplicator.Should().NotBeNull();
        var vectors = new List<float[]>
        {
            new float[] { x1, y1, z1 },
            new float[] { x2, y2, z2 },
            new float[] { x3, y3, z3 },
        };
        _filteredBatch = _deduplicator!.FilterBatch(vectors);
    }

    [When("I filter batch with vectors [{float},{float},{float}]")]
    public void WhenIFilterBatchWithVectorsSingle(float x, float y, float z)
    {
        _deduplicator.Should().NotBeNull();
        var vectors = new List<float[]> { new float[] { x, y, z } };
        _filteredBatch = _deduplicator!.FilterBatch(vectors);
    }

    [When("I filter another batch with vectors [{float},{float},{float}], [{float},{float},{float}]")]
    public void WhenIFilterAnotherBatchWithVectorsTwo(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        _deduplicator.Should().NotBeNull();
        var vectors = new List<float[]>
        {
            new float[] { x1, y1, z1 },
            new float[] { x2, y2, z2 },
        };
        _filteredBatch = _deduplicator!.FilterBatch(vectors);
    }

    [When("I filter async stream with vectors [{float},{float},{float}], [{float},{float},{float}], [{float},{float},{float}]")]
    public async Task WhenIFilterAsyncStreamWithVectors(float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3)
    {
        _deduplicator.Should().NotBeNull();
        var stream = CreateAsyncEnumerable(new float[] { x1, y1, z1 }, new float[] { x2, y2, z2 }, new float[] { x3, y3, z3 });
        var filtered = _deduplicator!.FilterStreamAsync(stream);
        _filteredStream = new List<float[]>();
        await foreach (var item in filtered)
        {
            _filteredStream.Add(item);
        }
    }

    [When("I filter empty async stream")]
    public async Task WhenIFilterEmptyAsyncStream()
    {
        _deduplicator.Should().NotBeNull();
        var stream = CreateAsyncEnumerable();
        var filtered = _deduplicator!.FilterStreamAsync(stream);
        _filteredStream = new List<float[]>();
        await foreach (var item in filtered)
        {
            _filteredStream.Add(item);
        }
    }

    [When("I filter async stream with cancellation after {int} vector")]
    public async Task WhenIFilterAsyncStreamWithCancellation(int count)
    {
        try
        {
            _deduplicator.Should().NotBeNull();
            var cts = new CancellationTokenSource();
            var stream = CreateAsyncEnumerableWithCancellation(cts, count);
            var filtered = _deduplicator!.FilterStreamAsync(stream, cts.Token);
            _filteredStream = new List<float[]>();
            await foreach (var item in filtered.WithCancellation(cts.Token))
            {
                _filteredStream.Add(item);
            }
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I call Deduplicate extension with threshold {double}")]
    public async Task WhenICallDeduplicateExtensionWithThreshold(double threshold)
    {
        _streamSource.Should().NotBeNull();
        var deduplicator = new StreamDeduplicator((float)threshold);
        var filtered = _streamSource!.Deduplicate(deduplicator);
        _filteredStream = new List<float[]>();
        await foreach (var item in filtered)
        {
            _filteredStream.Add(item);
        }
    }

    [Then("the deduplicator should not be null")]
    public void ThenTheDeduplicatorShouldNotBeNull()
    {
        _deduplicator.Should().NotBeNull();
    }

    [Then("it should throw ArgumentOutOfRangeException for threshold")]
    [Then("it should throw ArgumentOutOfRangeException for maxCacheSize")]
    public void ThenItShouldThrowArgumentOutOfRangeException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<ArgumentOutOfRangeException>();
    }

    [Then("it should throw OperationCanceledException")]
    public void ThenItShouldThrowOperationCanceledException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<OperationCanceledException>();
    }

    [Then("it should not be a duplicate")]
    public void ThenItShouldNotBeADuplicate()
    {
        _isDuplicate.Should().BeFalse();
    }

    [Then("it should be a duplicate")]
    public void ThenItShouldBeADuplicate()
    {
        _isDuplicate.Should().BeTrue();
    }

    [Then("the filtered batch should have {int} vector")]
    [Then("the filtered batch should have {int} vectors")]
    public void ThenTheFilteredBatchShouldHaveVectors(int count)
    {
        _filteredBatch.Should().NotBeNull();
        _filteredBatch!.Count.Should().Be(count);
    }

    [Then("the second batch should have {int} vector")]
    public void ThenTheSecondBatchShouldHaveVector(int count)
    {
        _filteredBatch.Should().NotBeNull();
        _filteredBatch!.Count.Should().Be(count);
    }

    [Then("the filtered batch should contain [{float},{float},{float}]")]
    public void ThenTheFilteredBatchShouldContainVector(float x, float y, float z)
    {
        _filteredBatch.Should().NotBeNull();
        _filteredBatch!.Should().ContainSingle(v =>
            Math.Abs(v[0] - x) < 0.0001f &&
            Math.Abs(v[1] - y) < 0.0001f &&
            Math.Abs(v[2] - z) < 0.0001f);
    }

    [Then("the filtered stream should yield {int} vector")]
    [Then("the filtered stream should yield {int} vectors")]
    [Then("the result should have {int} vectors")]
    public void ThenTheFilteredStreamShouldYieldVectors(int count)
    {
        _filteredStream.Should().NotBeNull();
        _filteredStream!.Count.Should().Be(count);
    }

    private static async IAsyncEnumerable<float[]> CreateAsyncEnumerable(params float[][] vectors)
    {
        foreach (var vector in vectors)
        {
            await Task.Yield();
            yield return vector;
        }
    }

    private static async IAsyncEnumerable<float[]> CreateAsyncEnumerableWithCancellation(CancellationTokenSource cts, int cancelAfter)
    {
        int count = 0;
        while (true)
        {
            await Task.Yield();
            count++;
            if (count > cancelAfter)
            {
                cts.Cancel();
            }

            yield return new float[] { 1.0f, 0.0f, 0.0f };
        }
    }
}
