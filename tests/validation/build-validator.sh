#!/bin/bash

# DotCompute Build Validation Script
# Builds projects in dependency order and tracks error reduction

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
RESULTS_FILE="$RESULTS_DIR/build_validation_$TIMESTAMP.json"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Ensure results directory exists
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}=== DotCompute Build Validation ===${NC}"
echo "Starting validation at $(date)"
echo "Results will be saved to: $RESULTS_FILE"
echo ""

# Build order based on dependencies
declare -a BUILD_ORDER=(
    # Layer 1: Core abstractions (no dependencies)
    "src/Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj"
    "src/Core/DotCompute.Memory/DotCompute.Memory.csproj"

    # Layer 2: Core runtime (depends on abstractions)
    "src/Core/DotCompute.Core/DotCompute.Core.csproj"

    # Layer 3: Runtime services (depends on core)
    "src/Runtime/DotCompute.Generators/DotCompute.Generators.csproj"
    "src/Runtime/DotCompute.Plugins/DotCompute.Plugins.csproj"
    "src/Runtime/DotCompute.Runtime/DotCompute.Runtime.csproj"

    # Layer 4: Backends (depends on core + runtime)
    "src/Backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj"
    "src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj"
    "src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj"

    # Layer 5: Extensions (depends on everything above)
    "src/Extensions/DotCompute.Algorithms/DotCompute.Algorithms.csproj"
    "src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj"

    # Layer 6: Test utilities (shared infrastructure)
    "tests/Shared/DotCompute.Tests.Common/DotCompute.Tests.Common.csproj"
    "tests/Shared/DotCompute.Tests.Mocks/DotCompute.Tests.Mocks.csproj"
    "tests/Shared/DotCompute.Tests.Implementations/DotCompute.Tests.Implementations.csproj"
    "tests/Shared/DotCompute.SharedTestUtilities/DotCompute.SharedTestUtilities.csproj"

    # Layer 7: Unit tests
    "tests/Unit/DotCompute.Abstractions.Tests/DotCompute.Abstractions.Tests.csproj"
    "tests/Unit/DotCompute.Memory.Tests/DotCompute.Memory.Tests.csproj"
    "tests/Unit/DotCompute.Core.Tests/DotCompute.Core.Tests.csproj"
    "tests/Unit/DotCompute.Plugins.Tests/DotCompute.Plugins.Tests.csproj"
    "tests/Unit/DotCompute.Backends.CPU.Tests/DotCompute.Backends.CPU.Tests.csproj"
    "tests/Unit/DotCompute.BasicTests/DotCompute.BasicTests.csproj"

    # Layer 8: Integration tests
    "tests/Integration/DotCompute.Generators.Integration.Tests/DotCompute.Generators.Integration.Tests.csproj"

    # Layer 9: Hardware tests
    "tests/Hardware/DotCompute.Hardware.Mock.Tests/DotCompute.Hardware.Mock.Tests.csproj"
    "tests/Hardware/DotCompute.Hardware.Cuda.Tests/DotCompute.Hardware.Cuda.Tests.csproj"
)

# Parallel build groups (projects that can build in parallel)
declare -a PARALLEL_GROUP_1=(
    "src/Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj"
    "src/Core/DotCompute.Memory/DotCompute.Memory.csproj"
)

declare -a PARALLEL_GROUP_2=(
    "src/Runtime/DotCompute.Generators/DotCompute.Generators.csproj"
    "src/Runtime/DotCompute.Plugins/DotCompute.Plugins.csproj"
)

declare -a PARALLEL_GROUP_3=(
    "src/Backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj"
    "src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj"
    "src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj"
)

# Statistics
total_projects=0
successful_builds=0
failed_builds=0
total_errors=0
total_warnings=0

# JSON results array
results='[]'

