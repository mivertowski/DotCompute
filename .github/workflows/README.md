# GitHub Actions Workflows

This directory contains the CI/CD pipelines for the DotCompute project.

## Workflows

### 1. CI/CD Pipeline (`ci.yml`)
**Triggers:** Push to main/develop, Pull requests, Manual dispatch, Version tags

**Jobs:**
- **Build & Test**: Multi-platform build and testing (Linux, Windows, macOS)
- **Code Quality**: Static analysis and code coverage reporting
- **Package Creation**: NuGet package generation for releases
- **Release**: Automated GitHub release creation and NuGet publishing

**Features:**
- .NET 9.0 support
- Cross-platform testing
- Code coverage with Codecov integration
- Automated versioning
- NuGet package publishing
- Artifact retention

### 2. Security (`security.yml`)
**Triggers:** Push to main, Pull requests, Weekly schedule

**Jobs:**
- **CodeQL Analysis**: Security and quality scanning
- **Dependency Check**: Vulnerability scanning for dependencies

**Features:**
- Automated security scanning
- SARIF report generation
- Weekly scheduled scans
- Security alerts integration

## Configuration

### Environment Variables
- `DOTNET_VERSION`: .NET SDK version (currently 9.0.x)
- `CI`: Set to true for CI environment detection

### Secrets Required
- `CODECOV_TOKEN`: (Optional) For Codecov integration
- `NUGET_API_KEY`: (Optional) For NuGet.org publishing

### Repository Variables
- `PUBLISH_TO_NUGET`: Set to 'true' to enable NuGet publishing

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

## Maintenance

The workflows are designed to be low-maintenance with:
- Automatic dependency updates via Dependabot
- Self-contained job definitions
- Clear separation of concerns
- Minimal external dependencies