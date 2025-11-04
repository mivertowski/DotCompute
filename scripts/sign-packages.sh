#!/bin/bash
#
# NuGet Package Signing Script
# Signs all DotCompute v0.2.0-alpha packages with Certum certificate
#
# Certificate Details:
# - Subject: CN=Michael Ivertowski, O=Michael Ivertowski, L=Uster, S=Zurich, C=CH
# - Thumbprint: D4CF73C16E699353F1D2222237D2250448850D2B
# - Valid until: 23.09.2026
#
# Requirements:
# - Certificate must be installed in Windows certificate store (CurrentUser\My)
# - Smart card reader must be connected with certificate card inserted
# - dotnet SDK 9.0+ installed

set -e  # Exit on error

# Configuration
CERT_FINGERPRINT="06406CF467075EDDED9D2FF6D0EF813DC08D6D726A2354C7FE5F7CFA94E9EC59"  # SHA-256
CERT_STORE_NAME="My"
CERT_STORE_LOCATION="CurrentUser"
TIMESTAMPER="http://time.certum.pl"
NUPKG_DIR="nupkgs"

# Color output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  DotCompute v0.2.0-alpha Package Signing${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check if nupkgs directory exists
if [ ! -d "$NUPKG_DIR" ]; then
    echo -e "${RED}Error: Directory '$NUPKG_DIR' not found!${NC}"
    exit 1
fi

# Count packages
PACKAGE_COUNT=$(ls -1 "$NUPKG_DIR"/*.nupkg "$NUPKG_DIR"/*.snupkg 2>/dev/null | wc -l)
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

# Prompt for confirmation
read -p "$(echo -e ${YELLOW}Press ENTER to start signing, or Ctrl+C to cancel...${NC})"

echo ""
echo -e "${BLUE}Starting package signing...${NC}"
echo ""

# Sign all .nupkg and .snupkg files
SIGNED_COUNT=0
FAILED_COUNT=0

for package in "$NUPKG_DIR"/*.nupkg "$NUPKG_DIR"/*.snupkg; do
    if [ -f "$package" ]; then
        PACKAGE_NAME=$(basename "$package")
        echo -e "${BLUE}Signing: ${NC}$PACKAGE_NAME"

        if dotnet nuget sign "$package" \
            --certificate-fingerprint "$CERT_FINGERPRINT" \
            --certificate-store-name "$CERT_STORE_NAME" \
            --certificate-store-location "$CERT_STORE_LOCATION" \
            --timestamper "$TIMESTAMPER" \
            --overwrite 2>&1; then
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
echo -e "${BLUE}  Signing Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}Successfully signed: $SIGNED_COUNT${NC}"
if [ "$FAILED_COUNT" -gt 0 ]; then
    echo -e "${RED}Failed: $FAILED_COUNT${NC}"
else
    echo -e "Failed: 0"
fi
echo ""

if [ "$FAILED_COUNT" -eq 0 ]; then
    echo -e "${GREEN}All packages signed successfully!${NC}"
    exit 0
else
    echo -e "${RED}Some packages failed to sign. Please review the errors above.${NC}"
    exit 1
fi
