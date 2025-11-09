# Custom Agent Usage Examples

This document demonstrates practical examples of using the GitHub Copilot custom agents in real-world scenarios.

## Example 1: Building a New Pipeline Step

**Scenario:** You want to add a new reasoning step that validates LLM outputs.

**Question to @functional-pipeline-expert:**
> "I need to create a composable pipeline step that validates LLM outputs against a JSON schema. The step should use Result<T> for error handling and integrate with the existing PipelineBranch. How should I structure this?"

**Expected Guidance:**
The agent would provide:
- Step signature using `Step<PipelineBranch, PipelineBranch>`
- Proper use of `Result<T>` monad for validation errors
- Integration with event sourcing
- Example implementation following MonadicPipeline patterns

## Example 2: Setting Up Model Orchestration

**Scenario:** You want to optimize model selection based on task complexity.

**Question to @ai-orchestration-specialist:**
> "I have multiple LLMs (GPT-4, GPT-3.5, and a local Ollama model). How do I set up SmartModelOrchestrator to automatically select the best model based on task complexity, cost, and performance?"

**Expected Guidance:**
The agent would provide:
- How to register models with capabilities
- Use case classification strategies
- Performance metrics tracking
- Code examples for orchestrator setup

## Example 3: Production Deployment

**Scenario:** You need to deploy the WebApi to Kubernetes with zero downtime.

**Question to @cloud-devops-expert:**
> "I need to deploy MonadicPipeline WebApi to Kubernetes with health checks, autoscaling, and zero-downtime updates. What's the complete setup?"

**Expected Guidance:**
The agent would provide:
- Kubernetes deployment YAML with health probes
- HorizontalPodAutoscaler configuration
- Rolling update strategy
- Service and Ingress setup
- Complete CI/CD pipeline

## Example 4: Building an Android App

**Scenario:** You need to create an Android app that uses the MonadicPipeline API.

**Question to @android-expert:**
> "I need to build an Android app that connects to the MonadicPipeline API. How should I structure the app using MVVM, Jetpack Compose, and Hilt for dependency injection?"

**Expected Guidance:**
The agent would provide:
- Clean Architecture setup with domain, data, and presentation layers
- Repository pattern for API integration
- ViewModel with StateFlow for UI state management
- Compose UI components with proper state hoisting
- Hilt modules for dependency injection
- Example implementation with proper error handling

## Example 5: Combining Multiple Agents

**Scenario:** Building a complete feature from design to deployment.

### Step 1: Design (Functional Pipeline Expert)
```
@functional-pipeline-expert How should I design a skill extraction pipeline that
analyzes successful executions and creates reusable skill templates?
```

### Step 2: AI Integration (AI Orchestration Specialist)
```
@ai-orchestration-specialist How do I integrate the skill extraction pipeline with
the existing MetaAI planner to enable automatic learning from execution experiences?
```

### Step 3: Deployment (Cloud DevOps Expert)
```
@cloud-devops-expert How do I deploy the updated system with the new skill
extraction feature, including metrics monitoring for skill extraction success rates?
```

## Testing the Agents

### Functional Pipeline Expert Tests

**Test 1: Monadic Composition**
```
@functional-pipeline-expert Review this code for proper monadic composition:

public static Step<string, string> ProcessText() =>
    async input =>
    {
        var result = await SomeOperation(input);
        if (result == null) throw new Exception("Failed");
        return result;
    };
```

**Expected Response:** The agent should identify the exception-based error handling and suggest using Result<T> instead.

**Test 2: Event Sourcing**
```
@functional-pipeline-expert Is this the correct way to add an event to a pipeline branch?

public void AddEvent(ReasoningStep step)
{
    Events.Add(step);
}
```

**Expected Response:** The agent should identify the mutable state modification and suggest using immutable updates with record `with` syntax.

### AI Orchestration Specialist Tests

**Test 1: Model Selection**
```
@ai-orchestration-specialist How do I implement confidence-based routing where
low-confidence tasks use ensemble models?
```

**Expected Response:** The agent should provide code examples with confidence thresholds and ensemble strategies.

**Test 2: Skill Extraction**
```
@ai-orchestration-specialist When should I extract a skill from an execution result?
```

**Expected Response:** The agent should explain quality thresholds, novelty detection, and reusability criteria.

### Cloud DevOps Expert Tests

**Test 1: Health Checks**
```
@cloud-devops-expert My pods are showing CrashLoopBackOff. How do I debug this?
```

**Expected Response:** The agent should provide kubectl commands to check logs, describe pods, and inspect health checks.

**Test 2: Security**
```
@cloud-devops-expert How do I run my container as non-root with minimal privileges?
```

**Expected Response:** The agent should provide Dockerfile examples with USER directives and security best practices.

### Android Expert Tests

**Test 1: Architecture**
```
@android-expert How do I implement MVVM with Clean Architecture for my Android app?
```

**Expected Response:** The agent should provide a layered architecture with domain, data, and presentation layers, including ViewModel, Repository, and Use Case examples.

**Test 2: Jetpack Compose**
```
@android-expert Review this Compose code for proper state management:

@Composable
fun UserScreen(viewModel: UserViewModel) {
    val user = viewModel.getUser()
    Text(text = user.name)
}
```

**Expected Response:** The agent should identify the lack of lifecycle-aware state collection and suggest using collectAsStateWithLifecycle().

**Test 3: Memory Leaks**
```
@android-expert How do I prevent memory leaks when using location services in Android?
```

**Expected Response:** The agent should provide lifecycle-aware component examples with DisposableEffect for proper cleanup.

## Verification Checklist

- [ ] Agents understand MonadicPipeline architecture
- [ ] Functional Pipeline Expert provides monadic patterns
- [ ] AI Orchestration Specialist knows Meta-AI architecture
- [ ] Cloud DevOps Expert has Kubernetes expertise
- [ ] Android Expert provides modern Android best practices
- [ ] Agents provide code examples with explanations
- [ ] Agents identify anti-patterns and suggest improvements
- [ ] Documentation is clear and accessible
- [ ] Agents complement each other without overlap

## Next Steps

After validating the agents:

1. Use them in real development tasks
2. Gather feedback from team members
3. Refine agent knowledge based on common questions
4. Add more specialized agents if needed (e.g., Testing Expert, Security Specialist)
5. Update agents when new features are added to MonadicPipeline

## Known Limitations

- Agents are based on the documented knowledge in their markdown files
- They may not have access to the latest code changes without updates
- Complex questions may require multiple agent consultations
- Agents work best with specific, focused questions

## Feedback

If you encounter issues:
1. Check if you're using the right agent for the question
2. Provide more context or code examples
3. Try breaking complex questions into smaller parts
4. Consult multiple agents for different aspects

---

**Remember:** These custom agents are tools to enhance productivity. They provide guidance based on best practices and MonadicPipeline's architecture, but always review and test their suggestions before implementing in production code.
