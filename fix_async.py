import re
import sys

# Read the file
with open(sys.argv[1], 'r') as f:
    content = f.read()

# Find all async ValueTask methods that return simple values and convert them
patterns = [
    (r'private async ValueTask<([^>]+)> (\w+Async)<([^>]*)>\(\s*([^)]*)\)\s*where\s*T\s*:\s*unmanaged\s*\n\s*{', 
     r'private \1 \2<\3>(\4) where T : unmanaged\n    {'),
    (r'private async ValueTask<([^>]+)> (\w+Async)\(\s*([^)]*)\)\s*\n\s*{', 
     r'private \1 \2(\3)\n    {'),
    (r'private async Task<([^>]+)> (\w+Async)\(\s*([^)]*)\)\s*\n\s*{', 
     r'private \1 \2(\3)\n    {'),
]

# Apply the patterns
for pattern, replacement in patterns:
    content = re.sub(pattern, replacement, content, flags=re.MULTILINE)

# Remove await Task.CompletedTask calls since we're making them sync
content = re.sub(r'\s*await Task\.CompletedTask\.ConfigureAwait\(false\);\s*\n', '\n', content)

# Write back the file
with open(sys.argv[1], 'w') as f:
    f.write(content)
