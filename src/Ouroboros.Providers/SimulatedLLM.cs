using System.Threading.Tasks;

namespace Ouroboros.Providers
{
    /// <summary>
    /// Simulated LLM provider for testing purposes.
    /// </summary>
    public class SimulatedLLM : ILlmProvider
    {
        /// <summary>
        /// Generates a response based on the prompt.
        /// </summary>
        /// <param name="prompt">The input prompt.</param>
        /// <returns>A simulated response.</returns>
        public async Task<string> GenerateAsync(string prompt)
        {
            await Task.Delay(10); // Simulate async operation

            if (prompt.Contains("suggestions"))
                return "[{\"step\":\"UseDraft\",\"explanation\":\"Generate draft\",\"confidence\":0.9}]";

            return "Simulated response";
        }
    }

    /// <summary>
    /// Interface for LLM providers.
    /// </summary>
    public interface ILlmProvider
    {
        Task<string> GenerateAsync(string prompt);
    }
}