namespace LangChainPipeline.Tools;

public sealed record ToolExecution(
    string ToolName,
    string Arguments,
    string Output,
    DateTime Timestamp
);