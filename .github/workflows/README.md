# GitHub Actions Workflows

This directory contains the CI/CD pipelines for the DotCompute project.

## Workflows

### 🚀 CI/CD Pipeline (`ci.yml`)
**Triggers:** Push to main/develop, Pull requests, Manual dispatch, Version tags

**Jobs:**
- **Build & Test**: Multi-platform build and testing (Linux, Windows, macOS)
- **Hardware Tests**: Optional GPU/CUDA tests (manual or via commit message `[run-hardware-tests]`)
- **Code Quality**: Static analysis and code coverage reporting
- **Package Creation**: NuGet package generation for releases
- **Release**: GitHub release creation with packages attached

**Features:**
- .NET 9.0 support
- Cross-platform testing
- Code coverage with Codecov integration
- Automated versioning
- **Manual NuGet publishing** (packages created but not auto-published)
- Artifact retention

**Note:** NuGet publishing requires manual action. Packages are created and attached to releases, but must be published manually using:
```bash
dotnet nuget push "*.nupkg" --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

### 🧪 Fast Tests (`tests.yml`)
**Triggers:** Push to main/develop, Pull requests, Manual dispatch

**Jobs:**
- **Unit Tests**: Quick feedback with Debug and Release builds
- **Test Summary**: Aggregated test results

**Purpose:** Provides fast test feedback for PRs without full CI/CD overhead.

### 📊 Test Coverage & Quality Gates (`test-coverage.yml`)
**Triggers:** Push, Pull requests

**Jobs:**
- **Test Coverage**: Runs on macOS for Metal backend testing
- **Coverage Reports**: HTML reports with badges and historical tracking
- **Quality Gates**: Enforces 80% coverage threshold
- **Performance Regression**: Benchmark execution and comparison
- **PR Comments**: Adds coverage reports to pull requests

### 🔒 Security (`security.yml`)
**Triggers:** Push to main, Pull requests, Weekly schedule

**Jobs:**
- **CodeQL Analysis**: Security and code quality scanning
- **Dependency Check**: OWASP vulnerability scanning for dependencies

**Features:**
- Automated security scanning
- SARIF report generation
- Weekly scheduled scans
- Security alerts integration

### 🌙 Nightly Full Test Suite (`nightly.yml`)
**Triggers:** Nightly at 2 AM UTC, Manual dispatch

**Jobs:**
- **Complete Testing**: Runs ALL tests including hardware tests
- **Detailed Reports**: Coverage reports with historical tracking
- **Failure Notification**: Creates GitHub issues for test failures

**Requirements:** Self-hosted runners with GPU support

## Configuration

### Environment Variables
- `DOTNET_VERSION`: .NET SDK version (currently 9.0.x)
- `CI`: Set to true for CI environment detection

### Secrets Required
- `CODECOV_TOKEN`: (Optional) For Codecov integration

### Removed Configuration
- ~~`NUGET_API_KEY`~~: No longer used - NuGet publishing is manual
- ~~`PUBLISH_TO_NUGET`~~: Removed - automated publishing disabled

## Versioning Strategy

- **Tagged releases** (v*): Use tag version
- **Main branch**: 0.1.0-alpha.{build_number}
- **Develop branch**: 0.1.0-dev.{build_number}

## Artifacts

All workflows produce artifacts that are retained for:
- Test results: 7 days
- NuGet packages: 30 days
- Security reports: 7 days

## Status Badges

Add these to your README:

```markdown
[![CI/CD Pipeline](https://github.com/mivertowski/DotCompute/actions/workflows/ci.yml/badge.svg)](https://github.com/mivertowski/DotCompute/actions/workflows/ci.yml)
[![Security](https://github.com/mivertowski/DotCompute/actions/workflows/security.yml/badge.svg)](https://github.com/mivertowski/DotCompute/actions/workflows/security.yml)
```

## Alpha Release Philosophy

For the alpha release phase, workflows are intentionally simplified:

**What Runs Automatically:**
- ✅ Unit tests on all PRs and pushes
- ✅ Code coverage tracking
- ✅ Security scanning
- ✅ Package creation for releases

**What Requires Manual Action:**
- 🔐 NuGet package publishing to NuGet.org
- 🔧 Hardware-dependent tests (unless explicitly requested via commit message or manual trigger)
- 🌙 Full nightly test suite (requires self-hosted GPU runners)

**Rationale:**
- Alpha releases need careful control over publishing
- Manual publishing allows for final verification before public release
- Automated testing ensures quality without automatic deployment
- Clear separation between package creation and publishing

## Running Workflows Manually

### Trigger Hardware Tests
Add `[run-hardware-tests]` to your commit message, or use GitHub Actions UI to run manually.

### Create a Release
1. Create and push a version tag:
   ```bash
   git tag v0.1.0-alpha && git push origin v0.1.0-alpha
   ```
2. CI automatically creates a GitHub release with NuGet packages attached
3. Download packages and manually publish to NuGet.org when ready:
   ```bash
   dotnet nuget push "*.nupkg" --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
   ```

### Run Full Test Suite
Use GitHub Actions UI to trigger `nightly.yml` manually (requires self-hosted GPU runner).

## Maintenance

The workflows are designed to be low-maintenance with:
- Automatic dependency updates via Dependabot
- Self-contained job definitions
- Clear separation of concerns
- Minimal external dependencies

## Simplification History

**2025-11-03:** Simplified workflows for alpha release
- Removed automatic NuGet publishing from release workflow
- Consolidated redundant parts of test workflows
- Added clear manual publishing instructions
- Maintained all quality and security workflows
- Updated documentation to reflect alpha release approach