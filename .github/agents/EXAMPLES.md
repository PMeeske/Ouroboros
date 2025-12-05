# Custom Agent Usage Examples

This document demonstrates practical examples of using the GitHub Copilot custom agents in real-world scenarios.

## 🔴 IMPORTANT: All Examples Include Mandatory Testing 🔴

Every example in this document now demonstrates proper testing practices. Custom agents are configured as valuable professionals who NEVER provide untested code.

When working with agents:
1. ✅ **Always ask about testing** - "How do I test this?"
2. ✅ **Request test examples** - "Show me the unit tests for this"
3. ✅ **Include tests in implementation** - Tests are part of the solution, not optional
4. ✅ **Follow test-first workflow** - Write tests before or during implementation

---

## Example 1: Building a New Pipeline Step (WITH TESTS)

**Scenario:** You want to add a new reasoning step that validates LLM outputs.

**Question to @functional-pipeline-expert:**
> "I need to create a composable pipeline step that validates LLM outputs against a JSON schema. The step should use Result<T> for error handling and integrate with the existing PipelineBranch. **Please include comprehensive unit tests that verify monadic laws and error handling.**"

**Expected Guidance:**
The agent would provide:
- Step signature using `Step<PipelineBranch, PipelineBranch>`
- Proper use of `Result<T>` monad for validation errors
- Integration with event sourcing
- Example implementation following Ouroboros patterns
- **✅ Complete test suite including:**
  - Unit tests for validation logic
  - Property-based tests for monadic laws
  - Integration tests for pipeline composition
  - Error handling test cases

**Example Test Code:**
```csharp
[Fact]
public async Task ValidationArrow_Should_Add_Validation_Event()
{
    // Arrange
    var branch = CreateTestBranch();
    var schema = LoadTestSchema();
    var validationStep = ValidationArrow(schema);
    
    // Act
    var result = await validationStep(branch);
    
    // Assert
    result.Events.Should().ContainSingle(e => e.State is ValidationResult);
}

[Theory]
[InlineData("{\"valid\":true}", true)]
[InlineData("{\"invalid\":123}", false)]
public async Task ValidationArrow_Should_Validate_Against_Schema(
    string json, bool shouldPass)
{
    // Test implementation...
}
```

---

## Example 2: Setting Up Model Orchestration (WITH TESTS)

**Scenario:** You want to optimize model selection based on task complexity.

**Question to @ai-orchestration-specialist:**
> "I have multiple LLMs (GPT-4, GPT-3.5, and a local Ollama model). How do I set up SmartModelOrchestrator to automatically select the best model based on task complexity, cost, and performance? **I need comprehensive tests to verify the selection logic works correctly under various scenarios.**"

**Expected Guidance:**
The agent would provide:
- How to register models with capabilities
- Use case classification strategies
- Performance metrics tracking
- Code examples for orchestrator setup
- **✅ Complete test suite including:**
  - Unit tests for model selection logic
  - Tests for confidence-based routing
  - Performance benchmarks
  - Integration tests with mock LLMs

**Example Test Code:**
```csharp
[Theory]
[InlineData("simple query", "gpt-3.5-turbo")]
[InlineData("complex reasoning task", "gpt-4")]
[InlineData("code generation", "codex")]
public async Task Orchestrator_Should_Select_Appropriate_Model(
    string prompt, string expectedModel)
{
    // Arrange
    var orchestrator = CreateConfiguredOrchestrator();
    
    // Act
    var result = await orchestrator.SelectModelAsync(prompt);
    
    // Assert
    result.Match(
        selected => selected.ModelName.Should().Be(expectedModel),
        error => Assert.Fail($"Selection failed: {error}"));
}
```

---

## Example 3: Production Deployment (WITH TESTS)

**Scenario:** You need to deploy the WebApi to Kubernetes with zero downtime.

**Question to @cloud-devops-expert:**
> "I need to deploy Ouroboros WebApi to Kubernetes with health checks, autoscaling, and zero-downtime updates. **Show me how to test the deployment configuration before applying it to production, including health check testing and rollback procedures.**"

**Expected Guidance:**
The agent would provide:
- Kubernetes deployment YAML with health probes
- HorizontalPodAutoscaler configuration
- Rolling update strategy
- Service and Ingress setup
- Complete CI/CD pipeline
- **✅ Complete testing strategy including:**
  - Terraform/Kubernetes manifest validation
  - Health check endpoint testing
  - Load testing configuration
  - Rollback procedure testing

