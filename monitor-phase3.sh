#!/bin/bash

# Phase 3 Integration Monitor Dashboard
# Updates every 30 seconds with build status

echo "🎯 Phase 3 Integration Monitor Dashboard"
echo "========================================"

while true; do
    clear
    echo "🎯 Phase 3 Integration Monitor Dashboard"
    echo "========================================"
    echo "📅 Last Update: $(date '+%Y-%m-%d %H:%M:%S')"
    echo ""
    
    # Count errors by category
    echo "📊 Build Status:"
    echo "----------------"
    
    # Run build and capture output
    BUILD_OUTPUT=$(dotnet build --no-incremental 2>&1)
    
    # Count total errors
    TOTAL_ERRORS=$(echo "$BUILD_OUTPUT" | grep -c " error ")
    TOTAL_WARNINGS=$(echo "$BUILD_OUTPUT" | grep -c " warning ")
    
    # Count errors by category
    PLUGIN_ERRORS=$(echo "$BUILD_OUTPUT" | grep -c "IBackendPlugin")
    TYPE_ERRORS=$(echo "$BUILD_OUTPUT" | grep -c -E "(IPlugin|IComputeEngine|PluginManager|PluginLoader)")
    METAL_ERRORS=$(echo "$BUILD_OUTPUT" | grep -c "Metal")
    CUDA_ERRORS=$(echo "$BUILD_OUTPUT" | grep -c "CUDA")
    TEST_ERRORS=$(echo "$BUILD_OUTPUT" | grep -c -E "(Test|Fixture)")
    
    echo "🔴 Total Errors: $TOTAL_ERRORS"
    echo "🟡 Total Warnings: $TOTAL_WARNINGS"
    echo ""
    
    echo "📋 Error Categories:"
    echo "-------------------"
    echo "  📌 Plugin Interface Issues: $PLUGIN_ERRORS"
    echo "  📌 Missing Type References: $TYPE_ERRORS"
    echo "  📌 Metal Backend Issues: $METAL_ERRORS"
    echo "  📌 CUDA Backend Issues: $CUDA_ERRORS"
    echo "  📌 Test Fixture Issues: $TEST_ERRORS"
    echo ""
    
    # Progress calculation
    INITIAL_ERRORS=52
    FIXED_ERRORS=$((INITIAL_ERRORS - TOTAL_ERRORS))
    PROGRESS=$((FIXED_ERRORS * 100 / INITIAL_ERRORS))
    
    echo "📈 Progress:"
    echo "------------"
    echo "  ✅ Fixed: $FIXED_ERRORS / $INITIAL_ERRORS ($PROGRESS%)"
    echo "  ❌ Remaining: $TOTAL_ERRORS"
    
    # Progress bar
    echo -n "  ["
    FILLED=$((PROGRESS / 5))
    for ((i=0; i<20; i++)); do
        if [ $i -lt $FILLED ]; then
            echo -n "█"
        else
            echo -n "░"
        fi
    done
    echo "] $PROGRESS%"
    echo ""
    
    # Recent activity from swarm memory
    echo "🐝 Recent Swarm Activity:"
    echo "------------------------"
    sqlite3 /home/mivertowski/DotCompute/DotCompute/.swarm/memory.db \
        "SELECT datetime(timestamp, 'localtime'), substr(message, 1, 80) 
         FROM notifications 
         ORDER BY timestamp DESC 
         LIMIT 5" 2>/dev/null | sed 's/|/ - /' || echo "  No recent activity"
    
    echo ""
    echo "🔄 Refreshing in 30 seconds... (Ctrl+C to stop)"
    
    # Store current status in memory
    npx claude-flow@alpha hooks notify --message "Monitor update: $TOTAL_ERRORS errors remaining ($PROGRESS% complete)" --level "info" >/dev/null 2>&1
    
    sleep 30
done