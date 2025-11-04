# Quick Publish Guide - Copy & Paste Commands

## ⚠️ Important: You're in WSL2 (Linux bash shell)

The syntax you need depends on which script you want to use:

---

## 🚀 RECOMMENDED: Bash Script (Easiest in WSL2)

```bash
# 1. Set your API key (replace with your actual key from nuget.org)
export NUGET_API_KEY="your-actual-api-key-here"

# 2. Run the publish script
./scripts/publish-packages.sh
```

**That's it!** The bash script will handle everything.

---

## 🔄 Alternative: PowerShell Script from WSL2

If you prefer the PowerShell script features (dry-run, etc.):

```bash
# Option A: Pass API key as parameter (no environment variable needed)
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "your-key-here"

# Option B: Dry run first to test (recommended!)
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "your-key-here" -DryRun

# Option C: Real publish after dry run succeeds
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "your-key-here"
```

---

## 📝 Step-by-Step First Time

### Step 1: Get Your NuGet API Key

1. Go to: https://www.nuget.org/account/apikeys
2. Click **"Create"**
3. Settings:
   - Name: `DotCompute-Publishing`
   - Glob Pattern: `DotCompute.*`
   - Select: `Push new packages and package versions`
4. Click **"Create"**
5. **COPY THE KEY** (you won't see it again!)

### Step 2: Test with Dry Run (Recommended)

```bash
# Test without actually publishing
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "YOUR_KEY" -DryRun
```

If you see:
```
Successfully published: 13
Failed: 0
Publication completed successfully!
```

You're ready for the real thing!

### Step 3: Publish for Real

```bash
# Using bash script (simpler):
export NUGET_API_KEY="YOUR_KEY"
./scripts/publish-packages.sh

# OR using PowerShell script:
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "YOUR_KEY"
```

The script will:
1. Find all 13 packages
2. Show you what will be published
3. Ask for confirmation (`YES`)
4. Publish each package
5. Show summary

---

## ❌ Common Errors and Fixes

### Error: `:NUGET_API_KEY: command not found`

**Problem:** You used PowerShell syntax (`$env:`) in bash shell

**Fix:** Use bash syntax:
```bash
export NUGET_API_KEY="your-key"
```

### Error: `command not found: ./scripts/publish-packages.sh`

**Problem:** Script not executable

**Fix:**
```bash
chmod +x scripts/publish-packages.sh
./scripts/publish-packages.sh
```

### Error: `Unauthorized` or `403`

**Problem:** API key is wrong or expired

**Fix:**
1. Get a new API key from nuget.org
2. Make sure it has `Push` permissions
3. Use the new key

### Error: `Package already exists`

**Problem:** Package version is already published

**Fix:** This is normal with `--skip-duplicate`. If you need to update:
1. Change version number (e.g., 0.2.1-alpha)
2. Rebuild and re-sign packages
3. Publish again

---

## 🎯 One-Liner Commands (Copy Exactly As-Is)

**Dry run test:**
```bash
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -DryRun -ApiKey "paste-key-here"
```

**Real publish (bash):**
```bash
export NUGET_API_KEY="paste-key-here" && ./scripts/publish-packages.sh
```

**Real publish (PowerShell):**
```bash
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "paste-key-here"
```

---

## ✅ After Publishing

1. Wait 5-15 minutes for packages to appear
2. Check: https://www.nuget.org/profiles/YOUR_USERNAME
3. Search: https://www.nuget.org/packages?q=DotCompute
4. Test install: `dotnet add package DotCompute.Core --version 0.2.0-alpha`

---

## 🔐 Security Tips

- ✅ DO: Use environment variables for API keys
- ✅ DO: Test with dry run first
- ✅ DO: Keep API keys secret
- ❌ DON'T: Commit API keys to git
- ❌ DON'T: Share API keys in public channels
- ❌ DON'T: Use API keys in scripts that get committed

---

**Ready?** Just copy one of these:

```bash
# Safest way (dry run first):
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "YOUR_KEY" -DryRun
# Then if good:
powershell.exe -ExecutionPolicy Bypass -File scripts/publish-packages.ps1 -ApiKey "YOUR_KEY"
```

Good luck! 🚀
