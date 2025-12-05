namespace Ouroboros.Application.Configuration;

public record ZipIngestionConfig
{
    public required string ArchivePath { get; init; }
    public bool IncludeXmlText { get; init; } = true;
    public int CsvMaxLines { get; init; } = DefaultIngestionSettings.CsvMaxLines;
    public int BinaryMaxBytes { get; init; } = DefaultIngestionSettings.BinaryMaxBytes;
    public long MaxTotalBytes { get; init; } = DefaultIngestionSettings.MaxArchiveSizeBytes;
    public double MaxCompressionRatio { get; init; } = DefaultIngestionSettings.MaxCompressionRatio;
    public HashSet<string>? SkipKinds { get; init; }
    public HashSet<string>? OnlyKinds { get; init; }
    public bool NoEmbed { get; init; } = false;
    public int BatchSize { get; init; } = DefaultIngestionSettings.DefaultBatchSize;
}

