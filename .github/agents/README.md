# GitHub Copilot Custom Agents

This directory contains specialized GitHub Copilot custom agents designed to assist with different aspects of the Ouroboros project. Each agent has deep domain expertise and provides context-aware guidance.

## 🔴 MANDATORY TESTING POLICY 🔴

**ALL custom agents enforce mandatory testing for EVERY functional change.**

Each agent is configured as a **valuable professional** who:
- ✅ NEVER introduces untested code
- ✅ ALWAYS writes tests BEFORE or DURING implementation
- ✅ ENSURES 100% of new/changed code is tested
- ✅ VALIDATES all quality gates pass before completion
- ✅ DOCUMENTS test coverage and results

**This policy is NON-NEGOTIABLE.** Untested code will be rejected in code review regardless of how well-written it appears.

### Testing Standards
All agents adhere to these minimum standards:
- **Unit Test Coverage:** ≥85% for new code (≥90% for core logic)
- **Integration Tests:** All component interactions tested
- **Quality Gates:** All tests pass, no regressions
- **Documentation:** Test strategy explained in PR descriptions
- **Code Review:** Test evidence MUST be provided

See individual agent files for domain-specific testing requirements.

---

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

### 4. GitHub Actions Expert (`github-actions-expert.md`)

**Expertise:** GitHub Actions Workflows, CI/CD Automation, Workflow Optimization

This agent specializes in:
- GitHub Actions workflow design and optimization
- CI/CD pipeline automation and best practices
- Reusable workflows and composite actions
- Matrix strategies and parallel execution
- Caching strategies for faster builds
- Security scanning and vulnerability detection
- Deployment automation and release management
- Workflow debugging and troubleshooting

**Use this agent when:**
- Designing or optimizing GitHub Actions workflows
- Setting up CI/CD pipelines with GitHub Actions
- Implementing automated testing and deployment
- Troubleshooting workflow failures
- Optimizing build times with caching
- Implementing security scanning in workflows
- Creating reusable workflow components

**Example invocation:**
```
@github-actions-expert How do I create a reusable workflow for .NET builds with caching?
@github-actions-expert How do I implement a progressive deployment workflow with staging and production environments?
```

### 5. Android & MAUI Expert (`android-expert.md`)

