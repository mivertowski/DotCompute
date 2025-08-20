# Documentation Naming Schema

## 📋 **Standard Naming Convention**

All documentation files AND folders follow this consistent pattern:

### **Files:**
```
[phase-]category-subcategory-descriptive-name.md
```

### **Folders:**
```
[phase-]category-purpose/
```

### **Components:**
- **`phase-`** - Optional phase indicator (phase1-, phase2-, phase3-, final-)
- **`category`** - Primary document/folder category
- **`subcategory`** - Specific area within category (files only)
- **`purpose`** - Clear folder purpose (folders only)
- **`descriptive-name`** - Clear, descriptive identifier (files only)
- **`.md`** - Markdown extension (files only)

### **Case Convention:**
- All lowercase with hyphens as separators
- No underscores, spaces, or special characters
- Clear, readable, and URL-friendly
- Consistent between files and folders

## 🎯 **Category Prefixes**

### **Core Documentation**
- `guide-` - User guides and tutorials
- `reference-` - API and technical references  
- `example-` - Code examples and patterns
- `architecture-` - System design documentation

### **Project Reports**
- `report-` - General reports and summaries
- `completion-` - Phase completion documentation
- `analysis-` - Technical analysis and studies
- `testing-` - Test coverage and validation
- `specialist-` - Expert agent reports

### **Management**
- `project-` - Project management documentation
- `plan-` - Implementation plans and roadmaps

## 📅 **Phase Indicators**

When applicable, documents include phase context:
- `phase1-` - Foundation phase documentation
- `phase2-` - Memory and CPU backend phase  
- `phase3-` - GPU acceleration and advanced features
- `final-` - Project completion and final documentation

## 🗂️ **Folder Naming Patterns**

### **Core Documentation Folders**
- **`guide-documentation/`** - User guides and tutorials
- **`reference-documentation/`** - API and technical references
- **`example-code/`** - Code examples and usage patterns
- **`architecture-design/`** - System design documentation

### **Project Folders**
- **`phase-reports/`** - Development phase completion reports
- **`completion-reports/`** - Final project completion documentation
- **`specialist-reports/`** - Expert agent completion reports
- **`technical-analysis/`** - Performance and technical analysis
- **`testing-documentation/`** - Test coverage and validation
- **`project-management/`** - Development guidelines and policies
- **`implementation-plans/`** - Implementation roadmaps

### **Special Purpose Folders**
- **`final-mission/`** - Final cleanup and completion documentation
- **`aot-compatibility/`** - Native AOT optimization documentation

## 🗂️ **File Patterns Within Folders**

### **`/guide-documentation/`** - User-facing documentation
- `guide-getting-started.md`
- `reference-api.md` 
- `guide-performance.md`
- `architecture-overview.md`
- `wiki-home.md`

### **`/reference-documentation/`** - API and technical references
- `api-reference-overview.md`
- `api-reference-detailed.md`

### **`/example-code/`** - Code examples and patterns
- `example-pipeline-usage.md`
- `example-plugin-system.md`
- `example-real-world-scenarios.md`
- `phase3-example-source-generators.md`

### **`/architecture-design/`** - System design documentation
- `phase2-architecture-memory-system.md`
- `phase3-architecture-overview.md`
- `phase3-architecture-implementation-plan.md`

### **`/phase-reports/`** - Development phase reports
- `phase2-completion-report.md`
- `phase2-completion-final-report.md`
- `phase2-completion-final-summary.md`
- `phase2-testing-strategy-report.md`
- `phase3-completion-report.md`
- `phase3-completion-final-report.md`
- `phase3-preparation-complete-report.md`

### **`/development-reports/`** - General development reports
- `phase1-completion-report.md`
- `phase1-completion-final-summary.md`
- `phase2-completion-report.md`
- `phase2-completion-final-report.md`
- `phase2-implementation-cpu-backend-summary.md`
- `phase2-progress-monitor-report.md`
- `phase2-status-report.md`
- `report-documentation-summary.md`

### **`/specialist-reports/`** - Expert agent reports  
- `final-specialist-aot-compatibility.md`
- `final-specialist-coverage-master.md`
- `final-specialist-documentation-master.md`
- `final-specialist-full-depth-implementation.md`

### **`/technical-analysis/`** - Technical studies
- `analysis-aot-compatibility.md`
- `analysis-build-system.md`
- `phase2-analysis-cpu-backend-fixes.md`
- `analysis-performance.md`
- `analysis-solution-errors.md`

### **`/testing/`** - Test documentation
- `testing-coverage-analysis.md`
- `testing-integration-summary.md`
- `testing-validation-report.md`
- `testing-execution-report.md`
- `phase2-testing-compilation-fixes.md`

### **`/examples/`** - Code examples
- `example-pipeline-usage.md`
- `example-plugin-system.md`
- `example-real-world-scenarios.md`
- `phase3-example-source-generators.md`

### **`/architecture/`** - System design
- `phase2-architecture-memory-system.md`
- `phase3-architecture-overview.md`
- `phase3-architecture-implementation-plan.md`

### **`/completion-reports/`** - Final completion docs
- `final-completion-handoff.md`
- `final-completion-documentation.md`

### **`/aot-compatibility/`** - AOT optimization
- `final-specialist-aot-mission-complete.md`
- `analysis-native-aot-optimization.md`

### **`/final-mission/`** - Cleanup mission
- `final-mission-swarm-cleanup-complete.md`

### **`/project-management/`** - Project guidelines
- `project-contributing-guidelines.md`
- `project-security-policy.md`

### **`/00_ImplementationPlans/`** - Implementation roadmaps
- `plan-implementation-roadmap.md`
- `plan-simd-optimization-playbook.md`
- `phase3-plan-advanced-features-summary.md`

## ✅ **Benefits of This Schema**

1. **Consistency** - Predictable naming across all documents
2. **Clarity** - Phase and category immediately apparent
3. **Organization** - Logical grouping and sorting
4. **Navigation** - Easy to find related documents
5. **Maintenance** - Simple to add new documents consistently
6. **URLs** - Clean, readable links when published

## 📝 **Examples**

**Before:**
- `Phase2_Final_Completion_Report.md`
- `COVERAGE_MASTER_FINAL_REPORT.md`
- `AotCompatibilityAnalysis.md`

**After:**
- `phase2-completion-final-report.md`
- `final-specialist-coverage-master.md`
- `analysis-aot-compatibility.md`

This schema ensures our documentation is professional, navigable, and maintainable! 🚀