# GitHub Pages Setup Instructions

This document explains how to set up GitHub Pages for the DotCompute documentation.

## Automated Deployment

The repository includes a GitHub Actions workflow (`.github/workflows/deploy-docs.yml`) that automatically builds and deploys the documentation to GitHub Pages whenever changes are pushed to the main branch.

## Repository Configuration

To enable GitHub Pages for this repository, follow these steps:

### 1. Enable GitHub Pages

1. Go to your repository on GitHub
2. Click **Settings** → **Pages** (in the left sidebar)
3. Under **Build and deployment**:
   - **Source**: Select "GitHub Actions"
   - The workflow will handle everything automatically

### 2. Verify Workflow Permissions

1. Go to **Settings** → **Actions** → **General**
2. Scroll to **Workflow permissions**
3. Ensure **Read and write permissions** is selected
4. Check **Allow GitHub Actions to create and approve pull requests**
5. Click **Save**

### 3. Trigger Initial Deployment

After configuration:

**Option A - Push to main branch:**
```bash
git add .github/workflows/deploy-docs.yml .nojekyll docfx.json
git commit -m "docs: Add GitHub Pages deployment workflow"
git push origin main
```

**Option B - Manual trigger:**
1. Go to **Actions** tab
2. Select **Deploy Documentation** workflow
3. Click **Run workflow** → **Run workflow**

### 4. Verify Deployment

1. Go to **Actions** tab and watch the workflow run
2. Once complete, your documentation will be available at:
   ```
   https://<username>.github.io/DotCompute/
   ```

   For example: `https://mivertowski.github.io/DotCompute/`

## Workflow Details

The deployment workflow performs these steps:

1. **Checkout** - Clones the repository
2. **Setup .NET** - Installs .NET 9.0 SDK
3. **Install DocFX** - Installs DocFX as a global tool
4. **Restore Dependencies** - Restores NuGet packages
5. **Build Solution** - Compiles the DotCompute solution
6. **Generate Metadata** - Extracts API documentation from XML comments
7. **Build Docs Site** - Generates the static website
8. **Deploy** - Uploads to GitHub Pages

## Automatic Updates

The workflow is triggered automatically on:
- Pushes to `main` branch that modify:
  - Source code (`src/**`)
  - Documentation (`docs/**`)
  - DocFX configuration (`docfx.json`)
  - README or CHANGELOG files
- Manual workflow dispatch

## File Structure

```
DotCompute/
├── .github/workflows/
│   └── deploy-docs.yml          # GitHub Actions workflow
├── .nojekyll                     # Disables Jekyll processing
├── docfx.json                    # DocFX configuration
├── docs/                         # Documentation source
│   ├── index.md                  # Documentation homepage
│   ├── toc.yml                   # Table of contents
│   └── articles/                 # Article documentation
├── api/                          # Generated API metadata (gitignored)
└── _site/                        # Generated website (gitignored)
```

## Build Artifacts

The following directories are generated during the build and should **not** be committed:
- `api/` - Intermediate YAML metadata
- `_site/` - Final generated website

These are excluded in `.gitignore`.

## Troubleshooting

### Build Fails

**Issue**: DocFX build fails with compilation errors

**Solution**: Ensure all projects build successfully:
```bash
dotnet build DotCompute.sln --configuration Release
```

### Pages Not Updating

**Issue**: Documentation doesn't reflect latest changes

**Solution**:
1. Check the Actions tab for workflow status
2. Verify the workflow completed successfully
3. Clear browser cache and reload
4. Check that GitHub Pages source is set to "GitHub Actions"

### Missing API Documentation

**Issue**: Some APIs aren't appearing in documentation

**Solution**:
1. Ensure XML documentation is enabled in `.csproj`:
   ```xml
   <GenerateDocumentationFile>true</GenerateDocumentationFile>
   ```
2. Check that projects are included in `docfx.json` metadata sources
3. Verify public APIs have XML documentation comments

### 404 Errors

**Issue**: Documentation returns 404 errors

**Solution**:
1. Verify `.nojekyll` file exists in repository root
2. Check GitHub Pages is configured to use "GitHub Actions" source
3. Ensure workflow has proper permissions (read contents, write pages)

## Manual Local Build

To build documentation locally:

```bash
# Install DocFX globally
dotnet tool install -g docfx

# Build the solution
dotnet build DotCompute.sln --configuration Release

# Generate documentation
docfx docfx.json

# Serve locally (optional)
docfx serve _site
```

Then open `http://localhost:8080` in your browser.

## Custom Domain (Optional)

To use a custom domain:

1. Add a `CNAME` file to the repository root with your domain
2. Configure DNS records with your domain provider
3. In GitHub Settings → Pages, add your custom domain

## Documentation Updates

The documentation automatically rebuilds when you:
- Add/update XML documentation comments in source code
- Modify markdown files in `docs/` directory
- Update `docfx.json` configuration
- Change README.md or CHANGELOG.md

No manual intervention required - just push to main!

## Support

For issues with GitHub Pages deployment:
- Check GitHub Actions logs in the Actions tab
- Review [GitHub Pages documentation](https://docs.github.com/en/pages)
- Review [DocFX documentation](https://dotnet.github.io/docfx/)

---

**Status**: The documentation workflow is production-ready and fully automated. After initial setup, it requires zero maintenance.
