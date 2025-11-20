#!/bin/bash
set -euo pipefail

# Environment setup script for DotCompute CI/CD
# Usage: ./setup-environment.sh [environment]

ENVIRONMENT="${1:-development}"
CI="${CI:-false}"

echo "🔧 Setting up DotCompute environment"
echo "Environment: $ENVIRONMENT"
echo "CI Mode: $CI"
echo "================================"

# Install .NET SDK if not available
if ! command -v dotnet &> /dev/null; then
    echo "📦 Installing .NET SDK..."
    
    if [[ "$OSTYPE" == "linux-gnu"* ]]; then
        # Linux
        wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
        sudo dpkg -i packages-microsoft-prod.deb
        rm packages-microsoft-prod.deb
        sudo apt-get update
        sudo apt-get install -y apt-transport-https
        sudo apt-get update
        sudo apt-get install -y dotnet-sdk-9.0
    elif [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        if command -v brew &> /dev/null; then
            brew install --cask dotnet
        else
            echo "Please install Homebrew first: https://brew.sh/"
            exit 1
        fi
    else
        echo "Please install .NET SDK manually: https://dotnet.microsoft.com/download"
        exit 1
    fi
else
    echo "✅ .NET SDK already installed"
    dotnet --version
fi

# Install GitVersion if in CI
if [ "$CI" = "true" ]; then
    echo "📦 Installing GitVersion..."
    dotnet tool install --global GitVersion.Tool --version 5.12.0 || echo "GitVersion already installed"
fi

# Install ReportGenerator for coverage reports
echo "📦 Installing ReportGenerator..."
dotnet tool install --global dotnet-reportgenerator-globaltool || echo "ReportGenerator already installed"

# Set up directories
echo "📁 Creating directories..."
mkdir -p artifacts/{packages,coverage,reports,logs}
mkdir -p .nuget/packages

# Restore tools
echo "🔧 Restoring tools..."
if [ -f ".config/dotnet-tools.json" ]; then
    dotnet tool restore
fi

# WSL2 CUDA Configuration
if grep -qi microsoft /proc/version 2>/dev/null; then
    echo "🪟 Detected WSL2 environment"

    # WSL2 requires special NVIDIA library path
    if [ -d "/usr/lib/wsl/lib" ]; then
        echo "✅ Configuring WSL2 NVIDIA driver path..."

        # Add WSL2 NVIDIA libraries to LD_LIBRARY_PATH
        if [[ ":$LD_LIBRARY_PATH:" != *":/usr/lib/wsl/lib:"* ]]; then
            export LD_LIBRARY_PATH="/usr/lib/wsl/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
            echo "   LD_LIBRARY_PATH=$LD_LIBRARY_PATH"
        fi

        # Verify CUDA drivers are accessible
        if [ -f "/usr/lib/wsl/lib/libcuda.so" ]; then
            echo "✅ WSL2 CUDA drivers found"
        else
            echo "⚠️  WSL2 CUDA drivers not found - GPU tests may fail"
        fi
    else
        echo "⚠️  /usr/lib/wsl/lib not found - GPU tests may fail"
        echo "   Install NVIDIA drivers on Windows host for WSL2 GPU support"
    fi
fi

# Environment-specific setup
case $ENVIRONMENT in
    "production")
        echo "🏭 Setting up production environment..."
        export DOTNET_ENVIRONMENT=Production
        export ASPNETCORE_ENVIRONMENT=Production
        ;;
    "staging")
        echo "🎭 Setting up staging environment..."
        export DOTNET_ENVIRONMENT=Staging
        export ASPNETCORE_ENVIRONMENT=Staging
        ;;
    "development")
        echo "🔨 Setting up development environment..."
        export DOTNET_ENVIRONMENT=Development
        export ASPNETCORE_ENVIRONMENT=Development
        ;;
esac

# Set CI-specific variables
if [ "$CI" = "true" ]; then
    echo "🤖 Configuring CI environment..."
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true
    export DOTNET_NOLOGO=true
    export DOTNET_CLI_TELEMETRY_OPTOUT=true
    export NUGET_PACKAGES=$PWD/.nuget/packages
fi

# Validate environment
echo "🔍 Validating environment..."
dotnet --info

# Check for required tools
TOOLS_OK=true

if ! dotnet tool list -g | grep -q "gitversion.tool" && [ "$CI" = "true" ]; then
    echo "❌ GitVersion not found"
    TOOLS_OK=false
fi

if ! dotnet tool list -g | grep -q "dotnet-reportgenerator-globaltool"; then
    echo "❌ ReportGenerator not found"
    TOOLS_OK=false
fi

if [ "$TOOLS_OK" = "true" ]; then
    echo "✅ All required tools installed"
else
    echo "❌ Some tools are missing"
    exit 1
fi

# Create environment info file
echo "📄 Creating environment info..."
cat > artifacts/environment-info.txt << EOF
DotCompute Environment Information
Generated: $(date)
Environment: $ENVIRONMENT
CI Mode: $CI
OS: $OSTYPE
.NET Version: $(dotnet --version)
EOF

# WSL2 detection
if grep -qi microsoft /proc/version 2>/dev/null; then
    echo "WSL2: Yes" >> artifacts/environment-info.txt
    echo "LD_LIBRARY_PATH: $LD_LIBRARY_PATH" >> artifacts/environment-info.txt
    echo "CUDA Drivers: $([ -f /usr/lib/wsl/lib/libcuda.so ] && echo 'Found' || echo 'Not Found')" >> artifacts/environment-info.txt
else
    echo "WSL2: No" >> artifacts/environment-info.txt
fi

if command -v git &> /dev/null; then
    echo "Git Version: $(git --version)" >> artifacts/environment-info.txt
    echo "Git Commit: $(git rev-parse HEAD 2>/dev/null || echo 'Not a git repository')" >> artifacts/environment-info.txt
fi

echo ""
echo "✅ Environment setup completed successfully!"
echo "📄 Environment info saved to: artifacts/environment-info.txt"
echo ""
echo "Next steps:"
echo "1. Run build: ./scripts/ci/build.sh"
echo "2. Run tests: dotnet test"
echo "3. Run quality checks: ./scripts/ci/quality-check.sh"