#!/usr/bin/env python3
"""
Fix script to correct incorrect Task.CompletedTask insertions
"""

import os
import re
from pathlib import Path

class IncorrectInsertionFixer:
    def __init__(self):
        self.files_fixed = 0
        self.total_fixes = 0
    
    def fix_file(self, file_path: Path) -> bool:
        """Fix incorrect Task.CompletedTask insertions in a file"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            original_content = content
            fixes_made = 0
            
            # Fix pattern 1: {PluginIdreturn Task.CompletedTask;\n} -> {PluginId}"
            pattern1 = r'(\{PluginId)return Task\.CompletedTask;\s*\n(\})'
            if re.search(pattern1, content):
                content = re.sub(pattern1, r'\1\2', content)
                fixes_made += 1
            
            # Fix pattern 2: {Directory}return Task.CompletedTask;\n -> {Directory}"
            pattern2 = r'(\{Directory)return Task\.CompletedTask;\s*\n(\})'
            if re.search(pattern2, content):
                content = re.sub(pattern2, r'\1\2', content)
                fixes_made += 1
            
            # Fix pattern 3: "...return Task.CompletedTask;\n}" in the middle of lines
            pattern3 = r'(\{[^}]*?)return Task\.CompletedTask;\s*\n(\})'
            matches = re.findall(pattern3, content)
            if matches:
                content = re.sub(pattern3, r'\1\2', content)
                fixes_made += len(matches)
            
            # Fix pattern 4: IsValid = true return Task.CompletedTask;\n };
            pattern4 = r'(IsValid = true)\s*return Task\.CompletedTask;\s*\n\s*(\};)'
            if re.search(pattern4, content):
                content = re.sub(pattern4, r'\1\n        \2', content)
                fixes_made += 1
            
            # Fix pattern 5: Assembly file not found: {assemblyPath}return Task.CompletedTask;\n}
            pattern5 = r'(\{assemblyPath)return Task\.CompletedTask;\s*\n(\})'
            if re.search(pattern5, content):
                content = re.sub(pattern5, r'\1\2', content)
                fixes_made += 1
            
            # Fix pattern 6: Remove incorrect insertions in string interpolation
            pattern6 = r'(\{[^}]*?)return Task\.CompletedTask;\s*\n(\}[^;]*;)'
            if re.search(pattern6, content):
                content = re.sub(pattern6, r'\1\2', content)
                fixes_made += 1
            
            # Fix pattern 7: return Task.CompletedTask;\n\n} in the middle of variable assignments
            pattern7 = r'(.*?return Task\.CompletedTask;)\s*\n\s*(\n\s*};)'
            # This is more complex, let's target specific patterns
            
            # Fix pattern 8: Specific malformed lines
            malformed_patterns = [
                # No plugin types found in assembly: {manifest.AssemblyPathreturn Task.CompletedTask;\n}
                (r'(\{manifest\.AssemblyPath)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
                # {PluginIdreturn Task.CompletedTask;\n} (various forms)
                (r'(\{PluginId)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
                # {assemblyPath}return Task.CompletedTask;\n}
                (r'(\{assemblyPath)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
                # {Type} (assembly path ignored in AOT mode)
                (r'(\{Type)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
                # Failed to load plugin {PluginIdreturn Task.CompletedTask;\n}
                (r'(\{PluginId)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
                # Plugin not running {PluginIdreturn Task.CompletedTask;\n}
                (r'(\{PluginId)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
                # Cannot start plugin in state {Statereturn Task.CompletedTask;\n}
                (r'(\{State)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
                # Configuration changed for plugin {PluginIdreturn Task.CompletedTask;\n}
                (r'(\{PluginId)return Task\.CompletedTask;\s*\n(\})', r'\1\2'),
            ]
            
            for pattern, replacement in malformed_patterns:
                if re.search(pattern, content):
                    content = re.sub(pattern, replacement, content)
                    fixes_made += 1
            
            # Fix incorrect insertions in the middle of statements
            # Look for ); followed by return Task.CompletedTask;\n that shouldn't be there
            pattern9 = r'(\);)\s*return Task\.CompletedTask;\s*\n'
            if re.search(pattern9, content):
                content = re.sub(pattern9, r'\1\n', content)
                fixes_made += 1
            
            # Fix pattern with variable initialization broken by return statement
            pattern10 = r'(=.*?return Task\.CompletedTask;)\s*\n\s*(.*?;)'
            matches = re.finditer(pattern10, content, re.MULTILINE)
            for match in matches:
                # This needs more careful handling - let's skip for now
                pass
            
            # Write the file if we made changes
            if content != original_content:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                self.files_fixed += 1
                self.total_fixes += fixes_made
                print(f"✅ Fixed {fixes_made} issues in {file_path.name}")
                return True
            
            return False
            
        except Exception as e:
            print(f"❌ Error processing {file_path}: {e}")
            return False
    
    def fix_all_files(self, directory: str = "."):
        """Fix all C# files in the directory"""
        cs_files = []
        
        for root, dirs, files in os.walk(directory):
            # Skip bin and obj directories
            dirs[:] = [d for d in dirs if d not in ['bin', 'obj']]
            
            for file in files:
                if file.endswith('.cs') and not file.endswith('.g.cs') and 'AssemblyInfo.cs' not in file:
                    cs_files.append(Path(root) / file)
        
        print(f"🔍 Found {len(cs_files)} C# files to check")
        
        for file_path in cs_files:
            self.fix_file(file_path)
        
        print(f"\n📊 Summary:")
        print(f"Files processed: {len(cs_files)}")
        print(f"Files fixed: {self.files_fixed}")
        print(f"Total fixes applied: {self.total_fixes}")

if __name__ == "__main__":
    fixer = IncorrectInsertionFixer()
    fixer.fix_all_files()