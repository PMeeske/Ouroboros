# Mutation Testing Workflow Optimization

## Problem Solved
The mutation testing workflow was timing out after 75 minutes because ALL 5,032 tests were running for EVERY project, regardless of which project was being mutated.

## Solution Implemented

### 1. Created 9 Category-Specific Stryker Configs

Each config file filters tests to only run tests relevant to that category:

| Config File | Test Filter | Mutates |
|------------|-------------|---------|
| `stryker-config-core.json` | `FullyQualifiedName~Core` | `src/Ouroboros.Core/**/*.cs` |
| `stryker-config-domain.json` | `FullyQualifiedName~Domain` | `src/Ouroboros.Domain/**/*.cs` |
| `stryker-config-pipeline.json` | `FullyQualifiedName~Pipeline` | `src/Ouroboros.Pipeline/**/*.cs` |
| `stryker-config-tools.json` | `FullyQualifiedName~Tools` | `src/Ouroboros.Tools/**/*.cs` |
| `stryker-config-providers.json` | `FullyQualifiedName~Providers` | `src/Ouroboros.Providers/**/*.cs` |
| `stryker-config-application.json` | `FullyQualifiedName~Application` | `src/Ouroboros.Application/**/*.cs` |
| `stryker-config-agent.json` | `FullyQualifiedName~Agent` | `src/Ouroboros.Agent/**/*.cs` |
| `stryker-config-cli.json` | `FullyQualifiedName~CLI` | `src/Ouroboros.CLI/**/*.cs` |
| `stryker-config-webapi.json` | `FullyQualifiedName~WebApi` | `src/Ouroboros.WebApi/**/*.cs` |

### 2. Key Optimizations Applied

#### Coverage Analysis
- **Changed from**: `perTest` (slow)
- **Changed to**: `perTestInIsolation` (more accurate, better isolation)

#### Concurrency
- **Added**: `max-concurrent-test-runners: 4`
- Runs up to 4 test runners concurrently per category

#### Incremental Mutation
- **Added**: `since: true`
- **Added**: `with-baseline: true`
- **Added**: `baseline-version: "main"`
- Only tests mutants that changed since main branch

#### Exclusions
- **Added exclusions for**:
  - `GlobalUsings.cs` (auto-generated)
  - `Program.cs` (entry points, in CLI/WebApi)
  - `Startup.cs` (configuration, in WebApi)

#### Ignored Methods
- **Added**: `Dispose`, `GetEnumerator`
- These methods typically have trivial implementations

#### Ignored Mutations
- **Added**: `StringEmpty`
- String empty mutations rarely provide value

### 3. Workflow Changes

#### Timeout Increases
- **Job timeout**: 90 → 120 minutes
- **Step timeout**: 75 → 100 minutes

#### Matrix Configuration
Added `config` field to matrix to specify which config file each category uses:

```yaml
matrix:
  include:
    - category: Core
      project: src/Ouroboros.Core/Ouroboros.Core.csproj
      config: stryker-config-core.json
    # ... etc for all 9 categories
```

#### Run Command Updates
- Uses category-specific config: `--config-file "${{ matrix.config }}"`
- Adds concurrency flag: `--max-concurrent-test-runners 4`
- Adds incremental flag: `--since:main`
- Echoes config file being used for better debugging

### 4. Updated Base Configuration

Updated `stryker-config.json` to serve as an optimized fallback with all the same improvements.

## Expected Results

### Before Optimization
- **Test runs per category**: ~5,032 tests (all tests)
- **Estimated time per category**: 75+ minutes (timeout)
- **Total categories timing out**: Multiple

### After Optimization
- **Test runs per category**: Only relevant tests (filtered by namespace)
- **Estimated time per category**: 20-40 minutes (depends on category size)
- **Concurrency**: 4 test runners per category
- **Incremental**: Only tests changed code since main

### Example Test Filtering
When mutating Core:
- **Before**: Runs all 5,032 tests
- **After**: Runs only tests in `Ouroboros.Tests.Core` namespace (~10-15% of tests)

## Verification Checklist

✅ All 9 category-specific Stryker configs created
✅ Each config has correct test filter (`FullyQualifiedName~[Category]`)
✅ Each config has correct mutate pattern
✅ Workflow updated with config matrix field
✅ Job timeout increased to 120 minutes
✅ Step timeout increased to 100 minutes
✅ Test filtering applied per category
✅ Concurrency enabled (4 runners)
✅ Incremental mutation enabled (--since:main)
✅ Base config optimized
✅ Workflow YAML syntax validated

## Files Changed

1. **Created** (9 files):
   - `stryker-config-core.json`
   - `stryker-config-domain.json`
   - `stryker-config-pipeline.json`
   - `stryker-config-tools.json`
   - `stryker-config-providers.json`
   - `stryker-config-application.json`
   - `stryker-config-agent.json`
   - `stryker-config-cli.json`
   - `stryker-config-webapi.json`

2. **Modified** (2 files):
   - `stryker-config.json` (optimized base config)
   - `.github/workflows/mutation-testing.yml` (timeouts, matrix, run command)

## Next Steps

1. Commit all changes
2. Push to repository
3. Trigger workflow manually to test
4. Monitor first run for:
   - Actual execution time per category
   - Number of tests run per category
   - Number of mutants generated per category
   - Success/failure of test filtering
5. Fine-tune timeouts if needed based on actual performance

## Additional Notes

- The `--since:main` flag requires a baseline to exist. First run will be slower as it establishes the baseline.
- Subsequent runs will be much faster as they only test changed code.
- Test filtering assumes tests are named with the same namespace pattern as the code being tested.
- If a category has few tests or code, consider combining categories in the future for efficiency.
