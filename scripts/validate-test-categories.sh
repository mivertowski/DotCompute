#!/bin/bash

# Test Category Validation Script
# Validates that hardware tests are properly categorized and can be filtered correctly

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}Test Category Validation${NC}"
echo "========================"

# Get the script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Project Root: $PROJECT_ROOT"
cd "$PROJECT_ROOT"

# Function to count test methods with specific traits
count_tests_with_trait() {
    local trait_name="$1"
    local trait_value="$2"
    local search_pattern="Trait(\"$trait_name\", \"$trait_value\")"
    
    find tests -name "*.cs" -exec grep -l "$search_pattern" {} \; | wc -l
}

# Function to find files with missing traits
find_hardware_tests_without_traits() {
    echo -e "${YELLOW}Searching for hardware test files without proper traits...${NC}"
    
    # Find files in Hardware directories that might need traits
    find tests/Hardware -name "*.cs" -type f | while read -r file; do
        # Skip utility files
        if [[ "$file" == *"Utilities"* ]] || [[ "$file" == *"TestCollection"* ]]; then
            continue
        fi
        
        # Check if file has class declaration but missing hardware traits
        if grep -q "public.*class.*Tests" "$file"; then
            if ! grep -q "Trait.*Hardware\|Trait.*Category.*HardwareRequired\|Trait.*Category.*Mock" "$file"; then
                echo "  ⚠️  Missing traits: $file"
            fi
        fi
    done
}

# Function to validate test settings files
validate_test_settings() {
    echo -e "${YELLOW}Validating test configuration files...${NC}"
    
    local ci_settings="tests/ci-test.runsettings"
    local hardware_settings="tests/hardware-only-test.runsettings"
    
    if [[ -f "$ci_settings" ]]; then
        echo -e "${GREEN}✅ CI test settings found${NC}: $ci_settings"
        
        # Check if CI settings exclude hardware tests
        if grep -q "TestCategory!=HardwareRequired" "$ci_settings"; then
            echo -e "${GREEN}  ✅ CI settings properly exclude hardware tests${NC}"
        else
            echo -e "${RED}  ❌ CI settings may not properly exclude hardware tests${NC}"
        fi
    else
        echo -e "${RED}❌ CI test settings missing${NC}: $ci_settings"
    fi
    
    if [[ -f "$hardware_settings" ]]; then
        echo -e "${GREEN}✅ Hardware test settings found${NC}: $hardware_settings"
        
        # Check if hardware settings include only hardware tests
        if grep -q "HardwareRequired\|CudaRequired" "$hardware_settings"; then
            echo -e "${GREEN}  ✅ Hardware settings properly include hardware tests${NC}"
        else
            echo -e "${RED}  ❌ Hardware settings may not properly filter hardware tests${NC}"
        fi
    else
        echo -e "${RED}❌ Hardware test settings missing${NC}: $hardware_settings"
    fi
}

# Function to test filter effectiveness
test_filter_effectiveness() {
    echo -e "${YELLOW}Testing test filtering effectiveness...${NC}"
    
    # Build the solution first
    echo -e "${BLUE}Building solution...${NC}"
    dotnet build --configuration Release --no-restore --verbosity quiet
    
    if [[ $? -ne 0 ]]; then
        echo -e "${RED}Build failed - cannot test filtering${NC}"
        return 1
    fi
    
    # Test CI filter (should find mock/unit tests)
    echo -e "${BLUE}Testing CI test filter...${NC}"
    ci_count=$(dotnet test --configuration Release --list-tests --settings tests/ci-test.runsettings 2>/dev/null | grep -c "Tests found:" || echo "0")
    echo "  CI-filtered tests: $ci_count"
    
    # Test hardware filter (should find hardware tests)
    echo -e "${BLUE}Testing hardware test filter...${NC}"
    hardware_count=$(dotnet test --configuration Release --list-tests --settings tests/hardware-only-test.runsettings 2>/dev/null | grep -c "Tests found:" || echo "0")
    echo "  Hardware-filtered tests: $hardware_count"
    
    # Test no filter (should find all tests)
    echo -e "${BLUE}Testing no filter (all tests)...${NC}"
    all_count=$(dotnet test --configuration Release --list-tests 2>/dev/null | grep -c "Tests found:" || echo "0")
    echo "  Total tests: $all_count"
}

# Function to check GitHub Actions workflow
validate_github_workflow() {
    echo -e "${YELLOW}Validating GitHub Actions workflow...${NC}"
    
    local workflow_file=".github/workflows/ci.yml"
    
    if [[ -f "$workflow_file" ]]; then
        echo -e "${GREEN}✅ GitHub workflow found${NC}: $workflow_file"
        
        if grep -q "ci-test.runsettings" "$workflow_file"; then
            echo -e "${GREEN}  ✅ Workflow uses CI test settings${NC}"
        else
            echo -e "${RED}  ❌ Workflow may not use proper test filtering${NC}"
        fi
        
        if grep -q "hardware-tests" "$workflow_file"; then
            echo -e "${GREEN}  ✅ Workflow includes optional hardware test job${NC}"
        else
            echo -e "${YELLOW}  ⚠️  No hardware test job found (optional)${NC}"
        fi
    else
        echo -e "${RED}❌ GitHub workflow missing${NC}: $workflow_file"
    fi
}

echo ""
echo -e "${YELLOW}=== VALIDATION RESULTS ===${NC}"
echo ""

# Run all validations
find_hardware_tests_without_traits
echo ""

validate_test_settings
echo ""

validate_github_workflow
echo ""

# Try to test filtering if build is available
echo -e "${YELLOW}Testing filter effectiveness (optional)...${NC}"
test_filter_effectiveness
echo ""

# Summary
echo -e "${YELLOW}=== SUMMARY ===${NC}"

# Count different test categories
mock_tests=$(find tests -name "*.cs" -exec grep -l 'Trait.*"Mock"' {} \; | wc -l)
hardware_tests=$(find tests -name "*.cs" -exec grep -l 'Trait.*"HardwareRequired"' {} \; | wc -l)
cuda_tests=$(find tests -name "*.cs" -exec grep -l 'Trait.*"CudaRequired"' {} \; | wc -l)

echo "Test file counts:"
echo "  📦 Mock test files: $mock_tests"
echo "  🔧 Hardware test files: $hardware_tests"  
echo "  🎮 CUDA test files: $cuda_tests"
echo ""

if [[ $mock_tests -gt 0 ]] && [[ $hardware_tests -gt 0 ]]; then
    echo -e "${GREEN}✅ Test categorization appears to be working${NC}"
else
    echo -e "${RED}❌ Test categorization may need improvement${NC}"
fi

echo ""
echo -e "${BLUE}Validation completed!${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Run CI tests: ./scripts/run-ci-tests.sh"
echo "  2. Run mock tests: ./scripts/run-mock-tests.sh"
echo "  3. Run hardware tests (if available): ./scripts/run-hardware-tests.sh"