// <copyright file="CliStepsConstants.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.CLI;

/// <summary>
/// Default settings for file ingestion and chunking operations.
/// </summary>
public static class DefaultIngestionSettings
{
    /// <summary>
    /// Default chunk size for text splitting (in characters).
    /// </summary>
    public const int ChunkSize = 1800;

    /// <summary>
    /// Default chunk overlap for text splitting (in characters).
    /// </summary>
    public const int ChunkOverlap = 180;

    /// <summary>
    /// Maximum archive size in bytes (500MB).
    /// </summary>
    public const long MaxArchiveSizeBytes = 500 * 1024 * 1024;

    /// <summary>
    /// Maximum compression ratio for archives.
    /// </summary>
    public const double MaxCompressionRatio = 200.0;

    /// <summary>
    /// Default batch size for document processing.
    /// </summary>
    public const int DefaultBatchSize = 16;

    /// <summary>
    /// Maximum number of lines to read from CSV files.
    /// </summary>
    public const int CsvMaxLines = 50;

    /// <summary>
    /// Maximum bytes to read from binary files (128KB).
    /// </summary>
    public const int BinaryMaxBytes = 128 * 1024;

    /// <summary>
    /// Document separator used to join multiple documents.
    /// </summary>
    public const string DocumentSeparator = "\n---\n";

    /// <summary>
    /// Maximum number of items to display in console output before truncation.
    /// </summary>
    public const int MaxConsoleDisplayItems = 100;

    /// <summary>
    /// Smaller batch size used for streaming operations.
    /// </summary>
    public const int StreamingBatchSize = 8;

    /// <summary>
    /// Maximum lines for streaming CSV operations.
    /// </summary>
    public const int StreamingCsvMaxLines = 20;

    /// <summary>
    /// Maximum bytes for streaming binary operations (32KB).
    /// </summary>
    public const int StreamingBinaryMaxBytes = 32 * 1024;

    /// <summary>
    /// Default batch size for batched directory ingestion.
    /// </summary>
    public const int DirectoryBatchedDefaultSize = 256;

    /// <summary>
    /// Default retrieval count for similarity search.
    /// </summary>
    public const int DefaultRetrievalK = 4;

    /// <summary>
    /// Default group size for divide-and-conquer RAG operations.
    /// </summary>
    public const int DefaultRagGroupSize = 6;
}

/// <summary>
/// State keys used for template formatting and state management.
/// </summary>
public static class StateKeys
{
    /// <summary>
    /// Key for text content in state.
    /// </summary>
    public const string Text = "text";

    /// <summary>
    /// Key for context information in state.
    /// </summary>
    public const string Context = "context";

    /// <summary>
    /// Key for question content in state.
    /// </summary>
    public const string Question = "question";

    /// <summary>
    /// Key for prompt content in state.
    /// </summary>
    public const string Prompt = "prompt";

    /// <summary>
    /// Key for topic information in state.
    /// </summary>
    public const string Topic = "topic";

    /// <summary>
    /// Key for query content in state.
    /// </summary>
    public const string Query = "query";
}

/// <summary>
/// Argument keys used for parsing CLI arguments.
/// </summary>
public static class ArgKeys
{
    /// <summary>
    /// Root directory path argument key.
    /// </summary>
    public const string Root = "root";

    /// <summary>
    /// File extension filter argument key.
    /// </summary>
    public const string Ext = "ext";

    /// <summary>
    /// Exclusion pattern argument key.
    /// </summary>
    public const string Exclude = "exclude";

    /// <summary>
    /// Pattern matching argument key.
    /// </summary>
    public const string Pattern = "pattern";

    /// <summary>
    /// Maximum value argument key.
    /// </summary>
    public const string Max = "max";

    /// <summary>
    /// No recursion flag argument key.
    /// </summary>
    public const string NoRec = "norec";
}