**Example Test Commands:**
```bash
# Validate Kubernetes manifests
kubectl apply --dry-run=server -f k8s/
kubectl apply --dry-run=client -f k8s/

# Test health endpoints
curl -f http://localhost:8080/health/ready
curl -f http://localhost:8080/health/live

# Load test
k6 run --vus 100 --duration 30s load-test.js

# Test rollback
kubectl rollout undo deployment/monadic-pipeline
kubectl rollout status deployment/monadic-pipeline
```

---

## Example 4: Building an Android or MAUI App (WITH TESTS)

**Scenario:** You need to create a mobile app that uses the Ouroboros API.

**Question to @android-expert (for native Android):**
> "I need to build an Android app with Kotlin that connects to the Ouroboros API. How should I structure the app using MVVM, Jetpack Compose, and Hilt for dependency injection? **Please include comprehensive tests for ViewModels, repositories, and UI components.**"

**Expected Guidance:**
The agent would provide:
- Clean Architecture setup with domain, data, and presentation layers
- Repository pattern for API integration with Retrofit
- ViewModel with StateFlow for UI state management
- Compose UI components with proper state hoisting
- **✅ Complete test suite including:**
  - Unit tests for ViewModels
  - Repository tests with mock API
  - UI tests with Compose Testing
  - Integration tests

**Example Test Code:**
```kotlin
@Test
fun `loadPipelines should update state with pipelines`() = runTest {
    // Arrange
    val repository = FakePipelineRepository()
    val viewModel = PipelineViewModel(repository)
    
    // Act
    viewModel.loadPipelines()
    
    // Assert
    viewModel.uiState.value.apply {
        isLoading shouldBe false
        pipelines shouldHaveSize 2
        error shouldBe null
    }
}

@Test
fun pipelineList_displays_pipelines() {
    composeTestRule.setContent {
        PipelineListScreen(pipelines = testPipelines)
    }
    
    composeTestRule.onNodeWithText("Pipeline 1").assertIsDisplayed()
}
```

---
- Hilt modules for dependency injection
- Example implementation with proper error handling

**Question to @android-expert (for cross-platform MAUI):**
> "I need to build a cross-platform app with .NET MAUI (C#) that connects to the Ouroboros API and runs on Android, iOS, and Windows. How should I structure it using MVVM with CommunityToolkit?"

**Expected Guidance:**
The agent would provide:
- .NET MAUI project structure with shared business logic
- MVVM with CommunityToolkit.Mvvm and source generators
- Cross-platform API client with Refit
- XAML or C# Markup UI with data binding
- Dependency injection setup in MauiProgram.cs
- Platform-specific code handling
- Example implementation for all target platforms

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

## Example 6: Python Code Review and Testing (WITH TESTS)

**Scenario:** You need to review and improve Python integration code for the Ollama agent.

**Question to @python-review-expert:**
> "I have this Ollama client code. Please review it for type safety, error handling, and async patterns. I also need comprehensive pytest tests."

```python
class OllamaAgent:
    def __init__(self, model="llama3", host="http://localhost:11434"):
        self.model = model
        self.host = host
        self.client = openai.OpenAI(base_url=f"{host}/v1", api_key="ollama")
    
    def __call__(self, messages):
        response = self.client.chat.completions.create(
            model=self.model,
            messages=messages,
            temperature=0.7,
        )
        return response.choices[0].message
```

**Expected Guidance:**
The agent would provide:
- Type hints for all methods and parameters
- Proper error handling with custom exceptions
- Async/await patterns for non-blocking operations
- Context manager for resource management
- Input validation
- Comprehensive pytest test suite including:
  - Unit tests with fixtures and mocking
  - Async tests with @pytest.mark.asyncio
  - Parametrized tests for different models
  - Error handling tests
  - Integration tests
- Configuration for mypy, ruff, and pytest

**Example Improved Code with Tests:**

