#!/usr/bin/env python3
"""
Copyright (c) 2025 Michael Ivertowski
Licensed under the MIT License. See LICENSE file in the project root for license information.

DotCompute Namespace Alignment Tool

This script analyzes and fixes namespace alignment with folder structure in DotCompute solution.
"""

import os
import re
import sys
import argparse
from pathlib import Path
from typing import Dict, List, Tuple, Optional


class NamespaceIssue:
    def __init__(self, file_path: str, current_namespace: str, expected_namespace: str, line_number: int):
        self.file_path = file_path
        self.current_namespace = current_namespace
        self.expected_namespace = expected_namespace
        self.line_number = line_number
        self.relative_path = os.path.relpath(file_path, SOLUTION_ROOT)


def get_expected_namespace(file_path: str) -> Optional[str]:
    """Get the expected namespace based on folder structure."""
    relative_path = os.path.relpath(file_path, SOLUTION_ROOT).replace('\\', '/')
    
    # Define namespace mapping rules based on folder structure
    namespace_mappings = {
        "src/DotCompute.Abstractions/": "DotCompute.Abstractions",
        "src/DotCompute.Algorithms/": "DotCompute.Algorithms",
        "src/DotCompute.Core/": "DotCompute.Core",
        "src/DotCompute.Generators/": "DotCompute.Generators",
        "src/DotCompute.Linq/": "DotCompute.Linq",
        "src/DotCompute.Memory/": "DotCompute.Memory",
        "src/DotCompute.Plugins/": "DotCompute.Plugins",
        "src/DotCompute.Runtime/": "DotCompute.Runtime",
        "plugins/backends/DotCompute.Backends.CPU/": "DotCompute.Backends.CPU",
        "plugins/backends/DotCompute.Backends.CUDA/": "DotCompute.Backends.CUDA",
        "plugins/backends/DotCompute.Backends.Metal/": "DotCompute.Backends.Metal",
        "tests/": "DotCompute.Tests",
        "benchmarks/": "DotCompute.Benchmarks",
        "samples/": "DotCompute.Samples",
        "examples/": "DotCompute.Examples",
        "tools/": "DotCompute.Tools",
    }
    
    # Find the best matching base namespace
    base_namespace = ""
    longest_match = 0
    
    for prefix, namespace in namespace_mappings.items():
        if relative_path.startswith(prefix) and len(prefix) > longest_match:
            base_namespace = namespace
            longest_match = len(prefix)
    
    if not base_namespace:
        return None
    
    # Extract the relative path after the base
    remaining_path = relative_path[longest_match:]
    
    # Remove filename and extract directory parts
    directory_parts = []
    parts = remaining_path.split('/')
    
    for part in parts:
        # Skip empty parts, file extensions, and common non-namespace directories
        if (part and 
            '.' not in part and 
            part not in ['src', 'tests', 'bin', 'obj']):
            directory_parts.append(part)
    
    # Build final namespace
    if directory_parts:
        return f"{base_namespace}.{'.'.join(directory_parts)}"
    else:
        return base_namespace


