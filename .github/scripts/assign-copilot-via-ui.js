#!/usr/bin/env node

/**
 * Playwright script to assign @copilot to GitHub issues via UI
 * 
 * This script uses Playwright to interact with GitHub's web UI to assign
 * copilot to issues. It authenticates using GitHub's cookie-based session.
 * 
 * Usage:
 *   node assign-copilot-via-ui.js <owner> <repo> <issue-number> [copilot-username]
 * 
 * Environment variables:
 *   GITHUB_TOKEN - GitHub Personal Access Token (required)
 *   GITHUB_COOKIE_USER_SESSION - GitHub user_session cookie (optional, for authenticated requests)
 *   COPILOT_USER - Username to assign (defaults to 'copilot')
 */

const { chromium } = require('@playwright/test');

// Parse command line arguments
const args = process.argv.slice(2);
if (args.length < 3) {
  console.error('Usage: node assign-copilot-via-ui.js <owner> <repo> <issue-number> [copilot-username]');
  console.error('Environment: GITHUB_TOKEN must be set');
  process.exit(1);
}

const [owner, repo, issueNumber, copilotUsername = 'copilot'] = args;
const githubToken = process.env.GITHUB_TOKEN;
const githubCookie = process.env.GITHUB_COOKIE_USER_SESSION;

if (!githubToken) {
  console.error('Error: GITHUB_TOKEN environment variable is required');
  process.exit(1);
}