```python
# ollama_client.py
from typing import Optional, Any
import logging
import aiohttp
import asyncio

logger = logging.getLogger(__name__)

class OllamaError(Exception):
    """Base exception for Ollama operations."""
    pass

class OllamaConnectionError(OllamaError):
    """Failed to connect to Ollama."""
    pass

class OllamaTimeoutError(OllamaError):
    """Request timed out."""
    pass

class OllamaClient:
    """Async client for Ollama API with proper error handling."""
    
    def __init__(
        self, 
        model: str = "llama3",
        host: str = "http://localhost:11434",
        timeout: float = 30.0
    ) -> None:
        """
        Initialize Ollama client.
        
        Args:
            model: Model name to use
            host: Ollama server URL
            timeout: Request timeout in seconds
        """
        if not model:
            raise ValueError("Model name cannot be empty")
        if not host:
            raise ValueError("Host URL cannot be empty")
        
        self.model = model
        self.host = host
        self.timeout = timeout
        self._session: Optional[aiohttp.ClientSession] = None
        logger.info(f"Initialized OllamaClient for model {model}")
    
    async def __aenter__(self) -> "OllamaClient":
        """Async context manager entry."""
        self._session = aiohttp.ClientSession()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb) -> None:
        """Async context manager exit."""
        if self._session:
            await self._session.close()
    
    async def generate(
        self,
        prompt: str,
        temperature: float = 0.7,
        max_tokens: Optional[int] = None
    ) -> str:
        """
        Generate text from prompt.
        
        Args:
            prompt: Input prompt
            temperature: Generation temperature (0.0-2.0)
            max_tokens: Maximum tokens to generate
            
        Returns:
            Generated text
            
        Raises:
            OllamaConnectionError: Connection failed
            OllamaTimeoutError: Request timed out
            ValueError: Invalid parameters
        """
        if not prompt:
            raise ValueError("Prompt cannot be empty")
        if not 0 <= temperature <= 2:
            raise ValueError("Temperature must be between 0 and 2")
        
        if not self._session:
            raise RuntimeError("Client not initialized. Use 'async with'")
        
        try:
            async with asyncio.timeout(self.timeout):
                async with self._session.post(
                    f"{self.host}/api/generate",
                    json={
                        "model": self.model,
                        "prompt": prompt,
                        "temperature": temperature,
                        **({"max_tokens": max_tokens} if max_tokens else {})
                    }
                ) as response:
                    response.raise_for_status()
                    data = await response.json()
                    result = data.get("response", "")
                    logger.debug(f"Generated {len(result)} characters")
                    return result
        except asyncio.TimeoutError:
            logger.error(f"Request timed out after {self.timeout}s")
            raise OllamaTimeoutError(f"Request timed out after {self.timeout}s")
        except aiohttp.ClientError as e:
            logger.error(f"Connection error: {e}")
            raise OllamaConnectionError(f"Failed to connect to Ollama: {e}")

# test_ollama_client.py
import pytest
from unittest.mock import Mock, AsyncMock, patch
from hypothesis import given, strategies as st
import aiohttp

@pytest.fixture
def ollama_client():
    """Create OllamaClient for testing."""
    return OllamaClient(model="llama3", host="http://localhost:11434")

@pytest.fixture
def mock_session():
    """Mock aiohttp session."""
    return AsyncMock(spec=aiohttp.ClientSession)

class TestOllamaClient:
    """Test suite for OllamaClient."""
    
    def test_initialization(self, ollama_client):
        """Test client initialization."""
        assert ollama_client.model == "llama3"
        assert ollama_client.host == "http://localhost:11434"
        assert ollama_client.timeout == 30.0
    
    def test_initialization_validation(self):
        """Test initialization validation."""
        with pytest.raises(ValueError, match="Model name cannot be empty"):
            OllamaClient(model="")
        
        with pytest.raises(ValueError, match="Host URL cannot be empty"):
            OllamaClient(host="")
    
    @pytest.mark.asyncio
    async def test_context_manager(self, ollama_client):
        """Test async context manager."""
        async with ollama_client as client:
            assert client._session is not None
        # Session should be closed after exit
    
    @pytest.mark.asyncio
    async def test_generate_success(self, ollama_client, mock_session):
        """Test successful generation."""
        # Arrange
        mock_response = AsyncMock()
        mock_response.json = AsyncMock(return_value={"response": "Hello, world!"})
        mock_response.raise_for_status = Mock()
        mock_session.post.return_value.__aenter__.return_value = mock_response
        ollama_client._session = mock_session
        
        # Act
        result = await ollama_client.generate("Test prompt")
        
        # Assert
        assert result == "Hello, world!"
        mock_session.post.assert_called_once()
    
    @pytest.mark.asyncio
    async def test_generate_timeout(self, ollama_client, mock_session):
        """Test timeout handling."""
        ollama_client._session = mock_session
        mock_session.post.side_effect = asyncio.TimeoutError()
        
        with pytest.raises(OllamaTimeoutError, match="timed out"):
            await ollama_client.generate("Test")
    
    @pytest.mark.asyncio
    async def test_generate_connection_error(self, ollama_client, mock_session):
        """Test connection error handling."""
        ollama_client._session = mock_session
        mock_session.post.side_effect = aiohttp.ClientError("Connection failed")
        
        with pytest.raises(OllamaConnectionError, match="Failed to connect"):
            await ollama_client.generate("Test")
    
    @pytest.mark.parametrize("temperature,should_fail", [
        (0.0, False),
        (0.7, False),
        (2.0, False),
        (-0.1, True),
        (2.1, True),
    ])
    @pytest.mark.asyncio
    async def test_temperature_validation(
        self, 
        ollama_client,
        temperature,
        should_fail
    ):
        """Test temperature parameter validation."""
        ollama_client._session = AsyncMock()
        
        if should_fail:
            with pytest.raises(ValueError, match="Temperature must be"):
                await ollama_client.generate("Test", temperature=temperature)
        else:
            # Would succeed if mocked properly
            pass
    
    @pytest.mark.asyncio
    async def test_empty_prompt_raises_error(self, ollama_client):
        """Test that empty prompt raises ValueError."""
        ollama_client._session = AsyncMock()
        
        with pytest.raises(ValueError, match="Prompt cannot be empty"):
            await ollama_client.generate("")
    
    @pytest.mark.integration
    @pytest.mark.asyncio
    async def test_real_ollama_generation(self):
        """Integration test with real Ollama instance."""
        async with OllamaClient("llama3") as client:
            response = await client.generate("Say hello in 3 words")
            assert len(response) > 0
            assert isinstance(response, str)

# Property-based tests
@given(
    prompt=st.text(min_size=1, max_size=500),
    temperature=st.floats(min_value=0.0, max_value=2.0)
)
def test_prompt_properties(prompt: str, temperature: float):
    """Test with various prompt and temperature combinations."""
    client = OllamaClient()
    assert client.model == "llama3"
    # Further property tests...
```

