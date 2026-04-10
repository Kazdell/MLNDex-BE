import sys, re, os

search_dir = r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\Application.Tests'

# Matches .WithMessage("...") or .WithMessage($"...") or .WithMessage('*...')
# Wait, the regex needs to be careful because some might have variables instead of strings.
# The simplest would be to remove .WithMessage(...) universally for tests that deal with AppException.
pattern = re.compile(r'\.WithMessage\([^)]+\)')

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    content = pattern.sub('', content)

    if content != original_content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print('Updated:', filepath)

for root, _, files in os.walk(search_dir):
    for filename in files:
        if filename.endswith('.cs'):
            process_file(os.path.join(root, filename))

print("Refactoring Tests completed.")
