#!/bin/bash
# Report Tests Runner for EYWA Node.js Client
# Run these commands to test the report functionality

echo "🧪 EYWA Node.js Report Testing Guide"
echo "======================================"
echo ""

# Make sure we're in the right directory
cd /Users/robi/dev/EYWA/core/clients/nodejs

echo "📋 Available Report Tests:"
echo ""

echo "1. Simple Card Report Test:"
echo "   eywa run --task-file examples/simple-card-report-task.json -c 'node examples/dynamic-report-robot.js'"
echo ""

echo "2. Table Report Test:" 
echo "   eywa run --task-file examples/table-report-task.json -c 'node examples/dynamic-report-robot.js'"
echo ""

echo "3. Complete Report with Image Test:"
echo "   eywa run --task-file examples/complete-report-task.json -c 'node examples/dynamic-report-robot.js'"
echo ""

echo "4. System Status Table-Only Test:"
echo "   eywa run --task-file examples/system-status-task.json -c 'node examples/dynamic-report-robot.js'"
echo ""

echo "5. Run Report Test Suite:"
echo "   eywa run --task-file examples/simple-card-report-task.json -c 'node examples/test-reports.js'"
echo ""

echo "🔧 Prerequisites:"
echo "   - Make sure you're connected to EYWA: eywa connect <your-eywa-url>"
echo "   - Install dependencies: npm install"
echo "   - Ensure task entities exist in your EYWA data model"
echo ""

echo "📊 To run with backend persistence, include task UUIDs in the task files."
echo "📝 To run console-only mode, remove UUIDs from task files or run: eywa disconnect"
echo ""

# Example execution (commented out - uncomment to run)
# echo "Running simple card report test..."
# eywa run --task-file examples/simple-card-report-task.json -c 'node examples/dynamic-report-robot.js'
