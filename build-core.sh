#!/bin/bash

echo "Building core DotCompute libraries..."

set -e  # Exit on any error

echo "Building Abstractions..."
dotnet build src/Core/DotCompute.Abstractions/DotCompute.Abstractions.csproj --no-restore --verbosity quiet

echo "Building Core..."
dotnet build src/Core/DotCompute.Core/DotCompute.Core.csproj --no-restore --verbosity quiet

echo "Building Memory..."
dotnet build src/Core/DotCompute.Memory/DotCompute.Memory.csproj --no-restore --verbosity quiet

echo "Building Plugins..."
dotnet build src/Runtime/DotCompute.Plugins/DotCompute.Plugins.csproj --no-restore --verbosity quiet

echo "Building Runtime..."
dotnet build src/Runtime/DotCompute.Runtime/DotCompute.Runtime.csproj --no-restore --verbosity quiet

echo "Building CPU Backend..."
dotnet build src/Backends/DotCompute.Backends.CPU/DotCompute.Backends.CPU.csproj --no-restore --verbosity quiet

echo "Building Algorithms..."
dotnet build src/Extensions/DotCompute.Algorithms/DotCompute.Algorithms.csproj --no-restore --verbosity quiet

echo "Building LINQ Extensions..."
dotnet build src/Extensions/DotCompute.Linq/DotCompute.Linq.csproj --no-restore --verbosity quiet

echo "Building CUDA Backend..."
dotnet build src/Backends/DotCompute.Backends.CUDA/DotCompute.Backends.CUDA.csproj --no-restore --verbosity quiet

echo "Building Metal Backend..."
dotnet build src/Backends/DotCompute.Backends.Metal/DotCompute.Backends.Metal.csproj --no-restore --verbosity quiet

echo "✓ All core libraries built successfully!"