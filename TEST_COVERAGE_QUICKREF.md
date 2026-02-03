# Test Coverage Quick Reference

## Quick Commands

### Generate Coverage Report
```bash
# Recommended: Use runsettings file for proper configuration
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings && \
  reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"TestCoverageReport" -reporttypes:"Html;MarkdownSummaryGithub"

# Alternative: One-line command (uses default settings)
dotnet test --collect:"XPlat Code Coverage" && \
  reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"TestCoverageReport" -reporttypes:"Html;MarkdownSummaryGithub"
```

### Using the Coverage Script
```bash
# Easiest way: Use the provided script
./scripts/run-coverage.sh

# Options:
./scripts/run-coverage.sh --no-clean    # Skip cleaning previous results
./scripts/run-coverage.sh --no-open     # Don't open report in browser
./scripts/run-coverage.sh --minimal     # Generate only text summary
```

### View Coverage Report
```bash
# Open HTML report in browser
open TestCoverageReport/index.html      # macOS
xdg-open TestCoverageReport/index.html  # Linux
start TestCoverageReport/index.html     # Windows
```

### Run Mutation Tests
```bash
# PowerShell
./scripts/run-mutation-tests.ps1

# Bash / WSL / macOS
./scripts/run-mutation-tests.sh
```

### Run Specific Tests
```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter "FullyQualifiedName~InputValidatorTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~Should_Validate_ValidInput"

# With coverage
dotnet test --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~InputValidatorTests"
```

---

## Current Coverage Snapshot

| Metric | Value |
|--------|-------|
| **Line Coverage** | 8.4% |
| **Branch Coverage** | 6.2% |
| **Tests Passing** | 111 / 111 |
| **Test Execution Time** | ~480ms |

### Coverage by Assembly

| Assembly | Coverage |
|----------|----------|
| Ouroboros.Domain | 80.1% ✅ |
| Ouroboros.Core | 34.2% ⚠️ |
| Ouroboros.Pipeline | 15.5% ❌ |
| Ouroboros.Tools | 2.8% ❌ |
| Ouroboros.Providers | 2.2% ❌ |
| Ouroboros.Agent | 0% ❌ |
| LangChainPipeline (CLI) | 0% ❌ |

---

## Test Files Status

### Active Test Files (105 tests)
- ✅ **InputValidatorTests.cs** - 15 tests
- ✅ **EventStoreTests.cs** - 13 tests
- ✅ **VectorStoreFactoryTests.cs** - 9 tests
- ✅ **TrackedVectorStoreTests.cs** - 4 tests
- ✅ **RecursiveChunkProcessorTests.cs** - 10 tests
- ✅ **ObjectPoolTests.cs** - 15 tests
- ✅ **MetricsCollectorTests.cs** - 14 tests
- ✅ **DistributedTracingTests.cs** - 18 tests
- ✅ **RefinementLoopArchitectureTests.cs** - 7 tests

### Stub Test Files (0 tests - Needs Implementation)
- ⚠️ SkillExtractionTests.cs
- ⚠️ Phase3EmergentIntelligenceTests.cs
- ⚠️ Phase2MetacognitionTests.cs
- ⚠️ PersistentMemoryStoreTests.cs
- ⚠️ OrchestratorTests.cs
- ⚠️ OllamaCloudIntegrationTests.cs
- ⚠️ MetaAiTests.cs
- ⚠️ MetaAIv2Tests.cs
- ⚠️ MetaAIv2EnhancementTests.cs
- ⚠️ MetaAIConvenienceTests.cs
- ⚠️ MemoryContextTests.cs
- ⚠️ MeTTaTests.cs
- ⚠️ MeTTaOrchestratorTests.cs
- ⚠️ LangChainConversationTests.cs
- ⚠️ CliEndToEndTests.cs

---

## Well-Tested Components (>80% Coverage)

### Domain Layer
- **InMemoryEventStore**: 98.3% line, 100% branch
- **TrackedVectorStore**: 95.9% line, 71.4% branch
- **VectorStoreFactory**: 88% line, 72.7% branch

### Security
- **InputValidator**: 100% line, 96% branch
- **ValidationContext**: 100%
- **ValidationOptions**: 100%
- **ValidationResult**: 100%

### Performance
- **ObjectPool<T>**: 96.6% line, 91.6% branch
- **CommonPools**: 100%
- **PooledHelpers**: 100%

