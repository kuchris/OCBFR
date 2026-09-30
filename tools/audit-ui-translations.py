"""Audit static UI strings using the same longest-prefix rule as UiText.Render."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
entries = sorted(json.loads((root / 'src/NorthIslandChestPlugin/UiTranslations.json').read_text(encoding='utf-8')), key=lambda e: -len(e['Source']))

def render(source, language):
    output = []
    while source:
        entry = next((e for e in entries if source.startswith(e['Source'])), None)
        if entry:
            output.append(entry[language])
            source = source[len(entry['Source']):]
        else:
            output.append(source[0])
            source = source[1:]
    return ''.join(output)

incomplete = 0
for entry in entries:
    if set('战斗后并范默认复闲么').intersection(entry['TraditionalChinese']):
        incomplete += 1
        print('INCOMPLETE TRADITIONAL:', entry['Source'], '=>', entry['TraditionalChinese'])

checked = 0
for name in ['ui-inventory.json', 'dashboard-inventory.json']:
    inventory = json.loads((root / 'artifacts' / name).read_text(encoding='utf-8-sig'))
    for value in inventory:
        if not value['Context'].startswith('Draw'):
            continue
        # Diagnostic logs retain their established text; language names are
        # native autonyms so the language picker remains understandable.
        if any(call.startswith('log.') for call in value['Calls']) or value['Context'] == 'DrawLanguageSelector':
            continue
        visible = value['Text'].split('##')[0]
        if not visible:
            continue
        checked += 1
        text = render(visible, 'English')
        if re.search('[\u4e00-\u9fff]', text):
            incomplete += 1
            print(f"MISSING ENGLISH: {value['Context']}:{value['Line']}: {visible} => {text}")
print(f'Audited {len(entries)} catalog entries and {checked} UI literals; issues={incomplete}')
raise SystemExit(1 if incomplete else 0)
