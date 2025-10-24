# Playwright-Based Copilot Assignment

## Overview

The Automated Development Cycle now uses **Playwright** to assign `@copilot` to GitHub issues via the GitHub web UI instead of using the GitHub API directly. This approach provides a more realistic and visual way to interact with GitHub, making the automation process more transparent and easier to debug.

## Why Playwright?

### Benefits

1. **UI-Based Interaction**: Interacts with GitHub exactly as a human would through the web interface
2. **Visual Debugging**: Screenshots are captured at each step for troubleshooting
3. **Realistic Workflow**: Demonstrates the actual user experience of assignment
4. **Fallback Support**: Automatically falls back to API if UI automation fails

### Trade-offs

- **Performance**: Slightly slower than direct API calls (adds ~5-10 seconds per assignment)
- **Complexity**: Requires browser automation dependencies (Playwright + Chromium)
- **Authentication**: More complex than API token authentication

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│           Automated Development Cycle                    │
└─────────────────────────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ▼                               ▼
    Create Issues               Find Unassigned Issues
         │                               │
         └───────────────┬───────────────┘
                         ▼
              ┌─────────────────────┐
              │  Setup Playwright   │
              │  (Node.js + Chrome) │
              └─────────────────────┘
                         │
                         ▼
              ┌─────────────────────┐
              │  Playwright Script  │
              │  assign-copilot-    │
              │  via-ui.js          │
              └─────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ▼                               ▼
    Success                           Failed
         │                               │
         ▼                               ▼
    Take Screenshot              Fallback to API
    Upload Artifacts            (GitHub REST API)
```

## Implementation Details

### Files Added

1. **`.github/scripts/assign-copilot-via-ui.js`**
   - Main Playwright automation script
   - Handles browser launch, navigation, and interaction
   - Captures screenshots for debugging
   - Implements error handling and retries

2. **`.github/scripts/package.json`**
   - Node.js package configuration
   - Declares Playwright dependency
   - Provides installation scripts

### Workflow Changes

The `copilot-automated-development-cycle.yml` workflow now includes:

1. **Issue Creation Step** (Modified)
   - Creates issues via API
   - Stores issue numbers for Playwright
   - No longer assigns directly via API

2. **Playwright Setup Steps** (New)
   - Checks out repository
   - Sets up Node.js environment
   - Installs Playwright and Chromium browser

3. **Playwright Assignment Step** (New)
   - Runs `assign-copilot-via-ui.js` for each issue
   - Passes repository info and issue numbers
   - Captures screenshots of the process

4. **Screenshot Upload Step** (New)
   - Uploads Playwright screenshots as artifacts
   - Retained for 7 days for debugging

5. **API Fallback Step** (New)
   - Checks if assignments succeeded
   - Falls back to GitHub API if Playwright failed
   - Adds labels for manual review if both fail

## Playwright Script Details

### Authentication

**IMPORTANT**: GitHub PAT tokens cannot be used for browser authentication. The script requires a valid GitHub session cookie.

**Authentication Strategy**:

1. **Required**: `GITHUB_COOKIE_USER_SESSION` - A valid user_session cookie from an authenticated GitHub browser session
2. **Fallback**: If no session cookie is provided, the script exits early and the workflow uses the GitHub API instead (which works with just a PAT token)

**How to obtain a session cookie**:

1. Open a browser and log into GitHub
2. Open Developer Tools (F12)
3. Go to Application → Cookies → github.com
4. Find and copy the value of the `user_session` cookie
5. Add it as a repository secret named `GITHUB_COOKIE_USER_SESSION`

**Note**: Session cookies expire after some time. If Playwright automation stops working, you may need to refresh the cookie.

### UI Interaction Flow

```javascript
1. Launch Chromium browser in headless mode
2. Navigate to issue URL: https://github.com/{owner}/{repo}/issues/{number}
3. Wait for page load and authentication check
4. Locate assignees button (tries multiple selectors)
5. Click to open assignees dropdown
6. Search for copilot username
7. Click on copilot user in results
8. Take screenshot of final state
9. Close browser
```

### Selectors Used

The script tries multiple CSS selectors for robustness:

**Assignees Button:**
- `[aria-label="Select assignees"]`
- `button[aria-label="Select assignees"]`
- `.sidebar-assignee button`
- `[data-hotkey="a"]`
- `summary:has-text("Assignees")`

**Search Input:**
- `input[placeholder*="Type or choose"]`
- `input[type="text"][aria-label*="assignee"]`
- `input.js-filterable-field`
- `input[name="query"]`

**User Selection:**
- `[data-filterable-for*="${copilotUsername}"]`
- `.select-menu-item:has-text("${copilotUsername}")`
- `a:has-text("${copilotUsername}")`
- `.menu-item:has-text("${copilotUsername}")`

### Error Handling

1. **Timeout Protection**: All operations have configurable timeouts
2. **Screenshot Capture**: Takes screenshots at each major step
3. **Graceful Degradation**: Returns success/failure status for fallback logic
4. **Detailed Logging**: Emits emoji-prefixed logs for easy debugging

### Screenshots

Screenshots are saved to `/tmp/` with descriptive names:

- `github-issue-page.png` - Initial page load
- `github-assignees-open.png` - After opening assignees
- `github-search-results.png` - After searching for user
- `github-final.png` - Final state after assignment
- `github-error.png` - Error state if something fails

## Configuration

### Setting Up GitHub Session Cookie (Optional)

To enable Playwright browser automation, you need to add the `GITHUB_COOKIE_USER_SESSION` secret:

1. **Obtain the cookie**:
   - Open a browser and log into GitHub
   - Press F12 to open Developer Tools
   - Navigate to: Application → Storage → Cookies → https://github.com
   - Find the `user_session` cookie
   - Copy its Value (a long alphanumeric string)

2. **Add as repository secret**:
   - Go to your repository on GitHub
   - Navigate to: Settings → Secrets and variables → Actions
   - Click "New repository secret"
   - Name: `GITHUB_COOKIE_USER_SESSION`
   - Value: Paste the cookie value you copied
   - Click "Add secret"

3. **Verify**: The next workflow run will use Playwright for UI automation

**Important**: Session cookies expire after a period of inactivity (typically 30-90 days). If Playwright automation stops working, you may need to refresh the cookie by repeating these steps.

### Environment Variables

**Required for API Fallback:**
- `GITHUB_TOKEN`: GitHub Personal Access Token with `repo` and `issues:write` scopes

**Required for Playwright UI Automation:**
- `GITHUB_COOKIE_USER_SESSION`: GitHub session cookie from an authenticated browser session

**Optional:**
- `COPILOT_USER`: Username to assign (defaults to 'copilot')
- `COPILOT_AGENT_USER`: Workflow-level setting for agent username

**Default Behavior**: If `GITHUB_COOKIE_USER_SESSION` is not provided, Playwright automation is skipped and the workflow uses the GitHub API directly (which only requires `GITHUB_TOKEN`).

### Workflow Inputs

```yaml
workflow_dispatch:
  inputs:
    assign_unassigned:
      description: 'Assign Copilot to unassigned issues'
      required: false
      default: true
      type: boolean
