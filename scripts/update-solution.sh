#!/bin/bash

# Script to update solution file with new test structure

SOLUTION_FILE="DotCompute.sln"

echo "Updating solution file with new test structure..."

# Create a backup
cp $SOLUTION_FILE ${SOLUTION_FILE}.bak

# Update test project paths
sed -i 's|tests\\DotCompute\.SharedTestUtilities\\|tests\\Shared\\DotCompute.Tests.Common\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.TestDoubles\\|tests\\Shared\\DotCompute.Tests.Mocks\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.TestImplementations\\|tests\\Shared\\DotCompute.Tests.Implementations\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.BasicTests\\|tests\\Unit\\DotCompute.BasicTests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Abstractions\.Tests\\|tests\\Unit\\DotCompute.Abstractions.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Core\.Tests\\|tests\\Unit\\DotCompute.Core.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Core\.UnitTests\\|tests\\Unit\\DotCompute.Core.UnitTests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Memory\.Tests\\|tests\\Unit\\DotCompute.Memory.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Plugins\.Tests\\|tests\\Unit\\DotCompute.Plugins.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Generators\.Tests\\|tests\\Unit\\DotCompute.Generators.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Algorithms\.Tests\\|tests\\Unit\\DotCompute.Algorithms.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Integration\.Tests\\|tests\\Integration\\DotCompute.Integration.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Hardware\.Tests\\|tests\\Hardware\\DotCompute.Hardware.Mock.Tests\\|g' $SOLUTION_FILE
sed -i 's|tests\\DotCompute\.Hardware\.RealTests\\|tests\\Hardware\\DotCompute.Hardware.Cuda.Tests\\|g' $SOLUTION_FILE

# Update project names
sed -i 's|"DotCompute\.SharedTestUtilities"|"DotCompute.Tests.Common"|g' $SOLUTION_FILE
sed -i 's|"DotCompute\.TestDoubles"|"DotCompute.Tests.Mocks"|g' $SOLUTION_FILE
sed -i 's|"DotCompute\.TestImplementations"|"DotCompute.Tests.Implementations"|g' $SOLUTION_FILE
sed -i 's|"DotCompute\.Hardware\.Tests"|"DotCompute.Hardware.Mock.Tests"|g' $SOLUTION_FILE
sed -i 's|"DotCompute\.Hardware\.RealTests"|"DotCompute.Hardware.Cuda.Tests"|g' $SOLUTION_FILE

# Update project file names in the paths
sed -i 's|DotCompute\.SharedTestUtilities\.csproj|DotCompute.Tests.Common.csproj|g' $SOLUTION_FILE
sed -i 's|DotCompute\.TestDoubles\.csproj|DotCompute.Tests.Mocks.csproj|g' $SOLUTION_FILE
sed -i 's|DotCompute\.TestImplementations\.csproj|DotCompute.Tests.Implementations.csproj|g' $SOLUTION_FILE
sed -i 's|DotCompute\.Hardware\.Tests\.csproj|DotCompute.Hardware.Mock.Tests.csproj|g' $SOLUTION_FILE
sed -i 's|DotCompute\.Hardware\.RealTests\.csproj|DotCompute.Hardware.Cuda.Tests.csproj|g' $SOLUTION_FILE

echo "Solution file updated successfully!"
echo "Backup saved as ${SOLUTION_FILE}.bak"