# DotCompute CI/CD Pipeline

A comprehensive, production-ready CI/CD pipeline for DotCompute alpha releases with automated testing, quality gates, and multi-platform deployment.

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- Git
- GitHub CLI (optional, for releases)

### Setup

```bash
# 1. Clone repository
git clone https://github.com/your-org/dotcompute.git
cd dotcompute

# 2. Setup environment
./scripts/ci/setup-environment.sh

# 3. Run build
./.github/scripts/build.sh Release

# 4. Run quality checks
./.github/scripts/quality-check.sh
```

## 📋 Features

### ✅ **Cross-Platform Builds**
- Ubuntu, Windows, macOS support
- Debug and Release configurations
- .NET 9.0 with AOT compatibility

### ✅ **Comprehensive Testing**
- Unit and integration tests
- Code coverage (≥85% threshold)
- Performance regression detection
- Hardware-specific test isolation

### ✅ **Quality Assurance**
- Automated code formatting
- Static analysis with CodeQL
- Dependency vulnerability scanning
- Package validation

### ✅ **Automated Releases**
- Semantic versioning with GitVersion
- Alpha/Beta/RC release support
- NuGet package publishing
- GitHub releases with changelogs

### ✅ **Security & Compliance**
- SAST with CodeQL
- Dependency scanning
- License compliance
- Supply chain security

## 🏗️ Pipeline Architecture

```
Push/PR → Build Matrix → Quality Gates → Security Scan → Package → Release
    ↓           ↓             ↓             ↓           ↓        ↓
 Trigger    Build/Test    Coverage      CodeQL      NuGet   GitHub
            3 OS × 2      Report       Analysis    Publish  Release
```

## 📊 Quality Gates

| Gate | Requirement | Threshold |
|------|-------------|-----------|
| **Build** | Zero errors | 100% |
| **Tests** | All pass | 100% |
| **Coverage** | Line coverage | ≥85% |
| **Security** | No high-severity issues | Zero |
| **Format** | Code style compliance | Warning only |

## 🔧 Configuration

### Repository Secrets

Configure these in GitHub Settings → Secrets:

| Secret | Description | Required |
|--------|-------------|----------|
| `NUGET_API_KEY` | NuGet.org publishing key | Yes |
| `CODECOV_TOKEN` | Codecov reporting token | Optional |

### Environment Setup

```bash
# Development
export DOTNET_ENVIRONMENT=Development

# CI/CD  
export CI=true
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
export DOTNET_NOLOGO=true
```

## 📦 Release Process

### Alpha Release

```bash
# Create alpha tag
git tag v0.1.0-alpha.1
git push origin v0.1.0-alpha.1

# Or use release script
./.github/scripts/release.sh 0.1.0-alpha.1 true
```

### Stable Release

```bash
# Create stable tag
git tag v1.0.0
git push origin v1.0.0

# Or use release script
./.github/scripts/release.sh 1.0.0 false
```

## 📝 Workflow Files

### Main Workflow
- **File**: `.github/workflows/ci.yml`
- **Triggers**: Push, PR, Manual dispatch
- **Features**: Full CI/CD pipeline

### Supporting Scripts
- **Build**: `.github/scripts/build.sh`
- **Quality**: `.github/scripts/quality-check.sh`
- **Release**: `.github/scripts/release.sh`
- **Coverage**: `scripts/ci/coverage-report.sh`

## 🧪 Local Development

### Build Locally

```bash
# Full build with tests
./.github/scripts/build.sh Release

# Build without tests (faster)
./.github/scripts/build.sh Release true

# Debug build
./.github/scripts/build.sh Debug
```

### Quality Checks

```bash
# Run all quality checks
./.github/scripts/quality-check.sh

# Run with automatic fixes
./.github/scripts/quality-check.sh fix
```

### Coverage Reports

```bash
# Generate HTML coverage report
./scripts/ci/coverage-report.sh Html

# Multiple formats
./scripts/ci/coverage-report.sh Html,Cobertura,JsonSummary
```

## 📊 Monitoring

### Pipeline Metrics

- **Build Duration**: ~15 minutes
- **Test Execution**: ~5 minutes  
- **Coverage Generation**: ~2 minutes
- **Total Pipeline**: ~25 minutes

### Quality Metrics

- **Code Coverage**: Target ≥85%
- **Test Success Rate**: 100%
- **Security Issues**: Zero tolerance
- **Package Quality**: 100% validation

## 🔍 Troubleshooting

### Common Issues

#### Build Failures

```bash
# Check logs
cat artifacts/logs/build.log

# Clean rebuild
dotnet clean && dotnet restore && dotnet build
```

#### Test Failures

```bash
# Run specific test
dotnet test --filter "TestClassName"

# Detailed output
dotnet test --logger:"console;verbosity=detailed"
```

#### Coverage Issues

```bash
# Verify coverage files
find artifacts/coverage -name "*.xml"

# Regenerate report
rm -rf artifacts/coverage-reports/
./scripts/ci/coverage-report.sh Html
```

### Support Resources

- **Documentation**: [Pipeline Overview](pipeline-overview.md)
- **Deployment Guide**: [Deployment Guide](deployment-guide.md)
- **Issues**: [GitHub Issues](../../issues)
- **Discussions**: [GitHub Discussions](../../discussions)

## 🔒 Security

### Static Analysis
- **CodeQL**: Automated security scanning
- **Dependencies**: Vulnerability detection
- **Secrets**: No hardcoded credentials

### Supply Chain
- **Package Signing**: Planned for stable releases
- **Reproducible Builds**: Deterministic compilation
- **Source Linking**: Full source traceability

## 📈 Performance

### Optimization Features
- **Parallel Builds**: Multi-core utilization
- **Incremental Builds**: Changed files only
- **Cached Dependencies**: NuGet package caching
- **AOT Ready**: Native compilation support

### Benchmarking
- **Baseline**: Performance regression detection
- **Profiling**: Memory and CPU analysis
- **Reporting**: Automated performance reports

## 🎯 Best Practices

### Code Quality
- ✅ Follow .NET coding standards
- ✅ Write comprehensive tests
- ✅ Maintain documentation
- ✅ Use semantic versioning

### CI/CD Practices
- ✅ Fail fast on errors
- ✅ Comprehensive logging
- ✅ Immutable artifacts
- ✅ Automated rollbacks

### Security Practices
- ✅ Least privilege access
- ✅ Secret management
- ✅ Dependency updates
- ✅ Vulnerability scanning

## 📚 Additional Resources

### Documentation
- [Pipeline Architecture](pipeline-overview.md)
- [Deployment Guide](deployment-guide.md)
- [Contributing Guide](../CONTRIBUTING.md)
- [Testing Strategy](../TESTING_STRATEGY.md)

### External Links
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET Build Tools](https://docs.microsoft.com/en-us/dotnet/core/tools/)
- [NuGet Package Management](https://docs.microsoft.com/en-us/nuget/)
- [GitVersion Documentation](https://gitversion.net/)

---

## 📄 License

This CI/CD pipeline configuration is part of the DotCompute project and is licensed under the MIT License. See [LICENSE](../../LICENSE) for details.

---

**Need Help?** 
- 📧 Create an [issue](../../issues/new)
- 💬 Start a [discussion](../../discussions)
- 📖 Check the [wiki](../../wiki)