```

## Running Locally

### Prerequisites

```bash
cd .github/scripts
npm install
```

### Manual Execution

**Without Session Cookie (will exit early, demonstrating fallback behavior):**
```bash
export GITHUB_TOKEN="your_github_token"
node assign-copilot-via-ui.js <owner> <repo> <issue-number> [copilot-username]
```

**With Session Cookie (enables full Playwright automation):**
```bash
export GITHUB_TOKEN="ghp_..."
export GITHUB_COOKIE_USER_SESSION="your_session_cookie"
node assign-copilot-via-ui.js PMeeske MonadicPipeline 123 copilot
```

**Note**: To obtain `GITHUB_COOKIE_USER_SESSION`, see the Authentication section above.

### Debug Mode

To see the browser in action (non-headless):

Edit `assign-copilot-via-ui.js`:
```javascript
browser = await chromium.launch({
  headless: false,  // Change to false
  args: ['--no-sandbox', '--disable-setuid-sandbox']
});
```

## Troubleshooting

### Common Issues

#### 1. Playwright Installation Fails

**Symptom**: Workflow fails during "Install Playwright dependencies"

**Solution**:
```yaml
- name: Install Playwright dependencies
  run: |
    npm install
    npx playwright install-deps  # Add system dependencies
    npx playwright install chromium --with-deps
