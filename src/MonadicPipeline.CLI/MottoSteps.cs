using System.Text;
using LangChainPipeline.Tools.MeTTa;

namespace MonadicPipeline.CLI;

public static class MottoSteps
{
    /// <summary>
    /// Initializes the MeTTa-Motto environment by importing the motto module.
    /// </summary>
    public class MottoInitializeStep
    {
        private readonly IMeTTaEngine _engine;

        public MottoInitializeStep(IMeTTaEngine engine)
        {
            _engine = engine;
        }

        public async Task<Result<Unit, string>> ExecuteAsync(Unit input, CancellationToken ct = default)
        {
            var result = await _engine.ExecuteQueryAsync("!(import! &self motto)", ct);
            return result.Match(
                success => Result<Unit, string>.Success(Unit.Value),
                failure => Result<Unit, string>.Failure($"Failed to import motto: {failure}")
            );
        }
    }

    /// <summary>
    /// Sends a user message to the basic ChatGPT agent in MeTTa-Motto.
    /// Corresponds to 01_basic_chatgpt.metta functionality.
    /// </summary>
    public class MottoChatStep
    {
        private readonly IMeTTaEngine _engine;

        public MottoChatStep(IMeTTaEngine engine)
        {
            _engine = engine;
        }

        public async Task<Result<string, string>> ExecuteAsync(string input, CancellationToken ct = default)
        {
            // Escape quotes in input
            var escapedInput = input.Replace("\"", "\\\"");
            var query = $"!((chat-gpt-agent) (user \"{escapedInput}\"))";
            
            var result = await _engine.ExecuteQueryAsync(query, ct);
            return result;
        }
    }

    /// <summary>
    /// Executes a MeTTa agent defined in a script file.
    /// Corresponds to 02_metta_agent.msa and 03_agent_call.metta functionality.
    /// </summary>
    public class MottoAgentStep
    {
        private readonly IMeTTaEngine _engine;
        private readonly string _scriptPath;
        private readonly string _runnerAgent;

        public MottoAgentStep(IMeTTaEngine engine, string scriptPath, string runnerAgent = "chat-gpt-agent")
        {
            _engine = engine;
            _scriptPath = scriptPath;
            _runnerAgent = runnerAgent;
        }

        public async Task<Result<string, string>> ExecuteAsync(string input, CancellationToken ct = default)
        {
            var escapedInput = input.Replace("\"", "\\\"");
            var query = $"!(( {_runnerAgent} ) (Script \"{_scriptPath}\") (user \"{escapedInput}\"))";
            
            var result = await _engine.ExecuteQueryAsync(query, ct);
            return result;
        }
    }

    /// <summary>
    /// Uses a prompt template to generate a response.
    /// Corresponds to 04_prompt_call.metta functionality.
    /// </summary>
    public class MottoPromptStep
    {
        private readonly IMeTTaEngine _engine;
        private readonly string _templatePath;

        public MottoPromptStep(IMeTTaEngine engine, string templatePath)
        {
            _engine = engine;
            _templatePath = templatePath;
        }

        public async Task<Result<string, string>> ExecuteAsync(Dictionary<string, string> input, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.Append($"!((chat-gpt-agent) (Script \"{_templatePath}\")");
            
            foreach (var kvp in input)
            {
                sb.Append($" ({kvp.Key} \"{kvp.Value.Replace("\"", "\\\"")}\")");
            }
            
            sb.Append(")");
            
            var result = await _engine.ExecuteQueryAsync(sb.ToString(), ct);
            return result;
        }
    }

    /// <summary>
    /// Maintains a dialog history with a stateful agent.
    /// Corresponds to 07_dialog.metta functionality.
    /// </summary>
    public class MottoDialogStep
    {
        private readonly IMeTTaEngine _engine;
        private readonly string _agentScriptPath;
        private readonly string _agentName;
        private bool _initialized = false;

        public MottoDialogStep(IMeTTaEngine engine, string agentScriptPath, string agentName = "chat")
        {
            _engine = engine;
            _agentScriptPath = agentScriptPath;
            _agentName = agentName;
        }

        public async Task<Result<string, string>> ExecuteAsync(string input, CancellationToken ct = default)
        {
            if (!_initialized)
            {
                // Bind the agent: !(bind! &chat (dialog-agent "script.msa"))
                var bindQuery = $"!(bind! &{_agentName} (dialog-agent \"{_agentScriptPath}\"))";
                var bindResult = await _engine.ExecuteQueryAsync(bindQuery, ct);
                if (!bindResult.IsSuccess)
                {
                    return Result<string, string>.Failure($"Failed to bind dialog agent: {bindResult.Error}");
                }
                _initialized = true;
            }

            // Call the agent: !(&chat (user "msg"))
            var escapedInput = input.Replace("\"", "\\\"");
            var query = $"!(&{_agentName} (user \"{escapedInput}\"))";
            
            var result = await _engine.ExecuteQueryAsync(query, ct);
            return result;
        }
    }
}