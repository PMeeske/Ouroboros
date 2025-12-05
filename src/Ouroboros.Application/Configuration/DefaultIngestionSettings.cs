namespace Ouroboros.Application.Configuration;

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
    
    /// <summary>Default maximum lines to read from CSV files.</summary>
    public const int CsvMaxLines = 50;
    
    /// <summary>Default maximum bytes to preview from binary files (128 KB).</summary>
    public const int BinaryMaxBytes = 128 * 1024;
    
    /// <summary>Default batch size for streaming operations.</summary>
    public const int StreamingBatchSize = 8;
    
    /// <summary>Reduced CSV line limit for streaming operations.</summary>
    public const int StreamingCsvMaxLines = 20;
    
    /// <summary>Reduced binary preview size for streaming operations (32 KB).</summary>
    public const int StreamingBinaryMaxBytes = 32 * 1024;
}

/// <summary>
/// Default settings for RAG operations.
/// </summary>
public static class RagDefaults
{
    /// <summary>Default group size for divide-and-conquer RAG.</summary>
    public const int GroupSize = 6;
    
    /// <summary>Default number of sub-questions for decompose-and-aggregate RAG.</summary>
    public const int SubQuestions = 4;
    
    /// <summary>Default number of documents per sub-question.</summary>
    public const int DocumentsPerSubQuestion = 6;
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