def get_namespace_from_file(file_path: str) -> Optional[Tuple[str, int]]:
    """Extract namespace declaration from a C# file."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
            
        for i, line in enumerate(lines):
            line = line.strip()
            match = re.match(r'^namespace\s+([^;{]+)', line)
            if match:
                namespace = match.group(1).strip().rstrip(';')
                return namespace, i + 1
                
    except Exception as e:
        print(f"Warning: Failed to read file {file_path}: {e}")
    
    return None


def find_namespace_issues() -> List[NamespaceIssue]:
    """Find all namespace alignment issues in the solution."""
    print("Scanning for C# files...")
    
    # Find all C# files
    cs_files = []
    for root, dirs, files in os.walk(SOLUTION_ROOT):
        # Skip binary and build directories
        dirs[:] = [d for d in dirs if d not in ['bin', 'obj', '.git']]
        
        for file in files:
            if (file.endswith('.cs') and 
                file not in ['GlobalAssemblyInfo.cs', 'AssemblyInfo.cs']):
                cs_files.append(os.path.join(root, file))
    
    print(f"Found {len(cs_files)} C# files to analyze")
    
    issues = []
    processed_count = 0
    
    for file_path in cs_files:
        processed_count += 1
        if processed_count % 50 == 0:
            print(f"Processed {processed_count} / {len(cs_files)} files...")
        
        namespace_info = get_namespace_from_file(file_path)
        if namespace_info:
            current_namespace, line_number = namespace_info
            expected_namespace = get_expected_namespace(file_path)
            
            if expected_namespace and current_namespace != expected_namespace:
                issue = NamespaceIssue(
                    file_path=file_path,
                    current_namespace=current_namespace,
                    expected_namespace=expected_namespace,
                    line_number=line_number
                )
                issues.append(issue)
    
    return issues


def show_analysis_report(issues: List[NamespaceIssue]) -> None:
    """Display the analysis report."""
    print("\n" + "=" * 50)
    print("NAMESPACE ALIGNMENT ANALYSIS REPORT")
    print("=" * 50)
    
    if not issues:
        print("✅ All namespaces are properly aligned with folder structure!")
        return
    
    print(f"❌ Found {len(issues)} namespace alignment issues:\n")
    
    # Group issues by expected namespace
    grouped = {}
    for issue in issues:
        if issue.expected_namespace not in grouped:
            grouped[issue.expected_namespace] = []
        grouped[issue.expected_namespace].append(issue)
    
    for expected_namespace, issue_list in sorted(grouped.items()):
        print(f"Expected Namespace: {expected_namespace}")
        print("-" * 50)
        
        for issue in issue_list:
            print(f"  📁 {issue.relative_path}")
            print(f"    Current:  {issue.current_namespace}")
            print(f"    Expected: {issue.expected_namespace}")
            print(f"    Line:     {issue.line_number}")
            print()
    
    # Summary statistics
    unique_namespaces = len(set(issue.current_namespace for issue in issues))
    
    print("SUMMARY")
    print("-------")
    print(f"Files with issues: {len(issues)}")
    print(f"Unique wrong namespaces: {unique_namespaces}")


def fix_namespace_issues(issues: List[NamespaceIssue], dry_run: bool = False) -> None:
    """Fix all namespace alignment issues."""
    if not issues:
        print("No issues to fix!")
        return
    
    print("\n" + "=" * 50)
    print("FIXING NAMESPACE ISSUES")
    print("=" * 50)
    
    if dry_run:
        print("DRY RUN MODE - No changes will be made\n")
    
    fixed_count = 0
    failed_count = 0
    
    for issue in issues:
        try:
            print(f"Fixing: {issue.relative_path}")
            print(f"  {issue.current_namespace} → {issue.expected_namespace}")
            
            if not dry_run:
                # Read file content
                with open(issue.file_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                # Replace the namespace declaration
                old_pattern = rf"namespace\s+{re.escape(issue.current_namespace)}"
                new_namespace = f"namespace {issue.expected_namespace}"
                
                new_content = re.sub(old_pattern, new_namespace, content)
                
                # Write back to file
                with open(issue.file_path, 'w', encoding='utf-8') as f:
                    f.write(new_content)
            
            fixed_count += 1
            
        except Exception as e:
            print(f"Failed to fix {issue.file_path}: {e}")
            failed_count += 1
    
    print(f"\nRESULTS")
    print("-------")
    print(f"Fixed: {fixed_count}")
    if failed_count > 0:
        print(f"Failed: {failed_count}")


def update_using_statements(issues: List[NamespaceIssue], dry_run: bool = False) -> None:
    """Update using statements affected by namespace changes."""
    if not issues:
        return
    
    print("\n" + "=" * 50)
    print("UPDATING USING STATEMENTS")
    print("=" * 50)
    
    # Create mapping of old namespace to new namespace
    namespace_mapping = {}
    for issue in issues:
        namespace_mapping[issue.current_namespace] = issue.expected_namespace
    
    # Find all C# files to update using statements
    cs_files = []
    for root, dirs, files in os.walk(SOLUTION_ROOT):
        dirs[:] = [d for d in dirs if d not in ['bin', 'obj', '.git']]
        
        for file in files:
            if file.endswith('.cs'):
                cs_files.append(os.path.join(root, file))
    
    updated_files = 0
    
    for file_path in cs_files:
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                original_content = f.read()
            
            new_content = original_content
            
            # Update using statements
            for old_namespace, new_namespace in namespace_mapping.items():
                pattern = rf"using\s+{re.escape(old_namespace)}"
                replacement = f"using {new_namespace}"
                new_content = re.sub(pattern, replacement, new_content)
            
            # Only write if content changed
            if new_content != original_content:
                relative_path = os.path.relpath(file_path, SOLUTION_ROOT)
                print(f"Updating using statements in: {relative_path}")
                
                if not dry_run:
                    with open(file_path, 'w', encoding='utf-8') as f:
                        f.write(new_content)
                
                updated_files += 1
                
        except Exception as e:
            print(f"Warning: Failed to update using statements in {file_path}: {e}")
    
    print(f"Updated using statements in {updated_files} files")


def main():
    """Main entry point."""
    global SOLUTION_ROOT
    
    parser = argparse.ArgumentParser(description='DotCompute Namespace Alignment Tool')
    parser.add_argument('--analyze', action='store_true', 
                       help='Only analyze and report misalignments without making changes')
    parser.add_argument('--fix', action='store_true', 
                       help='Fix all identified namespace misalignments')
    parser.add_argument('--dry-run', action='store_true', 
                       help='Show what changes would be made without actually making them')
    
    args = parser.parse_args()
    
    # Validate parameters
    if not (args.analyze or args.fix or args.dry_run):
        print("Please specify one of: --analyze, --fix, or --dry-run")
        return 1
    
    # Set solution root
    SOLUTION_ROOT = Path(__file__).parent.parent.absolute()
    
    print("DotCompute Namespace Alignment Tool")
    print("=" * 50)
    
    # Find all namespace issues
    issues = find_namespace_issues()
    
    # Show analysis report
    show_analysis_report(issues)
    
    # Perform fixes if requested
    if args.fix or args.dry_run:
        fix_namespace_issues(issues, args.dry_run)
        update_using_statements(issues, args.dry_run)
        
        if not args.dry_run:
            print("\n✅ Namespace alignment complete!")
            print("\nNext steps:")
            print("1. Build the solution to verify no compilation errors")
            print("2. Run tests to ensure functionality is preserved")
            print("3. Commit the namespace alignment changes")
    
    return 0


if __name__ == '__main__':
    sys.exit(main())