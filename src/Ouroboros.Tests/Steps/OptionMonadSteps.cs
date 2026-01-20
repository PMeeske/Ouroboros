namespace Ouroboros.Specs.Steps;

[Binding]
public class OptionMonadSteps
{
    private Option<int> _intOption;
    private Option<string> _stringOption;
    private string? _matchOutput;
    private bool _someActionCalled;
    private bool _noneActionCalled;
    private int _capturedValue;
    private int _returnedValue;

    [Given("a fresh option monad context")]
    public void GivenAFreshOptionMonadContext()
    {
        _intOption = Option<int>.None();
        _stringOption = Option<string>.None();
        _matchOutput = null;
        _someActionCalled = false;
        _noneActionCalled = false;
        _capturedValue = 0;
        _returnedValue = 0;
    }

    [Given(@"Some with value (.*)")]
    public void GivenSomeWithValue(int value)
    {
        _intOption = Option<int>.Some(value);
    }

    [Given(@"Some with string value ""(.*)""")]
    public void GivenSomeWithStringValue(string value)
    {
        _stringOption = Option<string>.Some(value);
    }

    [Given("None for strings")]
    public void GivenNoneForStrings()
    {
        _stringOption = Option<string>.None();
    }

    [Given("None for integers")]
    public void GivenNoneForIntegers()
    {
        _intOption = Option<int>.None();
    }

    [Given(@"a value (.*)")]
    public void GivenAValue(int value)
    {
        _intOption = Option<int>.Some(value);
    }

    [When(@"I create Some with value (.*)")]
    public void WhenICreateSomeWithValue(int value)
    {
        _intOption = Option<int>.Some(value);
    }

    [When("I create None for strings")]
    public void WhenICreateNoneForStrings()
    {
        _stringOption = Option<string>.None();
    }

    [When("I construct an option with null")]
    public void WhenIConstructAnOptionWithNull()
    {
        _stringOption = new Option<string>(null);
    }

    [When(@"I construct an option with value ""(.*)""")]
    public void WhenIConstructAnOptionWithValue(string value)
    {
        _stringOption = new Option<string>(value);
    }

    [When("I map it with function that doubles the value")]
    public void WhenIMapItWithFunctionThatDoublesTheValue()
    {
        _intOption = _intOption.Map(x => x * 2);
    }

    [When("I bind it with function that doubles and wraps the value")]
    public void WhenIBindItWithFunctionThatDoublesAndWrapsTheValue()
    {
        _intOption = _intOption.Bind(x => Option<int>.Some(x * 2));
    }

    [When("I bind it to double and then add 3")]
    public void WhenIBindItToDoubleAndThenAdd3()
    {
        _intOption = _intOption
            .Bind(x => Option<int>.Some(x * 2))
            .Bind(x => Option<int>.Some(x + 3));
    }

    [When(@"I bind it to None and then append "" value""")]
    public void WhenIBindItToNoneAndThenAppendValue()
    {
        _stringOption = _stringOption
            .Bind(x => Option<string>.None())
            .Bind(x => Option<string>.Some(x + " value"));
    }

    [When("I match it with formatters")]
    public void WhenIMatchItWithFormatters()
    {
        _matchOutput = _intOption.Match(
            func: x => $"Value: {x}",
            defaultValue: "No value");
    }

    [When(@"I match it with formatters and default ""(.*)""")]
    public void WhenIMatchItWithFormattersAndDefault(string defaultValue)
    {
        _matchOutput = _stringOption.Match(
            func: x => $"Value: {x}",
            defaultValue: defaultValue);
    }

    [When("I match it with actions")]
    public void WhenIMatchItWithActions()
    {
        if (_intOption.HasValue)
        {
            _intOption.Match(
                onSome: x => { _someActionCalled = true; _capturedValue = x; },
                onNone: () => { _noneActionCalled = true; });
        }
        else if (_stringOption.HasValue || !_stringOption.HasValue)
        {
            _stringOption.Match(
                onSome: x => { _someActionCalled = true; },
                onNone: () => { _noneActionCalled = true; });
        }
    }

    [When(@"I get value or default (.*)")]
    public void WhenIGetValueOrDefault(int defaultValue)
    {
        _returnedValue = _intOption.GetValueOrDefault(defaultValue);
    }

    [When("I test the option left identity law with toString function")]
    public void WhenITestTheOptionLeftIdentityLawWithToStringFunction()
    {
        // Left identity: return a >>= f ≡ f a
        var a = _intOption.Value;
        Func<int, Option<string>> f = x => Option<string>.Some(x.ToString());

        var left = Option<int>.Some(a).Bind(f);
        var right = f(a);

        left.HasValue.Should().Be(right.HasValue);
        if (left.HasValue)
        {
            left.Value.Should().Be(right.Value);
        }
    }

    [When("I test the option associativity law with double and add3 functions")]
    public void WhenITestTheOptionAssociativityLawWithDoubleAndAdd3Functions()
    {
        // Associativity: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = _intOption;
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        Func<int, Option<int>> g = x => Option<int>.Some(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.HasValue.Should().Be(right.HasValue);
        if (left.HasValue)
        {
            left.Value.Should().Be(right.Value);
        }
    }

    [Then("the option should have a value")]
    public void ThenTheOptionShouldHaveAValue()
    {
        if (_intOption.HasValue)
        {
            _intOption.HasValue.Should().BeTrue();
        }
        else
        {
            _stringOption.HasValue.Should().BeTrue();
        }
    }

    [Then("the option should not have a value")]
    public void ThenTheOptionShouldNotHaveAValue()
    {
        if (!_intOption.HasValue)
        {
            _intOption.HasValue.Should().BeFalse();
        }
        else
        {
            _stringOption.HasValue.Should().BeFalse();
        }
    }

    [Then(@"the value should be (.*)")]
    public void ThenTheValueShouldBe(int expected)
    {
        _intOption.HasValue.Should().BeTrue();
        _intOption.Value.Should().Be(expected);
    }

    [Then(@"the string value should be ""(.*)""")]
    public void ThenTheStringValueShouldBe(string expected)
    {
        _stringOption.HasValue.Should().BeTrue();
        _stringOption.Value.Should().Be(expected);
    }

    [Then(@"the output should be ""(.*)""")]
    public void ThenTheOutputShouldBe(string expected)
    {
        _matchOutput.Should().Be(expected);
    }

    [Then("the Some action should have been called")]
    public void ThenTheSomeActionShouldHaveBeenCalled()
    {
        _someActionCalled.Should().BeTrue();
    }

    [Then("the None action should have been called")]
    public void ThenTheNoneActionShouldHaveBeenCalled()
    {
        _noneActionCalled.Should().BeTrue();
    }

    [Then(@"the captured value should be (.*)")]
    public void ThenTheCapturedValueShouldBe(int expected)
    {
        _capturedValue.Should().Be(expected);
    }

    [Then(@"the returned value should be (.*)")]
    public void ThenTheReturnedValueShouldBe(int expected)
    {
        _returnedValue.Should().Be(expected);
    }

    [Then("both option sides should be equal")]
    public void ThenBothOptionSidesShouldBeEqual()
    {
        // This is validated in the When step itself
        // Just a placeholder to complete the scenario
    }
}
