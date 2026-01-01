#!/usr/bin/env python3
"""Fix decompiler artifacts in C# files."""
import re
import sys
import os

def fix_file(filepath):
    with open(filepath, 'r') as f:
        content = f.read()

    original = content

    # Pattern 1: Fix ((Type)(ref var))._002Ector(args) on same line as declaration
    # Pattern: Type var = default(Type); followed by ((Type)(ref var))._002Ector(args);
    # Combine: Type var = default(Type); + var._002Ector(args) -> Type var = new Type(args);

    # First, fix the _002Ector pattern to use "new Type(...)"
    # Pattern: ((TypeName)(ref varName))._002Ector(args);
    pattern_ctor = r'\(\((SK\w+|SKRect|SKSize|SKPoint|SKColor|Thickness|Font|LayoutOptions|SKFontMetrics|RectF|Rect)\)\(ref\s+(\w+)\)\)\._002Ector\(([^;]+)\);'

    def replace_ctor(match):
        type_name = match.group(1)
        var_name = match.group(2)
        args = match.group(3)
        return f'{var_name} = new {type_name}({args});'

    content = re.sub(pattern_ctor, replace_ctor, content)

    # Also handle simpler pattern: var._002Ector(args);
    pattern_simple = r'(\w+)\._002Ector\(([^;]+)\);'
    def replace_simple(match):
        var_name = match.group(1)
        args = match.group(2)
        # We need to figure out the type from context - look for declaration
        return f'// FIXME: {var_name} = new TYPE({args});'

    # Don't do the simple pattern - it's harder to fix without knowing the type

    # Pattern 2: Fix _003F (which is just ?)
    content = content.replace('_003F', '?')

    # Pattern 2.5: Fix broken nullable cast patterns
    # (((??)something) ?? fallback) -> (something ?? fallback)
    content = re.sub(r'\(\(\(\?\?\)(\w+\.\w+)\)', r'(\1', content)
    content = content.replace('((?)', '((')  # Fix broken nullable casts
    content = content.replace('(?))', '))')  # Fix broken casts

    # Pattern 3: Clean up remaining ((Type)(ref var)) patterns without _002Ector
    # These become just var
    # First handle more types: Font, Thickness, Color, LayoutOptions, GridLength, etc.
    types_to_fix = r'SK\w+|Font|Thickness|Color|LayoutOptions|SKFontMetrics|Rectangle|Point|Size|Rect|GridLength|GRGlFramebufferInfo|CornerRadius|RectF'
    pattern_ref = r'\(\((' + types_to_fix + r')\)\(ref\s+(\w+)\)\)'
    content = re.sub(pattern_ref, r'\2', content)

    # Pattern 3.5: Handle static property refs like ((SKColor)(ref SKColors.White))
    pattern_static_ref = r'\(\((' + types_to_fix + r')\)\(ref\s+(\w+\.\w+)\)\)'
    content = re.sub(pattern_static_ref, r'\2', content)

    # Pattern 4: Also handle ViewHandler casts like ((ViewHandler<ISearchBar, SkiaSearchBar>)(object)handler)
    # This should stay as-is but the inner (ref x) needs fixing first

    # Pattern 5: Fix simple (ref var) that might appear in other contexts
    # Pattern: (ref varName) when standalone (not in a cast)
    # Skip for now as this could break valid ref usage

    if content != original:
        with open(filepath, 'w') as f:
            f.write(content)
        return True
    return False

def main():
    base_dir = '/Users/nible/Documents/GitHub/maui-linux-main'
    count = 0
    for root, dirs, files in os.walk(base_dir):
        # Skip hidden dirs and .git
        dirs[:] = [d for d in dirs if not d.startswith('.')]
        for fname in files:
            if fname.endswith('.cs'):
                filepath = os.path.join(root, fname)
                if fix_file(filepath):
                    print(f'Fixed: {filepath}')
                    count += 1
    print(f'Fixed {count} files')

if __name__ == '__main__':
    main()
