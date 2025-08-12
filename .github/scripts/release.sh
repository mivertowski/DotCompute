#!/bin/bash
set -euo pipefail

# Release script for DotCompute
# Usage: ./release.sh [version] [prerelease]

VERSION="${1:-}"
PRERELEASE="${2:-false}"
DRY_RUN="${DRY_RUN:-false}"

if [ -z "$VERSION" ]; then
    echo "❌ Error: Version is required"
    echo "Usage: $0 <version> [prerelease]"
    echo "Examples:"
    echo "  $0 1.0.0"
    echo "  $0 1.0.0-alpha.1 true"
    exit 1
fi

echo "🚀 DotCompute Release Script"
echo "Version: $VERSION"
echo "Prerelease: $PRERELEASE"
echo "Dry Run: $DRY_RUN"
echo "================================"

# Validate version format
if ! [[ $VERSION =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.-]+)?$ ]]; then
    echo "❌ Error: Invalid version format. Expected: x.y.z or x.y.z-prerelease"
    exit 1
fi

# Check if we're on main or develop branch
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [[ "$CURRENT_BRANCH" != "main" && "$CURRENT_BRANCH" != "develop" ]]; then
    echo "❌ Error: Releases can only be created from main or develop branch"
    echo "Current branch: $CURRENT_BRANCH"
    exit 1
fi

# Check if working directory is clean
if ! git diff-index --quiet HEAD --; then
    echo "❌ Error: Working directory is not clean"
    echo "Please commit or stash your changes before creating a release"
    exit 1
fi

# Check if tag already exists
if git rev-parse "v$VERSION" >/dev/null 2>&1; then
    echo "❌ Error: Tag v$VERSION already exists"
    exit 1
fi

# Update version in Directory.Build.props
echo "📝 Updating version in Directory.Build.props..."
if [ "$DRY_RUN" = "false" ]; then
    sed -i.bak "s|<VersionPrefix>.*</VersionPrefix>|<VersionPrefix>$(echo $VERSION | cut -d'-' -f1)</VersionPrefix>|" Directory.Build.props
    
    if [[ $VERSION == *"-"* ]]; then
        SUFFIX=$(echo $VERSION | cut -d'-' -f2-)
        sed -i.bak "s|<VersionSuffix>.*</VersionSuffix>|<VersionSuffix>$SUFFIX</VersionSuffix>|" Directory.Build.props
    else
        sed -i.bak "s|<VersionSuffix>.*</VersionSuffix>|<VersionSuffix></VersionSuffix>|" Directory.Build.props
    fi
    
    rm Directory.Build.props.bak
fi

# Run build and tests
echo "🔨 Building and testing..."
if [ "$DRY_RUN" = "false" ]; then
    ./scripts/ci/build.sh Release false
fi

# Generate changelog
echo "📋 Generating changelog..."
PREVIOUS_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
CHANGELOG_FILE="artifacts/CHANGELOG-$VERSION.md"

if [ "$DRY_RUN" = "false" ]; then
    mkdir -p artifacts
    
    echo "# Release $VERSION" > "$CHANGELOG_FILE"
    echo "" >> "$CHANGELOG_FILE"
    echo "## What's Changed" >> "$CHANGELOG_FILE"
    echo "" >> "$CHANGELOG_FILE"
    
    if [ -n "$PREVIOUS_TAG" ]; then
        git log --pretty=format:"* %s (%h)" "$PREVIOUS_TAG"..HEAD >> "$CHANGELOG_FILE"
    else
        echo "* Initial release" >> "$CHANGELOG_FILE"
    fi
    
    echo "" >> "$CHANGELOG_FILE"
    echo "" >> "$CHANGELOG_FILE"
    echo "## NuGet Packages" >> "$CHANGELOG_FILE"
    echo "" >> "$CHANGELOG_FILE"
    
    for package in ./artifacts/packages/*.nupkg; do
        if [[ $package != *".symbols.nupkg" ]]; then
            basename=$(basename "$package")
            package_name=${basename%.*}
            echo "* [$package_name](https://www.nuget.org/packages/$package_name)" >> "$CHANGELOG_FILE"
        fi
    done
    
    echo "📋 Changelog generated: $CHANGELOG_FILE"
fi

# Create and push tag
echo "🏷️  Creating tag v$VERSION..."
if [ "$DRY_RUN" = "false" ]; then
    git add Directory.Build.props
    git commit -m "chore: bump version to $VERSION"
    git tag -a "v$VERSION" -m "Release $VERSION"
    
    echo "📤 Pushing tag..."
    git push origin "v$VERSION"
    git push origin "$CURRENT_BRANCH"
fi

# Create GitHub release (if GitHub CLI is available)
if command -v gh &> /dev/null && [ "$DRY_RUN" = "false" ]; then
    echo "🎉 Creating GitHub release..."
    
    RELEASE_NOTES=""
    if [ -f "$CHANGELOG_FILE" ]; then
        RELEASE_NOTES=$(cat "$CHANGELOG_FILE")
    fi
    
    PRERELEASE_FLAG=""
    if [ "$PRERELEASE" = "true" ]; then
        PRERELEASE_FLAG="--prerelease"
    fi
    
    gh release create "v$VERSION" \
        --title "Release $VERSION" \
        --notes "$RELEASE_NOTES" \
        $PRERELEASE_FLAG \
        ./artifacts/packages/*.nupkg \
        ./artifacts/packages/*.snupkg
        
    echo "🎉 GitHub release created: https://github.com/$(git remote get-url origin | sed 's/.*github.com[:/]\([^.]*\).*/\1/')/releases/tag/v$VERSION"
else
    echo "ℹ️  GitHub CLI not available or dry run mode. Skipping GitHub release creation."
fi

echo "✅ Release $VERSION completed successfully!"
echo ""
echo "Next steps:"
echo "1. The CI/CD pipeline will automatically publish packages to NuGet"
echo "2. Monitor the GitHub Actions workflow for completion"
echo "3. Verify packages are available on NuGet.org"

if [ "$PRERELEASE" = "true" ]; then
    echo "4. Consider promoting to stable release when ready"
fi