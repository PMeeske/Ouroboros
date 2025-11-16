# Mutation Testing Guide

Stryker.NET provides mutation testing for MonadicPipeline to ensure that unit tests detect behavioral changes. This guide explains how to run and interpret mutation tests locally.

## Prerequisites

- .NET 8 SDK or later
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
./scripts/run-mutation-tests.ps1 -ConfigurationPath stryker-config.json --reporters progress
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

- `solution`: `MonadicPipeline.sln`
- `project` / `testProject`: `src/MonadicPipeline.Tests/MonadicPipeline.Tests.csproj`
- `mutationLevel`: `Standard`
- `coverageAnalysis`: `perTest`
- `reporters`: `html`, `progress`, `cleartext`, `dashboard`
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

To add mutation testing to CI, install the local tools and execute Stryker in your workflow:

```yaml
- name: Restore dotnet tools
  run: dotnet tool restore

- name: Run mutation tests
  run: dotnet stryker --config-file stryker-config.json --reporters "html" "progress"
```

Consider running mutation tests on a nightly build or gated branch to balance runtime with coverage benefits.

## Troubleshooting

- **Build failures**: Ensure all projects build with `dotnet build` before running Stryker.
- **Timeouts**: Increase `timeoutMS` or `additionalTimeoutMS` in `stryker-config.json` for long-running tests.
- **Insufficient coverage**: Mutation testing is most effective when the baseline unit tests provide strong coverage. Address unit test gaps first.

For additional options, run `dotnet stryker --help` after restoring tools.
