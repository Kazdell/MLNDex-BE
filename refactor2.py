import sys, re, os

controllers_dir = r'C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\mlndex-backend\Controllers'

for root, _, files in os.walk(controllers_dir):
    for filename in files:
        if filename.endswith('.cs') and filename != 'BaseController.cs':
            filepath = os.path.join(root, filename)
            with open(filepath, 'r', encoding='utf-8') as f:
                content = f.read()

            original_content = content
            content = re.sub(r':\s*ControllerBase', r': BaseController', content)
            
            if content != original_content:
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(content)
                print('Updated to BaseController:', filepath)
