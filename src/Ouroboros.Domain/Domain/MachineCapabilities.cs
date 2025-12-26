#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Ouroboros.Domain;

/// <summary>
/// Machine capabilities detection utilities.
/// Note: Uses global:: prefix for System.Environment to avoid conflict with Ouroboros.Domain.Environment namespace.
/// </summary>
public static class MachineCapabilities
{
    public static int CpuCores => global::System.Environment.ProcessorCount;

    public static long TotalMemoryMb
    {
        get
        {
            try
            {
                return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            }
            catch
            {
                return 4096; // fallback
            }
        }
    }

    public static int GpuCount
    {
        get
        {
            // You can read env vars or defaults
            string? env = global::System.Environment.GetEnvironmentVariable("OLLAMA_NUM_GPU");
            if (int.TryParse(env, out int gpus)) return gpus;
            return 0; // assume CPU only if unknown
        }
    }
}
