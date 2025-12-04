#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace LangChainPipeline.Pipeline.Reasoning;

public static class Prompts
{
    public static readonly PromptTemplate Thinking = new(@"
You are a deep thinker.

Available tools (JSON Schema):
{tools_schemas}

Context:
{context}

Task:
Think deeply about '{topic}'.
Analyze the requirements, potential pitfalls, and architectural implications.
Output your thoughts in a structured manner.
If you need to verify facts or perform calculations, use tools.
Emit tool calls like: [TOOL:tool_name arguments]
Do not generate the final spec yet, just the reasoning process.
");

    public static readonly PromptTemplate Draft = new(@"
You are a careful, evidence-grounded architect. Use tools if asked.

Available tools (JSON Schema):
{tools_schemas}

Context:
{context}

Task:
Write [THOUGHTS] and [DRAFT_SPEC] for '{topic}'.
If you need calculations or lookups, emit tool calls like:
[TOOL:math 2+2*5]  or  [TOOL:search {{""q"":""tenant cache"", ""k"":3}}]
");

    public static readonly PromptTemplate Critique = new(@"
Available tools (JSON Schema):
{tools_schemas}

Context:
{context}

Draft:
{draft}

Task:
Critique the draft of '{topic}'.
- Identify [MAJOR_GAPS]
- Identify [MINOR_ISSUES]
- Identify [UNSUPPORTED_CLAIMS]
Use tools if needed (e.g. [TOOL:math (3*7)+1], [TOOL:search {""q"":""edge cases""}]).
");

    public static readonly PromptTemplate Improve = new(@"
Available tools (JSON Schema):
{tools_schemas}

Context:
{context}

Draft:
{draft}

Critique:
{critique}

Task:
Refine the draft of '{topic}' into FINAL_SPEC.
- Address all gaps/issues
- Add examples
- Include migration risks
- End with a short [CHANGELOG]
Tools allowed if helpful.
");
}
