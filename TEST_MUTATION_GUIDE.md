# Mutation Testing Guide

Stryker.NET provides mutation testing for Ouroboros to ensure that unit tests detect behavioral changes. This guide explains how to run and interpret mutation tests locally.

## Prerequisites

- .NET 10 SDK or later
- Local dotnet tool restore permissions

The repository uses a local tool manifest at `.config/dotnet-tools.json` to pin the Stryker.NET version.

## Running Mutation Tests

### PowerShell

```powershell
# From the repository root
./scripts/run-mutation-tests.ps1
```

### Bash / WSL / macOS

```bash
# From the repository root
./scripts/run-mutation-tests.sh
```

Both scripts automatically:

1. Restore local dotnet tools (`dotnet tool restore`).
2. Execute `dotnet stryker --config-file stryker-config.json`.

Pass additional arguments directly after the configuration path:

```powershell
# Example: run without the HTML reporter
./scripts/run-mutation-tests.ps1 -ConfigurationPath stryker-config.json --reporter Progress
```

```bash
# Example: run with baseline comparison (requires dashboard configuration)
./scripts/run-mutation-tests.sh stryker-config.json --with-baseline
```

Set `-OpenReport` when using PowerShell to automatically launch the latest HTML report after the run:

```powershell
./scripts/run-mutation-tests.ps1 -OpenReport
```

## Configuration Overview

Key configuration is stored in `stryker-config.json`:

- `solution`: `Ouroboros.sln`
- `project` / `testProject`: `src/Ouroboros.Tests/Ouroboros.Tests.csproj`
- `mutationLevel`: `Standard`
- `coverageAnalysis`: `perTest`
- `reporters`: `Html`, `Progress`, `Json`, `ClearText`
- `thresholds`: high 80, low 60, break 50
- `mutate`: all source files under `src/` excluding generated artifacts and tests
- `ignoreMethods`: skips mutations for `ToString`, `GetHashCode`, and `Equals`

Adjust the configuration as needed for new projects or stricter thresholds.

## Reports

Stryker generates output under `StrykerOutput/<timestamp>/`. Important artifacts:

- `reports/mutation-report.html` — interactive HTML summary
- `reports/mutation-report.json` — structured data for dashboards and CI
- `logs/stryker.log` — detailed execution log when `logToFile` is enabled

The `.gitignore` is configured to exclude `StrykerOutput/` and temporary folders.

## CI/CD Integration

The repository includes an **automated mutation testing workflow** (`.github/workflows/mutation-testing.yml`) that runs mutation tests in CI/CD:

### Automated Execution

**Schedule:**
- Runs automatically every night at 2 AM UTC
- Ensures regular monitoring of test suite quality

**Manual Trigger:**
- Navigate to Actions → Mutation Testing → Run workflow
- Configure mutation level (Standard, Complete, or Basic)
- Configure verbosity level (info, debug, or trace)

### Workflow Features

The mutation testing workflow provides:

1. **Artifact Generation**: HTML and JSON reports are uploaded as artifacts
2. **Workflow Summary**: Displays mutation testing results in the Actions summary
3. **Configurable Parameters**: Adjust mutation level and verbosity on-demand
4. **Timeout Protection**: 120-minute timeout prevents runaway processes
5. **Retry Mechanism**: Automatic retries for transient failures
6. **NuGet Caching**: Speeds up dependency restoration
7. **Non-Breaking**: Workflow warns but doesn't fail on low scores

### Viewing Results

After a workflow run completes:

1. Go to the Actions tab in GitHub
2. Select the "Mutation Testing" workflow
3. Click on a specific run
4. Download the `mutation-report-html` artifact
5. Extract and open `mutation-report.html` in a browser

### Manual CI/CD Integration

To integrate mutation testing into other workflows, add these steps:

```yaml
- name: Restore dotnet tools
  run: dotnet tool restore

- name: Run mutation tests
  run: |
    dotnet stryker \
      --config-file stryker-config.json \
      --reporter Html --reporter Progress --reporter Json
```

Consider running mutation tests on a nightly build or gated branch to balance runtime with coverage benefits.

## Troubleshooting

- **Build failures**: Ensure all projects build with `dotnet build` before running Stryker.
- **Timeouts**: Increase `timeoutMS` or `additionalTimeoutMS` in `stryker-config.json` for long-running tests.
- **Insufficient coverage**: Mutation testing is most effective when the baseline unit tests provide strong coverage. Address unit test gaps first.

For additional options, run `dotnet stryker --help` after restoring tools.
