#!/bin/bash
#
# NuGet Package Signing Script (WSL-compatible)
# Signs all DotCompute v0.4.2-rc2 packages with Certum certificate
#
# Certificate Details:
# - Subject: CN=Michael Ivertowski, O=Michael Ivertowski, L=Uster, S=Zurich, C=CH
# - SHA-256 Fingerprint: 06406CF467075EDDED9D2FF6D0EF813DC08D6D726A2354C7FE5F7CFA94E9EC59
# - Valid until: 23.09.2026
#
# Requirements:
# - Certificate must be installed in Windows certificate store (CurrentUser\My)
# - Smart card reader must be connected with certificate card inserted
# - NuGet CLI (nuget.exe) must be available in Windows PATH or at default location
#
# WSL Note: This script calls Windows nuget.exe from WSL to access Windows certificate store

set -e  # Exit on error

# Configuration
CERT_FINGERPRINT="06406CF467075EDDED9D2FF6D0EF813DC08D6D726A2354C7FE5F7CFA94E9EC59"  # SHA-256
CERT_STORE_NAME="My"
CERT_STORE_LOCATION="CurrentUser"
TIMESTAMPER="http://time.certum.pl"
NUPKG_DIR="artifacts/packages"

# Try to find nuget.exe in common Windows locations
NUGET_EXE=""
NUGET_PATHS=(
    "/mnt/c/ProgramData/chocolatey/bin/nuget.exe"
    "/mnt/c/Program Files/NuGet/nuget.exe"
    "/mnt/c/Tools/nuget.exe"
    "$HOME/.nuget/nuget.exe"
)

# Check if nuget.exe is in Windows PATH
if command -v nuget.exe &> /dev/null; then
    NUGET_EXE="nuget.exe"
else
    # Search common locations
    for path in "${NUGET_PATHS[@]}"; do
        if [ -f "$path" ]; then
            NUGET_EXE="$path"
            break
        fi
    done
fi

# Color output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  DotCompute v0.4.2-rc2 Package Signing${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check if nuget.exe was found
if [ -z "$NUGET_EXE" ]; then
    echo -e "${RED}Error: nuget.exe not found!${NC}"
    echo ""
    echo "Please install NuGet CLI:"
    echo "  Windows: choco install nuget.commandline"
    echo "  Or download from: https://www.nuget.org/downloads"
    echo ""
    echo "After installation, you may need to add it to your PATH or place it in:"
    echo "  C:\\Tools\\nuget.exe"
    exit 1
fi

echo -e "${GREEN}Found NuGet CLI: ${NC}$NUGET_EXE"
echo ""

# Check if nupkgs directory exists
if [ ! -d "$NUPKG_DIR" ]; then
    echo -e "${RED}Error: Directory '$NUPKG_DIR' not found!${NC}"
    exit 1
fi

# Count packages
PACKAGE_COUNT=$(find "$NUPKG_DIR" -maxdepth 1 -type f \( -name "*.nupkg" -o -name "*.snupkg" \) 2>/dev/null | wc -l)
if [ "$PACKAGE_COUNT" -eq 0 ]; then
    echo -e "${RED}Error: No packages found in '$NUPKG_DIR'!${NC}"
    exit 1
fi

echo -e "${GREEN}Found $PACKAGE_COUNT package(s) to sign${NC}"
echo ""
echo "Certificate Details:"
echo "  SHA-256 Fingerprint: $CERT_FINGERPRINT"
echo "  Store: $CERT_STORE_LOCATION\\$CERT_STORE_NAME"
echo "  Timestamper: $TIMESTAMPER"
echo ""
echo -e "${YELLOW}Note: Please ensure your smart card is inserted and unlocked.${NC}"
echo ""

# Prompt for confirmation
read -r -p "$(echo -e "${YELLOW}Press ENTER to start signing, or Ctrl+C to cancel...${NC}")"

echo ""
echo -e "${BLUE}Starting package signing...${NC}"
echo ""

# Convert WSL path to Windows path
WIN_NUPKG_DIR=$(wslpath -w "$NUPKG_DIR" 2>/dev/null || echo "$NUPKG_DIR")

# Sign all .nupkg and .snupkg files
SIGNED_COUNT=0
FAILED_COUNT=0

for package in "$NUPKG_DIR"/*.nupkg "$NUPKG_DIR"/*.snupkg; do
    if [ -f "$package" ]; then
        PACKAGE_NAME=$(basename "$package")
        WIN_PACKAGE=$(wslpath -w "$package" 2>/dev/null || echo "$package")

        echo -e "${BLUE}Signing: ${NC}$PACKAGE_NAME"

        # Call Windows nuget.exe from WSL
        if "$NUGET_EXE" sign "$WIN_PACKAGE" \
            -CertificateFingerprint "$CERT_FINGERPRINT" \
            -CertificateStoreName "$CERT_STORE_NAME" \
            -CertificateStoreLocation "$CERT_STORE_LOCATION" \
            -Timestamper "$TIMESTAMPER" \
            -Overwrite 2>&1; then
            echo -e "${GREEN}✓ Successfully signed: $PACKAGE_NAME${NC}"
            SIGNED_COUNT=$((SIGNED_COUNT + 1))
        else
            echo -e "${RED}✗ Failed to sign: $PACKAGE_NAME${NC}"
            FAILED_COUNT=$((FAILED_COUNT + 1))
        fi
        echo ""
    fi
done

# Summary
echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Signing Summary (v0.4.2-rc2)${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Successfully signed: $SIGNED_COUNT${NC}"
if [ "$FAILED_COUNT" -gt 0 ]; then
    echo -e "${RED}Failed: $FAILED_COUNT${NC}"
else
    echo -e "Failed: 0"
fi
echo ""

if [ "$FAILED_COUNT" -eq 0 ]; then
    echo -e "${GREEN}✓ All packages signed successfully!${NC}"
    echo ""
    echo "Signed packages are ready for publishing to NuGet.org:"
    echo "  dotnet nuget push artifacts/packages/*.nupkg --source https://api.nuget.org/v3/index.json --api-key <your-api-key>"
    exit 0
else
    echo -e "${RED}✗ Some packages failed to sign. Please review the errors above.${NC}"
    echo ""
    echo "Common issues:"
    echo "  - Smart card not inserted or locked (enter PIN)"
    echo "  - Certificate not in CurrentUser\\My store"
    echo "  - Certificate fingerprint mismatch"
    echo "  - NuGet CLI version too old (requires 5.0+)"
    exit 1
fi
