#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.Providers.Ollama;

namespace Ouroboros.Providers;

public static class OllamaPresets
{
    /// <summary>
    /// Preset for DeepSeek Coder 33B with conservative defaults optimized for code generation.
    /// Automatically adapts threads, context window, and GPU usage based on <see cref="MachineCapabilities"/>.
    /// </summary>
    public static OllamaChatSettings DeepSeekCoder33B
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            // conservative defaults
            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = memMb > 64000 ? 8192 : 4096, // more RAM → larger context
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? gpus : 0,
                MainGpu = 0,
                LowVram = gpus == 0, // force low-VRAM path if CPU-only
                Temperature = 0.2f,  // coder → low creativity
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                KeepAlive = 10 * 60, // keep model in memory 10 min
                UseMmap = true,
                UseMlock = false
            };

            return settings;
        }
    }

    /// <summary>
    /// Preset for Llama 3 (general conversation). Balanced temperature and retrieval-friendly settings.
    /// Adapts to available CPU cores, memory size and GPU count.
    /// </summary>
    public static OllamaChatSettings Llama3General
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = memMb > 32000 ? 8192 : 4096,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.5f, // balanced for general chat
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                KeepAlive = 10 * 60,
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }

    /// <summary>
    /// Preset for Llama 3 summarization. Lower temperature and slightly stronger repeat penalty
    /// to encourage concise, deterministic output.
    /// </summary>
    public static OllamaChatSettings Llama3Summarize
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = memMb > 32000 ? 8192 : 4096,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.3f, // more deterministic summaries
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.15f,
                KeepAlive = 10 * 60,
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }

    /// <summary>
    /// Preset for DeepSeek R1 14B (reasoning). Larger context window and exploratory temperature
    /// for deeper chains-of-thought, within conservative system limits.
    /// </summary>
    public static OllamaChatSettings DeepSeekR1_14B_Reason
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = memMb > 64000 ? 12288 : 8192, // reasoning benefits from larger ctx
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.6f, // allow broader exploration for reasoning
                TopP = 0.92f,
                TopK = 50,
                RepeatPenalty = 1.05f,
                KeepAlive = 10 * 60,
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }

    /// <summary>
    /// Preset for DeepSeek R1 32B (reasoning). Tries to leverage up to 2 GPUs if present
    /// and expands context window with sufficient host memory.
    /// </summary>
    public static OllamaChatSettings DeepSeekR1_32B_Reason
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = memMb > 96000 ? 16384 : (memMb > 64000 ? 12288 : 8192),
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 2) : 0, // allow 2 GPUs if available
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.55f,
                TopP = 0.92f,
                TopK = 50,
                RepeatPenalty = 1.05f,
                KeepAlive = 10 * 60,
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }

    /// <summary>
    /// Preset for Mistral 7B (general). Light-weight, suitable for CPU-only or single-GPU setups,
    /// with a modest context window for RAG tasks.
    /// </summary>
    public static OllamaChatSettings Mistral7BGeneral
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = memMb > 16000 ? 4096 : 3072,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.5f,
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                KeepAlive = 10 * 60,
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }

    /// <summary>
    /// Preset for Qwen2.5 7B (general). Balanced configuration for mixed tasks and RAG.
    /// </summary>
    public static OllamaChatSettings Qwen25_7B_General
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = memMb > 16000 ? 4096 : 3072,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.45f,
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                KeepAlive = 10 * 60,
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }

    /// <summary>
    /// Preset for Phi-3 Mini (general). Small footprint model preset for quick local runs.
    /// </summary>
    public static OllamaChatSettings Phi3MiniGeneral
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = 4096,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.5f,
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                KeepAlive = 10 * 60,
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }

    /// <summary>
    /// Preset for TinyLlama optimized for high-performance parallel execution.
    /// Designed for divide-and-conquer strategies where multiple instances run concurrently.
    /// Ultra-low context window and aggressive threading for maximum throughput.
    /// </summary>
    public static OllamaChatSettings TinyLlamaFast
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            // Reserve threads for parallel execution - each instance gets fewer threads
            // to allow multiple instances to run simultaneously
            int threadsPerInstance = Math.Max(1, cores / 4);

            OllamaChatSettings settings = new OllamaChatSettings
            {
                NumCtx = 2048, // Small context for speed
                NumThread = threadsPerInstance,
                NumGpu = gpus > 0 ? 1 : 0, // Single GPU if available
                MainGpu = 0,
                LowVram = true, // Always use low VRAM mode for parallel efficiency
                Temperature = 0.4f, // Lower temperature for more deterministic parallel results
                TopP = 0.85f,
                TopK = 30,
                RepeatPenalty = 1.1f,
                KeepAlive = 5 * 60, // Shorter keep-alive for memory efficiency
                UseMmap = true,
                UseMlock = false
            };
            return settings;
        }
    }
}
