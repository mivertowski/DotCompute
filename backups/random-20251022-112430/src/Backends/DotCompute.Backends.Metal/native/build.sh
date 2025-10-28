#!/bin/bash
set -e

# Build script for DotCompute Metal native library
# This script builds the native Metal interop library for macOS

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${SCRIPT_DIR}/build"
INSTALL_DIR="${SCRIPT_DIR}/.."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if running on macOS
if [[ "$OSTYPE" != "darwin"* ]]; then
    log_error "This script must be run on macOS to build Metal native library"
    exit 1
fi

# Check macOS version
MACOS_VERSION=$(sw_vers -productVersion)
MACOS_MAJOR=$(echo $MACOS_VERSION | cut -d '.' -f 1)
MACOS_MINOR=$(echo $MACOS_VERSION | cut -d '.' -f 2)

if [ "$MACOS_MAJOR" -lt 10 ] || ([ "$MACOS_MAJOR" -eq 10 ] && [ "$MACOS_MINOR" -lt 13 ]); then
    log_error "macOS 10.13 or later is required for Metal compute shader support"
    exit 1
fi

log_info "Building on macOS $MACOS_VERSION"

# Check for required tools
if ! command -v cmake &> /dev/null; then
    log_error "CMake is required but not installed. Please install CMake."
    exit 1
fi

if ! xcode-select -p &> /dev/null; then
    log_error "Xcode command line tools are required but not installed."
    log_info "Please run: xcode-select --install"
    exit 1
fi

# Determine build configuration
BUILD_TYPE="${1:-Release}"
if [[ "$BUILD_TYPE" != "Debug" && "$BUILD_TYPE" != "Release" ]]; then
    log_warning "Invalid build type '$BUILD_TYPE', using 'Release'"
    BUILD_TYPE="Release"
fi

log_info "Building in $BUILD_TYPE mode"

# Clean previous build
if [ -d "$BUILD_DIR" ]; then
    log_info "Cleaning previous build directory"
    rm -rf "$BUILD_DIR"
fi

# Create build directory
mkdir -p "$BUILD_DIR"
cd "$BUILD_DIR"

# Configure CMake
log_info "Configuring CMake..."

CMAKE_ARGS=(
    -DCMAKE_BUILD_TYPE="$BUILD_TYPE"
    -DCMAKE_OSX_DEPLOYMENT_TARGET="10.13"
    -DCMAKE_INSTALL_PREFIX="$INSTALL_DIR"
)

# Add architecture-specific flags
ARCH=$(uname -m)
if [ "$ARCH" = "arm64" ]; then
    log_info "Building for Apple Silicon (arm64)"
    CMAKE_ARGS+=(-DCMAKE_OSX_ARCHITECTURES="arm64")
elif [ "$ARCH" = "x86_64" ]; then
    log_info "Building for Intel x86_64"
    CMAKE_ARGS+=(-DCMAKE_OSX_ARCHITECTURES="x86_64")
else
    log_warning "Unknown architecture '$ARCH', using system default"
fi

if ! cmake "${CMAKE_ARGS[@]}" ..; then
    log_error "CMake configuration failed"
    exit 1
fi

# Build the library
log_info "Building native library..."
if ! make -j$(sysctl -n hw.ncpu); then
    log_error "Build failed"
    exit 1
fi

# Check if the library was built successfully
DYLIB_PATH="$BUILD_DIR/libDotComputeMetal.dylib"
if [ ! -f "$DYLIB_PATH" ]; then
    log_error "Built library not found at expected path: $DYLIB_PATH"
    exit 1
fi

# Verify the library
log_info "Verifying built library..."
if ! file "$DYLIB_PATH" | grep -q "Mach-O"; then
    log_error "Built file is not a valid Mach-O library"
    exit 1
fi

# Check library dependencies
DEPS=$(otool -L "$DYLIB_PATH")
if ! echo "$DEPS" | grep -q "Metal.framework"; then
    log_error "Library is not linked against Metal framework"
    exit 1
fi

if ! echo "$DEPS" | grep -q "Foundation.framework"; then
    log_error "Library is not linked against Foundation framework"
    exit 1
fi

# Copy to parent directory
log_info "Installing library..."
cp "$DYLIB_PATH" "$INSTALL_DIR/libDotComputeMetal.dylib"

# Verify the copied library
if [ ! -f "$INSTALL_DIR/libDotComputeMetal.dylib" ]; then
    log_error "Failed to copy library to install directory"
    exit 1
fi

# Print library info
DYLIB_SIZE=$(stat -f%z "$INSTALL_DIR/libDotComputeMetal.dylib")
DYLIB_SIZE_KB=$((DYLIB_SIZE / 1024))

log_success "Successfully built Metal native library"
log_info "Library size: ${DYLIB_SIZE_KB}KB"
log_info "Library path: $INSTALL_DIR/libDotComputeMetal.dylib"

# Optional: Run basic symbol check
if command -v nm &> /dev/null; then
    log_info "Checking exported symbols..."
    SYMBOL_COUNT=$(nm -D "$INSTALL_DIR/libDotComputeMetal.dylib" 2>/dev/null | grep -c "DCMetal_" || true)
    if [ "$SYMBOL_COUNT" -gt 0 ]; then
        log_success "Found $SYMBOL_COUNT DCMetal_ exported symbols"
    else
        log_warning "No DCMetal_ symbols found in library"
    fi
fi

log_success "Build completed successfully!"