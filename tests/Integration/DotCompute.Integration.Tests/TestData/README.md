# Test Data Directory

This directory contains test data files used by the DotCompute integration tests.

## Structure

- `kernels/` - Sample kernel definitions for testing
- `images/` - Test image data for image processing tests
- `datasets/` - Large datasets for performance testing
- `configs/` - Test configuration files

## Usage

Test data files are automatically copied to the output directory during build and are available to tests at runtime.

## Adding New Test Data

1. Place files in appropriate subdirectories
2. Ensure files are small enough for version control
3. Update `.csproj` to include new files with `CopyToOutputDirectory="PreserveNewest"`
4. Document file formats and usage in test code