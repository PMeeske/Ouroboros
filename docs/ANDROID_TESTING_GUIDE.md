# Android App Testing Guide for Testers

Welcome to the Ouroboros Android App testing team! This guide will help you access test builds, install them on your device, and report issues effectively.

## 📥 Getting Test Builds

### Automated CI/CD Builds

Every time code is pushed to the repository, an Android APK is automatically built and made available for testing. Here's how to access it:

#### Method 1: Email Notification (Easiest)

If you're on the testing team email list, you'll receive automated notifications with:
- Direct download link
- QR code for mobile download
- Build version and details
- What's new in this build

Simply click the link or scan the QR code to access the build.

#### Method 2: GitHub Actions (Manual)

1. **Navigate to Actions**
   - Go to: https://github.com/PMeeske/Ouroboros/actions/workflows/android-build.yml
   - Or click the "Actions" tab in the GitHub repository

2. **Find the Latest Build**
   - Look for the most recent successful run (green checkmark ✅)
   - Click on the workflow run name

3. **Download the APK**
   - Scroll down to the "Artifacts" section
   - Click on **monadic-pipeline-android-apk** to download
   - Extract the ZIP file to get the APK

4. **Check Build Metadata** (Optional)
   - Download the **build-metadata** artifact for version info
   - Contains: version number, commit hash, build date

### Build Versioning

Builds follow this version scheme:
- **Version Format**: `1.0.<build_number>`
- **Example**: `1.0.345` means this is the 345th build
- Each build has a unique commit hash (first 7 characters shown)

## 📲 Installing the APK on Your Device

### Prerequisites
- Android device running **Android 5.0 (Lollipop) or higher**
- At least **100 MB** of free storage
- Internet connection (for downloading)

### Installation Steps

#### For First-Time Installation:

1. **Download the APK**
   - Use one of the methods above to download the APK file
   - Note: You may need to extract it from a ZIP file first

2. **Transfer to Your Android Device**
   - **Option A (USB)**: Connect your device to computer, copy APK to Downloads folder
   - **Option B (Email)**: Email the APK to yourself, download on device
   - **Option C (Cloud)**: Upload to Google Drive/Dropbox, download on device
   - **Option D (Direct)**: Download directly on your Android device using mobile browser

3. **Enable Unknown Sources**
   - Go to **Settings** → **Security** (or **Privacy**)
   - Find **Install Unknown Apps** (Android 8+) or **Unknown Sources** (Android 7 and below)
   - Enable installation from your file manager or browser
   - ⚠️ **Security Note**: Only enable this temporarily for testing

4. **Install the APK**
   - Open your **Files** or **Downloads** app
   - Tap on the APK file
   - Tap **Install** when prompted
   - Wait for installation to complete
   - Tap **Open** to launch, or find **Ouroboros CLI** in your app drawer

#### For Updating to a New Build:

You have two options:

**Option A: Clean Install (Recommended)**
1. Uninstall the previous version: Settings → Apps → Ouroboros CLI → Uninstall
2. Follow first-time installation steps above
3. ⚠️ **Note**: This will clear all app data (settings, history)

**Option B: Update Over Existing**
1. Simply install the new APK over the old one
2. Your settings and data should be preserved
3. If you experience issues, do a clean install instead

## 🧪 What to Test

### Critical Tests (Always Check These)

#### 1. Purple Screen Bug Check
**What it is**: A critical bug where the app shows only a purple screen on startup

**How to test**:
- Fresh install: Uninstall, reinstall, launch
- **Expected**: Terminal interface with welcome message appears
- **Bug Symptom**: Only purple screen, no text or UI
- **If you see this**: 🚨 CRITICAL BUG - Report immediately!

#### 2. App Launch and Initialization
- App launches within 5 seconds
- Welcome message appears: "Ouroboros CLI v1.0"
- Terminal prompt appears: ">"
- No crash or freeze on startup

#### 3. Basic Commands
Test these core commands:

```
help
```
- **Expected**: List of available commands with descriptions
- **Test**: Do all commands appear?

```
version
```
- **Expected**: Shows app version (e.g., "Ouroboros CLI v1.0")
- **Test**: Version number matches build metadata?

```
status
```
- **Expected**: Shows connection status
- **Test**: Does it show "Not configured" on first launch?

#### 4. Configuration Flow
```
config http://192.168.1.100:11434
```
- **Expected**: "✓ Endpoint configured: http://192.168.1.100:11434"
- **Test**: Replace with your actual Ollama server IP

