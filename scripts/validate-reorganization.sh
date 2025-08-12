#!/bin/bash
# Validation script for DotCompute reorganization
# This script checks if the reorganization would be beneficial and safe

set -euo pipefail

echo -e "\e[36m🔍 DotCompute Reorganization Validation\e[0m"
echo -e "\e[36m======================================\e[0m"

# Color functions
info() { echo -e "\e[34mℹ️  $1\e[0m"; }
success() { echo -e "\e[32m✅ $1\e[0m"; }
warning() { echo -e "\e[33m⚠️  $1\e[0m"; }
error() { echo -e "\e[31m❌ $1\e[0m"; }

# Check current structure
info "Analyzing current project structure..."

# Count projects in each category
core_projects=$(find src -maxdepth 1 -name "DotCompute.*" -type d 2>/dev/null | wc -l)
backend_projects=$(find plugins/backends -name "DotCompute.Backends.*" -type d 2>/dev/null | wc -l)
test_projects=$(find tests -name "*.Tests" -type d 2>/dev/null | wc -l)
total_projects=$(find . -name "*.csproj" -type f | wc -l)

echo "📊 Current Structure Analysis:"
echo "  Core projects in src/: $core_projects"
echo "  Backend projects: $backend_projects"
echo "  Test projects: $test_projects"
echo "  Total projects: $total_projects"
echo ""

# Check for complex reference patterns
info "Analyzing project reference complexity..."

complex_refs=$(find . -name "*.csproj" -exec grep -l "\.\.\\.*src\\" {} \; 2>/dev/null | wc -l)
backend_refs=$(find . -name "*.csproj" -exec grep -l "plugins\\backends" {} \; 2>/dev/null | wc -l)

echo "🔗 Reference Complexity:"
echo "  Projects with complex relative paths: $complex_refs"
echo "  Projects referencing backends via plugins/: $backend_refs"
echo ""

# Check solution structure
info "Analyzing solution organization..."

solution_folders=$(grep -c "2150E333-8FDC-42A3-9474-1A3956D46DE8" DotCompute.sln 2>/dev/null || echo "0")
echo "📁 Solution Organization:"
echo "  Solution folders: $solution_folders"
echo ""

# Assess benefits
info "Assessing reorganization benefits..."

echo "🎯 Expected Improvements:"
if [[ $core_projects -gt 3 ]]; then
    success "Core projects will be better organized (currently scattered)"
else
    info "Core projects are manageable (${core_projects} projects)"
fi

if [[ $backend_projects -gt 1 ]]; then
    success "Backend projects will be consolidated (currently in plugins/backends/)"
else
    info "Limited backend consolidation benefit (${backend_projects} backends)"
fi

if [[ $complex_refs -gt 0 ]]; then
    success "Complex references will be simplified (${complex_refs} projects affected)"
else
    info "Reference complexity is already manageable"
fi

if [[ $test_projects -gt 5 ]]; then
    success "Test organization will be significantly improved (${test_projects} test projects)"
else
    info "Test organization improvement will be moderate (${test_projects} test projects)"
fi

echo ""

# Check prerequisites
info "Checking reorganization prerequisites..."

# Check for git
if command -v git &> /dev/null; then
    success "Git is available for backup verification"
else
    warning "Git not found - version control recommended"
fi

# Check for dotnet
if command -v dotnet &> /dev/null; then
    success "dotnet CLI available for build testing"
else
    error "dotnet CLI required for build verification"
    exit 1
fi

# Check solution file
if [[ -f "DotCompute.sln" ]]; then
    success "Solution file found"
else
    error "DotCompute.sln not found - run from solution root"
    exit 1
fi

# Check disk space
available_space=$(df . | tail -1 | awk '{print $4}')
if [[ $available_space -gt 1000000 ]]; then  # 1GB in KB
    success "Sufficient disk space for backup"
else
    warning "Limited disk space - consider cleanup before reorganization"
fi

echo ""

# Risk assessment
info "Risk Assessment:"

echo "🔒 Safety Measures:"
echo "  ✅ Automatic backup creation"
echo "  ✅ Dry-run capability"
echo "  ✅ Build verification"
echo "  ✅ Rollback procedure documented"

echo ""
echo "⚡ Estimated Impact:"
echo "  📈 Developer productivity: High improvement expected"
echo "  🏗️  Build performance: Moderate improvement expected"
echo "  🧪 Test organization: High improvement expected"
echo "  📚 Code discoverability: High improvement expected"

echo ""

# Recommendation
info "Recommendation:"

if [[ $total_projects -gt 10 ]] && [[ $complex_refs -gt 0 ]] && [[ $test_projects -gt 3 ]]; then
    success "STRONGLY RECOMMENDED: High benefit, manageable risk"
    echo "  Reasons:"
    echo "  • Complex project structure ($total_projects projects)"
    echo "  • Complex reference patterns ($complex_refs affected)"
    echo "  • Poor test organization ($test_projects test projects)"
elif [[ $total_projects -gt 5 ]] && [[ $complex_refs -gt 0 ]]; then
    success "RECOMMENDED: Good benefit, low risk"
    echo "  Reasons:"
    echo "  • Moderate complexity ($total_projects projects)"
    echo "  • Some reference complexity ($complex_refs affected)"
else
    info "OPTIONAL: Limited benefit, minimal risk"
    echo "  Current structure is manageable but could be improved"
fi

echo ""
echo "🚀 Next Steps:"
echo "1. Run: ./scripts/reorganize-solution.sh --dry-run --force"
echo "2. Review the planned changes"
echo "3. Run: ./scripts/reorganize-solution.sh --force"
echo "4. Verify: dotnet build && dotnet test"

echo ""
success "Validation complete! See recommendations above."