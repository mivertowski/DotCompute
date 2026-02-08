# DotCompute Migration Guides

Welcome to the DotCompute migration documentation. These guides help you upgrade your applications between major versions.

---

## Available Migration Guides

| From | To | Guide |
|------|-----|-------|
| v0.9.x | v1.0.0 | [MIGRATION-v0.9-to-v1.0.md](./MIGRATION-v0.9-to-v1.0.md) |
| v0.5.x | v0.9.x | [MIGRATION-v0.5-to-v0.9.md](./MIGRATION-v0.5-to-v0.9.md) |

---

## Version History

| Version | Release Date | Status |
|---------|--------------|--------|
| v1.0.0 | TBD | **Planned** |
| v0.9.x | TBD | Planned |
| v0.6.2 | February 2026 | **Current** |
| v0.5.x | October 2025 | Previous |

---

## General Migration Strategy

### 1. Prepare
- Read the migration guide for your version jump
- Review breaking changes
- Back up your project
- Create a migration branch

### 2. Update Dependencies
```bash
# Update all DotCompute packages
dotnet list package | grep DotCompute
dotnet add package DotCompute --version 0.6.2
```

### 3. Fix Compilation Errors
- Update namespaces
- Fix API changes
- Address analyzer warnings

### 4. Test
- Run unit tests
- Run integration tests
- Benchmark performance

### 5. Deploy
- Stage environment first
- Monitor for issues
- Gradual rollout

---

## Skipping Versions

If migrating across multiple versions (e.g., v0.5 → v1.0):

1. **Option A**: Step-by-step migration
   - v0.5 → v0.9 → v1.0
   - Safer, but more work

2. **Option B**: Direct migration
   - Combine changes from all guides
   - Review all breaking changes
   - More testing required

**Recommendation**: Use Option A for large codebases.

---

## Getting Help

- **GitHub Issues**: https://github.com/mivertowski/DotCompute/issues
- **Discussions**: https://github.com/mivertowski/DotCompute/discussions
- **Tag**: Use `migration` label for migration-related issues

---

## Contributing

Found an issue or have suggestions for these guides?
- Submit a PR to improve documentation
- Open an issue for unclear instructions
- Share your migration experience
