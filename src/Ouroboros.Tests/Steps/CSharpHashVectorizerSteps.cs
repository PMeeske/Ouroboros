using Ouroboros.Infrastructure.FeatureEngineering;

namespace Ouroboros.Specs.Steps;

/// <summary>
/// Step definitions for CSharp hash vectorizer feature scenarios.
/// Provides bindings to create vectorizers, transform code samples,
/// and assert similarity, dimensions, normalization and structural effects.
/// </summary>
[Binding]
public class CSharpHashVectorizerSteps
{
    private CSharpHashVectorizer? _vectorizerPrimary;
    private CSharpHashVectorizer? _vectorizerSecondary;
    private readonly List<string> _codeSamples = new();
    private float[]? _vector1;
    private float[]? _vector2;
    private Exception? _thrown;

    [Given("a fresh hash vectorizer context")]
    public void GivenAFreshHashVectorizerContext()
    {
        _vectorizerPrimary = null;
        _vectorizerSecondary = null;
        _codeSamples.Clear();
        _vector1 = null;
        _vector2 = null;
        _thrown = null;
    }

    [When(@"I create a vectorizer with dimension (.*)")]
    public void WhenICreateAVectorizerWithDimension(int dimension)
    {
        _vectorizerPrimary = new CSharpHashVectorizer(dimension);
    }

    [Then("the vectorizer should not be null")]
    public void ThenTheVectorizerShouldNotBeNull()
    {
        _vectorizerPrimary.Should().NotBeNull();
    }

    [When(@"I attempt to create a vectorizer with dimension (.*)")]
    public void WhenIAttemptToCreateAVectorizerWithDimension(int dimension)
    {
        try
        {
            _vectorizerPrimary = new CSharpHashVectorizer(dimension);
        }
        catch (Exception ex)
        {
            _thrown = ex;
        }
    }

    /// <summary>Asserts an ArgumentException with a specific parameter name was thrown.</summary>
    [Then("it should throw ArgumentException with parameter \"(.*)\"")]
    public void ThenItShouldThrowArgumentExceptionWithParameter(string paramName)
    {
        _thrown.Should().NotBeNull();
        _thrown.Should().BeOfType<ArgumentException>();
        ((ArgumentException)_thrown!).ParamName.Should().Be(paramName);
    }

    /// <summary>Creates the primary vectorizer with the specified dimension.</summary>
    /// <param name="dimension">Power-of-two dimension >= 256.</param>
    [Given(@"a vectorizer with dimension (.*)")]
    public void GivenAVectorizerWithDimension(int dimension)
    {
        _vectorizerPrimary = new CSharpHashVectorizer(dimension);
    }

    /// <summary>Creates the primary vectorizer with dimension and lowercase option.</summary>
    /// <param name="dimension">Power-of-two dimension.</param>
    /// <param name="lowercase">Whether to lowercase tokens.</param>
    [Given(@"a vectorizer with dimension (.*) and lowercase true")]
    public void GivenAVectorizerWithDimensionAndLowercaseTrue(int dimension)
    {
        _vectorizerPrimary = new CSharpHashVectorizer(dimension, lowercase: true);
    }

    /// <summary>Adds a C# code sample for transformation.</summary>
    /// <param name="code">The code snippet to store.</param>
    [Given("C# code \"(.*)\"")]
    public void GivenCSharpCode(string code)
    {
        _codeSamples.Add(code);
    }

    /// <summary>Transforms the most recently added code sample using the primary vectorizer.</summary>
    [When("I transform the code")]
    public void WhenITransformTheCode()
    {
        _vectorizerPrimary.Should().NotBeNull();
        _codeSamples.Count.Should().BeGreaterThan(0);
        _vector1 = _vectorizerPrimary!.TransformCode(_codeSamples[^1]);
    }

    /// <summary>Transforms the latest code sample twice to produce two vectors for equality checks.</summary>
    [When("I transform the code twice")]
    public void WhenITransformTheCodeTwice()
    {
        _vectorizerPrimary.Should().NotBeNull();
        _codeSamples.Count.Should().BeGreaterThan(0);
        var vA = _vectorizerPrimary!.TransformCode(_codeSamples[^1]);
        var vB = _vectorizerPrimary.TransformCode(_codeSamples[^1]);
        _vector1 = vA;
        _vector2 = vB;
    }

    /// <summary>Transforms the last two added code samples into separate vectors.</summary>
    [When("I transform both codes")]
    public void WhenITransformBothCodes()
    {
        _vectorizerPrimary.Should().NotBeNull();
        _codeSamples.Count.Should().BeGreaterThan(1);
        _vector1 = _vectorizerPrimary!.TransformCode(_codeSamples[^2]);
        _vector2 = _vectorizerPrimary.TransformCode(_codeSamples[^1]);
    }

