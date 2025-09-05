#!/bin/bash

# CUDA Setup Verification Script for DotCompute
# This script verifies that CUDA is properly installed and configured for the DotCompute project

echo "=========================================="
echo "CUDA Setup Verification for DotCompute"
echo "=========================================="
echo ""

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Track overall status
SETUP_OK=true

# Function to print colored status
print_status() {
    if [ "$1" = "OK" ]; then
        echo -e "${GREEN}✓${NC} $2"
    elif [ "$1" = "FAIL" ]; then
        echo -e "${RED}✗${NC} $2"
        SETUP_OK=false
    else
        echo -e "${YELLOW}⚠${NC} $2"
    fi
}

# 1. Check nvidia-smi
echo "1. Checking NVIDIA Driver and GPU..."
if command -v nvidia-smi &> /dev/null; then
    print_status "OK" "nvidia-smi found"
    
    # Get GPU info
    GPU_INFO=$(nvidia-smi --query-gpu=name,compute_cap,driver_version --format=csv,noheader 2>/dev/null)
    if [ $? -eq 0 ]; then
        echo "   GPU: $GPU_INFO"
        
        # Check for RTX 2000 Ada (compute capability 8.9)
        if echo "$GPU_INFO" | grep -q "8.9"; then
            print_status "OK" "RTX 2000 Ada Generation GPU detected (Compute Capability 8.9)"
        fi
    else
        print_status "FAIL" "Could not query GPU information"
    fi
else
    print_status "FAIL" "nvidia-smi not found - NVIDIA drivers may not be installed"
fi

echo ""

# 2. Check CUDA toolkit
echo "2. Checking CUDA Toolkit Installation..."
if command -v nvcc &> /dev/null; then
    CUDA_VERSION=$(nvcc --version | grep "release" | sed 's/.*release //' | sed 's/,.*//')
    print_status "OK" "CUDA compiler (nvcc) found - Version $CUDA_VERSION"
else
    print_status "WARN" "nvcc not found - CUDA toolkit may not be in PATH"
fi

# Check CUDA installation directory
CUDA_DIRS=("/usr/local/cuda" "/usr/local/cuda-13" "/usr/local/cuda-13.0" "/usr/local/cuda-12" "/usr/local/cuda-12.8")
CUDA_FOUND=false

for dir in "${CUDA_DIRS[@]}"; do
    if [ -d "$dir" ]; then
        print_status "OK" "CUDA installation found at $dir"
        CUDA_FOUND=true
        
        # Check for version file
        if [ -f "$dir/version.txt" ]; then
            echo "   Version: $(cat $dir/version.txt)"
        elif [ -f "$dir/version.json" ]; then
            echo "   Version info available in $dir/version.json"
        fi
        break
    fi
done

if [ "$CUDA_FOUND" = false ]; then
    print_status "FAIL" "No CUDA installation directory found"
fi

echo ""

# 3. Check CUDA Runtime Libraries
echo "3. Checking CUDA Runtime Libraries..."

# Function to check library existence
check_library() {
    local lib_path=$1
    if [ -f "$lib_path" ] || [ -L "$lib_path" ]; then
        # Get actual file if it's a symlink
        if [ -L "$lib_path" ]; then
            local target=$(readlink -f "$lib_path")
            print_status "OK" "$lib_path -> $target"
        else
            print_status "OK" "$lib_path"
        fi
        return 0
    else
        return 1
    fi
}

# Check for CUDA runtime libraries
RUNTIME_FOUND=false

# WSL specific check
if [ -d "/usr/lib/wsl/lib" ]; then
    echo "   WSL environment detected, checking WSL libraries:"
    check_library "/usr/lib/wsl/lib/libcuda.so.1" && RUNTIME_FOUND=true
    check_library "/usr/lib/wsl/lib/libcudart.so.1" && RUNTIME_FOUND=true
fi

# Check standard locations
echo "   Checking standard library locations:"
CUDA_LIBS=(
    "/usr/local/cuda/targets/x86_64-linux/lib/libcudart.so.13"
    "/usr/local/cuda/targets/x86_64-linux/lib/libcudart.so"
    "/usr/local/cuda-13.0/targets/x86_64-linux/lib/libcudart.so.13.0.48"
    "/usr/local/cuda/lib64/libcudart.so"
    "/usr/lib/x86_64-linux-gnu/libcudart.so"
    "/usr/local/cuda-12/targets/x86_64-linux/lib/libcudart.so.12"
)

for lib in "${CUDA_LIBS[@]}"; do
    if check_library "$lib"; then
        RUNTIME_FOUND=true
    fi
done

if [ "$RUNTIME_FOUND" = false ]; then
    print_status "FAIL" "No CUDA runtime libraries found"
fi

echo ""

