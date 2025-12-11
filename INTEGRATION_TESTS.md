# Integration Tests

This document provides guidance on running integration tests for the Ouroboros project.

## Overview

Integration tests verify the functionality of components against real external services (like GitHub API) and complex internal systems (like CLI interactive modes). These tests are separate from unit tests and may require proper configuration to run.

## CLI Interactive Mode Integration Tests

Location: `src/Ouroboros.Tests/IntegrationTests/CliInteractiveModeIntegrationTests.cs`

These tests verify the CLI interactive mode functionality including:
- **MeTTa REPL mode**: Symbolic reasoning, fact/rule management, and plan constraint checking
- **Skills REPL mode**: Skill registration, search, and command parsing

### Test Categories

**MeTTa Interactive Mode Tests:**
- Engine initialization and lifecycle
- Query execution through in-memory MeTTa engine
- Fact addition to knowledge base
- Rule application and reasoning
- Knowledge base reset functionality
- Plan constraint validation
- Unsafe operation detection in read-only context
- Best plan selection from multiple candidates

**Skills Interactive Mode Tests:**
- Skill registry initialization and persistence
- Skill registration and retrieval
- Skill search functionality
- Command parsing for skills REPL commands

**Command Parsing Tests:**
- Basic command parsing (help, query, exit, etc.)
- Whitespace handling
- Empty input handling

**Plan Action Tests:**
- FileSystem action MeTTa atom conversion
- Network action MeTTa atom conversion
- Tool action MeTTa atom conversion
- Safe context MeTTa atom conversion

### Running CLI Interactive Mode Tests

```bash
# Run all CLI interactive mode integration tests
dotnet test --filter "FullyQualifiedName~CliInteractiveModeIntegrationTests"

# Run only MeTTa-related tests
dotnet test --filter "FullyQualifiedName~CliInteractiveModeIntegrationTests.MeTTa"

# Run only Skills-related tests
dotnet test --filter "FullyQualifiedName~CliInteractiveModeIntegrationTests.Skills"
```

### No External Dependencies Required

Unlike GitHub integration tests, CLI interactive mode tests use in-memory implementations:
- **In-memory MeTTa engine**: No Docker or external MeTTa installation required
- **JSON-based skill storage**: Temporary files in system temp directory
- All tests clean up after themselves

## GitHub Tools Integration Tests

Location: `src/Ouroboros.Tests/IntegrationTests/GitHubToolsIntegrationTests.cs`

These tests verify the GitHub tools (`GitHubSearchTool`, `GitHubIssueReadTool`, `GitHubLabelTool`, `GitHubCommentTool`, etc.) work correctly against the live GitHub API.

### Required Environment Variables

Before running the GitHub integration tests, set these environment variables:

```bash
export GITHUB_TOKEN="your_github_personal_access_token"
export GITHUB_TEST_OWNER="repository_owner_or_organization"
export GITHUB_TEST_REPO="repository_name"
```

**Windows (PowerShell):**
```powershell
$env:GITHUB_TOKEN="your_github_personal_access_token"
$env:GITHUB_TEST_OWNER="repository_owner_or_organization"
$env:GITHUB_TEST_REPO="repository_name"
```

### GitHub Token Requirements

1. Create a GitHub Personal Access Token at: https://github.com/settings/tokens
2. Required permissions:
   - `repo` (for read-only tests: just `public_repo` is sufficient)
   - For write tests (create issues, add comments, manage labels): full `repo` access

### Test Repository Selection

Choose a test repository carefully:
- **Recommended**: Use a dedicated test repository
- The repository should have at least one existing issue for read tests
- For write tests (create, update, comment): ensure you have write access
- Avoid using production repositories

### Running Integration Tests

**Run all integration tests:**
```bash
dotnet test --filter "Category=Integration"
```

**Run only GitHub integration tests:**
```bash
dotnet test --filter "FullyQualifiedName~GitHubToolsIntegrationTests"
```

**Run a specific test:**
```bash
dotnet test --filter "FullyQualifiedName~GitHubToolsIntegrationTests.GitHubSearchTool_SearchForIssues_ReturnsSuccessResult"
```

### Graceful Skipping

If environment variables are not set, the tests will skip gracefully and pass without attempting API calls. This ensures:
- Integration tests don't fail in CI without credentials
- Developers can run unit tests without GitHub setup
- No false negatives from missing credentials

