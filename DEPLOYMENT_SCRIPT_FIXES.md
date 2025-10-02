# Deployment Script Fixes

## Overview

This document describes the fixes applied to the deployment scripts to address shellcheck warnings and cross-platform compatibility issues.

## Issues Fixed

### 1. Shellcheck Warnings

**SC2064: Unsafe trap command**
- **Problem**: `trap "rm -rf $TEMP_DIR" EXIT` expands the variable immediately rather than when the trap is triggered
- **Solution**: Changed to `trap 'rm -rf "$TEMP_DIR"' EXIT` using single quotes
- **Files**: `scripts/deploy-cloud.sh`, `scripts/deploy-aks.sh`

**SC2162: Read without -r flag**
- **Problem**: `read` without `-r` will mangle backslashes in input
- **Solution**: Changed all `read` commands to use `read -r` flag
- **Files**: `scripts/deploy-cloud.sh`

### 2. Cross-Platform sed Compatibility

**Problem**: The `sed -i` command works differently on Linux vs macOS:
- **GNU sed (Linux)**: `sed -i "pattern" file`
- **BSD sed (macOS)**: `sed -i '' "pattern" file` (requires empty extension)

**Solution**: Added platform detection to use the correct syntax:

```bash
if sed --version >/dev/null 2>&1; then
    # GNU sed (Linux)
    sed -i "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/deployment.yaml"
else
    # BSD sed (macOS)
    sed -i '' "s|REGISTRY_URL|${REGISTRY_URL}|g" "$TEMP_DIR/deployment.yaml"
fi
```

This ensures the scripts work correctly on both Linux and macOS systems.

## Files Changed

1. **scripts/deploy-cloud.sh**
   - Fixed trap command
   - Added `-r` flag to read commands
   - Made sed -i commands portable

2. **scripts/deploy-aks.sh**
   - Fixed trap command
   - Made sed -i commands portable

3. **IMAGEPULLBACKOFF-FIX.md**
   - Updated documentation to show both Linux and macOS sed syntax

## Validation

All scripts have been validated:

```bash
# Shellcheck validation
shellcheck scripts/deploy-cloud.sh scripts/deploy-aks.sh
# ✓ No warnings

# Bash syntax validation
bash -n scripts/deploy-k8s.sh
bash -n scripts/deploy-cloud.sh
bash -n scripts/deploy-aks.sh
# ✓ All scripts have valid syntax
```

## Impact

- **Shellcheck compliance**: Scripts now pass shellcheck with no warnings
- **Cross-platform support**: Scripts work correctly on both Linux and macOS
- **Robustness**: Improved error handling and variable expansion safety
- **No breaking changes**: All existing functionality preserved

## Related Documentation

- [DEPLOYMENT.md](DEPLOYMENT.md) - Full deployment guide
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Troubleshooting guide
- [DEPLOYMENT_ISSUE_RESOLUTION.md](DEPLOYMENT_ISSUE_RESOLUTION.md) - Previous deployment fixes
