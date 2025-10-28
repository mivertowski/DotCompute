# DotCompute Validation System

## Overview

This directory contains a comprehensive testing and validation system for the DotCompute CA1848 fix initiative. The system provides automated build validation, error tracking, test orchestration, and quality assurance.

## Quick Start

```bash
# Navigate to validation directory
cd tests/validation

# Run full validation pipeline
./build-validator.sh && ./error-tracker.sh && ./test-plan.sh

# Or for quick smoke test
./smoke-tests.sh
```

## Scripts

### 1. build-validator.sh
**Purpose**: Validates builds in dependency order

**Features**:
- Builds 26 projects in correct dependency order
- Supports parallel builds for independent projects
- Tracks warnings and errors per project
- Generates JSON reports with timestamps
- Calculates build success rate

**Usage**:
```bash
./build-validator.sh
```

**Output**:
- Console: Colored progress and summary
- File: `results/build_validation_YYYYMMDD_HHMMSS.json`

**Exit Codes**:
- `0`: All builds succeeded
- `1`: One or more builds failed

### 2. error-tracker.sh
**Purpose**: Tracks CA1848 violations and fix progress

**Features**:
- Establishes error baseline on first run
- Counts current CA1848 violations
- Compares with baseline to show progress
- Groups errors by file for prioritization
- Generates fix recommendations
- Maintains progress history

**Usage**:
```bash
./error-tracker.sh
```

**Output**:
- Console: Current status, comparison, recommendations
- Files:
  - `tracking/error_baseline.json` (first run only)
  - `tracking/error_current_YYYYMMDD_HHMMSS.json`
  - `tracking/error_progress.json` (historical)

**Exit Codes**:
- `0`: Zero CA1848 violations
- `1`: CA1848 violations present

### 3. test-plan.sh
**Purpose**: Orchestrates multi-phase test execution

**Features**:
- Runs tests in 4 phases (fast → slow)
- Detects hardware availability (GPU)
- Estimates test duration per phase
- Tracks pass/fail/skip counts
- Generates detailed test reports

**Test Phases**:
1. **Fast** (< 1 min): Abstractions, Memory
2. **Medium** (2-3 min): Core, Plugins, CPU, Basic
3. **Integration** (1-2 min): Generators
4. **Hardware** (5-10 min): Mock, CUDA (requires GPU)

**Usage**:
```bash
./test-plan.sh
```

**Output**:
- Console: Phase progress, hardware detection, summary
- File: `results/test_results_YYYYMMDD_HHMMSS.json`

**Exit Codes**:
- `0`: All tests passed (or ≥95% pass rate)
- `1`: Test failures

### 4. smoke-tests.sh
**Purpose**: Quick validation of critical functionality

**Features**:
- Tests 5 critical paths in < 2 minutes
- Core kernel execution
- CPU backend operations
- Memory management
- Source generators
- CUDA backend (if GPU available)

**Usage**:
```bash
./smoke-tests.sh
```

**Output**:
- Console: Pass/fail for each critical path

**Exit Codes**:
- `0`: All critical paths passed
- `1`: One or more critical paths failed

## Documentation

### TESTING-STRATEGY.md
Complete testing and validation strategy document covering:
- Build validation approach
- Error tracking methodology
- Test execution plan
- Quality gates
- Success criteria
- Timeline estimates
- Troubleshooting guide

**Length**: ~5000 words
**Audience**: Developers, QA engineers, CI/CD maintainers

### validation-checklist.md
Step-by-step checklist for manual validation:
- Pre-fix validation
- Per-fix validation
- Batch validation
- Final validation
- Quality gate definitions
- Smoke test procedures
- Rollback criteria
- Sign-off checklist

**Format**: Markdown checklist
**Use Case**: Manual review and approval process

## Directory Structure

```
tests/validation/
├── README.md                    # This file
├── TESTING-STRATEGY.md          # Complete strategy document
├── validation-checklist.md      # Manual validation checklist
├── build-validator.sh           # Build validation script
├── error-tracker.sh             # Error tracking script
├── test-plan.sh                 # Test orchestration script
├── smoke-tests.sh               # Smoke test script
├── results/                     # JSON reports (auto-created)
│   ├── build_validation_*.json
│   ├── test_results_*.json
│   └── validation_summary_*.json
└── tracking/                    # Progress tracking (auto-created)
    ├── error_baseline.json      # Initial error count
    ├── error_current_*.json     # Snapshots
    └── error_progress.json      # Historical progress
```

## Build Order

Projects build in 6 dependency layers:

**Layer 1: Core Abstractions** (no dependencies)
- DotCompute.Abstractions
- DotCompute.Memory

**Layer 2: Core Runtime** (depends on Layer 1)
- DotCompute.Core

**Layer 3: Runtime Services** (depends on Layer 2)
- DotCompute.Generators
- DotCompute.Plugins
- DotCompute.Runtime

**Layer 4: Backends** (depends on Layers 1-3)
- DotCompute.Backends.CPU
- DotCompute.Backends.CUDA
- DotCompute.Backends.Metal

**Layer 5: Extensions** (depends on all previous)
- DotCompute.Algorithms
- DotCompute.Linq

**Layer 6: Tests** (depends on relevant production code)
- Test utilities → Unit tests → Integration tests → Hardware tests

## Quality Gates

All fixes must pass these 4 quality gates:

