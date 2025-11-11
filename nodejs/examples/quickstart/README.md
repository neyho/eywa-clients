# 🚀 Quickstart Examples

Get up and running with EYWA in minutes!

## Examples in this folder

### `hello-world.js`
Your first EYWA robot. Demonstrates basic communication patterns.

```bash
eywa run -c 'node examples/quickstart/hello-world.js'
```

### `basic-graphql.js`
Learn how to make GraphQL queries directly to EYWA.

```bash
eywa run -c 'node examples/quickstart/basic-graphql.js'
```

## Next Steps

Once you've run these examples:
1. Check out `../reports/` for task reporting
2. Explore `../files/` for file operations
3. Build custom robots in `../robots/`

## Tips

- Use `console.log()` for debugging output
- Use `eywa.info()` for task logging that persists
- Always call `eywa.close_task()` to properly finish
