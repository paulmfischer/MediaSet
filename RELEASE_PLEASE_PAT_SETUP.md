# Release Please PAT Setup

## Problem

GitHub Actions workflows triggered by events created using the default `GITHUB_TOKEN` **cannot trigger other workflows**. This is a security feature to prevent infinite workflow loops.

When `release-please` creates a release using `GITHUB_TOKEN`:
- ✅ The release is created successfully
- ❌ The `release` event does NOT trigger docker-api.yml or docker-ui.yml workflows

## Solution

Configure `release-please` to use a Personal Access Token (PAT) or GitHub App token instead of the default `GITHUB_TOKEN`.

## Setup Steps

### Option 1: Personal Access Token (PAT) - Recommended for personal repos

1. **Create a Fine-Grained Personal Access Token:**
   - Go to GitHub Settings → Developer settings → Personal access tokens → Fine-grained tokens
   - Click "Generate new token"
   - Configure:
     - **Name**: `Release Please Token`
     - **Expiration**: Choose appropriate duration (e.g., 90 days, 1 year, or no expiration)
     - **Repository access**: Select "Only select repositories" → Choose `MediaSet`
     - **Repository permissions**:
       - `Contents`: Read and write (for creating releases and tags)
       - `Pull requests`: Read and write (for creating release PRs)
       - `Metadata`: Read-only (required)
   - Click "Generate token"
   - **IMPORTANT**: Copy the token immediately (you won't be able to see it again)

2. **Add the token as a repository secret:**
   - Go to your MediaSet repository on GitHub
   - Settings → Secrets and variables → Actions
   - Click "New repository secret"
   - **Name**: `RELEASE_PLEASE_TOKEN`
   - **Value**: Paste the PAT you created
   - Click "Add secret"

3. **Verify the workflows:**
   - The release-please.yml workflow is already configured to use this token
   - When the next release-please PR is merged:
     - It will create a release using the PAT
     - The release event WILL trigger docker-api.yml and docker-ui.yml
     - Docker images will be built and pushed automatically

### Option 2: GitHub App (Recommended for organization repos)

If you prefer using a GitHub App instead of a PAT:

1. Create a GitHub App with appropriate permissions
2. Install the app on your repository
3. Use actions like `tibdex/github-app-token@v1` to generate a token
4. Update the release-please workflow to use the app token

## Testing

After setting up the PAT:

1. Make a commit to a feature branch and merge to main
2. Release-please will create/update a PR
3. Merge the release-please PR
4. Check Actions → you should see:
   - Release Please workflow completes
   - Docker - API workflow triggers
   - Docker - UI workflow triggers

## Fallback Behavior

The workflow is configured with a fallback:
```yaml
token: ${{ secrets.RELEASE_PLEASE_TOKEN || secrets.GITHUB_TOKEN }}
```

- If `RELEASE_PLEASE_TOKEN` exists: Uses it (workflows WILL trigger)
- If not: Falls back to `GITHUB_TOKEN` (release created but workflows WON'T trigger)

## Why This Is Necessary

From GitHub's documentation:
> When you use the repository's GITHUB_TOKEN to perform tasks, events triggered by the GITHUB_TOKEN will not create a new workflow run. This prevents you from accidentally creating recursive workflow runs.

Sources:
- https://docs.github.com/en/actions/security-guides/automatic-token-authentication#using-the-github_token-in-a-workflow
- https://github.com/googleapis/release-please/blob/main/docs/cli.md#github-token
