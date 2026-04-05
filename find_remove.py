import ast
import os

def check_file(filename):
    with open(filename, 'r', encoding='utf-8') as f:
        content = f.read()
    
    tree = ast.parse(content)
    
    for node in ast.walk(tree):
        if isinstance(node, ast.Call):
            if isinstance(node.func, ast.Attribute):
                if node.func.attr in ('remove', 'index'):
                    print(f"{filename}:{node.lineno} -> {node.func.attr}()")

for root, dirs, files in os.walk('mahjong_engine'):
    for file in files:
        if file.endswith('.py'):
            check_file(os.path.join(root, file))
