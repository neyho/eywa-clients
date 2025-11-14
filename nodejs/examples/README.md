# EYWA Node.js Client Examples

Welcome to the EYWA Node.js client examples! This directory contains well-organized examples to help you get started quickly.

## 📁 Directory Structure

```
examples/
├── README.md              # This file
├── RUN-EXAMPLES.md        # Running instructions
├── quickstart/            # Get started in 5 minutes
│   ├── hello-world.js     # Basic EYWA connection
│   ├── basic-graphql.js   # GraphQL queries
│   └── basic-files.js     # File operations
├── reports/               # Task reporting examples
├── legacy/                # Older examples
└── simple-files-demo.js   # Comprehensive file operations demo
```

## 🚀 Quick Start

1. **First time?** Start with `quickstart/hello-world.js`
2. **Basic file ops?** Try `quickstart/basic-files.js`
3. **Building reports?** Check `reports/`
4. **Comprehensive demo?** Run `simple-files-demo.js`

## 🎯 Running Examples

All examples can be run with:
```bash
eywa run -c 'node examples/[example].js'
```

For quickstart examples:
```bash
eywa run -c 'node examples/quickstart/[example].js'
```

Or with task input:
```bash
eywa run --task-file examples/[category]/tasks/[task].json -c 'node examples/[category]/[robot].js'
```

## 📚 Learning Path

1. **Beginner**: `quickstart/hello-world.js` - Basic EYWA connection
2. **GraphQL**: `quickstart/basic-graphql.js` - Direct GraphQL queries  
3. **Files**: `quickstart/basic-files.js` - Essential file operations
4. **Advanced Files**: `simple-files-demo.js` - Comprehensive file demo
5. **Reports**: `reports/simple-card.js` - Basic report creation
6. **Dashboards**: `reports/complete-dashboard.js` - Full dashboard

## ⚡ Prerequisites

```bash
# Install dependencies
npm install

# Connect to EYWA (optional - for backend persistence)
eywa connect <your-eywa-url>
```

## 🆘 Need Help?

- Check the README in each subdirectory
- Look at the inline comments in examples
- Review the main client documentation
- See RUN-EXAMPLES.md for detailed running instructions

## 📝 File Operations Examples

The Node.js client now includes comprehensive file operations support:

### Basic Files (`quickstart/basic-files.js`)
- Create folders
- Upload content and files
- Download files  
- List files with filtering
- Basic cleanup

### Advanced Demo (`simple-files-demo.js`)
- Idempotent operations with predefined UUIDs
- Protocol abstraction for S3 uploads
- GraphQL verification of operations
- Error handling demonstrations
- Comprehensive cleanup
- Multiple upload methods (file path, content)
- Stream-based downloads

Both examples demonstrate the simplified API that mirrors the Python client while staying true to the GraphQL-first design philosophy.
