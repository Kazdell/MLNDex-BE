import os
import re

search_dir = r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\Application\Services'

# We want to match: new AppException( SOME_ARGUMENTS_UNTIL_UNBALANCED_CLOSE_PAREN )
# It's tricky with regex, but we can iterate through the file char by char when we see "AppException("

def find_app_exceptions(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    issues = []
    idx = 0
    while True:
        idx = content.find('AppException(', idx)
        if idx == -1:
            break
        
        # start of arguments
        start = idx + len('AppException(')
        
        # find matching closing parenthesis
        paren_count = 1
        curr = start
        in_string = False
        escape = False
        while curr < len(content) and paren_count > 0:
            c = content[curr]
            if in_string:
                if escape:
                    escape = False
                elif c == '\\':
                    escape = True
                elif c == '"':
                    in_string = False
            else:
                if c == '"':
                    in_string = True
                elif c == '(':
                    paren_count += 1
                elif c == ')':
                    paren_count -= 1
                    
            curr += 1
            
        args_str = content[start:curr-1].strip()
        
        # If args_str contains a comma NOT inside a nested parenthesis/string, it has multiple args
        # But honestly, any string literal " inside the args_str means it's passing a string!
        # Except if it's commented out? We don't care, we'll flag it anyway.
        
        if '"' in args_str:
            # Let's count line numbers
            line_no = content.count('\n', 0, idx) + 1
            issues.append(f"Line {line_no}: AppException({args_str})")
            
        idx = curr

    if issues:
        print(f"--- {filepath} ---")
        for i in issues:
            print(i)
        return True
    return False

found_any = False
for root, _, files in os.walk(search_dir):
    for filename in files:
        if filename.endswith('.cs'):
            if find_app_exceptions(os.path.join(root, filename)):
                found_any = True

if not found_any:
    print("ALL CLEAN: No AppException with string literals found in Services.")
