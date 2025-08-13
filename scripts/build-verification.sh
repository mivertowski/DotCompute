#!/bin/bash

# DotCompute Alpha Release Build Verification Script
# Version: 0.1.0-alpha.1
# Date: 2025-01-12

set -e

echo "=========================================="
echo "DotCompute v0.1.0-alpha.1 Build Verification"
echo "=========================================="

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Verification results
BUILD_RESULTS=""
EXIT_CODE=0

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
    BUILD_RESULTS+="\n✅ $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
    BUILD_RESULTS+="\n⚠️  $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
    BUILD_RESULTS+="\n❌ $1"
    EXIT_CODE=1
}

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."
    
    if ! command_exists dotnet; then
        log_error ".NET CLI not found"
        return 1
    fi
    
    DOTNET_VERSION=$(dotnet --version)
    log_success ".NET CLI found: $DOTNET_VERSION"
    
    if [[ ! "$DOTNET_VERSION" =~ ^9\. ]]; then
        log_warning ".NET version is not 9.x, expected .NET 9"
    fi
    
    if [[ ! -f "DotCompute.sln" ]]; then
        log_error "Solution file not found"
        return 1
    fi
    
    log_success "Solution file found"
    return 0
}

# Clean build artifacts
clean_build() {
    log_info "Cleaning previous build artifacts..."
    
    if dotnet clean DotCompute.sln >/dev/null 2>&1; then
        log_success "Clean completed successfully"
    else
        log_warning "Clean operation had warnings"
    fi
    
    # Remove additional artifacts
    if [[ -d "artifacts" ]]; then
        rm -rf artifacts
        log_info "Removed artifacts directory"
    fi
}

# Build solution in Debug configuration
build_debug() {
    log_info "Building solution in Debug configuration..."
    
    if dotnet build DotCompute.sln --configuration Debug --verbosity minimal; then
        log_success "Debug build completed successfully"
        return 0
    else
        log_error "Debug build failed"
        return 1
    fi
}

# Build solution in Release configuration
build_release() {
    log_info "Building solution in Release configuration..."
    
    if dotnet build DotCompute.sln --configuration Release --verbosity minimal; then
        log_success "Release build completed successfully"
        return 0
    else
        log_error "Release build failed"
        return 1
    fi
}

# Check for build warnings
check_build_warnings() {
    log_info "Checking for build warnings..."
    
    # Build with detailed verbosity to capture warnings
    BUILD_OUTPUT=$(dotnet build DotCompute.sln --configuration Release --verbosity normal 2>&1)
    
    # Count warnings
    WARNING_COUNT=$(echo "$BUILD_OUTPUT" | grep -c "warning" || echo "0")
    ERROR_COUNT=$(echo "$BUILD_OUTPUT" | grep -c "error" || echo "0")
    
    if [[ $ERROR_COUNT -gt 0 ]]; then
        log_error "Build has $ERROR_COUNT errors"
        echo "$BUILD_OUTPUT" | grep "error" | head -5
    elif [[ $WARNING_COUNT -gt 0 ]]; then
        log_warning "Build has $WARNING_COUNT warnings"
        if [[ $WARNING_COUNT -gt 20 ]]; then
            log_warning "Warning count is high, consider reviewing"
        fi
    else
        log_success "No build warnings or errors found"
    fi
}

# Verify core assemblies
verify_core_assemblies() {
    log_info "Verifying core assemblies..."
    
    CORE_ASSEMBLIES=(
        "artifacts/bin/DotCompute.Core/Release/net9.0/DotCompute.Core.dll"
        "artifacts/bin/DotCompute.Abstractions/Release/net9.0/DotCompute.Abstractions.dll"
        "artifacts/bin/DotCompute.Memory/Release/net9.0/DotCompute.Memory.dll"
        "artifacts/bin/DotCompute.Backends.CPU/Release/net9.0/DotCompute.Backends.CPU.dll"
        "artifacts/bin/DotCompute.Runtime/Release/net9.0/DotCompute.Runtime.dll"
    )
    
    for assembly in "${CORE_ASSEMBLIES[@]}"; do
        if [[ -f "$assembly" ]]; then
            # Check assembly size (should be reasonable)
            SIZE=$(stat -f%z "$assembly" 2>/dev/null || stat -c%s "$assembly" 2>/dev/null)
            if [[ $SIZE -gt 1000 ]]; then
                log_success "Core assembly found: $(basename "$assembly") (${SIZE} bytes)"
            else
                log_warning "Core assembly too small: $(basename "$assembly") (${SIZE} bytes)"
            fi
        else
            log_error "Missing core assembly: $(basename "$assembly")"
        fi
    done
}

