#!/bin/bash
set -euo pipefail

# Build script for DotCompute CI/CD Pipeline
# Usage: ./build.sh [configuration] [skip-tests]

CONFIGURATION="${1:-Release}"
SKIP_TESTS="${2:-false}"
BUILD_NUMBER="${GITHUB_RUN_NUMBER:-local}"
BRANCH_NAME="${GITHUB_REF_NAME:-local}"

echo "🚀 DotCompute Build Script"
echo "Configuration: $CONFIGURATION"
echo "Skip Tests: $SKIP_TESTS"
echo "Build Number: $BUILD_NUMBER"
echo "Branch: $BRANCH_NAME"
echo "================================"

# Set up directories
mkdir -p artifacts/packages
mkdir -p artifacts/coverage
mkdir -p artifacts/reports

# Restore dependencies
echo "📦 Restoring dependencies..."
dotnet restore --locked-mode

# GitVersion (if available)
if command -v dotnet-gitversion &> /dev/null; then
    echo "🏷️  Determining version..."
    VERSION=$(dotnet-gitversion /showVariable NuGetVersionV2)
    ASSEMBLY_VERSION=$(dotnet-gitversion /showVariable AssemblySemVer)
    FILE_VERSION=$(dotnet-gitversion /showVariable AssemblySemFileVer)
    INFORMATIONAL_VERSION=$(dotnet-gitversion /showVariable InformationalVersion)
    
    echo "Version: $VERSION"
    echo "Assembly Version: $ASSEMBLY_VERSION"
    echo "File Version: $FILE_VERSION"
    echo "Informational Version: $INFORMATIONAL_VERSION"
else
    echo "⚠️  GitVersion not found, using default versioning"
    VERSION="0.1.0-local.${BUILD_NUMBER}"
    ASSEMBLY_VERSION="0.1.0"
    FILE_VERSION="0.1.0.${BUILD_NUMBER}"
    INFORMATIONAL_VERSION="0.1.0-local.${BUILD_NUMBER}+${BRANCH_NAME}"
fi

# Build solution
echo "🔨 Building solution..."
dotnet build \
    --configuration "$CONFIGURATION" \
    --no-restore \
    --verbosity minimal \
    -p:Version="$VERSION" \
    -p:AssemblyVersion="$ASSEMBLY_VERSION" \
    -p:FileVersion="$FILE_VERSION" \
    -p:InformationalVersion="$INFORMATIONAL_VERSION" \
    -p:TreatWarningsAsErrors=false \
    -p:ContinuousIntegrationBuild=true

# Run tests (if not skipped)
if [ "$SKIP_TESTS" != "true" ]; then
    echo "🧪 Running tests..."
    dotnet test \
        --configuration "$CONFIGURATION" \
        --no-build \
        --verbosity minimal \
        --logger trx \
        --logger "console;verbosity=minimal" \
        --collect:"XPlat Code Coverage" \
        --results-directory ./artifacts/coverage/ \
        --settings coverlet.runsettings \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
        
    echo "✅ Tests completed"
else
    echo "⏭️  Tests skipped"
fi

# Create NuGet packages
echo "📦 Creating NuGet packages..."
dotnet pack \
    --configuration "$CONFIGURATION" \
    --no-build \
    --verbosity minimal \
    --output ./artifacts/packages/ \
    -p:PackageVersion="$VERSION" \
    -p:AssemblyVersion="$ASSEMBLY_VERSION" \
    -p:FileVersion="$FILE_VERSION" \
    -p:InformationalVersion="$INFORMATIONAL_VERSION"

# Display package information
echo "📋 Created packages:"
ls -la ./artifacts/packages/

echo "✅ Build completed successfully!"
echo "Version: $VERSION"
echo "Packages: $(ls ./artifacts/packages/*.nupkg | wc -l)"

if [ "$SKIP_TESTS" != "true" ]; then
    echo "Test Results: ./artifacts/coverage/"
fi