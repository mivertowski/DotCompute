#!/bin/bash

# Monitor warnings in real-time
SOLUTION_PATH="/Users/mivertowski/DEV/DotCompute/DotCompute/DotCompute.sln"
LOG_FILE="/tmp/warning-monitor.log"

echo "=== Warning Monitor Started at $(date) ===" | tee -a "$LOG_FILE"
echo "" | tee -a "$LOG_FILE"

while true; do
    TIMESTAMP=$(date '+%Y-%m-%d %H:%M:%S')

    # Get warning count
    WARNING_COUNT=$(dotnet build "$SOLUTION_PATH" --configuration Release --no-incremental 2>&1 | grep "Warning(s)" | tail -1 | awk '{print $1}')

    if [ -n "$WARNING_COUNT" ]; then
        echo "[$TIMESTAMP] Total Warnings: $WARNING_COUNT" | tee -a "$LOG_FILE"

        # Get breakdown
        dotnet build "$SOLUTION_PATH" --configuration Release --no-incremental 2>&1 | \
            grep -E "warning (CS|CA|IDE|DC)" | \
            sed 's/.*warning \([^:]*\):.*/\1/' | \
            sort | uniq -c | sort -rn | head -10 | tee -a "$LOG_FILE"

        echo "---" | tee -a "$LOG_FILE"
    fi

    # Wait 3 minutes
    sleep 180
done
