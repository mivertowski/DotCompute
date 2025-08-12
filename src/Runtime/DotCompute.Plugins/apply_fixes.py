#!/usr/bin/env python3
"""
Bulk C# Code Fixer Script
Applies common fixes to all .cs files in the project
"""

import os
import re
import sys
import argparse
from pathlib import Path
from typing import List, Dict, Tuple

class Colors:
    RED = '\033[0;31m'
    GREEN = '\033[0;32m'
    YELLOW = '\033[1;33m'
    BLUE = '\033[0;34m'
    NC = '\033[0m'  # No Color

class BulkFixer:
    def __init__(self, what_if: bool = False, verbose: bool = False):
        self.what_if = what_if
        self.verbose = verbose
        self.files_processed = 0
        self.total_fixes = 0
        self.fix_types = {
            "CA1852": 0,  # Sealed internal classes
            "CA1822": 0,  # Static methods
            "CS1503": 0,  # Logger type fixes
            "CS1998": 0,  # Async Task.CompletedTask
            "CS1061": 0,  # Missing method/property fixes
            "Other": 0    # Other fixes
        }
    
    def log_info(self, message: str):
        print(f"{Colors.BLUE}🔄 {message}{Colors.NC}")
    
    def log_success(self, message: str):
        print(f"{Colors.GREEN}✅ {message}{Colors.NC}")
    
    def log_warning(self, message: str):
        print(f"{Colors.YELLOW}⚠️  {message}{Colors.NC}")
    
    def log_error(self, message: str):
        print(f"{Colors.RED}❌ {message}{Colors.NC}")
    
    def apply_ca1852_fix(self, content: str, file_path: str) -> Tuple[str, int]:
        """Add sealed keyword to internal classes"""
        fixes = 0
        
        # Pattern for internal classes that should be sealed
        pattern = r'^(\s*)(internal\s+class\s+\w+(?:\([^)]*\))?(?:\s*:\s*[^{]*)?)\s*$'
        lines = content.split('\n')
        new_lines = []
        
        for line in lines:
            match = re.match(pattern, line)
            if match:
                indent = match.group(1)
                class_decl = match.group(2)
                
                # Skip if already sealed, abstract, or appears to be a base class
                if (not re.search(r'\b(sealed|abstract)\b', class_decl) and 
                    not re.search(r'\bBase\b', class_decl)):
                    
                    new_decl = re.sub(r'\binternal\s+class\b', 'internal sealed class', class_decl)
                    line = f"{indent}{new_decl}"
                    fixes += 1
                    
                    if self.verbose:
                        self.log_warning(f"  CA1852: Sealed internal class in {os.path.basename(file_path)}")
            
            new_lines.append(line)
        
        self.fix_types["CA1852"] += fixes
        return '\n'.join(new_lines), fixes
    
    def apply_cs1998_fix(self, content: str, file_path: str) -> Tuple[str, int]:
        """Add await Task.CompletedTask to async methods without await"""
        fixes = 0
        
        # Find async methods
        async_pattern = r'(public|private|protected|internal)?\s*async\s+Task[^{]*\{[^}]*\}'
        matches = list(re.finditer(async_pattern, content, re.MULTILINE | re.DOTALL))
        
        for match in reversed(matches):  # Reverse to maintain string positions
            method_content = match.group()
            
            # Check if method contains await
            if 'await' not in method_content:
                start_pos = match.start()
                end_pos = match.end()
                
                # Check for empty method body
                if re.search(r'\{\s*(//[^\r\n]*)?\s*\}', method_content):
                    # Replace with await Task.CompletedTask
                    new_content = re.sub(r'\{\s*(//[^\r\n]*)?\s*\}', '{ await Task.CompletedTask; }', method_content)
                    content = content[:start_pos] + new_content + content[end_pos:]
                    fixes += 1
                    
                    if self.verbose:
                        self.log_warning(f"  CS1998: Added Task.CompletedTask to async method in {os.path.basename(file_path)}")
                
                # For non-empty methods, add return Task.CompletedTask before closing brace
                elif re.search(r'\{[^}]+\}', method_content):
                    # Find the closing brace and add return statement
                    new_content = re.sub(r'(\s*)(\}\s*)$', r'\1return Task.CompletedTask;\n\1\2', method_content)
                    if new_content != method_content:
                        content = content[:start_pos] + new_content + content[end_pos:]
                        fixes += 1
                        
                        if self.verbose:
                            self.log_warning(f"  CS1998: Added Task.CompletedTask return to async method in {os.path.basename(file_path)}")
        
        self.fix_types["CS1998"] += fixes
        return content, fixes
    
    def apply_other_fixes(self, content: str, file_path: str) -> Tuple[str, int]:
        """Apply miscellaneous fixes"""
        fixes = 0
        original_content = content
        
        # Remove trailing whitespace
        lines = content.split('\n')
        new_lines = []
        
        for line in lines:
            # Remove trailing whitespace
            cleaned_line = line.rstrip()
            new_lines.append(cleaned_line)
        
        # Remove consecutive empty lines
        final_lines = []
        prev_empty = False
        
        for line in new_lines:
            if not line.strip():
                if not prev_empty:
                    final_lines.append(line)
                prev_empty = True
            else:
                final_lines.append(line)
                prev_empty = False
        
        # Ensure file ends with newline
        content = '\n'.join(final_lines)
        if content and not content.endswith('\n'):
            content += '\n'
        
        if content != original_content:
            fixes = 1
            self.fix_types["Other"] += fixes
        
        return content, fixes
    
    def find_cs_files(self, directory: str) -> List[Path]:
        """Find all C# files in the directory, excluding generated files"""
        cs_files = []
        
        for root, dirs, files in os.walk(directory):
            # Skip bin and obj directories
            dirs[:] = [d for d in dirs if d not in ['bin', 'obj']]
            
            for file in files:
                if file.endswith('.cs') and not file.endswith('.g.cs') and 'AssemblyInfo.cs' not in file:
                    cs_files.append(Path(root) / file)
        
        return cs_files
    
    def process_file(self, file_path: Path) -> bool:
        """Process a single C# file and apply fixes"""
        try:
            self.log_info(f"Processing {file_path.name}...")
            
            # Read file content
            with open(file_path, 'r', encoding='utf-8') as f:
                original_content = f.read()
            
            if not original_content.strip():
                self.log_warning(f"Skipping empty file: {file_path.name}")
                return False
            
            content = original_content
            total_file_fixes = 0
            
            # Apply all fixes
            content, fixes = self.apply_ca1852_fix(content, str(file_path))
            total_file_fixes += fixes
            
            content, fixes = self.apply_cs1998_fix(content, str(file_path))
            total_file_fixes += fixes
            
            content, fixes = self.apply_other_fixes(content, str(file_path))
            total_file_fixes += fixes
            
            # Write file if changed
            if content != original_content:
                if not self.what_if:
                    with open(file_path, 'w', encoding='utf-8') as f:
                        f.write(content)
                    self.log_success(f"Fixed {file_path.name}")
                else:
                    self.log_warning(f"Would fix {file_path.name}")
                self.total_fixes += 1
                return True
            else:
                if self.verbose:
                    self.log_success(f"No changes needed for {file_path.name}")
                return False
        
        except Exception as e:
            self.log_error(f"Error processing {file_path.name}: {e}")
            return False
        
        finally:
            self.files_processed += 1
    
    def run(self, directory: str = "."):
        """Run the bulk fixer on the specified directory"""
        self.log_info(f"Finding C# files in {directory}...")
        
        cs_files = self.find_cs_files(directory)
        
        print(f"{Colors.BLUE}Found {len(cs_files)} C# files to process.{Colors.NC}")
        print()
        
        self.log_info("Starting bulk fixes...")
        print()
        
        for file_path in cs_files:
            self.process_file(file_path)
        
        # Print summary
        print()
        print(f"{Colors.BLUE}===== BULK FIX SUMMARY ====={Colors.NC}")
        print(f"Files processed: {self.files_processed}")
        print(f"Total files with fixes: {self.total_fixes}")
        print()
        print(f"{Colors.BLUE}Fix breakdown:{Colors.NC}")
        
        for fix_type, count in self.fix_types.items():
            if count > 0:
                print(f"  {fix_type}: {count} fixes")
        
        if self.what_if:
            print()
            self.log_warning("This was a dry run. Remove --what-if to actually apply fixes.")
        
        print()
        self.log_success("Bulk fix operation completed!")

def main():
    parser = argparse.ArgumentParser(description="Apply bulk fixes to C# files")
    parser.add_argument("--what-if", action="store_true", help="Show what would be changed without making changes")
    parser.add_argument("--verbose", "-v", action="store_true", help="Show detailed output")
    parser.add_argument("--directory", "-d", default=".", help="Directory to process")
    
    args = parser.parse_args()
    
    fixer = BulkFixer(what_if=args.what_if, verbose=args.verbose)
    fixer.run(args.directory)

if __name__ == "__main__":
    main()