**Expertise:** Android Development (Kotlin), .NET MAUI Cross-Platform (C#), Mobile Architecture

This agent specializes in:
- Native Android development with Kotlin and Jetpack Compose
- Cross-platform development with .NET MAUI (C#)
- Mobile architecture patterns (MVVM, Clean Architecture)
- Dependency injection (Hilt for Android, built-in DI for MAUI)
- Coroutines/Flow (Kotlin) and async/await (C#)
- Performance optimization and memory management
- Mobile testing strategies (unit, UI, integration)
- Material Design and cross-platform UI
- Security best practices for mobile apps

**Use this agent when:**
- Building native Android applications with Kotlin
- Developing cross-platform apps with .NET MAUI
- Implementing mobile architecture components
- Optimizing app performance and memory usage
- Writing mobile unit and UI tests
- Integrating third-party libraries
- Implementing mobile security best practices

**Example invocation:**
```
@android-expert How do I implement a MVVM architecture with Jetpack Compose and Hilt?
@android-expert How do I build a cross-platform app with .NET MAUI that shares business logic?
```

### 6. Testing & Quality Assurance Expert (`testing-quality-expert.md`)

**Expertise:** Comprehensive Testing, Code Coverage, Mutation Testing, Quality Metrics

This agent specializes in:
- Unit, integration, and end-to-end testing strategies
- Mutation testing with Stryker.NET
- Code coverage analysis and reporting
- Property-based testing with FsCheck
- Performance benchmarking with BenchmarkDotNet
- Test automation and CI/CD integration
- Quality metrics and code analysis

**Use this agent when:**
- Writing comprehensive test suites
- Improving code coverage and test quality
- Setting up mutation testing
- Implementing property-based tests
- Configuring test automation pipelines
- Analyzing code quality metrics
- Troubleshooting flaky tests

**Example invocation:**
```
@testing-quality-expert How do I set up mutation testing for my monadic pipeline code?
```

### 7. C# & .NET Architecture Expert (`csharp-dotnet-expert.md`)

**Expertise:** C# Language Features, .NET 8 Patterns, Performance Optimization

This agent specializes in:
- Modern C# features (C# 12, records, pattern matching)
- Async/await best practices and ValueTask optimization
- Memory-efficient code with Span<T> and Memory<T>
- Dependency injection and service lifetime management
- LINQ optimization and custom operators
- Performance profiling and optimization

**Use this agent when:**
- Leveraging modern C# language features
- Optimizing async/await patterns
- Implementing memory-efficient code
- Configuring dependency injection
- Writing high-performance LINQ queries
- Profiling and optimizing bottlenecks
- Implementing advanced .NET patterns

**Example invocation:**
```
@csharp-dotnet-expert How do I optimize this LINQ query for better performance?
```

### 8. API Design & Documentation Expert (`api-design-expert.md`)

**Expertise:** RESTful API Design, OpenAPI, API Versioning, Developer Experience

This agent specializes in:
- RESTful API design principles and best practices
- OpenAPI/Swagger specification and documentation
- API versioning strategies (URL, header, content negotiation)
- Request/response validation and error handling
- Pagination, filtering, and HATEOAS
- Rate limiting and API security

**Use this agent when:**
- Designing new API endpoints
- Creating OpenAPI specifications
- Implementing API versioning
- Writing API documentation
- Implementing pagination and filtering
- Setting up rate limiting
- Improving developer experience

**Example invocation:**
```
@api-design-expert How do I design a RESTful API for pipeline management with proper versioning?
```

### 9. Database & Persistence Expert (`database-persistence-expert.md`)

**Expertise:** Vector Databases (Qdrant), Event Sourcing, Data Modeling, Caching

This agent specializes in:
- Qdrant vector database integration and optimization
- Event sourcing and CQRS patterns
- Repository pattern and specifications
- Multi-level caching strategies
- Optimistic concurrency control
- Connection resilience and retry policies

**Use this agent when:**
- Integrating vector databases
- Implementing event sourcing
- Designing persistence layers
- Optimizing database queries
- Setting up caching strategies
- Handling concurrency conflicts
- Managing database connections

**Example invocation:**
```
@database-persistence-expert How do I optimize vector similarity search in Qdrant?
```

### 10. Security & Compliance Expert (`security-compliance-expert.md`)

**Expertise:** Application Security, Authentication, Authorization, Secrets Management

This agent specializes in:
- OWASP Top 10 vulnerabilities and mitigations
- Authentication (OAuth 2.0, JWT, API keys)
- Authorization (RBAC, ABAC, policy-based)
- Secrets management (Azure Key Vault, Kubernetes Secrets)
- Input validation and output sanitization
- Audit logging and compliance (GDPR, SOC 2)

**Use this agent when:**
- Implementing authentication/authorization
- Managing secrets securely
- Validating and sanitizing inputs
- Setting up audit logging
- Addressing security vulnerabilities
- Ensuring compliance requirements
- Security hardening

**Example invocation:**
```
@security-compliance-expert How do I implement secure JWT authentication with proper secret management?
```

### 11. .NET Senior Developer Customer Agent (`dotnet-senior-developer-agent.md`)

**Expertise:** Production-Ready Code, Specification Compliance, Comprehensive Testing, No Shortcuts

This agent specializes in:
- **Diligent Development**: Thorough, persistent, and meticulous implementation
- **Specification Adherence**: Exact compliance with requirements, no assumptions
- **Complete Solutions**: Features are finished when they're truly finished, not just "mostly done"
- **Production Readiness**: Error handling, logging, monitoring, security from day one
- **Comprehensive Testing**: ≥90% coverage, mutation testing, all scenarios covered
- **Quality Standards**: SOLID principles, Clean Architecture, code review excellence
- **No Compromises**: Takes no shortcuts, does things the right way even if it takes longer

**Use this agent when:**
- You need someone who won't take shortcuts or deliver half-solutions
- Requirements must be followed exactly without deviation
- Production-ready code quality is non-negotiable
- Comprehensive testing and error handling are mandatory
- You need a persistent developer who ensures tasks are truly complete
- Code must meet the highest professional standards
- Features must work perfectly, not just in the happy path

**Example invocation:**
```
@dotnet-senior-developer-agent Implement a customer registration API that meets production standards with comprehensive error handling and testing.
@dotnet-senior-developer-agent Review this code and ensure it meets all specification requirements with no shortcuts taken.
```

**Key Characteristics:**
- ✅ **No TODOs in production code** - either implement it or don't ship it
- ✅ **Every error path handled** - production systems encounter errors constantly
- ✅ **Comprehensive testing** - untested code is broken code
- ✅ **Specification compliance** - requirements are sacred
- ✅ **Production-ready from day one** - logging, monitoring, security, resilience
- ✅ **Professional pride** - code represents your reputation

### 12. Python Review Expert (`python-review-expert.md`)

**Expertise:** Python Code Review, Type Safety, Testing, Modern Python Patterns

This agent specializes in:
- Modern Python features (3.10+): pattern matching, type unions, dataclasses
- Type hints and static type checking (mypy, pyright)
- Testing with pytest: fixtures, parametrization, async tests, property-based testing
- Code quality: PEP 8, Black/Ruff formatting, pylint analysis
- Async/await patterns: asyncio, async context managers, async iterators
- Error handling: custom exceptions, context managers, proper logging
- Best practices: SOLID principles, dependency injection, resource management

**Use this agent when:**
- Reviewing Python code for quality and correctness
- Ensuring proper type hints and type safety
- Writing comprehensive pytest test suites
- Implementing async/await patterns correctly
- Setting up Python code quality tools (mypy, ruff, black)
- Following Python best practices and conventions
- Integrating Python components with the Ouroboros system

**Example invocation:**
```
@python-review-expert Review my Ollama client code for type safety and async patterns.
@python-review-expert How do I write comprehensive pytest tests for this async function?
@python-review-expert What's the best way to structure error handling in this Python module?
```

**Key Characteristics:**
- ✅ **Type hints everywhere** - full type coverage with mypy validation
- ✅ **Comprehensive pytest tests** - ≥85% coverage, async tests, property-based tests
- ✅ **Modern Python** - leverages Python 3.10+ features
- ✅ **Code quality** - formatted with Black/Ruff, linted with Ruff/Pylint
- ✅ **Proper async** - correct async/await patterns, context managers
- ✅ **Error handling** - custom exceptions, no bare excepts, proper logging
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
   - GitHub Actions/CI/CD questions → `@github-actions-expert`
   - Mobile development questions → `@android-expert`
   - Testing and quality questions → `@testing-quality-expert`
   - C# and .NET questions → `@csharp-dotnet-expert`
   - API design questions → `@api-design-expert`
   - Database and persistence questions → `@database-persistence-expert`
   - Security and compliance questions → `@security-compliance-expert`
   - Production-ready development, specification compliance → `@dotnet-senior-developer-agent`
   - Python code review, testing, type safety → `@python-review-expert`

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

5. **🔴 ALWAYS Include Testing 🔴:**
   - **EVERY agent requires tests for EVERY functional change**
   - Write tests BEFORE or DURING implementation
   - Include test coverage in your questions (e.g., "How do I test this?")
   - Show test results when requesting code review
   - Follow the testing standards defined by each agent
   - **Untested code will be REJECTED - no exceptions**

## Agent Capabilities Matrix

| Capability | Functional | AI Orch | DevOps | GH Actions | Android | Testing | C#/.NET | API | Database | Security | Sr Dev | Python |
|------------|------------|---------|--------|------------|---------|---------|---------|-----|----------|----------|--------|--------|
| Monadic Composition | ✅ | ⚠️ | ❌ | ❌ | ❌ | ✅ | ✅ | ❌ | ⚠️ | ❌ | ✅ | ⚠️ |
| Event Sourcing | ✅ | ⚠️ | ❌ | ❌ | ❌ | ✅ | ⚠️ | ❌ | ✅ | ❌ | ✅ | ❌ |
| LangChain Integration | ✅ | ✅ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ✅ |
| Model Orchestration | ⚠️ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ |
| Self-Improving Agents | ⚠️ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Kubernetes | ❌ | ❌ | ✅ | ⚠️ | ❌ | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ | ❌ |
| CI/CD Pipelines | ❌ | ❌ | ✅ | ✅ | ⚠️ | ✅ | ⚠️ | ❌ | ❌ | ⚠️ | ✅ | ⚠️ |
| GitHub Actions | ❌ | ❌ | ⚠️ | ✅ | ❌ | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ | ❌ |
| Workflow Optimization | ❌ | ❌ | ⚠️ | ✅ | ❌ | ⚠️ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ❌ |
| Reusable Workflows | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Matrix Strategies | ❌ | ❌ | ❌ | ✅ | ❌ | ⚠️ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Infrastructure as Code | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ | ❌ |
| Observability | ⚠️ | ⚠️ | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ⚠️ |
| Unit Testing | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ |
| Integration Testing | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ |
| Mutation Testing | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ⚠️ | ❌ | ❌ | ❌ | ✅ | ⚠️ |
| Code Coverage | ⚠️ | ❌ | ⚠️ | ✅ | ⚠️ | ✅ | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| C# Language Features | ⚠️ | ⚠️ | ⚠️ | ❌ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| Python Language Features | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ✅ |
| Type Hints/Safety | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ | ✅ | ⚠️ | ❌ | ⚠️ | ✅ | ✅ |
| Async/Await Patterns | ✅ | ⚠️ | ❌ | ❌ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ |
| Memory Optimization | ⚠️ | ❌ | ❌ | ❌ | ✅ | ⚠️ | ✅ | ❌ | ⚠️ | ❌ | ✅ | ⚠️ |
| Dependency Injection | ⚠️ | ⚠️ | ⚠️ | ❌ | ✅ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| RESTful API Design | ❌ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ | ⚠️ | ✅ | ❌ | ⚠️ | ✅ | ⚠️ |
| OpenAPI/Swagger | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ✅ | ❌ | ❌ | ⚠️ | ❌ |
| API Versioning | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ⚠️ | ❌ |
| Vector Databases | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ❌ | ❌ | ✅ | ❌ | ⚠️ | ⚠️ |
| Qdrant | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ |
| CQRS | ⚠️ | ❌ | ❌ | ❌ | ❌ | ⚠️ | ⚠️ | ⚠️ | ✅ | ❌ | ✅ | ❌ |
| Repository Pattern | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ✅ | ⚠️ | ❌ | ✅ | ❌ | ✅ | ⚠️ |
| Caching Strategies | ❌ | ⚠️ | ⚠️ | ✅ | ⚠️ | ❌ | ⚠️ | ⚠️ | ✅ | ❌ | ✅ | ⚠️ |
| Authentication | ❌ | ❌ | ⚠️ | ⚠️ | ✅ | ⚠️ | ⚠️ | ✅ | ❌ | ✅ | ✅ | ⚠️ |
| Authorization | ❌ | ❌ | ⚠️ | ⚠️ | ✅ | ⚠️ | ⚠️ | ✅ | ❌ | ✅ | ✅ | ⚠️ |
| Secrets Management | ❌ | ❌ | ✅ | ✅ | ⚠️ | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ | ⚠️ |
| Input Validation | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ✅ | ⚠️ | ✅ | ⚠️ | ✅ | ✅ | ✅ |
| Audit Logging | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ❌ | ❌ | ❌ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ |
| OWASP Top 10 | ❌ | ❌ | ⚠️ | ⚠️ | ⚠️ | ❌ | ❌ | ⚠️ | ❌ | ✅ | ✅ | ⚠️ |
| Mobile Development | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Error Handling | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ |
| Production Readiness | ⚠️ | ⚠️ | ✅ | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Specification Compliance | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ⚠️ | ✅ | ⚠️ | ✅ | ✅ | ✅ |
| Code Quality Standards | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | ⚠️ | ✅ | ✅ |
| No Shortcuts Allowed | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ⚠️ | ⚠️ | ⚠️ | ⚠️ | ✅ | ✅ |

**Legend:**
- ✅ Expert: Deep expertise, primary responsibility
- ⚠️ Basic: General knowledge, can assist
- ❌ N/A: Outside scope of expertise

**Column Headers:**
- Functional = Functional Pipeline Expert
- AI Orch = AI Orchestration Specialist
- DevOps = Cloud-Native DevOps Expert
- GH Actions = GitHub Actions Expert
- Android = Android & MAUI Expert
- Testing = Testing & Quality Assurance Expert
- C#/.NET = C# & .NET Architecture Expert
- API = API Design & Documentation Expert
- Database = Database & Persistence Expert
- Security = Security & Compliance Expert
- Sr Dev = .NET Senior Developer Customer Agent
- Python = Python Review Expert

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
   @cloud-devops-expert What's the best way to containerize the Ouroboros
   WebApi with multi-stage builds and security best practices?
   ```

2. **Kubernetes Setup:**
   ```
   @cloud-devops-expert Create a production-ready Kubernetes deployment with
   health checks, autoscaling, and zero-downtime updates.
   ```

3. **CI/CD:**
   ```
   @github-actions-expert Set up a GitHub Actions workflow that builds, tests,
   and deploys to Kubernetes on merge to main.
   ```

### Workflow 4: API Development

1. **Design:**
   ```
   @api-design-expert How should I design a RESTful API for pipeline execution
   with proper versioning and error handling?
   ```

2. **Implementation:**
   ```
   @csharp-dotnet-expert What's the best way to structure the WebApi project
   with dependency injection and async patterns?
   ```

3. **Security:**
   ```
   @security-compliance-expert How do I implement JWT authentication with
   proper secret management for the API?
   ```

4. **Testing:**
   ```
   @testing-quality-expert How do I write integration tests for the API
   endpoints with proper mocking?
   ```

### Workflow 5: Database Integration

1. **Design:**
   ```
   @database-persistence-expert How do I integrate Qdrant for vector search
   with optimal performance?
   ```

2. **Implementation:**
   ```
   @csharp-dotnet-expert What's the best pattern for implementing the
   repository with async operations?
   ```

3. **Testing:**
   ```
   @testing-quality-expert How do I test database interactions with
   integration tests?
   ```

### Workflow 6: CI/CD Pipeline Optimization

1. **Workflow Design:**
   ```
   @github-actions-expert How do I create a reusable workflow for .NET builds
   that includes caching, testing, and code coverage reporting?
   ```

2. **Matrix Testing:**
   ```
   @github-actions-expert How do I set up matrix testing across multiple .NET
   versions and operating systems with optimal parallelization?
   ```

3. **Security Scanning:**
   ```
   @github-actions-expert How do I integrate CodeQL and dependency scanning
   into my CI pipeline with proper failure thresholds?
   ```

4. **Performance:**
   ```
   @github-actions-expert My workflow takes 20 minutes to complete. How can I
   optimize it using caching and conditional job execution?
   ```

### Workflow 7: Production-Ready Feature Development

1. **Requirements Analysis:**
   ```
   @dotnet-senior-developer-agent I need to implement a customer registration
   feature. Review the specification document and identify all requirements,
   edge cases, and error scenarios that must be handled.
   ```

2. **Complete Implementation:**
   ```
   @dotnet-senior-developer-agent Implement the customer registration API with:
   - Full input validation and error handling
   - Database persistence with transactions
   - Event publishing for successful registrations
   - Comprehensive logging and monitoring
   - 90%+ test coverage including all error paths
   - Production-ready security and performance
   No shortcuts - do it right the first time.
   ```

3. **Verification:**
   ```
   @dotnet-senior-developer-agent Review this implementation against the
   specification. Verify that:
   - All requirements are met exactly
   - All error scenarios are handled
   - Tests cover all code paths
   - Documentation is complete
   - Code is production-ready
   Report any gaps or issues that must be fixed.
   ```

4. **Quality Assurance:**
   ```
   @dotnet-senior-developer-agent Run the complete quality checklist:
   - Code quality standards met
   - All tests pass (unit, integration, performance)
   - Security vulnerabilities addressed
   - Documentation complete
   - Specification compliance verified
   Provide evidence for each quality gate.
   ```

## Contributing

When adding new agents:

1. Create a new markdown file in this directory
2. Follow the structure of existing agents:
   - Clear expertise section
   - Design principles with examples
   - Code patterns and best practices
   - Common mistakes to avoid
   - **🔴 MANDATORY TESTING REQUIREMENTS section (REQUIRED)**
3. Update this README with agent information
4. Add to the capabilities matrix
5. **Ensure the agent enforces mandatory testing for all functional changes**

## Agent Update Policy

Custom agents should be updated when:
- New major features are added to Ouroboros
- Architecture patterns change
- New best practices emerge
- Common questions are identified
- **Testing standards or requirements change**

## Feedback

If you find issues with agent responses or have suggestions for improvements:
1. Open an issue describing the problem
2. Tag it with `custom-agents`
3. Mention which agent needs improvement

---

**Last Updated:** 2025-11-21

**Version:** 3.0.0 - MANDATORY TESTING ENFORCED

**Maintained by:** Ouroboros Team

## 🔴 Final Reminder 🔴

**Every custom agent is now a "most valuable professional" who:**
- Takes pride in their work by ensuring it's thoroughly tested
- Understands that untested code is incomplete code
- Never compromises on quality for speed
- Documents testing evidence in every PR
- Helps maintain the high standards of the Ouroboros project

**If you encounter an agent that suggests untested code or allows you to skip testing, report it immediately as a bug.**