async function assignCopilotToIssue() {
  console.log(`🚀 Starting Playwright automation...`);
  console.log(`   Repository: ${owner}/${repo}`);
  console.log(`   Issue: #${issueNumber}`);
  console.log(`   Assignee: @${copilotUsername}`);
  
  let browser;
  try {
    // Launch browser in headless mode
    browser = await chromium.launch({
      headless: true,
      args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-dev-shm-usage']
    });
    
    const context = await browser.newContext({
      userAgent: 'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
      extraHTTPHeaders: {
        'Authorization': `token ${githubToken}`,
        'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8'
      }
    });
    
    // If we have a session cookie, use it for authentication
    if (githubCookie) {
      await context.addCookies([{
        name: 'user_session',
        value: githubCookie,
        domain: '.github.com',
        path: '/',
        httpOnly: true,
        secure: true,
        sameSite: 'Lax'
      }]);
      console.log('🔐 Added GitHub session cookie');
    }
    
    const page = await context.newPage();
    
    // Construct the issue URL
    const issueUrl = `https://github.com/${owner}/${repo}/issues/${issueNumber}`;
    console.log(`📄 Navigating to: ${issueUrl}`);
    
    // Navigate to the issue page
    await page.goto(issueUrl, { waitUntil: 'networkidle' });
    
    // Wait a moment for page to fully load
    await page.waitForTimeout(2000);
    
    // Take a screenshot for debugging
    await page.screenshot({ path: '/tmp/github-issue-page.png', fullPage: true });
    console.log('📸 Screenshot saved to /tmp/github-issue-page.png');
    
    // Check if we're authenticated by looking for user menu
    const userMenu = await page.locator('[data-target="react-partial.embeddedData"]').first();
    const isAuthenticated = await userMenu.isVisible({ timeout: 2000 }).catch(() => false);
    
    if (!isAuthenticated) {
      console.log('⚠️ Warning: May not be authenticated. Continuing anyway...');
    } else {
      console.log('✅ Authenticated successfully');
    }
    
    // Try to find and click the assignees section
    console.log('🔍 Looking for assignees section...');
    
    // GitHub issue page has a sidebar with assignees - try multiple selectors
    let assigneesButton = null;
    
    // Try different possible selectors for the assignees button
    const selectors = [
      '[aria-label="Select assignees"]',
      'button[aria-label="Select assignees"]',
      '.sidebar-assignee button',
      '[data-hotkey="a"]',
      'summary:has-text("Assignees")'
    ];
    
    for (const selector of selectors) {
      assigneesButton = await page.locator(selector).first();
      if (await assigneesButton.isVisible({ timeout: 2000 }).catch(() => false)) {
        console.log(`✅ Found assignees button using selector: ${selector}`);
        break;
      }
      assigneesButton = null;
    }
    
    if (assigneesButton && await assigneesButton.isVisible({ timeout: 1000 }).catch(() => false)) {
      console.log('✅ Clicking assignees button');
      await assigneesButton.click();
      await page.waitForTimeout(1500);
      
      // Take screenshot after clicking
      await page.screenshot({ path: '/tmp/github-assignees-open.png', fullPage: true });
      console.log('📸 Screenshot after opening assignees');
      
      // Search for the copilot user - try multiple selectors for search input
      const searchSelectors = [
        'input[placeholder*="Type or choose"]',
        'input[type="text"][aria-label*="assignee"]',
        'input.js-filterable-field',
        'input[name="query"]'
      ];
      
      let searchInput = null;
      for (const selector of searchSelectors) {
        searchInput = await page.locator(selector).first();
        if (await searchInput.isVisible({ timeout: 2000 }).catch(() => false)) {
          console.log(`✅ Found search input using selector: ${selector}`);
          break;
        }
        searchInput = null;
      }
      
      if (searchInput) {
        console.log(`🔎 Searching for user: ${copilotUsername}`);
        await searchInput.fill(copilotUsername);
        await page.waitForTimeout(2000);
        
        // Take screenshot after search
        await page.screenshot({ path: '/tmp/github-search-results.png', fullPage: true });
        console.log('📸 Screenshot after search');
        
        // Click on the user from the dropdown - try multiple ways
        const userSelectors = [
          `[data-filterable-for*="${copilotUsername}"]`,
          `.select-menu-item:has-text("${copilotUsername}")`,
          `a:has-text("${copilotUsername}")`,
          `.menu-item:has-text("${copilotUsername}")`
        ];
        
        let userOption = null;
        for (const selector of userSelectors) {
          userOption = await page.locator(selector).first();
          if (await userOption.isVisible({ timeout: 2000 }).catch(() => false)) {
            console.log(`✅ Found user option using selector: ${selector}`);
            break;
          }
          userOption = null;
        }
        
        if (userOption) {
          console.log(`✅ Found user ${copilotUsername}, clicking...`);
          await userOption.click();
          await page.waitForTimeout(2000);
          
          // Take final screenshot
          await page.screenshot({ path: '/tmp/github-final.png', fullPage: true });
          console.log('📸 Final screenshot saved');
          
          console.log('✅ Successfully assigned copilot to the issue!');
          return true;
        } else {
          console.error(`❌ User ${copilotUsername} not found in dropdown`);
          await page.screenshot({ path: '/tmp/github-search-failed.png', fullPage: true });
          return false;
        }
      } else {
        console.error('❌ Could not find search input');
        await page.screenshot({ path: '/tmp/github-no-search.png', fullPage: true });
        return false;
      }
    } else {
      console.log('⚠️ Assignees button not visible');
      
      // Check if there's a login prompt
      const isLoginPage = page.url().includes('/login');
      if (isLoginPage) {
        console.error('❌ Redirected to login page - authentication failed');
        console.log('ℹ️  Note: GitHub PAT tokens cannot be used for UI authentication');
        console.log('ℹ️  This approach requires a valid GitHub session cookie or OAuth flow');
        return false;
      }
      
      // Check page content for clues
      const pageContent = await page.content();
      if (pageContent.includes('Sign in') || pageContent.includes('Sign up')) {
        console.error('❌ Not authenticated - page shows sign-in prompt');
      }
      
      await page.screenshot({ path: '/tmp/github-no-assignees.png', fullPage: true });
      return false;
    }
    
  } catch (error) {
    console.error('❌ Error during Playwright automation:', error.message);
    console.error(error.stack);
    if (browser) {
      try {
        const pages = await browser.pages();
        if (pages && pages.length > 0) {
          await pages[0].screenshot({ path: '/tmp/github-error.png', fullPage: true });
          console.log('📸 Error screenshot saved to /tmp/github-error.png');
        }
      } catch (screenshotError) {
        console.error('Could not take error screenshot:', screenshotError.message);
      }
    }
    return false;
  } finally {
    if (browser) {
      await browser.close();
      console.log('🔒 Browser closed');
    }
  }
}

// Run the script
assignCopilotToIssue()
  .then(success => {
    if (success) {
      console.log('✅ Script completed successfully');
      process.exit(0);
    } else {
      console.error('❌ Script failed to assign copilot');
      process.exit(1);
    }
  })
  .catch(error => {
    console.error('💥 Unexpected error:', error);
    process.exit(1);
  });
