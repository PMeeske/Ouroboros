# Email Notification Setup for Android APK Builds

This guide explains how to configure email notifications for successful Android APK builds.

## Overview

When the Android build workflow (`android-build.yml`) successfully creates an APK, it will automatically send an email notification with a download link to the build artifacts.

## Required GitHub Secrets

You need to configure the following secrets in your GitHub repository:

### Navigation to Secrets
1. Go to your repository on GitHub
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**

### Required Secrets

#### 1. `SMTP_SERVER`
- **Description**: Your email server address
- **Examples**:
  - Gmail: `smtp.gmail.com`
  - Outlook: `smtp.office365.com`
  - Custom SMTP server: `mail.yourdomain.com`

#### 2. `SMTP_PORT`
- **Description**: SMTP server port number
- **Common values**:
  - `587` - TLS (recommended)
  - `465` - SSL
  - `25` - Unencrypted (not recommended)

#### 3. `SMTP_USERNAME`
- **Description**: Email account username
- **Examples**:
  - Gmail: `your-email@gmail.com`
  - Outlook: `your-email@outlook.com`
  - Custom: Your email username

#### 4. `SMTP_PASSWORD`
- **Description**: Email account password or app-specific password
- **Important Notes**:
  - For Gmail: Use an [App Password](https://support.google.com/accounts/answer/185833)
  - For Outlook: Use your account password or app password
  - Never use your main account password if 2FA is enabled

#### 5. `SMTP_FROM_EMAIL`
- **Description**: The email address to send from
- **Example**: `noreply@yourdomain.com` or your email address

#### 6. `NOTIFICATION_EMAIL`
- **Description**: The recipient email address(es)
- **Examples**:
  - Single recipient: `developer@example.com`
  - Multiple recipients: `dev1@example.com,dev2@example.com`

## Setup Examples

### Example 1: Gmail Configuration

```
SMTP_SERVER: smtp.gmail.com
SMTP_PORT: 587
SMTP_USERNAME: your-email@gmail.com
SMTP_PASSWORD: your-app-specific-password
SMTP_FROM_EMAIL: your-email@gmail.com
NOTIFICATION_EMAIL: recipient@example.com
```

**Gmail App Password Setup:**
1. Go to [Google Account Security](https://myaccount.google.com/security)
2. Enable 2-Step Verification
3. Go to [App Passwords](https://myaccount.google.com/apppasswords)
4. Generate a new app password for "Mail"
5. Use this password in `SMTP_PASSWORD` secret

### Example 2: Outlook/Office 365 Configuration

```
SMTP_SERVER: smtp.office365.com
SMTP_PORT: 587
SMTP_USERNAME: your-email@outlook.com
SMTP_PASSWORD: your-password
SMTP_FROM_EMAIL: your-email@outlook.com
NOTIFICATION_EMAIL: recipient@example.com
```

### Example 3: Custom SMTP Server

```
SMTP_SERVER: mail.yourdomain.com
SMTP_PORT: 587
SMTP_USERNAME: notifications@yourdomain.com
SMTP_PASSWORD: secure-password
SMTP_FROM_EMAIL: noreply@yourdomain.com
NOTIFICATION_EMAIL: team@yourdomain.com
```

## Email Notification Content

The automated email includes:

- ✅ Build success status
- 📱 Repository and branch information
- 📝 Commit SHA and message
- 👤 User who triggered the build
- 🔗 Direct link to download the APK artifact
- 📲 Installation instructions
- ⏰ Artifact retention period (30 days)

## Testing the Configuration

1. Set up all required secrets in your repository
2. Make a change to the Android app code
3. Push to a branch that triggers the workflow (main, develop, or copilot/**)
4. Monitor the workflow run
5. Check your email for the notification

## Troubleshooting

### Email Not Received

1. **Check workflow logs**:
   - Go to Actions tab → Select the workflow run
   - Check the "Send email notification" step for errors

2. **Verify SMTP credentials**:
   - Ensure username and password are correct
   - For Gmail: Verify app password is used (not account password)
   - Check if 2FA requires app-specific passwords

3. **Check spam folder**:
   - Automated emails might be filtered as spam
   - Add the sender to your contacts

4. **Verify port and server**:
   - Ensure SMTP_PORT matches your provider's requirements
   - Test SMTP server is accessible from GitHub Actions runners

5. **Check email limits**:
   - Some providers have sending limits
   - Ensure you haven't exceeded them

### Common Errors

#### "Authentication Failed"
- **Solution**: Verify SMTP_USERNAME and SMTP_PASSWORD
- For Gmail: Ensure you're using an App Password

#### "Connection Timeout"
- **Solution**: Check SMTP_SERVER and SMTP_PORT values
- Ensure port 587 or 465 is correct for your provider

#### "Sender Address Rejected"
- **Solution**: Verify SMTP_FROM_EMAIL matches your authenticated account
- Some providers require from address to match username

## Security Best Practices

1. **Never commit secrets** to the repository
2. **Use app-specific passwords** when available
3. **Limit secret access** to necessary workflows only
4. **Rotate credentials** regularly
5. **Use organization secrets** for shared configurations
6. **Monitor secret usage** in GitHub audit logs

## Alternative: GitHub Notifications

If you prefer not to set up email SMTP, you can:

1. **Enable GitHub Notifications**:
   - Go to repository Settings → Notifications
   - Enable workflow notifications

2. **Watch the repository**:
   - Click "Watch" → "Custom" → Enable "Actions"

3. **Use GitHub mobile app**:
   - Receive push notifications for workflow runs

## Disabling Email Notifications

To disable email notifications without removing the workflow:

1. **Option 1**: Remove the email notification step from `android-build.yml`
2. **Option 2**: Don't configure the secrets (step will be skipped automatically)
3. **Option 3**: Comment out the email notification step with `#`

## Support

For issues related to:
- **Workflow configuration**: Check `.github/workflows/android-build.yml`
- **SMTP providers**: Consult your email provider's documentation
- **GitHub Actions**: See [GitHub Actions documentation](https://docs.github.com/en/actions)

## Additional Features

You can customize the email by editing the workflow file:

- **Change subject line**: Modify the `subject:` field
- **Customize body**: Edit the `body:` field (supports Markdown)
- **Add attachments**: Use the `attachments:` field (note: GitHub artifacts are links, not direct attachments)
- **Change priority**: Modify `priority:` (low, normal, high)

Example customization:
```yaml
- name: Send email notification with APK download link
  if: success()
  uses: dawidd6/action-send-mail@v3
  with:
    # ... other fields ...
    subject: "🚀 NEW APK READY - ${{ github.ref_name }}"
    priority: high
```
