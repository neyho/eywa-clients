# 🎯 Run These Commands

Here are the commands to test the new clean examples structure:

## 🚀 Quick Start
```bash
# Your first EYWA robot
eywa run -c 'node examples/quickstart/hello-world.js'

# Learn basic GraphQL
eywa run -c 'node examples/quickstart/basic-graphql.js'
```

## 📊 Reports
```bash
# Simple card report
eywa run -c 'node examples/reports/simple-card.js'

# Table with data
eywa run -c 'node examples/reports/table-data.js'

# Complete dashboard  
eywa run -c 'node examples/reports/complete-dashboard.js'

# Test all report features
eywa run -c 'node examples/reports/test-suite.js'
```

## 🎯 With Task Files (Backend Persistence)
```bash
# Reports with task context
eywa run --task-file examples/reports/tasks/simple-task.json -c 'node examples/reports/simple-card.js'

eywa run --task-file examples/reports/tasks/dashboard-task.json -c 'node examples/reports/complete-dashboard.js'
```

## 🧹 Clean Up Old Structure
```bash
# Remove the old messy files (optional)
chmod +x examples/cleanup-old-structure.sh
./examples/cleanup-old-structure.sh
```

## 📁 New Clean Structure

```
examples/
├── README.md              # Main documentation
├── quickstart/            # Start here!
│   ├── hello-world.js
│   ├── basic-graphql.js
│   └── README.md
├── reports/               # Task reporting
│   ├── simple-card.js
│   ├── table-data.js
│   ├── complete-dashboard.js
│   ├── test-suite.js
│   ├── tasks/
│   │   ├── simple-task.json
│   │   └── dashboard-task.json
│   └── README.md
├── legacy/                # Original files
│   ├── task-report-demo.js
│   └── README.md
└── cleanup-old-structure.sh
```

This is SO much cleaner and more user-friendly! 🎉