# 4. Check ldconfig
echo "4. Checking Library Configuration (ldconfig)..."
if command -v ldconfig &> /dev/null; then
    CUDA_IN_LDCONFIG=$(ldconfig -p 2>/dev/null | grep -E "libcudart|libcuda\.so" | head -5)
    if [ -n "$CUDA_IN_LDCONFIG" ]; then
        print_status "OK" "CUDA libraries found in ldconfig cache:"
        echo "$CUDA_IN_LDCONFIG" | while read line; do
            echo "   $line"
        done
    else
        print_status "WARN" "CUDA libraries not found in ldconfig cache"
        echo "   You may need to run: sudo ldconfig"
    fi
else
    print_status "WARN" "ldconfig not available"
fi

echo ""

# 5. Check environment variables
echo "5. Checking Environment Variables..."

if [ -n "$CUDA_HOME" ] || [ -n "$CUDA_PATH" ]; then
    [ -n "$CUDA_HOME" ] && print_status "OK" "CUDA_HOME=$CUDA_HOME"
    [ -n "$CUDA_PATH" ] && print_status "OK" "CUDA_PATH=$CUDA_PATH"
else
    print_status "WARN" "CUDA_HOME/CUDA_PATH not set"
fi

if echo "$PATH" | grep -q "cuda"; then
    print_status "OK" "CUDA appears to be in PATH"
else
    print_status "WARN" "CUDA not found in PATH"
fi

if [ -n "$LD_LIBRARY_PATH" ]; then
    if echo "$LD_LIBRARY_PATH" | grep -q "cuda"; then
        print_status "OK" "CUDA libraries in LD_LIBRARY_PATH"
    else
        print_status "WARN" "CUDA not in LD_LIBRARY_PATH"
    fi
else
    print_status "WARN" "LD_LIBRARY_PATH not set"
fi

echo ""

# 6. Test compilation
echo "6. Testing CUDA Compilation..."

# Create temporary CUDA file
TMP_CUDA=$(mktemp /tmp/test_cuda.XXXXXX.cu)
TMP_EXEC=$(mktemp /tmp/test_cuda.XXXXXX)

cat > "$TMP_CUDA" << 'EOF'
#include <cuda_runtime.h>
#include <stdio.h>

int main() {
    int deviceCount = 0;
    cudaError_t error = cudaGetDeviceCount(&deviceCount);
    
    if (error != cudaSuccess) {
        printf("CUDA Error: %s\n", cudaGetErrorString(error));
        return 1;
    }
    
    printf("CUDA Devices: %d\n", deviceCount);
    
    for (int i = 0; i < deviceCount; i++) {
        cudaDeviceProp prop;
        cudaGetDeviceProperties(&prop, i);
        printf("Device %d: %s (Compute Capability %d.%d)\n", 
               i, prop.name, prop.major, prop.minor);
    }
    
    return 0;
}
EOF

if command -v nvcc &> /dev/null; then
    if nvcc -o "$TMP_EXEC" "$TMP_CUDA" 2>/dev/null; then
        print_status "OK" "CUDA compilation successful"
        
        # Try to run the test
        if OUTPUT=$("$TMP_EXEC" 2>&1); then
            print_status "OK" "CUDA runtime test successful"
            echo "$OUTPUT" | sed 's/^/   /'
        else
            print_status "FAIL" "CUDA runtime test failed"
            echo "$OUTPUT" | sed 's/^/   /'
        fi
    else
        print_status "FAIL" "CUDA compilation failed"
    fi
else
    print_status "SKIP" "nvcc not available, skipping compilation test"
fi

# Cleanup
rm -f "$TMP_CUDA" "$TMP_EXEC"

echo ""

# 7. DotCompute specific checks
echo "7. DotCompute Project Checks..."

# Check if we're in the DotCompute directory
if [ -f "DotCompute.sln" ]; then
    print_status "OK" "In DotCompute project directory"
    
    # Check if the project builds
    echo "   Testing project build..."
    if dotnet build -c Release --verbosity quiet 2>/dev/null; then
        print_status "OK" "Project builds successfully"
    else
        print_status "WARN" "Project build had issues (run 'dotnet build' for details)"
    fi
else
    print_status "WARN" "Not in DotCompute project root directory"
fi

echo ""
echo "=========================================="

# Final summary
if [ "$SETUP_OK" = true ]; then
    echo -e "${GREEN}✓ CUDA setup verification PASSED${NC}"
    echo ""
    echo "Your CUDA environment appears to be properly configured for DotCompute."
    exit 0
else
    echo -e "${RED}✗ CUDA setup verification FAILED${NC}"
    echo ""
    echo "Please address the issues above to ensure CUDA tests work properly."
    echo ""
    echo "Common fixes:"
    echo "1. Install CUDA Toolkit 12.x or 13.x from NVIDIA"
    echo "2. Add CUDA to your PATH and LD_LIBRARY_PATH"
    echo "3. Run 'sudo ldconfig' to update library cache"
    echo "4. For WSL users, ensure WSL2 with GPU support is enabled"
    exit 1
fi