using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Learning;

namespace Ouroboros.Core.Tracing
{
    public interface ITracingService
    {
        Task<Result<Unit, string>> EnableTracing();
        Task<Result<Unit, string>> DisableTracing();
        Task<Result<Unit, string>> EnableTracingWithCallbacks(Action started, Action stopped);
        Task<Result<Activity, string>> StartActivity(string name, Dictionary<string, object>? tags = null);
        Task<Result<Unit, string>> RecordEvent(Activity activity, string eventName, string detail);
        Task<Result<Unit, string>> RecordException(Activity activity, Exception exception);
        Task<Result<Unit, string>> SetStatus(Activity activity, string status, string description);
        Task<Result<Unit, string>> AddTag(Activity activity, string key, string value);
        Option<string> GetTraceId(Activity activity);
        Option<string> GetSpanId(Activity activity);
        Option<string> GetParentSpanId(Activity activity);
        Task<Result<Activity, string>> TraceToolExecution(string toolName, string input);
        Task<Result<Activity, string>> TracePipelineExecution(string pipelineName);
        Task<Result<Activity, string>> TraceLlmRequest(string model, int maxTokens);
        Task<Result<Activity, string>> TraceVectorOperation(string operation, int dimension);
        Task<Result<Unit, string>> CompleteLlmRequest(Activity activity, int responseLength, int tokenCount);
        Task<Result<Unit, string>> CompleteToolExecution(Activity activity, bool success, int outputLength);
        Task<Result<Unit, string>> StopActivity(Activity activity);
    }
}
