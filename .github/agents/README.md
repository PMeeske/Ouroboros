# GitHub Copilot Custom Agents

This directory contains specialized GitHub Copilot custom agents designed to assist with different aspects of the MonadicPipeline project. Each agent has deep domain expertise and provides context-aware guidance.

## Available Agents

### 1. Functional Pipeline Expert (`functional-pipeline-expert.md`)

**Expertise:** Category Theory, Functional Programming, Monadic Composition

This agent specializes in:
- Building type-safe, composable AI workflows
- Implementing monadic patterns (`Result<T>`, `Option<T>`)
- Creating Kleisli arrows and pipeline steps
- Event sourcing and immutable state management
- LangChain integration with functional paradigms

**Use this agent when:**
- Implementing new pipeline steps or reasoning components
- Working with monads and functional composition
- Designing event-sourced architectures
- Need guidance on category theory principles
- Building type-safe abstractions

**Example invocation:**
```
@functional-pipeline-expert How do I create a new composable pipeline step that processes reasoning states?
```

### 2. AI Orchestration Specialist (`ai-orchestration-specialist.md`)

**Expertise:** AI Orchestration, Self-Improving Agents, Meta-Learning

This agent specializes in:
- Smart model selection and orchestration
- Building self-improving agent systems
- Implementing skill extraction and transfer learning
- Metacognitive architectures (Phase 2)
- Uncertainty-aware routing and confidence-based decisions
- Performance optimization and cost management

**Use this agent when:**
- Setting up model orchestrators or multi-model systems
- Implementing learning loops (plan-execute-verify-learn)
- Building self-evaluating or self-improving agents
- Need guidance on Meta-AI architecture
- Optimizing for performance and cost

**Example invocation:**
```
@ai-orchestration-specialist How do I implement a self-improving agent that learns from execution experiences?
```

### 3. Cloud-Native DevOps Expert (`cloud-devops-expert.md`)

**Expertise:** Kubernetes, CI/CD, Infrastructure as Code, Observability

This agent specializes in:
- Kubernetes deployments and cluster management
- Docker container optimization and security
- CI/CD pipeline design with GitHub Actions
- Infrastructure as Code (Terraform, Helm)
- Observability (metrics, logging, tracing)
- Cloud platforms (IONOS, AWS, Azure, GCP)

**Use this agent when:**
- Deploying to Kubernetes or managing clusters
- Setting up CI/CD pipelines
- Implementing monitoring and observability
- Troubleshooting deployment issues
- Optimizing infrastructure costs
- Security hardening

**Example invocation:**
```
@cloud-devops-expert How do I set up zero-downtime deployments with health checks?
```

## How to Use Custom Agents

### In GitHub Copilot Chat

1. **Direct Mention:**
   ```
   @functional-pipeline-expert How do I implement a critique arrow?
   ```

2. **Context-Aware Questions:**
   ```
   @ai-orchestration-specialist I need to build a model orchestrator that selects
   the best LLM based on task complexity and performance metrics. How should I approach this?
   ```

3. **Code Review:**
   ```
   @functional-pipeline-expert Review this code for proper monadic composition:
   [paste code]
   ```

### Best Practices

1. **Choose the Right Agent:**
   - Functional programming questions → `@functional-pipeline-expert`
   - AI/ML orchestration questions → `@ai-orchestration-specialist`
   - Infrastructure/deployment questions → `@cloud-devops-expert`

2. **Provide Context:**
   - Include relevant code snippets
   - Mention specific files or components
   - Describe what you're trying to achieve

3. **Be Specific:**
   - Ask focused questions
   - Provide error messages if troubleshooting
   - Mention constraints (performance, security, etc.)

4. **Iterate:**
   - Start with high-level design questions
   - Follow up with implementation details
   - Request code reviews for critical changes

## Agent Capabilities Matrix

| Capability | Functional Pipeline | AI Orchestration | Cloud DevOps |
|------------|-------------------|------------------|--------------|
| Monadic Composition | ✅ Expert | ⚠️ Basic | ❌ N/A |
| Event Sourcing | ✅ Expert | ⚠️ Basic | ❌ N/A |
| LangChain Integration | ✅ Expert | ✅ Expert | ❌ N/A |
| Model Orchestration | ⚠️ Basic | ✅ Expert | ❌ N/A |
| Self-Improving Agents | ⚠️ Basic | ✅ Expert | ❌ N/A |
| Skill Extraction | ❌ N/A | ✅ Expert | ❌ N/A |
| Kubernetes | ❌ N/A | ❌ N/A | ✅ Expert |
| CI/CD Pipelines | ❌ N/A | ❌ N/A | ✅ Expert |
| Infrastructure as Code | ❌ N/A | ❌ N/A | ✅ Expert |
| Observability | ⚠️ Basic | ⚠️ Basic | ✅ Expert |
| Security | ⚠️ Basic | ⚠️ Basic | ✅ Expert |

Legend:
- ✅ Expert: Deep expertise, primary responsibility
- ⚠️ Basic: General knowledge, can assist
- ❌ N/A: Outside scope of expertise

## Common Workflows

### Workflow 1: Adding a New Feature

1. **Design Phase:**
   ```
   @functional-pipeline-expert I want to add a new reasoning step that validates
   LLM outputs against a schema. What's the best way to structure this as a
   composable pipeline step?
   ```

2. **Implementation:**
   - Follow agent guidance
   - Implement using monadic patterns
   - Add tests

3. **Review:**
   ```
   @functional-pipeline-expert Review this implementation for functional
   programming best practices:
   [paste code]
   ```

### Workflow 2: Optimizing AI Performance

1. **Analysis:**
   ```
   @ai-orchestration-specialist My pipeline is too slow. How can I use the
   SmartModelOrchestrator to optimize model selection based on task complexity?
   ```

2. **Implementation:**
   - Set up orchestrator
   - Configure model capabilities
   - Implement metrics tracking

3. **Monitoring:**
   ```
   @cloud-devops-expert How do I set up Prometheus metrics to monitor model
   selection performance?
   ```

### Workflow 3: Production Deployment

1. **Containerization:**
   ```
   @cloud-devops-expert What's the best way to containerize the MonadicPipeline
   WebApi with multi-stage builds and security best practices?
   ```

2. **Kubernetes Setup:**
   ```
   @cloud-devops-expert Create a production-ready Kubernetes deployment with
   health checks, autoscaling, and zero-downtime updates.
   ```

3. **CI/CD:**
   ```
   @cloud-devops-expert Set up a GitHub Actions workflow that builds, tests,
   and deploys to Kubernetes on merge to main.
   ```

## Contributing

When adding new agents:

1. Create a new markdown file in this directory
2. Follow the structure of existing agents:
   - Clear expertise section
   - Design principles with examples
   - Code patterns and best practices
   - Common mistakes to avoid
3. Update this README with agent information
4. Add to the capabilities matrix

## Agent Update Policy

Custom agents should be updated when:
- New major features are added to MonadicPipeline
- Architecture patterns change
- New best practices emerge
- Common questions are identified

## Feedback

If you find issues with agent responses or have suggestions for improvements:
1. Open an issue describing the problem
2. Tag it with `custom-agents`
3. Mention which agent needs improvement

---

**Last Updated:** 2025-11-09

**Version:** 1.0.0

**Maintained by:** MonadicPipeline Team