```
ping
```
- **Expected**: Connection success or clear error message
- **Test**: Is the error message helpful if connection fails?

#### 5. Model Management
```
models
```
- **Expected**: List of available models or error message
- **Test**: If you have Ollama running, are models listed?

```
pull tinyllama
```
- **Expected**: Download progress or error
- **Test**: Cancel during download - does app handle gracefully?

### Advanced Tests (If Time Permits)

#### 6. AI Interaction
```
ask What is functional programming?
```
- **Expected**: AI generates a response
- **Test**: Response appears? Streaming works? Any errors?

#### 7. Command History
- Type a command and execute
- Tap the **↑** button
- **Expected**: Previous command appears in input field
- **Test**: Can you navigate through history?

#### 8. Auto-Suggestions
- Type partial command: `mo`
- **Expected**: Suggestions appear (e.g., "models")
- **Test**: Do suggestions make sense? Can you select them?

#### 9. Quick Action Buttons
- Tap the **help** button directly (without typing)
- **Expected**: Help command executes immediately
- **Test**: Do all quick action buttons work?

#### 10. Settings and Configuration
- Tap **⚙️ Settings** button
- Navigate through: AI Providers, Symbolic Reasoning, etc.
- **Expected**: All screens load without crashing
- **Test**: Can you configure and save settings?

### Error Handling Tests

Try to break the app (in a good way!):

1. **Invalid Commands**
   ```
   invalidcommand12345
   ```
   - **Expected**: Clear error message
   - **Test**: App doesn't crash?

2. **Network Errors**
   - Configure endpoint to invalid address: `config http://999.999.999.999:11434`
   - Try `ping` or `models`
   - **Expected**: Clear error message explaining network issue
   - **Test**: App recovers? Can try again?

3. **Empty Input**
   - Press Execute with no command
   - **Expected**: Nothing happens or friendly message
   - **Test**: No crash?

4. **Very Long Input**
   - Type a very long command (200+ characters)
   - **Expected**: Handles gracefully (truncate, scroll, or error)
   - **Test**: UI doesn't break?

5. **Rapid Commands**
   - Execute 10 commands quickly in succession
   - **Expected**: All execute or queue properly
   - **Test**: App remains responsive?

## 🐛 Reporting Issues

### What Makes a Good Bug Report?

Include these details:

1. **Device Information**
   - Device model (e.g., "Samsung Galaxy S21")
   - Android version (e.g., "Android 12")
   - Find this: Settings → About Phone → Android Version

2. **Build Information**
   - APK version (from email or download)
   - Build number (e.g., "1.0.345")
   - Build date

3. **Steps to Reproduce**
   - List exact steps that caused the issue
   - Be specific: "Typed 'help', pressed Execute"
   - Can you reproduce it consistently?

4. **Expected vs Actual**
   - **Expected**: What should have happened?
   - **Actual**: What actually happened?

5. **Screenshots/Videos**
   - Screenshots are very helpful!
   - Screen recording even better for complex issues
   - Use Android's built-in screen recorder

6. **Logs (Advanced)**
   - If the app crashes, Android may capture a crash log
   - You can view this via: Settings → Developer Options → Bug Report

### Issue Severity Levels

Help us prioritize by indicating severity:

- **🚨 CRITICAL**: App crashes, purple screen, data loss
- **🔴 HIGH**: Major features broken, blocks testing
- **🟡 MEDIUM**: Feature works but has issues, workaround exists
- **🟢 LOW**: Minor UI glitch, typos, cosmetic issues
- **💡 ENHANCEMENT**: Suggestion for improvement

### Where to Report

**Option 1: GitHub Issues** (Preferred)
1. Go to: https://github.com/PMeeske/Ouroboros/issues
2. Click **New Issue**
3. Choose **Bug Report** template
4. Fill in the details
5. Add label: `android` and `testing`

**Option 2: Email**
- Send to the development team email
- Use subject: `[Android Bug] Brief description`
- Include all details from above

**Option 3: Team Channel**
- Post in your team's communication channel
- Tag the development team
- Include screenshots inline if possible

### Example Bug Report

