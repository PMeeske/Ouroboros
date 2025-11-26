namespace LangChainPipeline.CLI.Configuration;

/// <summary>
/// Default settings for document ingestion operations.
/// </summary>
public static class DefaultIngestionSettings
{
    /// <summary>Default chunk size for text splitting (characters).</summary>
    public const int ChunkSize = 1800;
    
    /// <summary>Default chunk overlap for text splitting (characters).</summary>
    public const int ChunkOverlap = 180;
    
    /// <summary>Default maximum archive size (500 MB).</summary>
    public const long MaxArchiveSizeBytes = 500 * 1024 * 1024;
    
    /// <summary>Default maximum compression ratio for zip files.</summary>
    public const double MaxCompressionRatio = 200.0;
    
    /// <summary>Default batch size for vector additions.</summary>
    public const int DefaultBatchSize = 16;
    
    /// <summary>Default document separator for combining contexts.</summary>
    public const string DocumentSeparator = "\n---\n";
}

/// <summary>
/// Standard keys used in pipeline state and chain values.
/// </summary>
public static class StateKeys
{
    public const string Text = "text";
    public const string Context = "context";
    public const string Question = "question";
    public const string Prompt = "prompt";
    public const string Topic = "topic";
    public const string Query = "query";
    public const string Input = "input";
    public const string Output = "output";
    public const string Documents = "documents";
}
