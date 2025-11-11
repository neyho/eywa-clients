#!/bin/bash
# Cleanup Script for Node.js Examples Reorganization
#
# This script removes the old unorganized files from the examples folder
# They have been reorganized into a clean directory structure

echo "🧹 Cleaning up old examples structure..."
echo "======================================"
echo ""

cd /Users/robi/dev/EYWA/core/clients/nodejs/examples

echo "📁 New organized structure:"
echo "  ✅ quickstart/     - Get started quickly"
echo "  ✅ reports/        - Task reporting examples"
echo "  ✅ legacy/         - Original files (kept for reference)"
echo ""

echo "🗑️  Removing old unorganized files:"

# List of old files to remove
OLD_FILES=(
    "task-report-demo.js"
    "simple-report.js"
    "graphql.js"
    "files-graphql-pattern.js"
    "cleanup-demo.js"
    "test_eywa_client.js"
    "simple-test.js"
    "webdriver.js"
    "test-reports.js"
    "hanging.js"
    "dynamic-report-robot.js"
    "simple-card-report-task.json"
    "table-report-task.json"
    "complete-report-task.json"
    "system-status-task.json"
    "run-report-tests.sh"
)

for file in "${OLD_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo "   🗑️  Removing $file"
        rm "$file"
    else
        echo "   ✅ $file already cleaned"
    fi
done

echo ""
echo "🎯 Cleanup complete!"
echo ""
echo "📋 Quick reference for new structure:"
echo "  eywa run -c 'node examples/quickstart/hello-world.js'"
echo "  eywa run -c 'node examples/reports/simple-card.js'"  
echo "  eywa run -c 'node examples/reports/test-suite.js'"
echo ""
echo "📖 Read examples/README.md for full documentation"