```

#### 2. Authentication Fails

**Symptom**: Script exits early with "No GitHub session cookie available" message

**Explanation**: GitHub PAT tokens cannot be used for browser authentication. A valid session cookie is required.

**Solution**: 
1. Obtain a `user_session` cookie from an authenticated GitHub browser session (see Authentication section above)
2. Add it as a repository secret named `GITHUB_COOKIE_USER_SESSION`
3. Re-run the workflow

**Workaround**: If you don't need Playwright UI automation, you can simply let the workflow use the API fallback, which works reliably without a session cookie.

#### 3. Selectors Break After GitHub UI Update

**Symptom**: "Could not find assignees button" error

**Solution**: Update the selectors array in `assign-copilot-via-ui.js` with new GitHub selectors.

#### 4. Screenshots Not Uploaded

**Symptom**: No artifacts in workflow run

**Check**:
- Screenshots are saved to `/tmp/` directory
- Artifact upload step runs (even on failure via `if: always()`)
- Check artifact retention policy (7 days by default)

### Debugging Tips

1. **View Screenshots**: Download artifacts from workflow run to see what Playwright saw
2. **Check Logs**: Playwright script emits detailed logs with emojis for easy scanning
3. **Run Locally**: Test the script on your machine with debug mode enabled
4. **API Fallback Logs**: Check if fallback mechanism engaged and succeeded

## Known Limitations

### GitHub Authentication

**Issue**: GitHub does not support PAT token authentication for browser sessions.

**Impact**: The Playwright script requires a valid GitHub session cookie to function. Without it, browser-based automation cannot work.

**Current Solution**: 
- Script detects when no session cookie is available and exits early
- Workflow automatically uses API fallback for reliable operation
- No additional configuration needed for API-only mode
- Screenshots are only captured when Playwright actually runs

**To Enable Playwright UI Automation**:
- Add `GITHUB_COOKIE_USER_SESSION` secret with a valid session cookie (see Authentication section)
- Note: Session cookies expire and need periodic refresh

### Performance

- Playwright adds ~5-10 seconds per issue assignment
- For bulk assignments (>10 issues), consider API-only mode

### Browser Resources

- Requires Chromium browser installation (~200MB)
- Uses headless mode to reduce resource consumption
- May require increased timeout for low-resource environments

## Future Enhancements

### Planned Improvements

1. **OAuth Integration**: Implement proper GitHub OAuth flow for browser authentication
2. **Session Management**: Cache authenticated session cookies between runs
3. **Parallel Execution**: Assign to multiple issues concurrently with browser contexts
4. **Retry Logic**: Implement exponential backoff for transient failures
5. **Metrics**: Track success rate and performance of Playwright vs API

### Possible Extensions

- Support for other GitHub actions (labeling, commenting)
- Integration with GitHub CLI for easier authentication
- Visual regression testing for GitHub UI changes
- Webhook-based triggering of assignments

## Testing

### Automated Tests

Run the test suite:
```bash
./scripts/test-copilot-workflows.sh
```

Tests validate:
- Playwright script exists
- Package.json configuration
- Workflow integration
- Fallback mechanisms
- .gitignore exclusions

### Manual Testing

1. Create a test issue in your repository
2. Run workflow with manual trigger
3. Check workflow logs for Playwright output
4. Download and review screenshots
5. Verify issue assignment in GitHub UI

## Best Practices

### When to Use Playwright

✅ **Use Playwright when:**
- You want visual proof of automation
- Debugging complex UI interactions
- Testing new GitHub UI features
- Demonstrating the automation process

❌ **Use API instead when:**
- Assigning to many issues (>10)
- Performance is critical
- Running in resource-constrained environments
- Authentication is properly configured

### Maintenance

1. **Monitor GitHub UI Changes**: GitHub updates its UI regularly; selectors may need updates
2. **Review Screenshots**: Periodically check uploaded screenshots for issues
3. **Update Dependencies**: Keep Playwright version current for latest browser support
4. **Test After Updates**: Run manual test after any GitHub UI or workflow changes

## Security Considerations

### Token Protection

- PAT tokens are stored as GitHub secrets
- Never log or expose tokens in output
- Tokens are passed via environment variables only

### Browser Security

- Headless mode reduces attack surface
- Sandbox flags prevent breakout attempts
- Browsers are isolated per run
- No persistent data stored

### Screenshot Privacy

- Screenshots may contain sensitive repository information
- Artifacts are retained for only 7 days
- Access controlled by repository permissions
- Consider disabling in public repositories if needed

## Related Documentation

- [Automated Development Cycle](AUTOMATED_DEVELOPMENT_CYCLE.md)
- [Copilot Development Loop](COPILOT_DEVELOPMENT_LOOP.md)
- [Playwright Documentation](https://playwright.dev/)
- [GitHub Actions Artifacts](https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts)

---

**MonadicPipeline**: Visual automation with Playwright 🎭
