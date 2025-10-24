# Playwright Copilot Assignment Scripts

This directory contains scripts for automating GitHub issue assignment using Playwright browser automation.

## Overview

The `assign-copilot-via-ui.js` script automates the assignment of issues to @copilot using GitHub's web interface through Playwright browser automation.

## Requirements

### Essential
- **Node.js**: v18.0.0 or higher
- **GITHUB_TOKEN**: GitHub Personal Access Token (for API fallback)

### Optional (for Playwright UI automation)
- **GITHUB_COOKIE_USER_SESSION**: GitHub session cookie for browser authentication

## Quick Start

### Install Dependencies

```bash
cd .github/scripts
npm install
```

This will install Playwright and its dependencies.

### Run the Script

**Basic usage (API fallback mode):**
```bash
export GITHUB_TOKEN="ghp_your_token_here"
node assign-copilot-via-ui.js <owner> <repo> <issue-number> [copilot-username]
```

**With Playwright UI automation:**
```bash
export GITHUB_TOKEN="ghp_your_token_here"
export GITHUB_COOKIE_USER_SESSION="your_session_cookie"
node assign-copilot-via-ui.js <owner> <repo> <issue-number> [copilot-username]

# Example:
# node assign-copilot-via-ui.js PMeeske MonadicPipeline 123 copilot
```

## Authentication

### Why Session Cookie is Needed

GitHub's web interface requires browser authentication, which **cannot** be done using PAT (Personal Access Tokens) alone. PAT tokens work great for API calls but don't authenticate browser sessions.

### How to Obtain Session Cookie

1. **Log into GitHub** in your browser
2. **Open Developer Tools** (Press F12)
3. **Navigate to**: Application → Storage → Cookies → https://github.com
4. **Find** the `user_session` cookie
5. **Copy** its Value (a long alphanumeric string)

### Adding Session Cookie for GitHub Actions

To enable Playwright UI automation in GitHub Actions workflows:

1. Go to your repository on GitHub
2. Navigate to: **Settings** → **Secrets and variables** → **Actions**
3. Click **"New repository secret"**
4. Name: `GITHUB_COOKIE_USER_SESSION`
5. Value: Paste the cookie value you copied
6. Click **"Add secret"**

### Cookie Expiration

**Note**: Session cookie lifespans may vary based on GitHub's policies. Typically they expire after a period of inactivity (often 30-90 days, but check GitHub's documentation for current information).

If Playwright automation stops working:

1. Obtain a fresh cookie using the steps above
2. Update the `GITHUB_COOKIE_USER_SESSION` secret
3. Re-run the workflow

## Default Behavior (Without Session Cookie)

**Don't have a session cookie? No problem!**

If `GITHUB_COOKIE_USER_SESSION` is not provided, the script will:

1. Detect the missing authentication
2. Log helpful setup instructions
3. Exit early (return code 1)
4. Allow the workflow to use API fallback

The API fallback method works reliably with just a PAT token and requires no additional configuration.

## Testing

### Run Authentication Tests

```bash
bash test-authentication.sh
```

This validates:
- Script authentication logic
- Documentation completeness
- Workflow configuration
- Script behavior without cookie

### Run All Workflow Tests

```bash
cd ../../scripts
bash test-copilot-workflows.sh
```

## Troubleshooting

### "GITHUB_COOKIE_USER_SESSION not provided"

**This is normal and expected** if you haven't configured the session cookie. The workflow will automatically use API fallback.

**To enable Playwright UI automation**: Follow the "How to Obtain Session Cookie" section above.

### "Not authenticated - page shows sign-in prompt"

Your session cookie has expired. Obtain a fresh cookie and update the secret.

### Script exits with code 1

This is expected when no session cookie is available. The workflow is designed to handle this and continue with API fallback.

### Playwright installation fails

Ensure you have Node.js 18+ and run:

```bash
npm install
npx playwright install chromium --with-deps
```

## Workflow Integration

The script is integrated into the `copilot-automated-development-cycle.yml` workflow with:

1. **Node.js setup**: Installs Node 20 runtime
2. **Playwright installation**: Installs Chromium browser
3. **Script execution**: Runs for each issue to assign
4. **Screenshot capture**: Saves debugging screenshots to `/tmp/`
5. **Artifact upload**: Uploads screenshots as workflow artifacts
6. **API fallback**: Assigns via API if Playwright fails

## Files

- `assign-copilot-via-ui.js` - Main Playwright automation script
- `package.json` - Node.js dependencies configuration
- `test-authentication.sh` - Authentication test suite
- `README.md` - This file

## Development

### Debug Mode

To see the browser in action (non-headless mode), edit `assign-copilot-via-ui.js`:

```javascript
browser = await chromium.launch({
  headless: false,  // Change to false
  args: ['--no-sandbox', '--disable-setuid-sandbox']
});
```

### Adding New Selectors

If GitHub's UI changes and existing selectors break, update the selector arrays in the script:

```javascript
const selectors = [
  '[aria-label="Select assignees"]',
  'button[aria-label="Select assignees"]',
  // Add new selectors here
];
```

## Related Documentation

- [Automated Development Cycle](../../docs/AUTOMATED_DEVELOPMENT_CYCLE.md)
- [Playwright Copilot Assignment](../../docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md)
- [Copilot Development Loop](../../docs/COPILOT_DEVELOPMENT_LOOP.md)

## Support

If you encounter issues:

1. Check the workflow logs in GitHub Actions
2. Review screenshots in workflow artifacts
3. Run the authentication test suite
4. Consult the troubleshooting section above

---

**MonadicPipeline**: Automated issue assignment with Playwright 🎭
