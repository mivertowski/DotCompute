#!/bin/bash
# Copyright (c) 2025 Michael Ivertowski
# Licensed under the MIT License. See LICENSE file in the project root for license information.

# Namespace Validation Script for CI/CD Integration
# Validates that all namespaces align with folder structure

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "🔍 Validating namespace alignment..."

# Run the namespace checker
if python3 "$SCRIPT_DIR/fix_namespaces.py" --analyze; then
    echo "✅ Namespace validation passed!"
    exit 0
else
    echo "❌ Namespace validation failed!"
    echo ""
    echo "To fix namespace issues, run:"
    echo "  python3 scripts/fix_namespaces.py --fix"
    echo ""
    exit 1
fi