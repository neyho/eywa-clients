# 📊 Report Examples

Learn how to create stunning task reports with EYWA!

## Examples in this folder

### `simple-card.js`
Basic markdown card report for status updates.
```bash
eywa run -c 'node examples/reports/simple-card.js'
```

### `table-data.js`  
Structured data tables with metrics and comparisons.
```bash
eywa run -c 'node examples/reports/table-data.js'
```

### `complete-dashboard.js`
Full-featured dashboard with cards, tables, and images.
```bash
eywa run -c 'node examples/reports/complete-dashboard.js'
```

### `test-suite.js`
Comprehensive test suite for all report features.
```bash
eywa run -c 'node examples/reports/test-suite.js'
```

## 📋 Task Files

Use with `--task-file` for backend persistence:

### `tasks/`
- `simple-task.json` - Basic task for simple reports
- `dashboard-task.json` - Complex task for dashboard reports

```bash
eywa run --task-file examples/reports/tasks/simple-task.json -c 'node examples/reports/simple-card.js'
```

## 🎨 Report Types

- **Card Only**: Markdown content for narratives
- **Table Only**: Structured data without description
- **Card + Tables**: Full business reports
- **Complete**: Everything including images/charts
- **Metadata**: Additional context and versioning

## 💡 Tips

- Use emoji in cards for visual appeal
- Keep table headers concise
- Include metadata for tracking
- Base64 encode images properly
- Test with the test suite first
