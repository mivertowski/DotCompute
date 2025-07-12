#!/bin/bash

echo "=== DotCompute Build Error Analysis ==="
echo ""

echo "## Native AOT Blockers (IL errors)"
grep -E "error IL[0-9]+" build_output.log | head -10
echo ""

echo "## Reflection Warnings (Native AOT risks)"
grep -E "warning IL[0-9]+" build_output.log | head -10
echo ""

echo "## Top 5 Most Common Errors"
grep -E "error (CA|CS|IDE|IL|VSTHRD)[0-9]+" build_output.log | sed 's/.*error \([A-Z]*[0-9]*\).*/\1/' | sort | uniq -c | sort -nr | head -5
echo ""

echo "## Memory/Disposal Issues (CA2000)"
grep "error CA2000" build_output.log | head -5
echo ""

echo "## Threading Issues (VSTHRD)"
grep "error VSTHRD" build_output.log | head -5
echo ""

echo "## Files with Most Errors"
grep -E "error (CA|CS|IDE|IL|VSTHRD)" build_output.log | sed 's/.*\/\([^/]*\)([0-9]*.*/\1/' | sort | uniq -c | sort -nr | head -10
echo ""

echo "## Struct Equality Issues (CA1815)"
grep "error CA1815" build_output.log | grep -o "error CA1815: [^(]*" | sed 's/error CA1815: //' | sort -u
echo ""