#!/bin/bash

file="src/Extensions/DotCompute.Linq/Analysis/ArithmeticOperatorAnalyzer.cs"

# Fix all incomplete switch expressions by adding missing closing braces and semicolons
# This handles patterns where switch statements are missing their closing brace and semicolon

# Method to find and fix incomplete switch expressions and other syntax issues
python3 << 'PYTHON_EOF'
import re

with open("src/Extensions/DotCompute.Linq/Analysis/ArithmeticOperatorAnalyzer.cs", "r") as f:
    content = f.read()

# Fix incomplete switch expressions and object initializers line by line
lines = content.split('\n')
fixed_lines = []
in_switch = False
in_object_init = False
brace_count = 0

for i, line in enumerate(lines):
    # Check if we're starting a switch expression
    if "=> " in line and " switch" in line and "{" not in line:
        in_switch = True
        fixed_lines.append(line)
        fixed_lines.append("    {")
        continue
    
    # Check if we're starting an object initializer
    if "return new " in line and "{" not in line and ";" not in line:
        in_object_init = True
        fixed_lines.append(line)
        fixed_lines.append("        {")
        continue
    
    # If we're in a switch, look for end patterns
    if in_switch:
        if "_ =>" in line and not line.strip().endswith(";"):
            # This is the default case, add closing brace and semicolon
            fixed_lines.append(line)
            fixed_lines.append("    };")
            in_switch = False
            continue
    
    # If we're in object init, look for end patterns  
    if in_object_init:
        if line.strip() and not line.strip().startswith("//") and "=" in line and not line.strip().endswith(","):
            # Last property line, add closing brace and semicolon
            fixed_lines.append(line + ",")
            fixed_lines.append("        };")
            fixed_lines.append("    }")
            in_object_init = False
            continue
    
    fixed_lines.append(line)

# Write the fixed content back
with open("src/Extensions/DotCompute.Linq/Analysis/ArithmeticOperatorAnalyzer.cs", "w") as f:
    f.write('\n'.join(fixed_lines))

PYTHON_EOF