# Function to build a single project
build_project() {
    local project_path="$1"
    local project_name=$(basename "$project_path" .csproj)
    local full_path="$PROJECT_ROOT/$project_path"

    if [ ! -f "$full_path" ]; then
        echo -e "${RED}✗ Project not found: $project_path${NC}"
        return 1
    fi

    echo -e "${BLUE}Building: ${NC}$project_name"

    # Create temp files for output
    local build_output=$(mktemp)
    local error_log=$(mktemp)

    # Build and capture output
    if dotnet build "$full_path" \
        --configuration Release \
        --no-restore \
        --verbosity quiet \
        > "$build_output" 2> "$error_log"; then

        # Count warnings/errors from output
        local warnings=$(grep -c "warning" "$build_output" 2>/dev/null || echo "0")
        local errors=$(grep -c "error" "$build_output" 2>/dev/null || echo "0")

        echo -e "${GREEN}✓ Success${NC} - Warnings: $warnings, Errors: $errors"

        # Add to results
        results=$(echo "$results" | jq --arg name "$project_name" \
            --arg path "$project_path" \
            --argjson warnings "$warnings" \
            --argjson errors "$errors" \
            '. + [{
                "project": $name,
                "path": $path,
                "status": "success",
                "warnings": $warnings,
                "errors": $errors,
                "timestamp": now
            }]')

        ((successful_builds++))
        ((total_warnings += warnings))
        ((total_errors += errors))

        rm -f "$build_output" "$error_log"
        return 0
    else
        # Build failed
        local warnings=$(grep -c "warning" "$error_log" 2>/dev/null || echo "0")
        local errors=$(grep -c "error" "$error_log" 2>/dev/null || echo "0")

        echo -e "${RED}✗ Failed${NC} - Warnings: $warnings, Errors: $errors"
        echo "Error details:"
        head -20 "$error_log" | sed 's/^/  /'

        # Add to results
        results=$(echo "$results" | jq --arg name "$project_name" \
            --arg path "$project_path" \
            --argjson warnings "$warnings" \
            --argjson errors "$errors" \
            --arg error_msg "$(cat "$error_log" | head -100)" \
            '. + [{
                "project": $name,
                "path": $path,
                "status": "failed",
                "warnings": $warnings,
                "errors": $errors,
                "error_message": $error_msg,
                "timestamp": now
            }]')

        ((failed_builds++))
        ((total_warnings += warnings))
        ((total_errors += errors))

        rm -f "$build_output" "$error_log"
        return 1
    fi
}

# Function to build projects in parallel
build_parallel_group() {
    local group_name="$1"
    shift
    local projects=("$@")

    echo -e "${YELLOW}=== Building Group: $group_name ===${NC}"

    local pids=()
    for project in "${projects[@]}"; do
        build_project "$project" &
        pids+=($!)
    done

    # Wait for all parallel builds
    for pid in "${pids[@]}"; do
        wait $pid || true
    done

    echo ""
}

# Main build sequence
echo -e "${YELLOW}Step 1: Restoring NuGet packages${NC}"
dotnet restore "$PROJECT_ROOT/DotCompute.sln" --verbosity quiet

echo -e "\n${YELLOW}Step 2: Building projects in dependency order${NC}\n"

for project in "${BUILD_ORDER[@]}"; do
    build_project "$project"
    ((total_projects++))
done

# Generate summary
echo -e "\n${BLUE}=== Build Validation Summary ===${NC}"
echo "Total Projects: $total_projects"
echo -e "Successful: ${GREEN}$successful_builds${NC}"
echo -e "Failed: ${RED}$failed_builds${NC}"
echo -e "Total Warnings: ${YELLOW}$total_warnings${NC}"
echo -e "Total Errors: ${RED}$total_errors${NC}"

# Calculate success rate
if [ $total_projects -gt 0 ]; then
    success_rate=$((successful_builds * 100 / total_projects))
    echo "Success Rate: ${success_rate}%"
fi

# Save results to JSON
summary=$(jq -n \
    --argjson total "$total_projects" \
    --argjson successful "$successful_builds" \
    --argjson failed "$failed_builds" \
    --argjson warnings "$total_warnings" \
    --argjson errors "$total_errors" \
    --argjson projects "$results" \
    '{
        "timestamp": now,
        "summary": {
            "total_projects": $total,
            "successful_builds": $successful,
            "failed_builds": $failed,
            "total_warnings": $warnings,
            "total_errors": $errors,
            "success_rate": ($successful / $total * 100)
        },
        "projects": $projects
    }')

echo "$summary" | jq . > "$RESULTS_FILE"
echo -e "\nResults saved to: $RESULTS_FILE"

# Exit with appropriate code
if [ $failed_builds -gt 0 ]; then
    exit 1
else
    exit 0
fi
