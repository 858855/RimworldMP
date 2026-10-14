#!/usr/bin/env python3
"""Static XML structure check for THIS knowledge base (no game-installation required).
Not a RimWorld XML-schema validator, no claims of load-time or multiplayer validity.
"""
from pathlib import Path
from xml.etree import ElementTree
import sys

root = Path(__file__).resolve().parents[1]
xml_files = sorted(root.rglob('*.xml'))
errors = []
print(f'Checking {len(xml_files)} XML examples in {root.name}')
for path in xml_files:
    try:
        tree = ElementTree.parse(path)
        tag = tree.getroot().tag
        if path.name == 'About.xml' and tag != 'ModMetaData':
            errors.append(f'{path}: expected ModMetaData, found {tag}')
        if path.name == 'About.xml':
            meta = tree.getroot()
            if not (meta.findtext('packageId') or '').strip():
                errors.append(f'{path}: missing packageId')
            if meta.find('supportedVersions') is None:
                errors.append(f'{path}: missing supportedVersions')
        print('XML_OK', path.relative_to(root), tag)
    except ElementTree.ParseError as exc:
        errors.append(f'{path}: {exc}')
for e in errors:
    print('ERROR:', e, file=sys.stderr)
if errors:
    sys.exit(1)
print(f'PASS: XML syntactically valid, {len(xml_files)} file(s).')
print('NOT TESTED: RimWorld load/definitions/XPath, C# build, or multiplayer.')
