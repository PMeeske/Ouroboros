# Playwright Authentication Flow

This document explains how authentication works in the Playwright-based GitHub issue assignment automation.

## Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    Workflow Execution Starts                     │
└───────────────────────────────┬─────────────────────────────────┘
                                │
                                ▼
                ┌───────────────────────────────┐
                │   Setup Node.js & Playwright  │
                │   Install Dependencies        │
                └───────────────┬───────────────┘
                                │
                                ▼
                ┌───────────────────────────────┐
                │   Run assign-copilot-via-ui.js│
                │   with environment variables  │
                └───────────────┬───────────────┘
                                │
                                ▼
                ┌───────────────────────────────┐
                │   Check for Session Cookie    │
                │   (GITHUB_COOKIE_USER_SESSION)│
                └───────────────┬───────────────┘
                                │
                ┌───────────────┴───────────────┐
                ▼                               ▼
    ┌──────────────────┐            ┌──────────────────┐
    │  Cookie Present? │            │  No Cookie?      │
    │  ✅ Yes          │            │  ⚠️ None Found   │
    └────────┬─────────┘            └────────┬─────────┘
             │                               │
             ▼                               ▼
    ┌──────────────────┐         ┌──────────────────────┐
    │  Launch Browser  │         │  Log Warning Message │
    │  with Cookie     │         │  Exit Early (code 1) │
    └────────┬─────────┘         └──────────┬───────────┘
             │                               │
             ▼                               │
    ┌──────────────────┐                    │
    │  Navigate to     │                    │
    │  Issue Page      │                    │
    └────────┬─────────┘                    │
             │                               │
             ▼                               │
    ┌──────────────────┐                    │
    │  Check Auth      │                    │
    │  Status          │                    │
    └────────┬─────────┘                    │
             │                               │
    ┌────────┴────────┐                     │
    ▼                 ▼                     │
┌──────┐        ┌──────────┐               │
│ Auth │        │ Not Auth │               │
│  ✅   │        │    ❌     │               │
└──┬───┘        └────┬─────┘               │
   │                 │                     │
   ▼                 ▼                     │
┌────────┐      ┌─────────┐               │
│Interact│      │  Exit   │               │
│with UI │      │ (fail)  │               │
└───┬────┘      └────┬────┘               │
    │                │                     │
    ▼                │                     │
┌────────┐           │                     │
│Assign  │           │                     │
│Success │           │                     │
└───┬────┘           │                     │
    │                │                     │
    └────────┬───────┴─────────────────────┘
             │
             ▼
    ┌──────────────────┐
    │   Playwright     │
    │   Exit Code      │
    │   (0=✅, 1=❌)    │
    └────────┬─────────┘
             │
             ▼
    ┌──────────────────┐
    │  Workflow Checks │
    │  Exit Code       │
    └────────┬─────────┘
             │
    ┌────────┴────────┐
    ▼                 ▼
┌──────┐         ┌──────────┐
│Exit 0│         │Exit 1    │
│(OK)  │         │(Expected)│
└──┬───┘         └────┬─────┘
   │                  │
   │    ┌─────────────┘
   │    │
   │    ▼
   │  ┌──────────────────────┐
   │  │  API Fallback Step   │
   │  │  (Always Runs)       │
   │  └──────────┬───────────┘
   │             │
   └─────────────┤
                 ▼
        ┌──────────────────┐
        │   Check if Issue │
        │   Already        │
        │   Assigned       │
        └────────┬─────────┘
                 │
        ┌────────┴────────┐
        ▼                 ▼
    ┌──────┐         ┌──────────┐
    │Already│         │Not Yet   │
    │Done  │         │Assigned  │
    └──┬───┘         └────┬─────┘
       │                  │
       │                  ▼
       │         ┌──────────────┐
       │         │  Assign via  │
       │         │  GitHub API  │
       │         │  (Reliable)  │
       │         └──────┬───────┘
       │                │
       └────────┬───────┘
                ▼
        ┌──────────────┐
        │   Success!   │
        │   Issue      │
        │   Assigned   │
        └──────────────┘
