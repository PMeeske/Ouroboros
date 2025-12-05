namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for PromptTemplate covering formatting, validation, and error handling.
/// </summary>
public class PromptTemplateTests
{
    [Fact]
    public void Constructor_WithValidTemplate_CreatesInstance()
    {
        // Arrange & Act
        var template = new PromptTemplate("Hello {name}!");

        // Assert
        template.Should().NotBeNull();
        template.ToString().Should().Be("Hello {name}!");
    }

    [Fact]
    public void Constructor_WithNullTemplate_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new PromptTemplate(null!));
    }

    [Fact]
    public void Format_WithMatchingVariables_ReplacesPlaceholders()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, you are {age} years old!");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["age"] = "30"
        };

        // Act
        var result = template.Format(vars);

        // Assert
        result.Should().Be("Hello Alice, you are 30 years old!");
    }

    [Fact]
    public void Format_WithNullVariables_ThrowsArgumentNullException()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}!");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => template.Format(null!));
    }

    [Fact]
    public void Format_WithMissingVariables_LeavesPlaceholders()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, you are {age} years old!");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice"
        };

        // Act
        var result = template.Format(vars);

        // Assert
        result.Should().Be("Hello Alice, you are {age} years old!");
    }

    [Fact]
    public void Format_WithNullValue_ReplacesWithEmptyString()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}!");
        var vars = new Dictionary<string, string>
        {
            ["name"] = null!
        };

        // Act
        var result = template.Format(vars);

        // Assert
        result.Should().Be("Hello !");
    }

    [Fact]
    public void Format_WithNoPlaceholders_ReturnsOriginalTemplate()
    {
        // Arrange
        var template = new PromptTemplate("Hello world!");
        var vars = new Dictionary<string, string>();

        // Act
        var result = template.Format(vars);

        // Assert
        result.Should().Be("Hello world!");
    }

    [Fact]
    public void SafeFormat_WithAllRequiredVariables_ReturnsSuccess()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}!");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice"
        };

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello Alice!");
    }

    [Fact]
    public void SafeFormat_WithMissingVariables_ReturnsFailure()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, you are {age} years old!");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice"
        };

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Missing required variables");
        result.Error.Should().Contain("age");
    }

    [Fact]
    public void SafeFormat_WithNullVariables_ReturnsFailure()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}!");

        // Act
        var result = template.SafeFormat(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Variables dictionary cannot be null");
    }

    [Fact]
    public void SafeFormat_WithExtraVariables_ReturnsSuccess()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}!");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["age"] = "30",
            ["city"] = "Paris"
        };

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello Alice!");
    }

    [Fact]
    public void RequiredVariables_ExtractsAllPlaceholders()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}, you are {age} years old and live in {city}!");

        // Act
        var required = template.RequiredVariables;

        // Assert
        required.Should().HaveCount(3);
        required.Should().Contain("name");
        required.Should().Contain("age");
        required.Should().Contain("city");
    }

    [Fact]
    public void RequiredVariables_WithDuplicatePlaceholders_ReturnsUnique()
    {
        // Arrange
        var template = new PromptTemplate("Hello {name}! Nice to meet you, {name}!");

        // Act
        var required = template.RequiredVariables;

        // Assert
        required.Should().HaveCount(1);
        required.Should().Contain("name");
    }

    [Fact]
    public void RequiredVariables_WithNoPlaceholders_ReturnsEmpty()
    {
        // Arrange
        var template = new PromptTemplate("Hello world!");

        // Act
        var required = template.RequiredVariables;

        // Assert
        required.Should().BeEmpty();
    }

    [Fact]
    public void ImplicitOperator_ConvertsStringToTemplate()
    {
        // Arrange & Act
        PromptTemplate template = "Hello {name}!";

        // Assert
        template.Should().NotBeNull();
        template.ToString().Should().Be("Hello {name}!");
    }

    [Fact]
    public void Format_WithMultiplePlaceholders_ReplacesAll()
    {
        // Arrange
        var template = new PromptTemplate("{greeting} {name}, welcome to {place}!");
        var vars = new Dictionary<string, string>
        {
            ["greeting"] = "Hi",
            ["name"] = "Bob",
            ["place"] = "Ouroboros"
        };

        // Act
        var result = template.Format(vars);

        // Assert
        result.Should().Be("Hi Bob, welcome to Ouroboros!");
    }

    [Fact]
    public void Format_WithEmptyTemplate_ReturnsEmpty()
    {
        // Arrange
        var template = new PromptTemplate("");
        var vars = new Dictionary<string, string>();

        // Act
        var result = template.Format(vars);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Test {var1}", "var1")]
    [InlineData("Test {var1} and {var2}", "var1", "var2")]
    [InlineData("No variables", new string[] { })]
    [InlineData("{var1}{var2}{var3}", "var1", "var2", "var3")]
    public void RequiredVariables_ExtractsCorrectPlaceholders(string templateString, params string[] expected)
    {
        // Arrange
        var template = new PromptTemplate(templateString);

        // Act
        var required = template.RequiredVariables;

        // Assert
        required.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Format_WithNestedBraces_HandlesGracefully()
    {
        // Arrange
        var template = new PromptTemplate("Code: {{name}}");
        var vars = new Dictionary<string, string>
        {
            ["name"] = "value"
        };

        // Act
        var result = template.Format(vars);

        // Assert
        result.Should().Contain("value");
    }

    [Fact]
    public void SafeFormat_WithComplexTemplate_ValidatesAllVariables()
    {
        // Arrange
        var template = new PromptTemplate(@"
            User: {user_name}
            Query: {query}
            Context: {context}
            Response: {response}
        ");
        var vars = new Dictionary<string, string>
        {
            ["user_name"] = "Alice",
            ["query"] = "What is AI?",
            ["context"] = "AI refers to artificial intelligence"
        };

        // Act
        var result = template.SafeFormat(vars);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("response");
    }

    [Fact]
    public void ToString_ReturnsOriginalTemplate()
    {
        // Arrange
        var originalTemplate = "Hello {name}, you are {age} years old!";
        var template = new PromptTemplate(originalTemplate);

        // Act
        var result = template.ToString();

        // Assert
        result.Should().Be(originalTemplate);
    }
}
