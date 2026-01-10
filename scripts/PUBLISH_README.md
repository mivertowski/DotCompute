# DotCompute Package Publishing Guide

This guide explains how to publish the DotCompute v0.5.3 packages to NuGet.org.

## Prerequisites

✅ **Before publishing, ensure:**
1. All packages are built in Release mode (completed)
2. All packages are signed with your Certum certificate (completed)
3. You have a NuGet.org account and API key
4. All packages pass verification

## Getting Your NuGet API Key

1. **Create/Login to NuGet.org Account**
   - Go to https://www.nuget.org/
   - Sign in or create an account

2. **Generate API Key**
   - Go to https://www.nuget.org/account/apikeys
   - Click "Create"
   - Key Name: `DotCompute-Publishing`
   - Glob Pattern: `DotCompute.*`
   - Select Scopes: `Push` and `Push new packages and package versions`
   - Expiration: Choose appropriate duration (recommended: 365 days)
   - Click "Create"
   - **Copy the API key immediately** (it won't be shown again)

## Option 1: Using PowerShell Script (Recommended for Windows/WSL2)

### Basic Usage

```powershell
# Set your API key as environment variable (recommended)
$env:NUGET_API_KEY = "your-api-key-here"

# Run the publish script
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1
```

### Advanced Usage

```powershell
# Dry run (test without publishing)
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -DryRun

# Publish with inline API key
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "your-api-key"

# Skip symbol packages
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -NoSymbols

# Custom NuGet source (private feed)
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -Source "https://your-feed.com/v3/index.json"
```

### Parameters

- `-ApiKey <string>` - NuGet API key (overrides environment variable)
- `-Source <string>` - NuGet source URL (default: nuget.org)
- `-SkipDuplicate` - Skip packages that already exist (default: true)
- `-NoSymbols` - Don't publish symbol packages (.snupkg)
- `-DryRun` - Test run without actually publishing

## Option 2: Using Bash Script (Linux/macOS)

### Basic Usage

```bash
# Set your API key as environment variable (recommended)
export NUGET_API_KEY="your-api-key-here"

# Run the publish script
./scripts/publish-packages.sh
```

### Environment Variables

```bash
# Set API key (recommended method)
export NUGET_API_KEY="your-api-key-here"

# Alternative: Store in dotnet configuration (one-time)
dotnet nuget push --help  # See instructions
```

## Option 3: Manual Publishing (Individual Packages)

### Publish Single Package

```bash
# With API key
dotnet nuget push nupkgs/DotCompute.Core.0.5.3.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY \
  --skip-duplicate

# Using stored API key
dotnet nuget push nupkgs/DotCompute.Core.0.5.3.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

### Publish All Packages

```bash
# Using bash
for pkg in nupkgs/*.nupkg nupkgs/*.snupkg; do
  dotnet nuget push "$pkg" \
    --source https://api.nuget.org/v3/index.json \
    --api-key $NUGET_API_KEY \
    --skip-duplicate
done
```

```powershell
# Using PowerShell
Get-ChildItem nupkgs/*.nupkg,nupkgs/*.snupkg | ForEach-Object {
  dotnet nuget push $_.FullName `
    --source https://api.nuget.org/v3/index.json `
    --api-key $env:NUGET_API_KEY `
    --skip-duplicate
}
```

## Verification After Publishing

### Check Package Status

1. **View Your Packages**
   - Go to https://www.nuget.org/profiles/YOUR_USERNAME
   - Verify all 12 packages appear

2. **Search for Packages**
   - Go to https://www.nuget.org/packages
   - Search for "DotCompute"
   - Note: It may take 5-15 minutes for packages to appear in search

3. **Verify Package Contents**
   ```bash
   # Download and inspect a package
   dotnet add package DotCompute.Core --version 0.5.3
   ```

### Verify Package Signatures

```bash
# Verify signature on published package
dotnet nuget verify DotCompute.Core --all
```

## Package Publication Checklist

Before running the publish script:

- [ ] All 12 packages built successfully (0 errors, 0 warnings)
- [ ] All packages signed with Certum certificate
- [ ] Version number is correct (0.5.3)
- [ ] README files are up to date in all packages
- [ ] NuGet API key is ready
- [ ] You understand that published packages cannot be deleted

After publishing:

- [ ] Verify all packages appear on NuGet.org
- [ ] Check package metadata and descriptions
- [ ] Test installing packages in a sample project
- [ ] Update GitHub releases with package links
- [ ] Announce the release

## Troubleshooting

### Error: "Package already exists"

This is normal if re-publishing. The script uses `--skip-duplicate` by default.

**Solution:** Use a new version number for updated packages.

### Error: "Unauthorized" or "403 Forbidden"

Your API key is invalid or expired.

**Solution:**
1. Generate a new API key on NuGet.org
2. Ensure the key has `Push` permissions
3. Check the glob pattern includes `DotCompute.*`

### Error: "Package signature is invalid"

The package signature couldn't be verified.

**Solution:**
1. Re-sign the packages using `scripts/sign-packages.ps1`
2. Verify signatures with `dotnet nuget verify`

### Error: "Package validation failed"

The package doesn't meet NuGet.org requirements.

**Solution:**
1. Ensure all packages have proper metadata
2. Check that README files are included
3. Verify license information is correct

## Publishing to Private Feeds

### Azure Artifacts

```bash
# Add feed
dotnet nuget add source "https://pkgs.dev.azure.com/YOUR_ORG/_packaging/YOUR_FEED/nuget/v3/index.json" \
  --name "AzureArtifacts" \
  --username "anything" \
  --password "YOUR_PAT"

# Publish
./scripts/publish-packages.sh
# Or specify source:
powershell.exe -File scripts/publish-packages.ps1 -Source "AzureArtifacts"
```

### GitHub Packages

```bash
# Add feed
dotnet nuget add source "https://nuget.pkg.github.com/YOUR_USERNAME/index.json" \
  --name "GitHubPackages" \
  --username "YOUR_USERNAME" \
  --password "YOUR_GITHUB_TOKEN"

# Publish
export NUGET_API_KEY="YOUR_GITHUB_TOKEN"
./scripts/publish-packages.sh
```

## Security Best Practices

1. **Never commit API keys** to version control
2. **Use environment variables** for sensitive data
3. **Rotate API keys regularly** (every 6-12 months)
4. **Use scoped API keys** with minimal permissions
5. **Review published packages** immediately after publication

## Package Maintenance

### Unlisting a Package

If you need to hide a package (not delete):

```bash
dotnet nuget delete DotCompute.Core 0.5.3 \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY \
  --non-interactive
```

Note: This unlists the package but doesn't delete it. Existing consumers can still use it.

### Publishing Updates

For bug fixes or updates:

1. Increment version number (e.g., 0.2.1-alpha)
2. Rebuild and re-sign packages
3. Run publish script with new packages

## Support

For issues with:
- **Package signing:** Review `scripts/sign-packages.ps1` and certificate setup
- **Package building:** Check build logs and `Directory.Build.props`
- **NuGet.org issues:** Visit https://www.nuget.org/policies/Contact
- **DotCompute project:** Open an issue at https://github.com/mivertowski/DotCompute

---

**Ready to publish?** Run the dry-run first to verify everything:

```powershell
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -DryRun
```

Then publish for real:

```powershell
$env:NUGET_API_KEY = "your-api-key-here"
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1
```

Good luck! 🚀
