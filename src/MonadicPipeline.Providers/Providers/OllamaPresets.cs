using LangChain.Providers.Ollama;

namespace LangChainPipeline.Providers;

public static class OllamaPresets
{
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
}
