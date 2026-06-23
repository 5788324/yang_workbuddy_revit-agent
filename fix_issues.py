import os
import re

def process_file(path, replacements):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    new_content = content
    for pattern, repl in replacements:
        new_content = re.sub(pattern, repl, new_content)
    
    if new_content != content:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Updated {path}")
    else:
        print(f"No changes made to {path}")

def fix_performance_linq():
    replacements = [
        # .OfClass(...).Select(x => x.Id).ToList() -> .OfClass(...).ToElementIds().ToList()
        # Wait, in C#, we can just replace .Select(x => x.Id).ToList()
        (r'\.Cast<[^>]+>\(\)\.Select\(\w+ => \w+\.Id\)\.ToList\(\)', r'.ToElementIds().ToList()'),
        (r'\.Select\(\w+ => \w+\.Id\)\.ToList\(\)', r'.ToElementIds().ToList()'),
        # .FirstOrDefault() -> .FirstElement()
        # Wait, FirstElement() returns Element. If it was casted to T, we need FirstElement() as T.
        (r'\.Cast<([^>]+)>\(\)\.FirstOrDefault\(\)', r'.FirstElement() as \1'),
        (r'\.OfClass\(([^)]+)\)\.FirstOrDefault\(\)', r'.OfClass(\1).FirstElement()'),
        # Cast<FilledRegionType>().Select(f => f.Name).ToHashSet()
        # Not easily fixable to native, but wait, the report said "Uses .Cast<FilledRegionType>().Select(f => f.Name).ToHashSet()" 
        # But wait, we can't do .ToElementIds() for Name. The report just noted it as an inefficiency or just grouped it.
    ]
    
    files = [
        r'src\YangTools.Revit\UI\ProjectAssetManagerWindow.xaml.cs',
        r'src\YangTools.Revit\UI\SheetManagerWindow.xaml.cs'
    ]
    
    for file in files:
        if os.path.exists(file):
            process_file(file, replacements)

def fix_caching_and_leak():
    # Caching in ProjectAssetManagerWindow CheckFilterUsedInViews / CheckFilterUsedInTemplates
    pass

if __name__ == "__main__":
    fix_performance_linq()
