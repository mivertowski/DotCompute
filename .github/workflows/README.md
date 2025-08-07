# DotCompute CI/CD Workflows

This directory contains all GitHub Actions workflows for the DotCompute project.

## Workflows Overview

### 🚀 Main CI/CD Pipeline (`main.yml`)
- **Triggers**: Push to main/develop, PRs, manual dispatch
- **Purpose**: Primary build, test, and release pipeline
- **Features**:
  - Multi-platform builds (Linux, Windows, macOS)
  - Comprehensive testing with coverage
  - Security scanning
  - NuGet package creation
  - Release automation
  - Performance benchmarks

### 🌙 Nightly Build (`nightly.yml`)
- **Triggers**: Daily at 2 AM UTC, manual dispatch
- **Purpose**: Extended testing and validation
- **Features**:
  - Long-running tests
  - Stress testing
  - Performance regression detection
  - Detailed code analysis
  - Historical coverage tracking

### 📦 Release Pipeline (`release.yml`)
- **Triggers**: Version tags (v*.*.*), manual dispatch
- **Purpose**: Official release creation
- **Features**:
  - Multi-platform binary creation
  - NuGet package publishing
  - GitHub release creation
  - Package signing (when configured)
  - Release notes generation

### 🔒 Security Analysis (`codeql.yml`)
- **Triggers**: Push to main/develop, PRs, weekly schedule
- **Purpose**: Security vulnerability detection
- **Features**:
  - CodeQL analysis
  - Security and quality queries
  - SARIF result upload
  - Vulnerability tracking

### 🔧 Enhanced CI (`ci-enhanced.yml`)
- **Purpose**: Resilient CI with relaxed warnings
- **Features**:
  - Handles compilation warnings gracefully
  - GPU mock testing
  - AOT validation
  - Comprehensive reporting

## Environment Variables

### Required Secrets
- `CODECOV_TOKEN`: Token for Codecov integration
- `NUGET_API_KEY`: NuGet.org API key for package publishing
- `SIGNING_CERTIFICATE`: Code signing certificate (optional)
- `CERTIFICATE_PASSWORD`: Certificate password (optional)

### Workflow Environment Variables
```yaml
DOTNET_SKIP_FIRST_TIME_EXPERIENCE: 1
DOTNET_NOLOGO: true
CI: true
DOTNET_CLI_TELEMETRY_OPTOUT: 1
```

## Workflow Dispatch Inputs

### Main Pipeline
- `skip_tests`: Skip test execution (boolean)
- `run_gpu_tests`: Run GPU tests on self-hosted runner (boolean)
- `create_release`: Create a release (boolean)

### Release Pipeline
- `version`: Version to release (string)
- `prerelease`: Mark as pre-release (boolean)

## Test Categories

Tests are categorized for selective execution:
- `RequiresGPU`: Tests requiring GPU hardware
- `LongRunning`: Tests taking >1 minute
- `Integration`: Integration tests
- `Stress`: Stress and load tests
- `Performance`: Performance benchmarks

## Coverage Reporting

### Local Coverage Generation
```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
```

### CI Coverage
- Automatic coverage collection on all test runs
- Upload to Codecov (when token configured)
- HTML reports generated and uploaded as artifacts
- Coverage badges available

## Build Configurations

### Debug
- Full debugging symbols
- No optimizations
- Assertions enabled

### Release
- Optimizations enabled
- Trimmed symbols
- AOT-ready
- Package creation

## Platform Support

### Operating Systems
- Ubuntu (latest)
- Windows (latest)
- macOS (latest)

### .NET Versions
- .NET 9.0 (preview)

### GPU Backends (Mock Testing)
- CUDA
- OpenCL
- DirectCompute
- Metal

## Artifact Retention

- Test Results: 30 days
- Coverage Reports: 30 days
- NuGet Packages: 30 days
- Nightly Builds: 7 days
- Release Binaries: Permanent (attached to releases)

## Troubleshooting

### Common Issues

1. **Compilation Warnings as Errors**
   - Workflows use `/p:TreatWarningsAsErrors=false`
   - Style violations suppressed with NoWarn

2. **GPU Tests Failing**
   - GPU tests run in mock mode on CI
   - Real hardware testing requires self-hosted runners

3. **AOT Compilation Issues**
   - AOT validation may fail on some platforms
   - Workflows continue on error for AOT

4. **Coverage Upload Failures**
   - Ensure CODECOV_TOKEN secret is configured
   - Check Codecov integration settings

### Manual Workflow Runs

```bash
# Trigger workflow via GitHub CLI
gh workflow run main.yml

# With inputs
gh workflow run release.yml -f version=1.0.0 -f prerelease=false
```

## Monitoring

### Status Badges
Add these to your README:
```markdown
[![Main CI/CD](https://github.com/yourusername/DotCompute/actions/workflows/main.yml/badge.svg)](https://github.com/yourusername/DotCompute/actions/workflows/main.yml)
[![CodeQL](https://github.com/yourusername/DotCompute/actions/workflows/codeql.yml/badge.svg)](https://github.com/yourusername/DotCompute/actions/workflows/codeql.yml)
[![codecov](https://codecov.io/gh/yourusername/DotCompute/branch/main/graph/badge.svg)](https://codecov.io/gh/yourusername/DotCompute)
```

### Workflow Notifications
- Failed nightly builds create GitHub issues
- Release failures notify maintainers
- Security alerts via GitHub Security tab

## Contributing

When adding new workflows:
1. Follow naming convention: `purpose.yml`
2. Include comprehensive documentation
3. Add workflow_dispatch for manual testing
4. Use consistent environment variables
5. Include proper error handling
6. Upload relevant artifacts

## Phase 4 Test Coverage

The workflows are configured to test all Phase 4 features:
- ✅ GPU Backend Tests (CUDA, OpenCL, DirectCompute)
- ✅ Security Validation System
- ✅ Parallel Execution Strategies
- ✅ Linear Algebra Kernels
- ✅ NuGet Plugin System
- ✅ 16,000+ lines of test code
- ✅ ~78% code coverage target