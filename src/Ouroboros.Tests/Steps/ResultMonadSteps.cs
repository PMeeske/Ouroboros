namespace Ouroboros.Specs.Steps;

[Binding]
[Scope(Feature = "Result Monad")]
public class ResultMonadSteps
{
    private Result<int, string>? _intResult;
    private Result<int, int>? _intErrorResult;
    private Exception? _thrownException;
    private string? _matchOutput;

    [Given("a fresh result monad context")]
    public void GivenAFreshResultMonadContext()
    {
        _intResult = null;
        _intErrorResult = null;
        _thrownException = null;
        _matchOutput = null;
    }

    [Given(@"a failed result with error ""(.*)""")]
    public void GivenAFailedResultWithError(string error)
    {
        _intResult = Result<int, string>.Failure(error);
    }

    [Given(@"a successful result with value (.*)")]
    public void GivenASuccessfulResultWithValue(int value)
    {
        _intResult = Result<int, string>.Success(value);
    }

    [Given(@"a value (.*)")]
    public void GivenAValue(int value)
    {
        // Store the value for later use in law tests
        _intResult = Result<int, string>.Success(value);
    }

    [When(@"I create a successful result with value (.*)")]
    public void WhenICreateASuccessfulResultWithValue(int value)
    {
        _intResult = Result<int, string>.Success(value);
    }

    [When(@"I create a failed result with error ""(.*)""")]
    public void WhenICreateAFailedResultWithError(string error)
    {
        _intResult = Result<int, string>.Failure(error);
    }

    [When("I attempt to access the value")]
    public void WhenIAttemptToAccessTheValue()
    {
        try
        {
            _ = _intResult!.Value.Value;
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I attempt to access the error")]
    public void WhenIAttemptToAccessTheError()
    {
        try
        {
            _ = _intResult!.Value.Error;
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I map it with function that doubles the value")]
    public void WhenIMapItWithFunctionThatDoublesTheValue()
    {
        _intResult = _intResult!.Value.Map(x => x * 2);
    }

    [When("I bind it with function that doubles and wraps the value")]
    public void WhenIBindItWithFunctionThatDoublesAndWrapsTheValue()
    {
        _intResult = _intResult!.Value.Bind(x => Result<int, string>.Success(x * 2));
    }

    [When("I bind it to double and then add 3")]
    public void WhenIBindItToDoubleAndThenAdd3()
    {
        _intResult = _intResult!.Value
            .Bind(x => Result<int, string>.Success(x * 2))
            .Bind(x => Result<int, string>.Success(x + 3));
    }

    [When(@"I bind it to fail with ""(.*)"" and then add 3")]
    public void WhenIBindItToFailWithAndThenAdd3(string error)
    {
        _intResult = _intResult!.Value
            .Bind(x => Result<int, string>.Failure(error))
            .Bind(x => Result<int, string>.Success(x + 3));
    }

    [When("I map the error to its length")]
    public void WhenIMapTheErrorToItsLength()
    {
        _intErrorResult = _intResult!.Value.MapError(err => err.Length);
    }

    [When("I match it with formatters")]
    public void WhenIMatchItWithFormatters()
    {
        _matchOutput = _intResult!.Value.Match(
            onSuccess: x => $"Success: {x}",
            onFailure: err => $"Error: {err}");
    }

    [When("I test the left identity law with toString function")]
    public void WhenITestTheLeftIdentityLawWithToStringFunction()
    {
        // Left identity: return a >>= f ≡ f a
        var a = _intResult!.Value.Value;
        Func<int, Result<string, string>> f = x => Result<string, string>.Success(x.ToString());

        var left = Result<int, string>.Success(a).Bind(f);
        var right = f(a);

        left.IsSuccess.Should().Be(right.IsSuccess);
        left.Value.Should().Be(right.Value);
    }

    [When("I test the associativity law with double and add3 functions")]
    public void WhenITestTheAssociativityLawWithDoubleAndAdd3Functions()
    {
        // Associativity: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = _intResult!.Value;
        Func<int, Result<int, string>> f = x => Result<int, string>.Success(x * 2);
        Func<int, Result<int, string>> g = x => Result<int, string>.Success(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.IsSuccess.Should().Be(right.IsSuccess);
        left.Value.Should().Be(right.Value);
    }

    [Then("the result should be successful")]
    public void ThenTheResultShouldBeSuccessful()
    {
        _intResult.Should().NotBeNull();
        _intResult!.Value.IsSuccess.Should().BeTrue();
    }

    [Then("the result should be a failure")]
    public void ThenTheResultShouldBeAFailure()
    {
        if (_intResult.HasValue)
        {
            _intResult.Value.IsFailure.Should().BeTrue();
        }
        else if (_intErrorResult.HasValue)
        {
            _intErrorResult.Value.IsFailure.Should().BeTrue();
        }
    }

    [Then("the result should not be a failure")]
    public void ThenTheResultShouldNotBeAFailure()
    {
        _intResult.Should().NotBeNull();
        _intResult!.Value.IsFailure.Should().BeFalse();
    }

    [Then("the result should not be successful")]
    public void ThenTheResultShouldNotBeSuccessful()
    {
        _intResult.Should().NotBeNull();
        _intResult!.Value.IsSuccess.Should().BeFalse();
    }

    [Then(@"the value should be (.*)")]
    public void ThenTheValueShouldBe(int expected)
    {
        _intResult.Should().NotBeNull();
        _intResult!.Value.IsSuccess.Should().BeTrue();
        _intResult.Value.Value.Should().Be(expected);
    }

    [Then(@"the error should be ""(.*)""")]
    public void ThenTheErrorShouldBe(string expected)
    {
        _intResult.Should().NotBeNull();
        _intResult!.Value.IsFailure.Should().BeTrue();
        _intResult.Value.Error.Should().Be(expected);
    }

    [Then(@"the error length should be (.*)")]
    public void ThenTheErrorLengthShouldBe(int expected)
    {
        _intErrorResult.Should().NotBeNull();
        _intErrorResult!.Value.IsFailure.Should().BeTrue();
        _intErrorResult.Value.Error.Should().Be(expected);
    }

    [Then("it should throw InvalidOperationException")]
    public void ThenItShouldThrowInvalidOperationException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<InvalidOperationException>();
    }

    [Then(@"the output should be ""(.*)""")]
    public void ThenTheOutputShouldBe(string expected)
    {
        _matchOutput.Should().Be(expected);
    }

    [Then("both sides should be equal")]
    public void ThenBothSidesShouldBeEqual()
    {
        // This is validated in the When step itself
        // Just a placeholder to complete the scenario
    }
}