## Example 7: Optimizing GitHub Actions Workflows

**Scenario:** Your CI/CD pipeline takes 20 minutes to run and you want to optimize it.

**Question to @github-actions-expert:**
> "My GitHub Actions workflow for Ouroboros takes 20 minutes to build, test, and deploy. It runs on every PR and uses .NET 8. How can I optimize it to run in under 5 minutes?"

**Expected Guidance:**
The agent would provide:
- Caching strategies for NuGet packages and build outputs
- Matrix strategies for parallel test execution
- Conditional job execution based on changed files
- Reusable workflow patterns to reduce duplication
- Docker layer caching for container builds
- Optimization of restore/build/test steps

**Question to @github-actions-expert:**
> "I want to create a reusable workflow for .NET builds that all my projects can use. It should support multiple .NET versions, run tests with coverage, and upload artifacts. How do I structure this?"

**Expected Guidance:**
The agent would provide:
- Reusable workflow YAML with workflow_call trigger
- Input parameters for customization (dotnet-version, configuration)
- Output values for downstream jobs
- Best practices for parameter validation
- Example of consuming the reusable workflow

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

### Android & MAUI Expert Tests

**Test 1: Android Architecture**
```
@android-expert How do I implement MVVM with Clean Architecture for my Android app using Kotlin and Compose?
```

**Expected Response:** The agent should provide a layered architecture with domain, data, and presentation layers, including ViewModel, Repository, and Use Case examples with Kotlin.

**Test 2: .NET MAUI Architecture**
```
@android-expert How do I implement MVVM with Clean Architecture for my .NET MAUI app using C# and CommunityToolkit?
```

**Expected Response:** The agent should provide a cross-platform architecture with shared business logic, MVVM with source generators, and platform-specific handling.

