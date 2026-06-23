import os
import re

def fix_memory_leaks():
    files = [
        r'src\YangTools.Revit\UI\ProjectAssetManagerWindow.xaml.cs',
        r'src\YangTools.Revit\UI\SheetManagerWindow.xaml.cs',
        r'src\YangTools.Revit\UI\FamilyInstanceManagerWindow.xaml.cs',
        r'src\YangTools.Revit\UI\FamilyManagerWindow.xaml.cs',
        r'src\YangTools.Revit\UI\LinearPlacementWindow.xaml.cs'
    ]

    for file in files:
        if not os.path.exists(file):
            continue
        with open(file, 'r', encoding='utf-8') as f:
            content = f.read()
        
        if 'protected override void OnClosed' in content:
            continue
        
        # Inject just before the last closing brace of the Window class
        # Look for the last '}' that closes the class.
        # An easier way is to insert it after the constructor, or just before the end of the class.
        # Let's insert it before the first '#region' or if not found, just after InitializeComponent() inside the class.
        # Actually, finding the class constructor and inserting after it is easiest.
        pattern = re.compile(r'(public \w+\([^)]*\)\s*\{[^}]*InitializeComponent\(\);[^}]*\})', re.DOTALL)
        match = pattern.search(content)
        if match:
            constructor = match.group(1)
            replacement = constructor + '\n\n        protected override void OnClosed(EventArgs e)\n        {\n            base.OnClosed(e);\n            _externalEvent?.Dispose();\n        }'
            content = content.replace(constructor, replacement)
            with open(file, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"Fixed leak in {file}")

if __name__ == "__main__":
    fix_memory_leaks()
