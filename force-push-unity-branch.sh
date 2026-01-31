#!/bin/bash
# Script to force-push the resolved copilot/implement-unity-ml-agents-client branch
# This resolves the rebase loop by updating the remote branch with a clean, conflict-free state

set -e

echo "===== Updating copilot/implement-unity-ml-agents-client Branch ====="
echo ""
echo "This script will force-push the resolved state to break the automation rebase loop."
echo ""

# Configuration
REPO="PMeeske/Ouroboros"
BRANCH="copilot/implement-unity-ml-agents-client"
RESOLVED_COMMIT="ffd8b41"

echo "Repository: $REPO"
echo "Branch: $BRANCH"
echo "Resolved Commit: $RESOLVED_COMMIT"
echo ""

# Verify we're in the right directory
if [ ! -d ".git" ]; then
    echo "Error: Not in a git repository"
    exit 1
fi

# Fetch latest
echo "Fetching latest from origin..."
git fetch origin

# Check if the commit exists locally
if ! git cat-file -e "$RESOLVED_COMMIT" 2>/dev/null; then
    echo "Error: Resolved commit $RESOLVED_COMMIT not found locally"
    echo "Please ensure you have the resolved state locally first"
    exit 1
fi

# Show what will be pushed
echo ""
echo "Current state of remote branch:"
git log --oneline origin/$BRANCH -1 2>/dev/null || echo "Branch not found on remote"
echo ""
echo "Resolved state to be pushed:"
git log --oneline $RESOLVED_COMMIT -1
echo ""

# Confirm
read -p "Do you want to force-push this resolved state? (yes/no): " confirm
if [ "$confirm" != "yes" ]; then
    echo "Aborted by user"
    exit 0
fi

# Force push
echo ""
echo "Force-pushing resolved state..."
if git push --force-with-lease origin "$RESOLVED_COMMIT:refs/heads/$BRANCH"; then
    echo ""
    echo "✅ SUCCESS: Branch $BRANCH has been updated with the resolved state!"
    echo ""
    echo "The resolved state includes:"
    echo "  - Updated .gitignore with Reqnroll/SpecFlow patterns"
    echo "  - All Unity ML Agents features (GymEnvironmentAdapter, RLAgent, etc.)"
    echo "  - Ethics Framework integration from main"
    echo "  - Clean rebase on main (db76cb3)"
    echo ""
    echo "The automation rebase loop should now be resolved."
else
    echo ""
    echo "❌ ERROR: Failed to push. You may need to configure git credentials."
    echo ""
    echo "Alternative command to run manually:"
    echo "  git push --force-with-lease origin $RESOLVED_COMMIT:refs/heads/$BRANCH"
    exit 1
fi