**Test 3: Jetpack Compose State Management**
```
@android-expert Review this Compose code for proper state management:

@Composable
fun UserScreen(viewModel: UserViewModel) {
    val user = viewModel.getUser()
    Text(text = user.name)
}
```

**Expected Response:** The agent should identify the lack of lifecycle-aware state collection and suggest using collectAsStateWithLifecycle().

**Test 4: MAUI Cross-Platform**
```
@android-expert How do I access platform-specific features in .NET MAUI while keeping my code testable?
```

**Expected Response:** The agent should provide examples of conditional compilation, platform abstractions, and dependency injection for platform services.

**Test 5: Memory Leaks (Android)**
```
@android-expert How do I prevent memory leaks when using location services in Android?
```

**Expected Response:** The agent should provide lifecycle-aware component examples with DisposableEffect for proper cleanup.

**Test 6: Memory Management (MAUI)**
```
@android-expert How do I properly dispose of resources in .NET MAUI ViewModels?
```

**Expected Response:** The agent should explain IDisposable implementation, proper cleanup in ViewModels, and lifecycle management in MAUI.

### GitHub Actions Expert Tests

**Test 1: Workflow Optimization**
```
@github-actions-expert Review this workflow and suggest optimizations:

name: CI
on: [push]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - run: dotnet restore
    - run: dotnet build
    - run: dotnet test
```

**Expected Response:** The agent should identify missing caching, suggest combining steps with --no-restore flags, recommend matrix strategies for parallel testing, and add code coverage reporting.

**Test 2: Reusable Workflows**
```
@github-actions-expert How do I create a reusable workflow for .NET builds that accepts parameters?
```

**Expected Response:** The agent should provide a complete reusable workflow example with workflow_call trigger, input parameters, outputs, and best practices for parameterization.

**Test 3: Security Best Practices**
```
@github-actions-expert Review this workflow for security issues:

jobs:
  deploy:
    permissions: write-all
    steps:
    - uses: actions/checkout@latest
    - run: echo "Secret: ${{ secrets.API_KEY }}"
```

**Expected Response:** The agent should identify overly broad permissions, unpinned action version (@latest), and secret exposure in logs. Should suggest minimal permissions, SHA pinning, and proper secret masking.

**Test 4: Matrix Strategies**
```
@github-actions-expert How do I test across multiple .NET versions and OS with include/exclude?
```

**Expected Response:** The agent should provide matrix configuration examples with include/exclude patterns, demonstrate conditional logic based on matrix values, and explain optimization strategies.

### Python Review Expert Tests

**Test 1: Type Hints**
```
@python-review-expert Review this Python code for type safety:

def process_data(data):
    return data['value']
```

**Expected Response:** The agent should identify missing type hints and suggest proper typing with dict annotations and return type.

**Test 2: Async Patterns**
```
@python-review-expert Review this async code:

async def fetch_data():
    response = requests.get("http://example.com")  # Blocking!
    return response.json()
```

**Expected Response:** The agent should identify the blocking call in async function and suggest using aiohttp or asyncio.to_thread.

**Test 3: Error Handling**
```
@python-review-expert Review error handling in this code:

try:
    result = risky_operation()
except:
    pass
```

**Expected Response:** The agent should identify bare except clause and silent error swallowing, suggest specific exception handling and logging.

**Test 4: Testing with pytest**
```
@python-review-expert How do I write pytest tests for this async Ollama client?

class OllamaClient:
    async def generate(self, prompt: str) -> str:
        # implementation
        pass
```

**Expected Response:** The agent should provide comprehensive pytest examples with fixtures, @pytest.mark.asyncio, mocking, and parametrization.

## Verification Checklist

