import sys, re, os

search_dirs = [
    r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\Application\Services',
    r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\Infrastructure'
]

# This pattern matches 	hrow new AppException(ErrorCode.SOMETHING, "message")
# and variations like 	hrow new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.XXX, "...")
# The group \1 will capture 	hrow new ...AppException(...ErrorCodes.XXX
# We'll just remove the comma and the string inside the parenthesis.
pattern = re.compile(r'(throw\s+new\s+(?:Application\.Exceptions\.)?AppException\s*\(\s*(?:Application\.DTOs\.Common\.)?ErrorCodes\.[A-Z0-9_]+)\s*,\s*(?:\$|@)?\"[^\"]*\"\s*\)')
# What if the string spans multiple lines? Or ends differently?
# Let's match the exact syntax broadly.
pattern2 = re.compile(r'(throw\s+new\s+(?:Application\.Exceptions\.)?AppException\s*\(\s*(?:Application\.DTOs\.Common\.)?ErrorCodes\.[A-Z0-9_]+)\s*,\s*(?:\$|@)?\"[^\"]*\"\s*\)')

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content

    content = pattern.sub(r'\1)', content)
    content = pattern2.sub(r'\1)', content)
    
    # Handle single parameter exceptions that were just a string without ErrorCodes?
    # e.g. throw new AppException("Something went wrong"); 
    content = re.sub(r'throw\s+new\s+(?:Application\.Exceptions\.)?AppException\(\s*(?:\$|@)?\"[^\"]*\"\s*\)', r'throw new AppException(ErrorCodes.INTERNAL_SERVER_ERROR)', content)

    if content != original_content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print('Updated:', filepath)

for search_dir in search_dirs:
    for root, _, files in os.walk(search_dir):
        for filename in files:
            if filename.endswith('.cs'):
                process_file(os.path.join(root, filename))

print("Refactoring Services completed.")
