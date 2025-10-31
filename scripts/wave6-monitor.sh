#!/bin/bash
# Wave 6 Multi-Agent Monitoring Script
# Tracks progress across 6 concurrent specialist agents

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MONITOR_LOG="/tmp/wave6_monitor.log"
PROGRESS_LOG="/tmp/wave6_progress.txt"

echo "=== Wave 6 Multi-Agent Monitor ===" | tee -a "$MONITOR_LOG"
echo "Started: $(date '+%Y-%m-%d %H:%M:%S')" | tee -a "$MONITOR_LOG"
echo "" | tee -a "$MONITOR_LOG"

# Function to check agent progress via memory
check_agent_status() {
    local agent_name=$1
    local memory_key=$2

    echo "Checking $agent_name..." | tee -a "$MONITOR_LOG"
    npx claude-flow@alpha memory retrieve "$memory_key" 2>/dev/null | tee -a "$MONITOR_LOG" || echo "  No data yet" | tee -a "$MONITOR_LOG"
    echo "" | tee -a "$MONITOR_LOG"
}

# Function to count current warnings
count_warnings() {
    echo "=== Current Warning Count ===" | tee -a "$MONITOR_LOG"

    if [ -f "$PROGRESS_LOG" ]; then
        local warning_count=$(grep -c "warning" "$PROGRESS_LOG" || echo "0")
        echo "Total warnings: $warning_count" | tee -a "$MONITOR_LOG"

        # Count by category
        echo "" | tee -a "$MONITOR_LOG"
        echo "By Category:" | tee -a "$MONITOR_LOG"
        grep -o "warning CA1849:" "$PROGRESS_LOG" | wc -l | xargs echo "  CA1849 (Async/await):" | tee -a "$MONITOR_LOG"
        grep -o "warning CS8602:" "$PROGRESS_LOG" | wc -l | xargs echo "  CS8602 (Null references):" | tee -a "$MONITOR_LOG"
        grep -o "warning CS1998:" "$PROGRESS_LOG" | wc -l | xargs echo "  CS1998 (Async no-await):" | tee -a "$MONITOR_LOG"
        grep -o "warning CA2213:" "$PROGRESS_LOG" | wc -l | xargs echo "  CA2213 (Undisposed):" | tee -a "$MONITOR_LOG"
        grep -o "warning CA2012:" "$PROGRESS_LOG" | wc -l | xargs echo "  CA2012 (ValueTask):" | tee -a "$MONITOR_LOG"
    else
        echo "No build data available yet" | tee -a "$MONITOR_LOG"
    fi
    echo "" | tee -a "$MONITOR_LOG"
}

# Main monitoring loop
echo "Checking Agent Status..." | tee -a "$MONITOR_LOG"
echo "=========================" | tee -a "$MONITOR_LOG"
echo "" | tee -a "$MONITOR_LOG"

check_agent_status "CA1849 Agent" "wave6-ca1849"
check_agent_status "CS8602 Agent" "wave6-cs8602"
check_agent_status "CS1998 Agent" "wave6-cs1998"
check_agent_status "CA2213 Agent" "wave6-ca2213"
check_agent_status "CA2012 Agent" "wave6-ca2012"
check_agent_status "Minor Warnings Agent" "wave6-minor"

count_warnings

# Store monitoring timestamp
npx claude-flow@alpha memory store wave6-last-check "$(date '+%Y-%m-%d %H:%M:%S')"

echo "=== Monitor Complete ===" | tee -a "$MONITOR_LOG"
echo "Full log: $MONITOR_LOG" | tee -a "$MONITOR_LOG"