### Diagnostics
- **MetricsCollector**: 99.4% line, 96.4% branch
- **DistributedTracing**: 100% line, 81.8% branch

---

## Priority Testing Areas

### Critical (0% Coverage)
1. **Ouroboros.Agent** - 73 classes
2. **LangChainPipeline CLI** - 18 classes
3. **MeTTa Tools** - Integration tooling
4. **Ollama Providers** - LLM adapters

### High (Low Coverage)
1. **ToolRegistry** - Currently 32.3%
2. **Pipeline Components** - Currently 15.5%
3. **Provider Adapters** - Currently 2.2%

---

## CI/CD Integration

### GitHub Actions Workflow
```yaml
# .github/workflows/dotnet-coverage.yml
- Run on: push, pull_request
- Generates: HTML report, Cobertura XML, Badges
- Uploads: Coverage artifacts
- Comments: PR with coverage summary
```

### Local Development
```bash
# Watch mode for TDD
dotnet watch test --project src/Ouroboros.Tests/Ouroboros.Tests.csproj

# Coverage during development
./scripts/run-coverage.sh  # Create this script if needed
```

---

## Coverage Goals

| Timeframe | Target | Focus |
|-----------|--------|-------|
| **Current** | 8.4% | Domain model |
| **Next Sprint** | 25% | Tools, Pipeline |
| **Next Quarter** | 50% | Providers, CLI |
| **Production** | 70%+ | Agent system |

---

## Useful Scripts

### Generate Coverage Badge
```bash
# Badge is generated in CoverageReport/badge_linecoverage.svg
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"CoverageReport" -reporttypes:"Badges"
```

### Coverage Diff (Compare Branches)
```bash
# Generate coverage on main
git checkout main
dotnet test --collect:"XPlat Code Coverage"
mv TestResults/*/coverage.cobertura.xml coverage-main.xml

# Generate coverage on feature branch
git checkout feature-branch
dotnet test --collect:"XPlat Code Coverage"
mv TestResults/*/coverage.cobertura.xml coverage-feature.xml

# Compare
reportgenerator \
  -reports:"coverage-feature.xml" \
  -targetdir:"CoverageDiff" \
  -reporttypes:"Html" \
  -historydir:"CoverageHistory"
```

---

## Troubleshooting

### Coverage Shows 0% or Very Low Percentage

**Solution**: Ensure you're using the `coverlet.runsettings` configuration file:

```bash
# Correct: Use runsettings file
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Incorrect: Without runsettings (may include test assemblies and generated code)
dotnet test --collect:"XPlat Code Coverage"
```

**Why it matters**:
- Without runsettings: Coverage includes test assemblies, generated files, designer files
- With runsettings: Properly filters to only production code
- Result: 0.2% coverage without config → 6.2%+ with proper configuration

**Configuration Details** (`coverlet.runsettings`):
- ✅ Includes: `[Ouroboros.*]*`, `[LangChainPipeline]*`
- ❌ Excludes: Test assemblies, generated code, designer files
- ❌ Excludes by attribute: `GeneratedCodeAttribute`, `CompilerGeneratedAttribute`
- ✅ Formats: Cobertura, JSON, LCOV

### Coverage Not Generated
```bash
# Ensure coverlet.collector is installed
dotnet add src/Ouroboros.Tests/Ouroboros.Tests.csproj package coverlet.collector

# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### ReportGenerator Not Found
```bash
# Install globally
dotnet tool install -g dotnet-reportgenerator-globaltool

# Or install locally
dotnet tool install dotnet-reportgenerator-globaltool --tool-path ./tools
./tools/reportgenerator --help
```

### Low Coverage Numbers
```bash
# Verify test discovery
dotnet test --list-tests

# Run with verbose output
dotnet test --verbosity detailed

# Check for compilation errors
dotnet build --no-incremental
```

---

## Additional Resources

- **Full Report**: [TEST_COVERAGE_REPORT.md](TEST_COVERAGE_REPORT.md)
- **HTML Report**: `TestCoverageReport/index.html` (after generation)
- **Documentation Index**: [docs/README.md](docs/README.md)
- **Archived Implementation Summaries**: [docs/archive/](docs/archive/)

---

**Last Updated:** February 3, 2026
**Coverage Tool:** Coverlet + ReportGenerator
**Test Framework:** xUnit 2.6.6
**Coverage Configuration:** coverlet.runsettings (required for accurate results)