### Gate 1: Build Success
- **Criteria**: 100% build success rate
- **Command**: `./build-validator.sh`
- **Success**: Exit code 0

### Gate 2: Zero CA1848
- **Criteria**: 0 CA1848 warnings
- **Command**: `./error-tracker.sh`
- **Success**: Error count = 0

### Gate 3: Test Pass Rate
- **Criteria**: ≥95% test pass rate
- **Command**: `./test-plan.sh`
- **Success**: Pass rate ≥ 95%

### Gate 4: No Regressions
- **Criteria**: No new errors or test failures
- **Validation**: Compare with baseline
- **Success**: Delta ≤ 0 for errors

## Success Criteria

### Primary Goals (Must Achieve)
- ✓ CA1848 violations: 0 (from current baseline)
- ✓ Build success rate: 100%
- ✓ Test pass rate: ≥95%
- ✓ No regressions introduced

### Secondary Goals (Desired)
- ✓ Test coverage: Maintained at ≥75%
- ✓ Performance impact: ≤±5%
- ✓ Code quality: Maintained or improved

## Usage Workflows

### Initial Baseline
```bash
# First time setup
cd tests/validation

# Establish baseline
./error-tracker.sh  # Creates baseline
./build-validator.sh > baseline_build.txt
./test-plan.sh > baseline_tests.txt
```

### After Each Fix Batch
```bash
# Validate changes
./build-validator.sh  # Check build status
./error-tracker.sh    # Check error reduction
./smoke-tests.sh      # Quick validation
```

### Before Commit
```bash
# Full validation
./build-validator.sh && \
./error-tracker.sh && \
./test-plan.sh && \
./smoke-tests.sh

# All must exit with code 0
```

### Progress Monitoring
```bash
# Check current status
./error-tracker.sh

# View historical progress
cat tracking/error_progress.json | jq '.[] |
  "\(.timestamp): \(.current) errors (\(.percentage)% reduction)"'

# View latest results
ls -lt results/ | head -5
```

### CI/CD Integration
```bash
#!/bin/bash
# Example CI/CD pipeline

set -e  # Exit on any failure

cd tests/validation

# Phase 1: Build validation
echo "Validating builds..."
./build-validator.sh

# Phase 2: Error check
echo "Checking for CA1848 violations..."
./error-tracker.sh

# Phase 3: Test execution
echo "Running test suite..."
./test-plan.sh

# Phase 4: Smoke tests
echo "Running smoke tests..."
./smoke-tests.sh

echo "All validation gates passed!"
```

## Timeline Estimates

### Fast Path (Automated)
- **Duration**: 2-3 hours
- **Approach**: Automated fixes with basic validation
- **Risk**: Medium (may miss edge cases)
- **Suitable for**: Initial cleanup, straightforward cases

### Balanced Path (Recommended)
- **Duration**: 1 day
- **Approach**: Batch fixes with thorough testing
- **Risk**: Low
- **Suitable for**: Production releases

### Conservative Path (Maximum Quality)
- **Duration**: 2 days
- **Approach**: Manual review + comprehensive validation
- **Risk**: Minimal
- **Suitable for**: Critical systems, compliance requirements

## Troubleshooting

### Script Permission Denied
```bash
chmod +x tests/validation/*.sh
```

### jq Not Found
```bash
# Ubuntu/Debian
sudo apt install jq

# macOS
brew install jq
```

### Hardware Tests Skipped
This is normal if no NVIDIA GPU is available. Hardware tests are optional.

### Build Fails with Restore Errors
```bash
# Restore packages first
dotnet restore DotCompute.sln
```

## Output Formats

### Console Output
All scripts provide colored console output:
- 🔵 Blue: Headers and phases
- 🟢 Green: Success messages
- 🟡 Yellow: Warnings and info
- 🔴 Red: Errors and failures

### JSON Reports
All scripts generate structured JSON for automation:

```json
{
  "timestamp": "2025-01-04T12:00:00Z",
  "summary": {
    "total_projects": 26,
    "successful_builds": 26,
    "failed_builds": 0,
    "success_rate": 100
  },
  "projects": [...]
}
```

## Dependencies

### Required
- bash (4.0+)
- dotnet (.NET 9.0+)
- jq (JSON processor)
- grep, sed (text processing)

### Optional
- nvidia-smi (GPU detection)
- git (for CI/CD)

## Platform Support

- ✓ Linux (native, WSL2)
- ✓ macOS (with bash 4+)
- ✓ Windows (via WSL2 or Git Bash)

## Maintenance

### Adding New Projects
1. Update `BUILD_ORDER` in `build-validator.sh`
2. Update `TEST_SUITES` in `test-plan.sh`
3. Update smoke tests if critical path

### Archiving Results
```bash
# Results auto-archive with timestamps
# Clean old results (keep last 10)
cd tests/validation/results
ls -t | tail -n +11 | xargs rm -f
```

## Support

For issues or questions:
1. Review this README
2. Check TESTING-STRATEGY.md
3. Examine JSON output for details
4. Review validation-checklist.md

## Version History

- **v1.0** (2025-01-04): Initial release
  - Build validator
  - Error tracker
  - Test plan
  - Smoke tests
  - Complete documentation

## License

Same as DotCompute project (MIT)

---

**Last Updated**: 2025-01-04
**Maintained By**: DotCompute Testing Team
**Status**: Production Ready
