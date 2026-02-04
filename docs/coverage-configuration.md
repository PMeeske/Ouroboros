# Code Coverage Configuration

## Overview

The `coverlet.runsettings` file at the root of the repository configures code coverage collection using Coverlet. This file is **required** to get accurate coverage reports.

## Why is this needed?

Without proper configuration, Coverlet includes:
- Test assemblies in coverage calculations
- Generated code (`.g.cs`, `Designer.cs`)
- Migrations and other infrastructure code

This results in artificially low coverage percentages (0.2% instead of the actual 6%+).

## Configuration Details

### Included Assemblies
```xml
<Include>[Ouroboros.*]*,[LangChainPipeline]*</Include>
```
- All Ouroboros production assemblies
- LangChainPipeline CLI assembly

### Excluded Assemblies
```xml
<Exclude>
  [Ouroboros.Tests]*,
  [Ouroboros.Benchmarks]*,
  [*Tests]*,
  [*]*.Generated.*,
  [*]*.Designer,
  [*]*.g.cs,
  [*]*.g.i.cs,
  [*]*Migrations.*
</Exclude>
```

### Excluded Files
- `**/obj/**` - Build artifacts
- `**/bin/**` - Build outputs
- `**/*Designer.cs` - Designer-generated files
- `**/*.g.cs` - Generated code
- `**/Migrations/**` - Database migrations
- `**/*Generated*.cs` - Any generated code

### Excluded Attributes
- `Obsolete` - Deprecated code
- `GeneratedCodeAttribute` - Auto-generated code
- `CompilerGeneratedAttribute` - Compiler-generated code
- `ExcludeFromCodeCoverageAttribute` - Explicitly excluded code
- `DebuggerNonUserCodeAttribute` - Debugger-skipped code

## Usage

### Local Development
```bash
# Use the script (automatically uses runsettings)
./scripts/run-coverage.sh

# Or run directly
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

### GitHub Actions
The `.github/workflows/dotnet-test-grid.yml` workflow automatically uses this configuration:
```yaml
dotnet test \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings \
  --results-directory ./TestResults
```

## Output Formats

The configuration generates multiple coverage report formats:
- **Cobertura** (`.xml`) - Standard format, works with ReportGenerator
- **JSON** (`.json`) - Machine-readable format
- **LCOV** (`.info`) - Alternative format for other tools

## Performance Settings

- **SingleHit**: `false` - More accurate coverage
- **SkipAutoProps**: `true` - Excludes auto-properties for better performance
- **IncludeTestAssembly**: `false` - Test code not included in coverage

## Verification

After making changes, verify the configuration works:

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Check that coverage files are generated
ls TestResults/*/coverage.cobertura.xml

# Generate report
reportgenerator \
  -reports:"TestResults/*/coverage.cobertura.xml" \
  -targetdir:"TestCoverageReport" \
  -reporttypes:"Html;TextSummary"

# View summary
cat TestCoverageReport/Summary.txt
```

Expected results:
- Coverage percentage > 5%
- Test assemblies not listed in coverage
- Generated files not listed

## Troubleshooting

### Coverage is 0% or very low
**Cause**: Not using the runsettings file
**Fix**: Add `--settings coverlet.runsettings` to your test command

### "Deterministic report not supported" error
**Cause**: OpenCover format doesn't support deterministic reports
**Fix**: Remove `opencover` from the Format configuration (already done)

### Coverage file not found
**Cause**: Test execution failed or coverage not collected
**Fix**: 
1. Check test execution succeeded
2. Verify `coverlet.collector` package is installed
3. Check the `TestResults` directory exists

## Modifying the Configuration

To customize coverage collection:

1. Edit `coverlet.runsettings` in the repository root
2. Test locally: `dotnet test --settings coverlet.runsettings`
3. Commit changes if working correctly

Common modifications:
- Add assembly filters to `<Include>` or `<Exclude>`
- Add file patterns to `<ExcludeByFile>`
- Add attributes to `<ExcludeByAttribute>`

## References

- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator Documentation](https://github.com/danielpalme/ReportGenerator)
- [Ouroboros Test Coverage Quick Reference](../TEST_COVERAGE_QUICKREF.md)
- [Ouroboros Test Coverage Report](../TEST_COVERAGE_REPORT.md)