```markdown
**Title**: Purple screen appears on fresh install (Android 11)

**Severity**: 🚨 CRITICAL

**Device**: Google Pixel 4a, Android 11

**Build**: v1.0.345, Built on 2025-12-31

**Steps to Reproduce**:
1. Uninstall previous version completely
2. Install APK v1.0.345
3. Launch app from app drawer
4. Wait 10 seconds

**Expected**: Terminal interface with welcome message

**Actual**: Only purple screen, no UI visible

**Screenshot**: [Attached]

**Additional Info**:
- Tried 3 times, happens every time
- Previous build (1.0.342) worked fine
- No error messages shown

**Can Reproduce**: ✅ Yes, consistently
```

## 📊 Testing Checklist

Use this checklist for each new build:

### Initial Check
- [ ] App installs successfully
- [ ] App launches (no crash)
- [ ] No purple screen bug
- [ ] Welcome screen appears
- [ ] Version matches build metadata

### Core Functionality
- [ ] `help` command works
- [ ] `version` command works
- [ ] `status` command works
- [ ] `config` command accepts endpoint
- [ ] `ping` command tests connection
- [ ] `models` command lists models
- [ ] Error messages are clear

### UI/UX
- [ ] Terminal is readable
- [ ] Text scrolls properly
- [ ] Input field works
- [ ] Execute button responds
- [ ] Quick action buttons work
- [ ] Settings menu accessible
- [ ] Navigation works smoothly

### Performance
- [ ] App launches quickly (< 5 seconds)
- [ ] Commands respond quickly (< 1 second)
- [ ] Smooth scrolling
- [ ] No noticeable lag
- [ ] Battery usage reasonable

### Error Handling
- [ ] Invalid commands show helpful errors
- [ ] Network errors explained clearly
- [ ] App recovers from errors
- [ ] No crashes observed

### Device-Specific
- [ ] Works in portrait mode
- [ ] Works in landscape mode
- [ ] Keyboard appears correctly
- [ ] Back button behaves properly
- [ ] App pause/resume works
- [ ] Notifications (if any) work

## 💬 Tips for Effective Testing

1. **Test Like a Real User**
   - Don't just test happy paths
   - Try things users might actually do
   - Think: "What would confuse a new user?"

2. **Document Everything**
   - Take notes as you test
   - Screenshot liberally
   - Record videos for complex issues

3. **Test on Your Daily Driver**
   - Use it like you would a regular app
   - This catches real-world issues

4. **Test Different Scenarios**
   - Fresh install vs update
   - First launch vs subsequent launches
   - With network vs without network
   - Different Ollama server configurations

5. **Be Patient with Early Builds**
   - Early builds may have known issues
   - Focus on critical functionality first
   - Enhancement suggestions are welcome but separate from bugs

6. **Communicate**
   - If something is unclear, ask!
   - If you're blocked, report it
   - If something is great, share that too!

## 🎯 Focus Areas for This Release

*The development team will update this section for each build to highlight what specifically needs testing.*

### Current Focus (Update per build)
- Purple screen bug fix verification
- Initialization error handling
- Service degradation gracefully
- User-friendly error messages

### Known Issues (Don't report these)
- *List of known issues that are being worked on*

## ❓ FAQ

### Q: Do I need to test every build?
**A**: No, test when you have time. Priority builds will be marked in notifications.

### Q: How long does testing take?
**A**: Quick test: 10-15 minutes. Thorough test: 30-45 minutes.

### Q: What if I don't have an Ollama server?
**A**: You can still test basic commands, UI, and error handling. Some features will show errors (which is also good to test!).

### Q: Can I test on an emulator?
**A**: Yes! Android Studio emulator works. Physical devices better for real-world testing.

### Q: The APK download link doesn't work
**A**: Artifacts expire after 30 days. Request a new build or check for a more recent one.

### Q: I found a security issue
**A**: Don't post publicly! Email the security team directly with details.

### Q: Can I test on multiple devices?
**A**: Absolutely! Device diversity helps catch more issues.

### Q: What if testing finds no issues?
**A**: Great! Report that too: "Tested build X.X.X on [device] - all tests passed ✅"

## 📚 Additional Resources

- **User Guide**: See [Android README](../src/Ouroboros.Android/README.md) for user documentation
- **Development Guide**: For technical contributors
- **CI/CD Documentation**: How builds are automated
- **Issue Tracker**: https://github.com/PMeeske/Ouroboros/issues?q=label%3Aandroid

## 🙏 Thank You!

Your testing helps make Ouroboros better for everyone. Every bug you find and report improves the app's quality. Thank you for your time and attention to detail!

---

**Last Updated**: 2025-12-31  
**Document Version**: 1.0  
**Maintained By**: Ouroboros Development Team
