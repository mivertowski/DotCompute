#!/bin/bash
# Script to find and categorize CS1998 warnings

echo "Building solution to capture CS1998 warnings..."
dotnet build DotCompute.sln -v:q 2>&1 | grep "CS1998" | tee /tmp/cs1998_full.txt

echo ""
echo "=== CS1998 Warning Summary ==="
echo "Total warnings: $(wc -l < /tmp/cs1998_full.txt)"
echo ""
echo "By project:"
grep "CS1998" /tmp/cs1998_full.txt | sed 's/.*\/src\///' | sed 's/:.*warning.*//' | sort | uniq -c | sort -rn
echo ""
echo "Full list saved to /tmp/cs1998_full.txt"
