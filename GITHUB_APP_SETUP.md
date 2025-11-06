# GitHub App Setup for Verified Release Commits

This guide explains how to create a GitHub App for the release-please workflow to ensure commits are automatically verified and can trigger other workflows.

## Why GitHub App instead of PAT?

- ✅ **Automatically verified** - Commits made by GitHub Apps are verified by GitHub
- ✅ **Triggers workflows** - Unlike `GITHUB_TOKEN`, can trigger other workflows (like Docker builds)
- ✅ **More secure** - Fine-grained permissions, no user impersonation, full audit trail
- ✅ **No third-party actions** - Uses official GitHub action `actions/create-github-app-token`
- ✅ **Revocable** - Can be revoked without affecting your personal account

## Steps to Create a GitHub App

### 1. Create a New GitHub App

1. Go to your account settings: `https://github.com/settings/apps`
2. Click **"New GitHub App"**
3. Fill in the following:
   - **GitHub App name**: `MediaSet Release Please` (or any unique name)
   - **Homepage URL**: `https://github.com/paulmfischer/MediaSet`
   - **Webhook**: Uncheck "Active" (not needed for this use case)
   - **Permissions** → Repository permissions:
     - **Contents**: Read and write
     - **Pull requests**: Read and write
     - **Metadata**: Read-only (automatically selected)
   - **Where can this GitHub App be installed?**: Select "Only on this account"
4. Click **"Create GitHub App"**

### 2. Note the App ID

After creating the app, you'll see the App ID on the app's settings page. **Save this value** - you'll need it for the GitHub secret `RELEASE_APP_ID`.

### 3. Generate a Private Key

1. On the app's settings page, scroll down to **"Private keys"**
2. Click **"Generate a private key"**
3. A `.pem` file will be downloaded to your computer
4. Open this file in a text editor and **copy the entire contents** (including the `-----BEGIN RSA PRIVATE KEY-----` and `-----END RSA PRIVATE KEY-----` lines)

### 4. Install the App on Your Repository

1. On the app's settings page, click **"Install App"** in the left sidebar
2. Click **"Install"** next to your username
3. Select **"Only select repositories"** and choose `paulmfischer/MediaSet`
4. Click **"Install"**

### 5. Add GitHub Secrets

Add the following secrets to your repository (Settings → Secrets and variables → Actions):

1. **`RELEASE_APP_ID`**: The App ID from step 2
2. **`RELEASE_APP_PRIVATE_KEY`**: The entire contents of the `.pem` file from step 3

## Verification

After setting this up:

1. Push a commit to the `main` branch that follows Conventional Commits
2. Wait for release-please to create or update a release PR
3. When you merge the release PR, the release commit should:
   - Be verified with a green "Verified" badge
   - Show the GitHub App as the committer
   - Trigger the Docker workflows

## Removing the Old PAT (Optional)

Once you verify the GitHub App is working:

1. You can remove the `RELEASE_PLEASE_TOKEN` secret from your repository
2. You can revoke the PAT from your GitHub settings if it's no longer needed

## Troubleshooting

### "Resource not accessible by integration" error

Check that:
- The GitHub App has the correct permissions (Contents: Read and write, Pull requests: Read and write)
- The App is installed on the repository

### Commits still not verified

- GitHub Apps commits are **automatically verified** by GitHub - no GPG setup needed
- Check that the commit shows the GitHub App name as the author
- If it shows your personal account, the token might not be correctly configured

### Docker workflows not triggering

- Ensure the GitHub App has **Contents: Read and write** permission
- Verify the App is installed on the repository
- Check that the tag creation is successful in the Actions logs

## Security Best Practices

- **Store the private key securely** - Keep a backup in a secure location
- **Rotate keys periodically** - Generate new private keys annually
- **Monitor app activity** - Check the app's activity logs in Settings → GitHub Apps
- **Limit permissions** - Only grant the minimum required permissions
- **Audit access** - Review who has access to the repository secrets

## References

- [GitHub Apps Documentation](https://docs.github.com/en/apps)
- [Creating a GitHub App](https://docs.github.com/en/apps/creating-github-apps/about-creating-github-apps/about-creating-github-apps)
- [actions/create-github-app-token](https://github.com/actions/create-github-app-token)
- [GitHub Apps vs PATs](https://docs.github.com/en/apps/creating-github-apps/about-creating-github-apps/deciding-when-to-build-a-github-app)