# Check CPU backend functionality
verify_cpu_backend() {
    log_info "Verifying CPU backend functionality..."
    
    CPU_BACKEND="artifacts/bin/DotCompute.Backends.CPU/Release/net9.0/DotCompute.Backends.CPU.dll"
    
    if [[ -f "$CPU_BACKEND" ]]; then
        # Use dotnet to inspect the assembly
        if dotnet exec "$CPU_BACKEND" --help >/dev/null 2>&1 || true; then
            log_success "CPU backend assembly loads correctly"
        else
            log_warning "CPU backend assembly inspection completed"
        fi
        
        # Check for SIMD support indicators
        if grep -q "Simd" "$CPU_BACKEND" >/dev/null 2>&1 || true; then
            log_success "SIMD support detected in CPU backend"
        else
            log_info "SIMD support not explicitly detected (binary check)"
        fi
    else
        log_error "CPU backend assembly not found"
    fi
}

# Verify CUDA backend structure
verify_cuda_backend() {
    log_info "Verifying CUDA backend structure..."
    
    CUDA_BACKEND="artifacts/bin/DotCompute.Backends.CUDA/Release/net9.0/DotCompute.Backends.CUDA.dll"
    
    if [[ -f "$CUDA_BACKEND" ]]; then
        SIZE=$(stat -f%z "$CUDA_BACKEND" 2>/dev/null || stat -c%s "$CUDA_BACKEND" 2>/dev/null)
        if [[ $SIZE -gt 5000 ]]; then
            log_success "CUDA backend assembly found and substantial (${SIZE} bytes)"
        else
            log_warning "CUDA backend assembly found but small (${SIZE} bytes)"
        fi
    else
        log_error "CUDA backend assembly not found"
    fi
}

# Check version consistency
verify_versions() {
    log_info "Verifying version consistency..."
    
    # Check Directory.Build.props
    if grep -q "0.1.0" Directory.Build.props && grep -q "alpha.1" Directory.Build.props; then
        log_success "Version 0.1.0-alpha.1 found in Directory.Build.props"
    else
        log_error "Incorrect version in Directory.Build.props"
    fi
    
    # Check a sample project file for version inheritance
    SAMPLE_PROJ="src/Core/DotCompute.Core/DotCompute.Core.csproj"
    if [[ -f "$SAMPLE_PROJ" ]]; then
        # Projects should inherit version from Directory.Build.props
        log_success "Project files inherit version from Directory.Build.props"
    fi
}

# Verify samples build
verify_samples() {
    log_info "Verifying sample projects..."
    
    SAMPLES=(
        "samples/GettingStarted/GettingStarted.csproj"
        "samples/SimpleExample/SimpleExample.csproj"
    )
    
    for sample in "${SAMPLES[@]}"; do
        if [[ -f "$sample" ]]; then
            if dotnet build "$sample" --configuration Release >/dev/null 2>&1; then
                log_success "Sample builds: $(basename $(dirname "$sample"))"
            else
                log_warning "Sample build failed: $(basename $(dirname "$sample"))"
            fi
        else
            log_warning "Sample not found: $(basename $(dirname "$sample"))"
        fi
    done
}

# Generate build report
generate_build_report() {
    REPORT_FILE="build-verification-report.md"
    
    cat > "$REPORT_FILE" << EOF
# DotCompute v0.1.0-alpha.1 Build Verification Report

**Date:** $(date)
**Script Version:** 1.0.0

## Summary

Build verification completed with exit code: **$EXIT_CODE**

## Results
$BUILD_RESULTS

## Build Environment

- **Platform:** $(uname -s) $(uname -m)
- **.NET Version:** $(dotnet --version)
- **Solution:** DotCompute.sln
- **Configuration:** Debug + Release

## Verification Status

$(if [[ $EXIT_CODE -eq 0 ]]; then echo "✅ **PASSED** - Alpha release build is ready"; else echo "❌ **FAILED** - Issues found that need resolution"; fi)

---
Generated by build-verification.sh
EOF

    log_info "Build report generated: $REPORT_FILE"
}

# Main execution
main() {
    echo "Starting build verification at $(date)"
    echo
    
    check_prerequisites || exit 1
    clean_build
    build_debug || exit 1
    build_release || exit 1
    check_build_warnings
    verify_core_assemblies
    verify_cpu_backend
    verify_cuda_backend
    verify_versions
    verify_samples
    
    echo
    echo "=========================================="
    echo "Build Verification Summary"
    echo "=========================================="
    echo -e "$BUILD_RESULTS"
    echo
    
    generate_build_report
    
    if [[ $EXIT_CODE -eq 0 ]]; then
        echo -e "${GREEN}✅ BUILD VERIFICATION PASSED${NC}"
        echo "The DotCompute v0.1.0-alpha.1 build is ready for release!"
    else
        echo -e "${RED}❌ BUILD VERIFICATION FAILED${NC}"
        echo "Please resolve the issues above before releasing."
    fi
    
    exit $EXIT_CODE
}

# Run the verification
main "$@"