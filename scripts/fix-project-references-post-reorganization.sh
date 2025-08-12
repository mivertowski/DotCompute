#!/bin/bash

# Fix project references after solution reorganization
set -euo pipefail

echo "🔧 Fixing project references after reorganization..."

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Function to fix references in a project file
fix_project_references() {
    local project_file="$1"
    echo -e "${BLUE}Fixing references in: $(basename "$project_file")${NC}"
    
    # Fix Core project references
    sed -i 's|Include=".*Core\\DotCompute\.Abstractions\\|Include="..\\..\\..\\src\\Core\\DotCompute.Abstractions\\|g' "$project_file"
    sed -i 's|Include=".*Core\\DotCompute\.Core\\|Include="..\\..\\..\\src\\Core\\DotCompute.Core\\|g' "$project_file"
    sed -i 's|Include=".*Core\\DotCompute\.Memory\\|Include="..\\..\\..\\src\\Core\\DotCompute.Memory\\|g' "$project_file"
    
    # Fix Runtime project references
    sed -i 's|Include=".*Runtime\\DotCompute\.Runtime\\|Include="..\\..\\..\\src\\Runtime\\DotCompute.Runtime\\|g' "$project_file"
    sed -i 's|Include=".*Runtime\\DotCompute\.Plugins\\|Include="..\\..\\..\\src\\Runtime\\DotCompute.Plugins\\|g' "$project_file"
    
    # Fix Algorithms project references
    sed -i 's|Include=".*DotCompute\.Algorithms\\|Include="..\\..\\..\\src\\Algorithms\\DotCompute.Algorithms\\|g' "$project_file"
    sed -i 's|Include=".*DotCompute\.Linq\\|Include="..\\..\\..\\src\\Algorithms\\DotCompute.Linq\\|g' "$project_file"
    
    # Fix test project references
    sed -i 's|Include=".*DotCompute\.Tests\.Common\\|Include="..\\..\\..\\tests\\Shared\\DotCompute.Tests.Common\\|g' "$project_file"
    sed -i 's|Include=".*DotCompute\.Tests\.Implementations\\|Include="..\\..\\..\\tests\\Shared\\DotCompute.Tests.Implementations\\|g' "$project_file"
    sed -i 's|Include=".*DotCompute\.Tests\.Mocks\\|Include="..\\..\\..\\tests\\Shared\\DotCompute.Tests.Mocks\\|g' "$project_file"
    sed -i 's|Include=".*DotCompute\.SharedTestUtilities\\|Include="..\\..\\..\\tests\\Shared\\DotCompute.SharedTestUtilities\\|g' "$project_file"
}

# Function to fix relative paths for specific project types
fix_src_project_references() {
    local project_file="$1"
    local project_dir="$(dirname "$project_file")"
    local project_name="$(basename "$project_dir")"
    
    echo -e "${YELLOW}Fixing source project: $project_name${NC}"
    
    # Determine the correct relative paths based on project location
    if [[ "$project_file" == *"/src/Core/"* ]]; then
        # Core projects (DotCompute.Abstractions, DotCompute.Core, DotCompute.Memory)
        sed -i 's|Include="..\\Core\\|Include="..\\|g' "$project_file"
        sed -i 's|Include="..\\..\\Core\\|Include="..\\|g' "$project_file"
    elif [[ "$project_file" == *"/src/Runtime/"* ]]; then
        # Runtime projects (DotCompute.Runtime, DotCompute.Plugins)
        sed -i 's|Include="..\\Core\\|Include="..\\..\\Core\\|g' "$project_file"
        sed -i 's|Include="..\\Runtime\\|Include="..\\|g' "$project_file"
    elif [[ "$project_file" == *"/src/Algorithms/"* ]]; then
        # Algorithm projects (DotCompute.Algorithms, DotCompute.Linq)
        sed -i 's|Include="..\\Core\\|Include="..\\..\\Core\\|g' "$project_file"
        sed -i 's|Include="..\\Runtime\\|Include="..\\..\\Runtime\\|g' "$project_file"
        sed -i 's|Include="..\\Algorithms\\|Include="..\\|g' "$project_file"
    fi
}

# Fix all project files
echo -e "${BLUE}Finding and fixing all project references...${NC}"

# Fix test project references
find "$PROJECT_ROOT/tests" -name "*.csproj" -type f | while read -r project_file; do
    fix_project_references "$project_file"
done

# Fix source project references
find "$PROJECT_ROOT/src" -name "*.csproj" -type f | while read -r project_file; do
    fix_src_project_references "$project_file"
done

# Fix backend plugin references
find "$PROJECT_ROOT/plugins" -name "*.csproj" -type f | while read -r project_file; do
    fix_project_references "$project_file"
    # Backend-specific fixes
    sed -i 's|Include="..\\..\\Core\\|Include="..\\..\\..\\src\\Core\\|g' "$project_file"
    sed -i 's|Include="..\\..\\Runtime\\|Include="..\\..\\..\\src\\Runtime\\|g' "$project_file"
done

# Fix sample/example project references
find "$PROJECT_ROOT/samples" -name "*.csproj" -type f | while read -r project_file; do
    fix_project_references "$project_file"
    # Sample-specific fixes
    sed -i 's|Include="..\\..\\Core\\|Include="..\\..\\src\\Core\\|g' "$project_file"
    sed -i 's|Include="..\\..\\Runtime\\|Include="..\\..\\src\\Runtime\\|g' "$project_file"
done

# Fix benchmark project references
find "$PROJECT_ROOT/benchmarks" -name "*.csproj" -type f | while read -r project_file; do
    fix_project_references "$project_file"
    # Benchmark-specific fixes
    sed -i 's|Include="..\\..\\Core\\|Include="..\\..\\src\\Core\\|g' "$project_file"
    sed -i 's|Include="..\\..\\Runtime\\|Include="..\\..\\src\\Runtime\\|g' "$project_file"
done

echo -e "${GREEN}✅ Project references fixed!${NC}"

# Test build to verify fixes
echo -e "${BLUE}Testing build after fixes...${NC}"
if dotnet build "$PROJECT_ROOT/DotCompute.sln" --configuration Release --verbosity minimal > /dev/null 2>&1; then
    echo -e "${GREEN}✅ Build successful after reference fixes!${NC}"
else
    echo -e "${RED}❌ Build still has issues. Manual intervention may be required.${NC}"
fi

echo -e "${BLUE}Reference fix complete!${NC}"