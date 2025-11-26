namespace LangChainPipeline.CLI.Configuration;

public record ZipIngestionConfig
{
    public required string ArchivePath { get; init; }
    public bool IncludeXmlText { get; init; } = true;
    public int CsvMaxLines { get; init; } = 50;
    public int BinaryMaxBytes { get; init; } = 128 * 1024;
    public long MaxTotalBytes { get; init; } = 500 * 1024 * 1024;
    public double MaxCompressionRatio { get; init; } = 200.0;
    public HashSet<string>? SkipKinds { get; init; }
    public HashSet<string>? OnlyKinds { get; init; }
    public bool NoEmbed { get; init; } = false;
    public int BatchSize { get; init; } = 16;
}
