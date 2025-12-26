using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Specs.Steps;

[Binding]
public class SchemaGeneratorSteps
{
    private string? _generatedSchema;
    private Exception? _thrownException;

    private sealed class ComplexArgs
    {
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("identifier")]
        public int Count { get; set; }

        public double? Optional { get; set; }

        public string[] Tags { get; set; } = [];
    }

    [Given("a fresh schema generation context")]
    public void GivenAFreshSchemaGenerationContext()
    {
        _generatedSchema = null;
        _thrownException = null;
    }

    [Given("a complex type with multiple properties")]
    public void GivenAComplexTypeWithMultipleProperties()
    {
        // The ComplexArgs type is already defined in this class
    }

    [When("I attempt to generate a schema from a null type")]
    public void WhenIAttemptToGenerateASchemaFromANullType()
    {
        try
        {
            _generatedSchema = SchemaGenerator.GenerateSchema(null!);
        }
        catch (Exception ex)
        {
            _thrownException = ex;
        }
    }

    [When("I generate the schema")]
    public void WhenIGenerateTheSchema()
    {
        _generatedSchema = SchemaGenerator.GenerateSchema(typeof(ComplexArgs));
    }

    [Then("it should throw an ArgumentNullException")]
    public void ThenItShouldThrowAnArgumentNullException()
    {
        _thrownException.Should().NotBeNull();
        _thrownException.Should().BeOfType<ArgumentNullException>();
    }

    [Then(@"the schema should have type ""(.*)""")]
    public void ThenTheSchemaShouldHaveType(string expectedType)
    {
        _generatedSchema.Should().NotBeNull();
        using JsonDocument document = JsonDocument.Parse(_generatedSchema!);
        JsonElement root = document.RootElement;
        root.GetProperty("type").GetString().Should().Be(expectedType);
    }

    [Then(@"the schema should define property ""(.*)"" as ""(.*)""")]
    public void ThenTheSchemaShouldDefinePropertyAs(string propertyName, string propertyType)
    {
        _generatedSchema.Should().NotBeNull();
        using JsonDocument document = JsonDocument.Parse(_generatedSchema!);
        JsonElement root = document.RootElement;
        JsonElement properties = root.GetProperty("properties");
        properties.GetProperty(propertyName).GetProperty("type").GetString().Should().Be(propertyType);
    }

    [Then(@"the schema should mark ""(.*)"" as required")]
    public void ThenTheSchemaShouldMarkAsRequired(string propertyName)
    {
        _generatedSchema.Should().NotBeNull();
        using JsonDocument document = JsonDocument.Parse(_generatedSchema!);
        JsonElement root = document.RootElement;
        string[] required = root.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToArray();
        required.Should().Contain(propertyName);
    }

    [Then(@"the schema should not mark ""(.*)"" as required")]
    public void ThenTheSchemaShouldNotMarkAsRequired(string propertyName)
    {
        _generatedSchema.Should().NotBeNull();
        using JsonDocument document = JsonDocument.Parse(_generatedSchema!);
        JsonElement root = document.RootElement;
        string[] required = root.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToArray();
        required.Should().NotContain(propertyName);
    }

    [Then(@"the property ""(.*)"" should have JsonPropertyName ""(.*)""")]
    public void ThenThePropertyShouldHaveJsonPropertyName(string propertyName, string jsonName)
    {
        _generatedSchema.Should().NotBeNull();
        using JsonDocument document = JsonDocument.Parse(_generatedSchema!);
        JsonElement root = document.RootElement;
        JsonElement properties = root.GetProperty("properties");
        properties.GetProperty(propertyName).GetProperty("description").GetString().Should().Be(jsonName);
    }
}
