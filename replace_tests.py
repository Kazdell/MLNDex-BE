import os
import re

dir_path = 'Application.Tests'

for root, dirs, files in os.walk(dir_path):
    # Exclude bin and obj
    dirs[:] = [d for d in dirs if d not in ['bin', 'obj']]
    for file in files:
        if file.endswith('.cs'):
            file_path = os.path.join(root, file)
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            new_content = content
            new_content = re.sub(r'Assert\.ThrowsAsync<InvalidOperationException>', r'Assert.ThrowsAsync<Application.Exceptions.AppException>', new_content)
            new_content = re.sub(r'Assert\.ThrowsAsync<Exception>', r'Assert.ThrowsAsync<Application.Exceptions.AppException>', new_content)
            new_content = re.sub(r'Assert\.Throws<InvalidOperationException>', r'Assert.Throws<Application.Exceptions.AppException>', new_content)
            new_content = re.sub(r'Assert\.Throws<Exception>', r'Assert.Throws<Application.Exceptions.AppException>', new_content)

            if new_content != content:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(new_content)
                print(f"Updated {file_path}")