```

## Authentication Methods Comparison

| Method | Works? | When to Use | Setup Complexity |
|--------|--------|-------------|------------------|
| **GitHub PAT Token (API)** | ✅ Yes | Default method | Easy - Just add token secret |
| **Session Cookie (Browser)** | ✅ Yes | For UI automation | Medium - Extract from browser |
| **PAT Token (Browser)** | ❌ No | Never | N/A - Doesn't work |

## Key Points

### 1. Why PAT Tokens Don't Work for Browsers

- PAT tokens are designed for **API authentication**
- GitHub's web UI requires **session-based authentication**
- Browsers use cookies, not Authorization headers
- This is a GitHub security design, not a bug

### 2. The Solution

**Option A: Use Session Cookie (Optional)**
- Extract cookie from authenticated browser
- Add as `GITHUB_COOKIE_USER_SESSION` secret
- Enables full Playwright UI automation
- Requires periodic refresh

**Option B: Use API Fallback (Default)**
- No additional setup needed
- Works with just `GITHUB_TOKEN` (PAT)
- Reliable and fast
- No browser automation

### 3. How Fallback Works

The workflow is designed to always succeed:

1. **Try Playwright**: If cookie available, use browser automation
2. **Catch Failure**: If Playwright fails/exits, continue workflow
3. **Use API**: Always check and assign via API if needed
4. **Result**: Issue gets assigned regardless of authentication method

### 4. Security Considerations

**Session Cookies:**
- More powerful than PAT tokens
- Have same permissions as the logged-in user
- Should be stored as repository secrets
- Expire after inactivity period
- Should be refreshed periodically

**PAT Tokens:**
- Limited to specific scopes
- Better for API-only operations
- Don't expire based on inactivity
- Safer for automation

## Configuration Examples

### Minimal Setup (API Only)

```yaml
env:
  GITHUB_TOKEN: ${{ secrets.COPILOT_BOT_TOKEN }}
```

**Result**: API assignment works, no Playwright

### Full Setup (Playwright + API)

```yaml
env:
  GITHUB_TOKEN: ${{ secrets.COPILOT_BOT_TOKEN }}
  GITHUB_COOKIE_USER_SESSION: ${{ secrets.GITHUB_COOKIE_USER_SESSION }}
```

**Result**: Playwright UI automation with API fallback

## Debugging Guide

### Check Authentication Status

Look for these messages in workflow logs:

**No Cookie:**
```
⚠️  GITHUB_COOKIE_USER_SESSION not provided
ℹ️   Browser authentication will not be available
```

**Cookie Present:**
```
✅ GITHUB_COOKIE_USER_SESSION provided
ℹ️  Browser authentication enabled
```

**Cookie Expired/Invalid:**
```
❌ Not authenticated - page shows sign-in prompt
```

### Verify Fallback Worked

Look for these messages:

```
🔄 Fallback: Checking assignments for issues: 123, 124
✅ Issue #123 already has assignees, skipping
🧑‍🚀 Assigned copilot to #124 via API
```

## Maintenance

### When to Refresh Session Cookie

- Cookie expired (Playwright fails with "not authenticated")
- After changing GitHub password
- After significant security setting changes
- Periodically (every 30-60 days recommended)

### How to Refresh

1. Log out of GitHub
2. Log back in
3. Extract new cookie value
4. Update `GITHUB_COOKIE_USER_SESSION` secret
5. Re-run workflow to verify

## Related Documentation

- [Main README](.github/scripts/README.md) - Quick start and usage
- [Test Suite](test-authentication.sh) - Authentication tests
- [Playwright Docs](../../docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md) - Detailed guide
- [Dev Cycle Docs](../../docs/AUTOMATED_DEVELOPMENT_CYCLE.md) - Workflow overview

---

**MonadicPipeline**: Secure and reliable automation 🔐
