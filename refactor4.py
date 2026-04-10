import sys, re, os

search_dirs = [
    r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\Application\Services',
]

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content
    # Look for AppException(..., $"...) or AppException(..., "...) where strings can span multiple lines using string concatenation.
    # We will just aggressively replace 	hrow new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.XXX, ANYTHING);
    # with 	hrow new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.XXX);
    # Warning: this uses regex with dotall if we are not careful.
    
    # Let's match 	hrow new \s* (?:Application\.Exceptions\.)?AppException\s*\(\s*(?:Application\.DTOs\.Common\.)?ErrorCodes\.([A-Z0-9_]+)\s*,\s*.*? up to the matching )
    # Since regex for balanced parenthesis is hard, we will just split by 	hrow new  and process.
    
    parts = content.split('throw new ')
    new_parts = [parts[0]]
    
    for part in parts[1:]:
        if 'AppException' in part and 'ErrorCodes.' in part:
            # try to find the closing parenthesis of the exception
            # We match AppException up to the first open parenthesis
            m = re.match(r'(.*?AppException\s*\(\s*.*?ErrorCodes\.[A-Z0-9_]+)\s*,\s*(.*)', part, re.DOTALL)
            if m:
                # we have a comma after the error code.
                # let's find the closing parenthesis of the exception.
                prefix = m.group(1)
                remainder = m.group(2)
                
                # find the first );
                idx = remainder.find(');')
                if idx != -1:
                    part = prefix + ');' + remainder[idx+2:]
                    
        new_parts.append(part)
        
    content = 'throw new '.join(new_parts)

    if content != original_content:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print('Updated:', filepath)

for search_dir in search_dirs:
    for root, _, files in os.walk(search_dir):
        for filename in files:
            if filename.endswith('.cs'):
                process_file(os.path.join(root, filename))

print("Refactoring 4 completed.")
