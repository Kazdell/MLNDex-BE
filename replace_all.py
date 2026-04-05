import os
import re

directories = ['Application', 'Infrastructure', 'mlndex-backend']

for directory in directories:
    for root, dirs, files in os.walk(directory):
        dirs[:] = [d for d in dirs if d not in ['bin', 'obj']]
        for file in files:
            if file.endswith('.cs'):
                file_path = os.path.join(root, file)
                with open(file_path, 'r', encoding='utf-8') as f:
                    content = f.read()

                new_content = content
                # Advanced regex to match throw new Exception(anything); and throw new InvalidOperationException(anything);
                # Matches cross-line until the closing semicolon.
                new_content = re.sub(
                    r'throw\s+new\s+InvalidOperationException\(([\s\S]*?)\)\s*;',
                    r'throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, \1);',
                    new_content
                )
                
                new_content = re.sub(
                    r'throw\s+new\s+Exception\(([\s\S]*?)\)\s*;',
                    r'throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.VALIDATION_ERROR, \1);',
                    new_content
                )

                if new_content != content:
                    with open(file_path, 'w', encoding='utf-8') as f:
                        f.write(new_content)
                    print(f"Updated {file_path}")
