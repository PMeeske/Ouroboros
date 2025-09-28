using LangChainPipeline.Tools;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the ToolCallParser DSL implementation.
/// Verifies that the parser can handle various tool invocation formats correctly.
/// </summary>
public static class ToolCallParserTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("=== Running ToolCallParser DSL Tests ===");
        
        TestBasicToolCalls();
        TestJsonArguments();
        TestMathExpressions();
        TestComplexNestedJson();
        TestEdgeCases();
        TestErrorHandling();
        TestArgumentTypeDetection();
        TestMultipleToolCalls();
        
        Console.WriteLine("✓ All ToolCallParser DSL tests passed!");
    }

    private static void TestBasicToolCalls()
    {
        Console.WriteLine("Testing basic tool calls...");

        // Test simple tool with no arguments
        var result1 = ToolCallParser.ParseSingleToolCall("[TOOL:simple]");
        Assert(result1.IsSuccess, "Simple tool should parse successfully");
        var call1 = result1.Value;
        Assert(call1.Name == "simple", $"Expected 'simple', got '{call1.Name}'");
        Assert(call1.Arguments == "", $"Expected empty args, got '{call1.Arguments}'");

        // Test tool with simple text arguments
        var result2 = ToolCallParser.ParseSingleToolCall("[TOOL:math 2+2*5]");
        Assert(result2.IsSuccess, "Math tool should parse successfully");
        var call2 = result2.Value;
        Assert(call2.Name == "math", $"Expected 'math', got '{call2.Name}'");
        Assert(call2.Arguments == "2+2*5", $"Expected '2+2*5', got '{call2.Arguments}'");

        Console.WriteLine("✓ Basic tool calls test passed");
    }

    private static void TestJsonArguments()
    {
        Console.WriteLine("Testing JSON arguments...");

        // Test simple JSON object
        var result1 = ToolCallParser.ParseSingleToolCall("[TOOL:search {\"q\":\"test\", \"k\":3}]");
        Assert(result1.IsSuccess, "Search tool should parse successfully");
        var call1 = result1.Value;
        Assert(call1.Name == "search", $"Expected 'search', got '{call1.Name}'");
        Assert(call1.Arguments == "{\"q\":\"test\", \"k\":3}", $"Expected JSON args, got '{call1.Arguments}'");

        // Test JSON with spaces and complex strings
        var result2 = ToolCallParser.ParseSingleToolCall("[TOOL:search {\"q\":\"tenant cache issues\", \"k\":5}]");
        Assert(result2.IsSuccess, "Complex search should parse successfully");
        var call2 = result2.Value;
        Assert(call2.Name == "search", $"Expected 'search', got '{call2.Name}'");
        Assert(call2.Arguments.Contains("tenant cache issues"), "Should preserve spaces in JSON strings");

        Console.WriteLine("✓ JSON arguments test passed");
    }

    private static void TestMathExpressions()
    {
        Console.WriteLine("Testing math expressions...");

        // Test parenthesized expressions
        var result1 = ToolCallParser.ParseSingleToolCall("[TOOL:math (3*7)+1]");
        Assert(result1.IsSuccess, "Parenthesized math should parse successfully");
        var call1 = result1.Value;
        Assert(call1.Name == "math", $"Expected 'math', got '{call1.Name}'");
        Assert(call1.Arguments == "(3*7)+1", $"Expected '(3*7)+1', got '{call1.Arguments}'");

        // Test complex expressions with spaces
        var result2 = ToolCallParser.ParseSingleToolCall("[TOOL:math (10 - 5) / 2 + 3 * 4]");
        Assert(result2.IsSuccess, "Complex math with spaces should parse successfully");
        var call2 = result2.Value;
        Assert(call2.Arguments == "(10 - 5) / 2 + 3 * 4", $"Expected complex expression, got '{call2.Arguments}'");

        Console.WriteLine("✓ Math expressions test passed");
    }

    private static void TestComplexNestedJson()
    {
        Console.WriteLine("Testing complex nested JSON...");

        // Test nested JSON objects
        var complexJson = "[TOOL:complex {\"config\":{\"nested\":{\"value\":\"test data\"}},\"array\":[1,2,3]}]";
        var result = ToolCallParser.ParseSingleToolCall(complexJson);
        Assert(result.IsSuccess, "Complex nested JSON should parse successfully");
        var call = result.Value;
        Assert(call.Name == "complex", $"Expected 'complex', got '{call.Name}'");
        Assert(call.Arguments.Contains("nested"), "Should preserve nested structure");
        Assert(call.Arguments.Contains("[1,2,3]"), "Should preserve array structure");

        Console.WriteLine("✓ Complex nested JSON test passed");
    }

    private static void TestEdgeCases()
    {
        Console.WriteLine("Testing edge cases...");

        // Test tool name with numbers/underscores
        var result1 = ToolCallParser.ParseSingleToolCall("[TOOL:tool_123 test]");
        Assert(result1.IsSuccess, "Tool with underscores should parse");
        Assert(result1.Value.Name == "tool_123", "Should handle underscores in names");

        // Test empty arguments
        var result2 = ToolCallParser.ParseSingleToolCall("[TOOL:empty ]");
        Assert(result2.IsSuccess, "Tool with empty args should parse");
        Assert(result2.Value.Arguments.Trim() == "", "Should handle empty arguments");

        // Test multiple spaces
        var result3 = ToolCallParser.ParseSingleToolCall("[TOOL:spaced    multiple   spaces]");
        Assert(result3.IsSuccess, "Tool with multiple spaces should parse");
        Assert(result3.Value.Arguments == "multiple   spaces", "Should preserve argument spacing");

        Console.WriteLine("✓ Edge cases test passed");
    }

    private static void TestErrorHandling()
    {
        Console.WriteLine("Testing error handling...");

        // Test invalid format
        var result1 = ToolCallParser.ParseSingleToolCall("TOOL:invalid");
        Assert(result1.IsFailure, "Invalid format should fail");

        // Test empty input
        var result2 = ToolCallParser.ParseSingleToolCall("");
        Assert(result2.IsFailure, "Empty input should fail");

        // Test missing closing bracket
        var result3 = ToolCallParser.ParseSingleToolCall("[TOOL:incomplete");
        Assert(result3.IsFailure, "Missing closing bracket should fail");

        // Test empty tool name
        var result4 = ToolCallParser.ParseSingleToolCall("[TOOL: ]");
        Assert(result4.IsFailure, "Empty tool name should fail");

        Console.WriteLine("✓ Error handling test passed");
    }

    private static void TestArgumentTypeDetection()
    {
        Console.WriteLine("Testing argument type detection...");

        // Test JSON detection
        Assert(ToolCallParser.IsJsonArguments("{\"key\":\"value\"}"), "Should detect JSON objects");
        Assert(ToolCallParser.IsJsonArguments("[1,2,3]"), "Should detect JSON arrays");
        Assert(!ToolCallParser.IsJsonArguments("simple text"), "Should not detect plain text as JSON");

        // Test math expression detection
        Assert(ToolCallParser.IsMathExpression("2+2*5"), "Should detect math expressions");
        Assert(ToolCallParser.IsMathExpression("(10-5)/2"), "Should detect parenthesized math");
        Assert(!ToolCallParser.IsMathExpression("simple text"), "Should not detect plain text as math");

        // Test JSON validation
        var validJson = ToolCallParser.ValidateJsonArguments("{\"valid\":true}");
        Assert(validJson.IsSuccess, "Valid JSON should pass validation");

        var invalidJson = ToolCallParser.ValidateJsonArguments("{invalid json}");
        Assert(invalidJson.IsFailure, "Invalid JSON should fail validation");

        Console.WriteLine("✓ Argument type detection test passed");
    }

    private static void TestMultipleToolCalls()
    {
        Console.WriteLine("Testing multiple tool calls in text...");

        string text = @"Let me help you with that.
[TOOL:math 2+2]
The result is shown above.
[TOOL:search {""q"":""test"", ""k"":2}]
Here are the search results.";

        var calls = ToolCallParser.ParseToolCalls(text);
        Assert(calls.Count == 2, $"Expected 2 tool calls, got {calls.Count}");
        
        Assert(calls[0].Name == "math", "First call should be math");
        Assert(calls[0].Arguments == "2+2", "First call args should be '2+2'");
        
        Assert(calls[1].Name == "search", "Second call should be search");
        Assert(calls[1].Arguments == "{\"q\":\"test\", \"k\":2}", "Second call should have JSON args");

        Console.WriteLine("✓ Multiple tool calls test passed");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Test assertion failed: {message}");
        }
    }
}