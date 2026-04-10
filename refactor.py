import sys, re, os

controllers_dir = r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\mlndex-backend\Controllers'

def process_file(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original_content = content

    # Replace ModelState
    content = re.sub(r'^\s*if\s*\(!ModelState\.IsValid\)\s*return\s+BadRequest\(ModelState\);\s*\n', '', content, flags=re.MULTILINE)

    # Replace returning Unauthorized()
    content = re.sub(r'return\s+Unauthorized\((?:\"[^\"]+\")?\);', r'throw new AppException(ErrorCodes.UNAUTHORIZED);', content)
    content = re.sub(r'if\s*\(([^)]+)\)\s*return\s+Unauthorized\((?:\"[^\"]+\")?\);', r'if (\1) throw new AppException(ErrorCodes.UNAUTHORIZED);', content)

    # Replace returning NotFound
    content = re.sub(r'return\s+NotFound\(new\s+ApiResponse[^;]+\);', r'throw new AppException(ErrorCodes.NOT_FOUND);', content)

    # Replace returning BadRequest(new { ... }) or BadRequest(string)
    content = re.sub(r'return\s+BadRequest\([^;]+\);', r'throw new AppException(ErrorCodes.INVALID_INPUT);', content)

    # Replace returning Ok(new ApiResponse<T>(...)) with return OkResponse(...)
    content = re.sub(r'return\s+Ok\(new\s+ApiResponse<[^>]+>\(true,\s*\"[^\"]+\",\s*([^)]+)\)\);', r'return OkResponse(\1);', content)

    # Replace returning Ok(new { success = true, data = history })
    content = re.sub(r'return\s+Ok\(new\s*{\s*success\s*=\s*true,\s*data\s*=\s*([^ }]+)\s*}\);', r'return OkResponse(\1);', content)

    # Replace returning Ok(result) -> OkResponse(result)
    content = re.sub(r'return\s+Ok\(([a-zA-Z0-9_]+)\);', r'return OkResponse(\1);', content)
    
    # Replace return Ok(new { Message = ..., Data = result });
    content = re.sub(r'return\s+Ok\(new\s*{\s*Message\s*=\s*\"[^\"]+\",\s*Data\s*=\s*([^ }]+)\s*}\);', r'return OkResponse(\1);', content)
    
    # Replace return Ok(new { Data = reports });
    content = re.sub(r'return\s+Ok\(new\s*{\s*Data\s*=\s*([^ }]+)\s*}\);', r'return OkResponse(\1);', content)

    # Replace return Ok(new { success = result, message = ... })
    content = re.sub(r'return\s+Ok\(new\s*{\s*success\s*=\s*([a-zA-Z0-9_]+)[^;]+\);', r'return OkResponse(\1);', content)

    # Clean up duplicate namespaces
    if content != original_content:
        # Check if requires new usings
        if 'throw new AppException' in content and 'using Application.Exceptions;' not in content:
            content = 'using Application.Exceptions;\n' + content
        if 'throw new AppException' in content and 'using Application.DTOs.Common;' not in content:
            content = 'using Application.DTOs.Common;\n' + content
            
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)
        print('Updated:', filepath)

for root, _, files in os.walk(controllers_dir):
    for filename in files:
        if filename.endswith('.cs') and filename != 'BaseController.cs' and filename != 'TestingController.cs':
             process_file(os.path.join(root, filename))

print("Refactoring completed.")