### Test Coverage

The integration test suite covers:

**Read-Only Operations (Safe to run frequently):**
- `GitHubSearchTool`: Search for issues and code
- `GitHubIssueReadTool`: Read issue details
- Error handling for non-existent issues
- Empty search results handling
- Input validation (empty queries, invalid types)

**Write Operations (Require write permissions, may modify repository):**
- `GitHubLabelTool`: Add/remove labels (test validates input)
- `GitHubCommentTool`: Add comments (test validates input)
- `GitHubIssueCreateTool`: Create issues (test validates input)
- `GitHubIssueUpdateTool`: Update issues (test validates input)

**Note**: Write operation tests primarily validate input handling and graceful failures. They don't create test data unless you have write permissions and valid issue numbers.

## CI/CD Considerations

### GitHub Actions

Integration tests are **NOT** run by default in CI. To run them in GitHub Actions:

1. Add repository secrets:
   - `INTEGRATION_TEST_GITHUB_TOKEN`
   - `INTEGRATION_TEST_OWNER`
   - `INTEGRATION_TEST_REPO`

2. Create a workflow or modify existing workflow:

```yaml
- name: Run Integration Tests
  if: github.event_name == 'workflow_dispatch' # Manual trigger only
  env:
    GITHUB_TOKEN: ${{ secrets.INTEGRATION_TEST_GITHUB_TOKEN }}
    GITHUB_TEST_OWNER: ${{ secrets.INTEGRATION_TEST_OWNER }}
    GITHUB_TEST_REPO: ${{ secrets.INTEGRATION_TEST_REPO }}
  run: dotnet test --filter "Category=Integration"
```

### Excluding from Regular Test Runs

To exclude integration tests from regular test runs:

```bash
dotnet test --filter "Category!=Integration"
```

## Best Practices

1. **Use a Dedicated Test Repository**: Don't run integration tests against production repositories
2. **Rotate Tokens Regularly**: Personal access tokens should be rotated periodically
3. **Minimal Permissions**: Use tokens with minimum required permissions
4. **Clean Up**: If tests create issues/comments, clean them up after (or use a disposable test repo)
5. **Rate Limiting**: Be aware of GitHub API rate limits (5000 requests/hour for authenticated requests)
6. **Local Testing**: Run integration tests locally before committing changes to GitHub tools

## Troubleshooting

### Tests Fail with "API rate limit exceeded"

- Wait for the rate limit to reset (check headers for reset time)
- Use a different token or wait for the hourly reset
- Reduce test frequency

### Tests Fail with "Resource not accessible by personal access token"

- Check your token has the required permissions (`repo` scope)
- Verify the repository exists and you have access
- Ensure `GITHUB_TEST_OWNER` and `GITHUB_TEST_REPO` are correct

### Tests Skip Silently

- This is expected behavior when environment variables are not set
- Verify environment variables are set correctly in your shell
- Check for typos in variable names

### Tests Fail with 404 Not Found

- Verify the repository exists: `https://github.com/{owner}/{repo}`
- Check you have access to the repository
- Ensure the repository is not private (or your token has access to private repos)

## Adding New Integration Tests

When adding new integration tests:

1. Mark with `[Trait("Category", "Integration")]`
2. Check `credentialsAvailable` in test setup
3. Skip gracefully when credentials not available (early return)
4. Follow existing patterns in `GitHubToolsIntegrationTests.cs`
5. Document required permissions in comments
6. Handle both success and expected failure cases
7. Don't assume write permissions - validate gracefully

## Example: Setting Up for Local Testing

```bash
# 1. Create a test repository on GitHub
# 2. Generate a personal access token
# 3. Set environment variables

export GITHUB_TOKEN="ghp_your_token_here"
export GITHUB_TEST_OWNER="your-username"
export GITHUB_TEST_REPO="test-repo"

# 4. Run the tests
cd /path/to/Ouroboros
dotnet test --filter "Category=Integration"
```

## Related Documentation

- [GitHub REST API Documentation](https://docs.github.com/en/rest)
- [Personal Access Tokens](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/creating-a-personal-access-token)
- [TEST_COVERAGE_REPORT.md](./TEST_COVERAGE_REPORT.md) - Overall test coverage
- [CONTRIBUTING.md](./CONTRIBUTING.md) - Contributing guidelines