- [ ] Agents understand Ouroboros architecture
- [ ] Functional Pipeline Expert provides monadic patterns
- [ ] AI Orchestration Specialist knows Meta-AI architecture
- [ ] Cloud DevOps Expert has Kubernetes expertise
- [ ] GitHub Actions Expert provides workflow optimization guidance
- [ ] Android & MAUI Expert provides modern mobile best practices (Kotlin & C#)
- [ ] Python Review Expert provides type safety and testing guidance
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
5. Update agents when new features are added to Ouroboros

## 🔴 Testing Mandate Examples 🔴

### Example: Agent Response WITH Mandatory Testing

**Question:**
> "@functional-pipeline-expert I need to create a new pipeline step that filters data."

**✅ GOOD Agent Response (Includes Tests):**
```markdown
Here's the pipeline step implementation:

\`\`\`csharp
public static Step<PipelineBranch, PipelineBranch> FilterArrow(
    Func<string, bool> predicate) =>
    async branch =>
    {
        var filtered = branch.Data.Where(predicate).ToList();
        return branch with { Data = filtered };
    };
\`\`\`

**MANDATORY TESTS (you must implement these):**

\`\`\`csharp
[Fact]
public async Task FilterArrow_Should_Filter_Data_Correctly()
{
    // Arrange
    var branch = CreateBranchWithData(["item1", "item2", "item3"]);
    var filterStep = FilterArrow(x => x.StartsWith("item1"));
    
    // Act
    var result = await filterStep(branch);
    
    // Assert
    result.Data.Should().ContainSingle();
    result.Data.Should().Contain("item1");
}

[Fact]
public async Task FilterArrow_Should_Preserve_Immutability()
{
    // Arrange
    var original = CreateBranchWithData(["a", "b"]);
    var originalData = original.Data.ToList();
    
    // Act
    var filtered = await FilterArrow(x => x == "a")(original);
    
    // Assert
    original.Data.Should().BeEquivalentTo(originalData);
    filtered.Should().NotBeSameAs(original);
}
\`\`\`

**Quality Gates:**
- ✅ Test coverage: 100% for new FilterArrow function
- ✅ Immutability verified
- ✅ Edge cases tested (empty list, all filtered, none filtered)
```

### Example: Agent Rejects Untested Code

**Question:**
> "@api-design-expert Here's my new API endpoint, can you review it?"
> [shows code without tests]

**✅ GOOD Agent Response:**
```markdown
❌ **REJECTED: No tests provided**

As a valuable professional, I cannot approve this code without comprehensive tests.

**REQUIRED before review:**
1. Unit tests for endpoint logic
2. Integration tests for API calls
3. Validation tests for input
4. Error scenario tests
5. Authentication/authorization tests

**Minimum requirements:**
- [ ] Test all HTTP status codes (200, 400, 401, 404, 500)
- [ ] Test request validation
- [ ] Test response schema matches OpenAPI spec
- [ ] Test error messages are helpful
- [ ] Achieve ≥90% code coverage

Please provide tests and I'll review the complete solution.
```

### Example: Testing Evidence in PR

**Good PR Description Format:**
```markdown
## Feature: Add Pipeline Filtering

### Implementation
- Created FilterArrow step
- Added validation logic
- Integrated with existing pipeline

### ✅ Testing Evidence (REQUIRED)
**Unit Tests:**
- 12 tests added, all passing
- Coverage: 94% (previous: 87%)

**Integration Tests:**
- 3 end-to-end scenarios tested
- Pipeline composition verified

**Property-Based Tests:**
- Filter preserves list ordering: ✓ (100 cases)
- Filter never adds elements: ✓ (100 cases)

**Quality Gates:**
- ✅ All 847 existing tests pass (no regressions)
- ✅ Mutation score: 89% (target: 80%)
- ✅ Performance: < 10ms per operation
- ✅ Memory: no leaks detected

### Test Execution Results
\`\`\`
Test run for Ouroboros.Tests.dll (.NET 10.0)
Total tests: 859
     Passed: 859
     Failed: 0
    Skipped: 0
     Total time: 4.2s
\`\`\`
```

## Known Limitations

- Agents are based on the documented knowledge in their markdown files
- They may not have access to the latest code changes without updates
- Complex questions may require multiple agent consultations
- Agents work best with specific, focused questions
- **Agents will REJECT code without adequate testing**

## Feedback

If you encounter issues:
1. Check if you're using the right agent for the question
2. Provide more context or code examples
3. Try breaking complex questions into smaller parts
4. Consult multiple agents for different aspects
5. **Always include testing in your questions and implementations**

---

**Remember:** These custom agents are tools to enhance productivity. They provide guidance based on best practices and Ouroboros's architecture, but always review and test their suggestions before implementing in production code.

**🔴 CRITICAL:** Every custom agent now enforces mandatory testing. If an agent provides code without tests or allows you to skip testing, report it as a bug immediately. Our agents are valuable professionals who take pride in their thoroughly tested work.
