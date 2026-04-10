import os
import re

search_dir = r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\mlndex-backend\Controllers'

def find_app_exceptions(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    issues = []
    idx = 0
    while True:
        idx = content.find('AppException(', idx)
        if idx == -1:
            break
        
        start = idx + len('AppException(')
        
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
        
        if '"' in args_str:
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
    print("ALL CLEAN: No AppException with string literals found in Controllers.")
