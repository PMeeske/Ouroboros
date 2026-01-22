using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Learning;
using Ouroboros.Diagnostics;

namespace Ouroboros.Core.Tracing
{
    public class TracingService : ITracingService
    {
        private bool _isEnabled = false;
        private Action? _startedCallback;
        private Action? _stoppedCallback;
        private Activity? _currentActivity;

        public async Task<Result<Unit, string>> EnableTracing()
        {
            TracingConfiguration.EnableTracing(
                onActivityStarted: _ => _startedCallback?.Invoke(),
                onActivityStopped: _ => _stoppedCallback?.Invoke()
            );
            _isEnabled = true;
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Unit, string>> DisableTracing()
        {
            TracingConfiguration.DisableTracing();
            _isEnabled = false;
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Unit, string>> EnableTracingWithCallbacks(Action started, Action stopped)
        {
            TracingConfiguration.EnableTracing(
                onActivityStarted: _ => started(),
                onActivityStopped: _ => stopped()
            );
            _isEnabled = true;
            _startedCallback = started;
            _stoppedCallback = stopped;
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Activity, string>> StartActivity(string name, Dictionary<string, object>? tags = null)
        {
            if (!_isEnabled) return Result<Activity, string>.Failure("Tracing is disabled");
            var activity = DistributedTracing.StartActivity(name, tags: tags?.ToDictionary(kv => kv.Key, kv => (object?)kv.Value));
            if (activity == null) return Result<Activity, string>.Failure("Failed to start activity");
            _currentActivity = activity;
            return Result<Activity, string>.Success(activity);
        }

        public async Task<Result<Unit, string>> RecordEvent(Activity activity, string eventName, string detail)
        {
            activity.AddEvent(new ActivityEvent(eventName, tags: new ActivityTagsCollection { { "detail", detail } }));
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Unit, string>> RecordException(Activity activity, Exception exception)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag("exception.type", exception.GetType().Name);
            activity.SetTag("exception.message", exception.Message);
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Unit, string>> SetStatus(Activity activity, string status, string description)
        {
            var statusCode = status == "Ok" ? ActivityStatusCode.Ok : ActivityStatusCode.Error;
            activity.SetStatus(statusCode, description);
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Unit, string>> AddTag(Activity activity, string key, string value)
        {
            activity.SetTag(key, value);
            return Result<Unit, string>.Success(Unit.Value);
        }

        public Option<string> GetTraceId(Activity activity)
        {
            var id = activity.TraceId.ToString();
            return string.IsNullOrEmpty(id) ? Option<string>.None() : Option<string>.Some(id);
        }

        public Option<string> GetSpanId(Activity activity)
        {
            var id = activity.SpanId.ToString();
            return string.IsNullOrEmpty(id) ? Option<string>.None() : Option<string>.Some(id);
        }

        public Option<string> GetParentSpanId(Activity activity)
        {
            return activity.ParentSpanId == default(ActivitySpanId) ? Option<string>.None() : Option<string>.Some(activity.ParentSpanId.ToString());
        }

        public async Task<Result<Activity, string>> TraceToolExecution(string toolName, string input)
        {
            var result = await StartActivity($"ToolExecution-{toolName}");
            if (result.IsFailure) return result;
            var activity = result.Value;
            activity.SetTag("tool.name", toolName);
            activity.SetTag("tool.input", input);
            return Result<Activity, string>.Success(activity);
        }

        public async Task<Result<Activity, string>> TracePipelineExecution(string pipelineName)
        {
            var result = await StartActivity($"PipelineExecution-{pipelineName}");
            if (result.IsFailure) return result;
            var activity = result.Value;
            activity.SetTag("pipeline.name", pipelineName);
            return Result<Activity, string>.Success(activity);
        }

        public async Task<Result<Activity, string>> TraceLlmRequest(string model, int maxTokens)
        {
            var result = await StartActivity("llm.request");
            if (result.IsFailure) return result;
            var activity = result.Value;
            activity.SetTag("llm.model", model);
            activity.SetTag("llm.max_tokens", maxTokens);
            return Result<Activity, string>.Success(activity);
        }

        public async Task<Result<Activity, string>> TraceVectorOperation(string operation, int dimension)
        {
            var result = await StartActivity($"VectorOperation-{operation}");
            if (result.IsFailure) return result;
            var activity = result.Value;
            activity.SetTag("vector.operation", operation);
            activity.SetTag("vector.dimension", dimension);
            return Result<Activity, string>.Success(activity);
        }

        public async Task<Result<Unit, string>> CompleteLlmRequest(Activity activity, int responseLength, int tokenCount)
        {
            activity.SetTag("llm.response_length", responseLength);
            activity.SetTag("llm.token_count", tokenCount);
            activity.SetStatus(ActivityStatusCode.Ok);
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Unit, string>> CompleteToolExecution(Activity activity, bool success, int outputLength)
        {
            activity.SetTag("tool.success", success);
            activity.SetTag("tool.output_length", outputLength);
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            return Result<Unit, string>.Success(Unit.Value);
        }

        public async Task<Result<Unit, string>> StopActivity(Activity activity)
        {
            activity.Stop();
            return Result<Unit, string>.Success(Unit.Value);
        }
    }
}
