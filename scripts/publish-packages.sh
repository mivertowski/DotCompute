#!/bin/bash
#
# NuGet Package Publishing Script
# Publishes all DotCompute packages to NuGet.org
#
# Requirements:
# - All packages must be signed
# - NuGet API key must be configured
# - dotnet SDK 9.0+ installed
#
# Setup NuGet API Key:
#   dotnet nuget push --help
#   # Store API key (one-time):
#   dotnet nuget push --source https://api.nuget.org/v3/index.json --api-key YOUR_API_KEY
#   # Or set environment variable:
#   export NUGET_API_KEY="your-api-key-here"

set -e  # Exit on error

# Configuration
NUGET_SOURCE="https://api.nuget.org/v3/index.json"
NUPKG_DIR="artifacts/packages"
SKIP_DUPLICATE="true"
NO_SYMBOLS="true"  # Skip symbol packages (no debug symbols in release build)

# Color output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  DotCompute Package Publishing${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check if nupkgs directory exists
if [ ! -d "$NUPKG_DIR" ]; then
    echo -e "${RED}Error: Directory '$NUPKG_DIR' not found!${NC}"
    exit 1
fi

# Check for API key
if [ -z "$NUGET_API_KEY" ]; then
    echo -e "${YELLOW}Warning: NUGET_API_KEY environment variable not set.${NC}"
    echo -e "${YELLOW}The command will use the API key stored in dotnet configuration.${NC}"
    echo -e "${YELLOW}If you haven't configured it, the push will fail.${NC}"
    echo ""
    read -p "$(echo -e ${YELLOW}Press ENTER to continue, or Ctrl+C to cancel...${NC})"
fi

# Count packages (exclude symbol packages if NO_SYMBOLS is true)
if [ "$NO_SYMBOLS" = "true" ]; then
    PACKAGE_COUNT=$(ls -1 "$NUPKG_DIR"/*.nupkg 2>/dev/null | wc -l)
else
    PACKAGE_COUNT=$(find "$NUPKG_DIR" -name "*.nupkg" -o -name "*.snupkg" | wc -l)
fi

if [ "$PACKAGE_COUNT" -eq 0 ]; then
    echo -e "${RED}Error: No packages found in '$NUPKG_DIR'!${NC}"
    exit 1
fi

echo -e "${GREEN}Found $PACKAGE_COUNT package(s) to publish${NC}"
echo ""
echo "Configuration:"
echo "  NuGet Source: $NUGET_SOURCE"
echo "  Skip Duplicate: $SKIP_DUPLICATE"
echo "  Publish Symbols: $([ "$NO_SYMBOLS" = "true" ] && echo "No" || echo "Yes")"
echo ""

# Final confirmation
echo -e "${YELLOW}⚠️  WARNING: This will publish packages to NuGet.org!${NC}"
echo -e "${YELLOW}⚠️  Published packages cannot be deleted, only unlisted.${NC}"
echo ""
read -p "$(echo -e ${RED}Type 'YES' to confirm publication: ${NC})" confirmation

if [ "$confirmation" != "YES" ]; then
    echo -e "${YELLOW}Publication cancelled.${NC}"
    exit 0
fi

echo ""
echo -e "${BLUE}Starting package publication...${NC}"
echo ""

# Publish all packages
PUBLISHED_COUNT=0
SKIPPED_COUNT=0
FAILED_COUNT=0

# Publish main packages (.nupkg)
for package in "$NUPKG_DIR"/*.nupkg; do
    if [ -f "$package" ]; then
        PACKAGE_NAME=$(basename "$package")
        echo -e "${BLUE}Publishing: ${NC}$PACKAGE_NAME"

        # Build dotnet nuget push command
        PUSH_CMD="dotnet nuget push \"$package\" --source \"$NUGET_SOURCE\""

        if [ "$SKIP_DUPLICATE" = "true" ]; then
            PUSH_CMD="$PUSH_CMD --skip-duplicate"
        fi

        if [ "$NO_SYMBOLS" = "true" ]; then
            PUSH_CMD="$PUSH_CMD --no-symbols"
        fi

        if [ -n "$NUGET_API_KEY" ]; then
            PUSH_CMD="$PUSH_CMD --api-key \"$NUGET_API_KEY\""
        fi

        # Execute push command
        if eval $PUSH_CMD 2>&1; then
            if echo "$?" | grep -q "409"; then
                echo -e "${YELLOW}⊘ Skipped (already exists): $PACKAGE_NAME${NC}"
                SKIPPED_COUNT=$((SKIPPED_COUNT + 1))
            else
                echo -e "${GREEN}✓ Successfully published: $PACKAGE_NAME${NC}"
                PUBLISHED_COUNT=$((PUBLISHED_COUNT + 1))
            fi
        else
            EXIT_CODE=$?
            if [ $EXIT_CODE -eq 409 ]; then
                echo -e "${YELLOW}⊘ Skipped (already exists): $PACKAGE_NAME${NC}"
                SKIPPED_COUNT=$((SKIPPED_COUNT + 1))
            else
                echo -e "${RED}✗ Failed to publish: $PACKAGE_NAME${NC}"
                FAILED_COUNT=$((FAILED_COUNT + 1))
            fi
        fi
        echo ""
    fi
done

# Publish symbol packages (.snupkg) if enabled
if [ "$NO_SYMBOLS" = "false" ]; then
    for package in "$NUPKG_DIR"/*.snupkg; do
        if [ -f "$package" ]; then
            PACKAGE_NAME=$(basename "$package")
            echo -e "${BLUE}Publishing symbols: ${NC}$PACKAGE_NAME"

            PUSH_CMD="dotnet nuget push \"$package\" --source \"$NUGET_SOURCE\""

            if [ "$SKIP_DUPLICATE" = "true" ]; then
                PUSH_CMD="$PUSH_CMD --skip-duplicate"
            fi

            if [ -n "$NUGET_API_KEY" ]; then
                PUSH_CMD="$PUSH_CMD --api-key \"$NUGET_API_KEY\""
            fi

            if eval $PUSH_CMD 2>&1; then
                echo -e "${GREEN}✓ Successfully published: $PACKAGE_NAME${NC}"
                PUBLISHED_COUNT=$((PUBLISHED_COUNT + 1))
            else
                if [ $? -eq 409 ]; then
                    echo -e "${YELLOW}⊘ Skipped (already exists): $PACKAGE_NAME${NC}"
                    SKIPPED_COUNT=$((SKIPPED_COUNT + 1))
                else
                    echo -e "${RED}✗ Failed to publish: $PACKAGE_NAME${NC}"
                    FAILED_COUNT=$((FAILED_COUNT + 1))
                fi
            fi
            echo ""
        fi
    done
fi

# Summary
echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Publishing Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Successfully published: $PUBLISHED_COUNT${NC}"
echo -e "${YELLOW}Skipped (duplicates): $SKIPPED_COUNT${NC}"
if [ "$FAILED_COUNT" -gt 0 ]; then
    echo -e "${RED}Failed: $FAILED_COUNT${NC}"
else
    echo -e "Failed: 0"
fi
echo ""

if [ "$FAILED_COUNT" -eq 0 ]; then
    echo -e "${GREEN}Publication completed successfully!${NC}"
    echo ""
    echo "View your packages at:"
    echo "  https://www.nuget.org/profiles/YOUR_PROFILE"
    echo ""
    echo "It may take a few minutes for packages to appear in search."
    exit 0
else
    echo -e "${RED}Some packages failed to publish. Please review the errors above.${NC}"
    exit 1
fi
