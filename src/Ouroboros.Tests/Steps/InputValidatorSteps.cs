using Ouroboros.Core.Security;
using ValidationResult = Ouroboros.Core.Security.ValidationResult;

namespace Ouroboros.Specs.Steps;

[Binding]
public class InputValidatorSteps
{
    private InputValidator? _validator;
    private ValidationContext? _context;
    private ValidationResult? _result;

    [Given("a fresh input validator context")]
    public void GivenAFreshInputValidatorContext()
    {
        _validator = null;
        _context = null;
        _result = null;
    }

    [Given("an input validator")]
    public void GivenAnInputValidator()
    {
        _validator = new InputValidator();
        _context = ValidationContext.Default;
    }

    [Given("an input validator with empty not allowed")]
    public void GivenAnInputValidatorWithEmptyNotAllowed()
    {
        _validator = new InputValidator();
        _context = new ValidationContext { AllowEmpty = false };
    }

    [Given("an input validator with empty allowed")]
    public void GivenAnInputValidatorWithEmptyAllowed()
    {
        _validator = new InputValidator();
        _context = new ValidationContext { AllowEmpty = true };
    }

    [Given(@"an input validator with max length (.*)")]
    public void GivenAnInputValidatorWithMaxLength(int maxLength)
    {
        _validator = new InputValidator();
        _context = new ValidationContext { MaxLength = maxLength };
    }

    [Given(@"an input validator with min length (.*)")]
    public void GivenAnInputValidatorWithMinLength(int minLength)
    {
        _validator = new InputValidator();
        _context = new ValidationContext { MinLength = minLength };
    }

    [Given("an input validator with blocked characters")]
    public void GivenAnInputValidatorWithBlockedCharacters()
    {
        _validator = new InputValidator();
        _context = new ValidationContext
        {
            BlockedCharacters = new HashSet<char> { '<', '>' },
        };
    }

    [Given("an input validator with trimming enabled")]
    public void GivenAnInputValidatorWithTrimmingEnabled()
    {
        _validator = new InputValidator();
        _context = new ValidationContext { TrimWhitespace = true };
    }

    [Given("an input validator with line ending normalization")]
    public void GivenAnInputValidatorWithLineEndingNormalization()
    {
        _validator = new InputValidator();
        _context = new ValidationContext { NormalizeLineEndings = true };
    }

    [Given("a lenient input validator with HTML escaping")]
    public void GivenALenientInputValidatorWithHtmlEscaping()
    {
        _validator = new InputValidator(ValidationOptions.Lenient);
        _context = new ValidationContext { EscapeHtml = true };
    }

    [Given("a strict validation context")]
    public void GivenAStrictValidationContext()
    {
        _context = ValidationContext.Strict;
    }

    [Given("a tool parameter validation context")]
    public void GivenAToolParameterValidationContext()
    {
        _context = ValidationContext.ToolParameter;
    }

    [When(@"I validate ""(.*)""")]
    public void WhenIValidate(string input)
    {
        _validator.Should().NotBeNull();
        _context.Should().NotBeNull();
        // Unescape common escape sequences like \0, \r, \n
        input = System.Text.RegularExpressions.Regex.Unescape(input);
        _result = _validator!.ValidateAndSanitize(input, _context!);
    }

    [When("I validate empty string")]
    public void WhenIValidateEmptyString()
    {
        _validator.Should().NotBeNull();
        _context.Should().NotBeNull();
        _result = _validator!.ValidateAndSanitize(string.Empty, _context!);
    }

    [When(@"I validate a string of (.*) characters")]
    public void WhenIValidateAStringOfCharacters(int length)
    {
        _validator.Should().NotBeNull();
        _context.Should().NotBeNull();
        var input = new string('a', length);
        _result = _validator!.ValidateAndSanitize(input, _context!);
    }

    [Then("the validation should succeed")]
    public void ThenTheValidationShouldSucceed()
    {
        _result.Should().NotBeNull();
        _result!.IsValid.Should().BeTrue();
    }

    [Then("the validation should fail")]
    public void ThenTheValidationShouldFail()
    {
        _result.Should().NotBeNull();
        _result!.IsValid.Should().BeFalse();
    }

    [Then(@"the sanitized value should be ""(.*)""")]
    public void ThenTheSanitizedValueShouldBe(string expected)
    {
        _result.Should().NotBeNull();
        _result!.SanitizedValue.Should().Be(expected);
    }

    [Then("there should be no errors")]
    public void ThenThereShouldBeNoErrors()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().BeEmpty();
    }

    [Then("there should be an error about empty input")]
    public void ThenThereShouldBeAnErrorAboutEmptyInput()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain("Input cannot be empty");
    }

    [Then("there should be an error about maximum length")]
    public void ThenThereShouldBeAnErrorAboutMaximumLength()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain(e => e.Contains("exceeds maximum length"));
    }

    [Then("there should be an error about minimum length")]
    public void ThenThereShouldBeAnErrorAboutMinimumLength()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain(e => e.Contains("must be at least"));
    }

    [Then("there should be an error about SQL injection")]
    public void ThenThereShouldBeAnErrorAboutSqlInjection()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain(e => e.Contains("SQL injection"));
    }

    [Then("there should be an error about script injection")]
    public void ThenThereShouldBeAnErrorAboutScriptInjection()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain(e => e.Contains("script injection"));
    }

    [Then("there should be an error about command injection")]
    public void ThenThereShouldBeAnErrorAboutCommandInjection()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain(e => e.Contains("command injection"));
    }

    [Then("there should be an error about null bytes")]
    public void ThenThereShouldBeAnErrorAboutNullBytes()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain(e => e.Contains("null bytes"));
    }

    [Then("there should be an error about blocked character")]
    public void ThenThereShouldBeAnErrorAboutBlockedCharacter()
    {
        _result.Should().NotBeNull();
        _result!.Errors.Should().Contain(e => e.Contains("blocked character"));
    }

    [Then(@"the sanitized value should contain ""(.*)""")]
    public void ThenTheSanitizedValueShouldContain(string expected)
    {
        _result.Should().NotBeNull();
        _result!.SanitizedValue.Should().Contain(expected);
    }

    [Then(@"the max length should be (.*)")]
    public void ThenTheMaxLengthShouldBe(int expected)
    {
        _context.Should().NotBeNull();
        _context!.MaxLength.Should().Be(expected);
    }

    [Then("HTML escaping should be enabled")]
    public void ThenHtmlEscapingShouldBeEnabled()
    {
        _context.Should().NotBeNull();
        _context!.EscapeHtml.Should().BeTrue();
    }

    [Then("blocked characters should include angle brackets")]
    public void ThenBlockedCharactersShouldIncludeAngleBrackets()
    {
        _context.Should().NotBeNull();
        _context!.BlockedCharacters.Should().NotBeNull();
        _context.BlockedCharacters!.Should().Contain(new[] { '<', '>', '&', '"', '\'' });
    }

    [Then("whitespace trimming should be enabled")]
    public void ThenWhitespaceTrimmingShouldBeEnabled()
    {
        _context.Should().NotBeNull();
        _context!.TrimWhitespace.Should().BeTrue();
    }

    [Then("line ending normalization should be enabled")]
    public void ThenLineEndingNormalizationShouldBeEnabled()
    {
        _context.Should().NotBeNull();
        _context!.NormalizeLineEndings.Should().BeTrue();
    }
}
