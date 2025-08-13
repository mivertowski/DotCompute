#!/bin/bash

# DotCompute Alpha Release Package Verification Script
# Version: 0.1.0-alpha.1
# Date: 2025-01-12

set -e

echo "=========================================="
echo "DotCompute v0.1.0-alpha.1 Package Verification"
echo "=========================================="

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Package results
PACKAGE_RESULTS=""
EXIT_CODE=0

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
    PACKAGE_RESULTS+="\n✅ $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
    PACKAGE_RESULTS+="\n⚠️  $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
    PACKAGE_RESULTS+="\n❌ $1"
    EXIT_CODE=1
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking package prerequisites..."
    
    if ! command -v dotnet >/dev/null 2>&1; then
        log_error ".NET CLI not found"
        return 1
    fi
    
    if [[ ! -f "DotCompute.sln" ]]; then
        log_error "Solution file not found"
        return 1
    fi
    
    # Check NuGet configuration
    if [[ -f "ci/nuget.config" ]] || [[ -f "nuget.config" ]]; then
        log_success "NuGet configuration found"
    else
        log_warning "NuGet configuration not found"
    fi
    
    return 0
}

# Clean previous packages
clean_packages() {
    log_info "Cleaning previous package artifacts..."
    
    # Remove existing packages
    rm -rf artifacts/packages 2>/dev/null || true
    rm -rf nupkgs/*.nupkg 2>/dev/null || true
    rm -rf nupkgs/*.snupkg 2>/dev/null || true
    
    log_success "Previous packages cleaned"
}

# Create NuGet packages
create_packages() {
    log_info "Creating NuGet packages..."
    
    # Create packages directory
    mkdir -p artifacts/packages
    
    # Pack the solution
    if dotnet pack DotCompute.sln --configuration Release --output artifacts/packages --include-symbols; then
        log_success "NuGet packages created successfully"
        return 0
    else
        log_error "NuGet package creation failed"
        return 1
    fi
}

# Verify core packages
verify_core_packages() {
    log_info "Verifying core packages..."
    
    EXPECTED_PACKAGES=(
        "DotCompute.Core"
        "DotCompute.Abstractions"
        "DotCompute.Memory"
        "DotCompute.Backends.CPU"
        "DotCompute.Runtime"
        "DotCompute.Plugins"
        "DotCompute.Generators"
    )
    
    local found_packages=0
    local total_packages=${#EXPECTED_PACKAGES[@]}
    
    for package in "${EXPECTED_PACKAGES[@]}"; do
        # Look for the package file
        PACKAGE_FILE=$(find artifacts/packages -name "${package}.*.nupkg" 2>/dev/null | head -1)
        
        if [[ -n "$PACKAGE_FILE" ]]; then
            # Check package size
            SIZE=$(stat -f%z "$PACKAGE_FILE" 2>/dev/null || stat -c%s "$PACKAGE_FILE" 2>/dev/null)
            
            if [[ $SIZE -gt 1000 ]]; then
                log_success "Core package verified: $package (${SIZE} bytes)"
                ((found_packages++))
            else
                log_warning "Core package too small: $package (${SIZE} bytes)"
            fi
            
            # Check for symbols package
            SYMBOLS_FILE="${PACKAGE_FILE%.nupkg}.snupkg"
            if [[ -f "$SYMBOLS_FILE" ]]; then
                log_success "Symbols package found: $package"
            else
                log_warning "Symbols package missing: $package"
            fi
        else
            log_error "Core package missing: $package"
        fi
    done
    
    if [[ $found_packages -eq $total_packages ]]; then
        log_success "All core packages verified ($found_packages/$total_packages)"
        return 0
    else
        log_error "Missing core packages ($found_packages/$total_packages found)"
        return 1
    fi
}

# Verify backend packages
verify_backend_packages() {
    log_info "Verifying backend packages..."
    
    BACKEND_PACKAGES=(
        "DotCompute.Backends.CPU"
        "DotCompute.Backends.CUDA"
    )
    
    for package in "${BACKEND_PACKAGES[@]}"; do
        PACKAGE_FILE=$(find artifacts/packages -name "${package}.*.nupkg" 2>/dev/null | head -1)
        
        if [[ -n "$PACKAGE_FILE" ]]; then
            SIZE=$(stat -f%z "$PACKAGE_FILE" 2>/dev/null || stat -c%s "$PACKAGE_FILE" 2>/dev/null)
            
            if [[ "$package" == "DotCompute.Backends.CPU" && $SIZE -gt 10000 ]]; then
                log_success "CPU backend package verified: $package (${SIZE} bytes)"
            elif [[ "$package" == "DotCompute.Backends.CUDA" && $SIZE -gt 5000 ]]; then
                log_success "CUDA backend package verified: $package (${SIZE} bytes)"
            else
                log_warning "Backend package size concern: $package (${SIZE} bytes)"
            fi
        else
            log_warning "Backend package not found: $package"
        fi
    done
}

# Verify extension packages
verify_extension_packages() {
    log_info "Verifying extension packages..."
    
    EXTENSION_PACKAGES=(
        "DotCompute.Algorithms"
        "DotCompute.Linq"
    )
    
    for package in "${EXTENSION_PACKAGES[@]}"; do
        PACKAGE_FILE=$(find artifacts/packages -name "${package}.*.nupkg" 2>/dev/null | head -1)
        
        if [[ -n "$PACKAGE_FILE" ]]; then
            SIZE=$(stat -f%z "$PACKAGE_FILE" 2>/dev/null || stat -c%s "$PACKAGE_FILE" 2>/dev/null)
            log_success "Extension package found: $package (${SIZE} bytes)"
        else
            log_warning "Extension package not found: $package (alpha quality expected)"
        fi
    done
}

# Verify package metadata
verify_package_metadata() {
    log_info "Verifying package metadata..."
    
    # Extract and check a sample package
    SAMPLE_PACKAGE=$(find artifacts/packages -name "DotCompute.Core.*.nupkg" 2>/dev/null | head -1)
    
    if [[ -n "$SAMPLE_PACKAGE" ]]; then
        # Create temp directory for extraction
        TEMP_DIR=$(mktemp -d)
        
        # Extract package (nupkg is a zip file)
        if unzip -q "$SAMPLE_PACKAGE" -d "$TEMP_DIR"; then
            # Check for nuspec file
            NUSPEC_FILE=$(find "$TEMP_DIR" -name "*.nuspec" | head -1)
            
            if [[ -f "$NUSPEC_FILE" ]]; then
                # Check version
                if grep -q "0.1.0-alpha.1" "$NUSPEC_FILE"; then
                    log_success "Package version 0.1.0-alpha.1 verified"
                else
                    log_error "Incorrect package version in metadata"
                fi
                
                # Check required metadata
                if grep -q "Michael Ivertowski" "$NUSPEC_FILE" && \
                   grep -q "DotCompute" "$NUSPEC_FILE" && \
                   grep -q "MIT" "$NUSPEC_FILE"; then
                    log_success "Package metadata verified (author, name, license)"
                else
                    log_warning "Package metadata incomplete"
                fi
                
                # Check description
                if grep -q -i "compute\|performance" "$NUSPEC_FILE"; then
                    log_success "Package description verified"
                else
                    log_warning "Package description may need improvement"
                fi
            else
                log_error "Package nuspec file not found"
            fi
        else
            log_error "Could not extract package for metadata verification"
        fi
        
        # Cleanup
        rm -rf "$TEMP_DIR"
    else
        log_error "No sample package found for metadata verification"
    fi
}

# Test package installation
test_package_installation() {
    log_info "Testing package installation..."
    
    # Create a temporary test project
    TEST_DIR=$(mktemp -d)
    cd "$TEST_DIR"
    
    # Create a simple test project
    if dotnet new console --name TestPackage >/dev/null 2>&1; then
        cd TestPackage
        
        # Add local package source
        LOCAL_SOURCE="$(realpath "$OLDPWD/artifacts/packages")"
        
        # Try to install core package
        CORE_PACKAGE=$(find "$LOCAL_SOURCE" -name "DotCompute.Core.*.nupkg" | head -1)
        if [[ -n "$CORE_PACKAGE" ]]; then
            PACKAGE_NAME=$(basename "$CORE_PACKAGE" .nupkg)
            PACKAGE_VERSION=$(echo "$PACKAGE_NAME" | sed 's/DotCompute\.Core\.//')
            
            if dotnet add package DotCompute.Core --version "$PACKAGE_VERSION" --source "$LOCAL_SOURCE" >/dev/null 2>&1; then
                log_success "Test package installation successful"
            else
                log_warning "Test package installation failed"
            fi
        else
            log_warning "No core package found for installation test"
        fi
    else
        log_warning "Could not create test project for installation test"
    fi
    
    # Return to original directory
    cd "$OLDPWD"
    
    # Cleanup
    rm -rf "$TEST_DIR"
}

# Verify package dependencies
verify_package_dependencies() {
    log_info "Verifying package dependencies..."
    
    # Check for reasonable dependency chains
    SAMPLE_PACKAGE=$(find artifacts/packages -name "DotCompute.Core.*.nupkg" 2>/dev/null | head -1)
    
    if [[ -n "$SAMPLE_PACKAGE" ]]; then
        # Extract and check dependencies
        TEMP_DIR=$(mktemp -d)
        
        if unzip -q "$SAMPLE_PACKAGE" -d "$TEMP_DIR"; then
            NUSPEC_FILE=$(find "$TEMP_DIR" -name "*.nuspec" | head -1)
            
            if [[ -f "$NUSPEC_FILE" ]]; then
                # Check for framework dependencies
                if grep -q "net9.0" "$NUSPEC_FILE"; then
                    log_success "Target framework .NET 9.0 verified"
                else
                    log_warning "Target framework not clearly specified"
                fi
                
                # Check for excessive dependencies
                DEPENDENCY_COUNT=$(grep -c "<dependency" "$NUSPEC_FILE" 2>/dev/null || echo "0")
                if [[ $DEPENDENCY_COUNT -lt 10 ]]; then
                    log_success "Reasonable dependency count: $DEPENDENCY_COUNT"
                else
                    log_warning "High dependency count: $DEPENDENCY_COUNT"
                fi
            fi
        fi
        
        rm -rf "$TEMP_DIR"
    fi
}

# Check package security
verify_package_security() {
    log_info "Verifying package security..."
    
    # Check for signed packages (if applicable)
    SAMPLE_PACKAGE=$(find artifacts/packages -name "DotCompute.Core.*.nupkg" 2>/dev/null | head -1)
    
    if [[ -n "$SAMPLE_PACKAGE" ]]; then
        # Basic security checks
        
        # Check file permissions
        PERMS=$(stat -f%A "$SAMPLE_PACKAGE" 2>/dev/null || stat -c%a "$SAMPLE_PACKAGE" 2>/dev/null)
        if [[ "$PERMS" =~ ^[67][0-7][0-7]$ ]]; then
            log_success "Package file permissions appropriate"
        else
            log_warning "Package file permissions: $PERMS"
        fi
        
        # Check for suspicious content (basic check)
        if unzip -l "$SAMPLE_PACKAGE" | grep -q -v -E "\.(dll|xml|json|nuspec|txt|md)$"; then
            log_warning "Package contains non-standard files"
        else
            log_success "Package content appears standard"
        fi
    fi
}

# Generate package report
generate_package_report() {
    REPORT_FILE="package-verification-report.md"
    
    # Count packages
    TOTAL_PACKAGES=$(find artifacts/packages -name "*.nupkg" 2>/dev/null | wc -l)
    TOTAL_SYMBOLS=$(find artifacts/packages -name "*.snupkg" 2>/dev/null | wc -l)
    
    cat > "$REPORT_FILE" << EOF
# DotCompute v0.1.0-alpha.1 Package Verification Report

**Date:** $(date)
**Script Version:** 1.0.0

## Summary

Package verification completed with exit code: **$EXIT_CODE**

## Package Statistics

- **Total Packages:** $TOTAL_PACKAGES .nupkg files
- **Symbol Packages:** $TOTAL_SYMBOLS .snupkg files
- **Package Location:** artifacts/packages/

## Package List

$(find artifacts/packages -name "*.nupkg" 2>/dev/null | sort | sed 's/^/- /')

## Results
$PACKAGE_RESULTS

## Package Categories

### ✅ Core Packages (Production Ready)
- DotCompute.Core
- DotCompute.Abstractions  
- DotCompute.Memory
- DotCompute.Backends.CPU
- DotCompute.Runtime
- DotCompute.Plugins
- DotCompute.Generators

### 🚧 Backend Packages (Alpha Quality)
- DotCompute.Backends.CUDA
- DotCompute.Backends.Metal (if present)

### 🚧 Extension Packages (Alpha Quality)
- DotCompute.Algorithms
- DotCompute.Linq

## Verification Status

$(if [[ $EXIT_CODE -eq 0 ]]; then echo "✅ **PASSED** - Alpha release packages are ready"; else echo "❌ **FAILED** - Package issues found"; fi)

---
Generated by package-verification.sh
EOF

    log_info "Package report generated: $REPORT_FILE"
}

# Main execution
main() {
    echo "Starting package verification at $(date)"
    echo
    
    check_prerequisites || exit 1
    clean_packages
    
    # Build solution first
    log_info "Building solution for packaging..."
    if ! dotnet build DotCompute.sln --configuration Release >/dev/null 2>&1; then
        log_error "Build failed - cannot create packages"
        exit 1
    fi
    log_success "Solution built successfully"
    
    # Create and verify packages
    create_packages || exit 1
    verify_core_packages || exit 1
    verify_backend_packages
    verify_extension_packages
    verify_package_metadata
    test_package_installation
    verify_package_dependencies
    verify_package_security
    
    echo
    echo "=========================================="
    echo "Package Verification Summary"
    echo "=========================================="
    echo -e "$PACKAGE_RESULTS"
    echo
    
    generate_package_report
    
    if [[ $EXIT_CODE -eq 0 ]]; then
        echo -e "${GREEN}✅ PACKAGE VERIFICATION PASSED${NC}"
        echo "The DotCompute v0.1.0-alpha.1 packages are ready for release!"
    else
        echo -e "${RED}❌ PACKAGE VERIFICATION FAILED${NC}"
        echo "Package issues found - please resolve before releasing."
    fi
    
    exit $EXIT_CODE
}

# Run the verification
main "$@"