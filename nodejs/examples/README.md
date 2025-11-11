# EYWA Node.js Client Examples

Welcome to the EYWA Node.js client examples! This directory contains well-organized examples to help you get started quickly.

## 📁 Directory Structure

```
examples/
├── README.md              # This file
├── quickstart/            # Get started in 5 minutes
├── reports/               # Task reporting examples
├── files/                 # File management examples
├── graphql/              # Direct GraphQL usage
├── robots/               # Complete robot examples
└── advanced/             # Advanced patterns
```

## 🚀 Quick Start

1. **First time?** Start with `quickstart/`
2. **Building reports?** Check `reports/`
3. **Working with files?** Explore `files/`
4. **Need GraphQL examples?** See `graphql/`
5. **Building robots?** Look at `robots/`

## 🎯 Running Examples

All examples can be run with:
```bash
eywa run -c 'node examples/[category]/[example].js'
```

Or with task input:
```bash
eywa run --task-file examples/[category]/tasks/[task].json -c 'node examples/[category]/[robot].js'
```

## 📚 Learning Path

1. **Beginner**: `quickstart/hello-world.js`
2. **Basic Reports**: `reports/simple-card.js`
3. **Advanced Reports**: `reports/complete-dashboard.js`
4. **File Operations**: `files/basic-upload.js`
5. **Custom Robots**: `robots/data-processor.js`

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