    /// <summary>Transforms the last code sample with both primary and secondary vectorizers.</summary>
    [When("I transform with both vectorizers")]
    public void WhenITransformWithBothVectorizers()
    {
        _vectorizerPrimary.Should().NotBeNull();
        _vectorizerSecondary.Should().NotBeNull();
        _codeSamples.Count.Should().BeGreaterThan(0);
        _vector1 = _vectorizerPrimary!.TransformCode(_codeSamples[^1]);
        _vector2 = _vectorizerSecondary!.TransformCode(_codeSamples[^1]);
    }

    /// <summary>Creates the secondary vectorizer without lowercasing for comparison.</summary>
    /// <param name="dimension">Vector dimension.</param>
    [Given(@"a vectorizer with dimension (.*) and lowercase false")]
    public void GivenASecondVectorizerWithDimensionAndLowercaseFalse(int dimension)
    {
        // Used after primary created with lowercase true to compare effect.
        _vectorizerSecondary = new CSharpHashVectorizer(dimension, lowercase: false);
    }

    /// <summary>Asserts transformed vector length equals expected.</summary>
    /// <param name="expected">Expected length.</param>
    [Then(@"the vector should have length (.*)")]
    public void ThenTheVectorShouldHaveLength(int expected)
    {
        _vector1.Should().NotBeNull();
        _vector1!.Length.Should().Be(expected);
    }

    /// <summary>Asserts transformed vector is L2 normalized.</summary>
    [Then("the vector L2 norm should be approximately 1.0")]
    public void ThenTheVectorL2NormShouldBeApproximatelyOne()
    {
        _vector1.Should().NotBeNull();
        float sumSq = 0f;
        foreach (var v in _vector1!)
        {
            sumSq += v * v;
        }
        var norm = MathF.Sqrt(sumSq);
        norm.Should().BeApproximately(1.0f, 0.0001f);
    }

    /// <summary>Asserts two transformed vectors are identical.</summary>
    [Then("both vectors should be identical")]
    public void ThenBothVectorsShouldBeIdentical()
    {
        _vector1.Should().NotBeNull();
        _vector2.Should().NotBeNull();
        _vector1!.Should().Equal(_vector2!);
    }

    /// <summary>Asserts cosine similarity greater than threshold.</summary>
    /// <param name="threshold">Similarity threshold.</param>
    [Then(@"the cosine similarity should be greater than (.*)")]
    public void ThenTheCosineSimilarityShouldBeGreaterThan(float threshold)
    {
        _vector1.Should().NotBeNull();
        _vector2.Should().NotBeNull();
        var sim = CSharpHashVectorizer.CosineSimilarity(ToFloat(_vector1!), ToFloat(_vector2!));
        sim.Should().BeGreaterThan(threshold);
    }

    /// <summary>Asserts cosine similarity less than threshold.</summary>
    /// <param name="threshold">Similarity upper bound.</param>
    [Then(@"the cosine similarity should be less than (.*)")]
    public void ThenTheCosineSimilarityShouldBeLessThan(float threshold)
    {
        _vector1.Should().NotBeNull();
        _vector2.Should().NotBeNull();
        var sim = CSharpHashVectorizer.CosineSimilarity(ToFloat(_vector1!), ToFloat(_vector2!));
        sim.Should().BeLessThan(threshold);
    }

    /// <summary>Asserts cosine similarity above supplied value for near-equality.</summary>
    /// <param name="threshold">Lower similarity bound.</param>
    [Then(@"the vectors should be similar with similarity > (.*)")]
    public void ThenTheVectorsShouldBeSimilarWithSimilarityGreaterThan(float threshold)
    {
        _vector1.Should().NotBeNull();
        _vector2.Should().NotBeNull();
        var sim = CSharpHashVectorizer.CosineSimilarity(ToFloat(_vector1!), ToFloat(_vector2!));
        sim.Should().BeGreaterThan(threshold);
    }

    /// <summary>Asserts vectors are not identical.</summary>
    [Then("the vectors should differ")]
    public void ThenTheVectorsShouldDiffer()
    {
        _vector1.Should().NotBeNull();
        _vector2.Should().NotBeNull();
        _vector1!.Should().NotEqual(_vector2!);
    }

    /// <summary>Asserts every element of the transformed vector is zero.</summary>
    [Then("all vector elements should be 0.0")]
    public void ThenAllVectorElementsShouldBeZero()
    {
        _vector1.Should().NotBeNull();
        foreach (var v in _vector1!)
        {
            v.Should().Be(0f);
        }
    }

    private static float[] ToFloat(float[] source) => source; // Already float, kept for symmetry.
}
