"""Assert every column the LLD data dictionary names exists in the EF migration.

Why this exists. The LLD was rewritten twice and passed a verification suite
each time, and 26 rows of its data dictionary still described columns the
schema does not have. The suite checked the text against itself and against the
deployment diagram, and never against the code, so it could only ever find the
defects its author had already thought of. This is the missing gate: the schema
is the authority, and the document is checked against it.

Run:  python tools/check-lld-schema.py
Exit: 0 when every identifier the dictionary names is in a migration, 1 otherwise.
"""
import re
import sys
import zipfile

ROOT = r'd:/SIMF/System/V1.0.0'
LLD = ROOT + '/docs/SIMF-LLD-003-Solution-Design-Document-v1.5.docx'
MIGRATIONS = [
    ROOT + '/src/Backend/SIMF.Infrastructure/Persistence/Migrations/App/00000000000000_InitialCreate.cs',
    ROOT + '/src/Backend/SIMF.Infrastructure/Persistence/Migrations/Identity/00000000000000_InitialCreate.cs',
]

# Identifiers the dictionary may legitimately name that are not columns: enum
# member names, project names, and the handful of prose words that happen to be
# PascalCase. Keep this list short and argued, or it becomes a way to hide a
# real miss.
ALLOW = {
    'SimfAppDbContext', 'SimfIdentityDbContext', 'InitialCreate',
    'ProfileType', 'MobileAppRole', 'NotificationKind', 'BookingStatus',
    'EmailTemplateType', 'RatingScope', 'FileService', 'AuditOutcome',
    'UserType', 'AccountState', 'AccountCodePurpose', 'SecondFactorKind',
    'SessionStatus', 'SeatReservationKind',
}


def docx_text(path):
    with zipfile.ZipFile(path) as z:
        xml = z.read('word/document.xml').decode('utf-8')
    xml = re.sub(r'</w:p>', '\n', xml)
    return re.sub(r'<[^>]+>', '', xml)


def main():
    schema = '\n'.join(open(p, encoding='utf-8').read() for p in MIGRATIONS)
    known = set(re.findall(r'(\w+) = table\.Column<', schema))
    known |= set(re.findall(r'name: "(\w+)"', schema))

    # The data dictionary is the run of tables whose cells are bare identifiers.
    # A cell that is a single PascalCase or camelCase word ending in Id, At, Utc,
    # Path, Url, Name, Code, Hash, Number or Count is a column claim.
    suspect = re.compile(
        r'^(?:[A-Z][A-Za-z0-9]*)'
        r'(Id|At|Utc|Path|Url|Name|Code|Hash|Number|Count|FileId|State|Kind|Status)$')

    missing = []
    for line in docx_text(LLD).split('\n'):
        cell = line.strip()
        if not cell or ' ' in cell or len(cell) < 4:
            continue
        if cell in ALLOW or cell in known:
            continue
        if suspect.match(cell):
            missing.append(cell)

    if not missing:
        print('PASS: every column the LLD data dictionary names exists in a migration.')
        return 0

    print('FAIL: the LLD names %d identifier(s) no migration creates:' % len(set(missing)))
    for name in sorted(set(missing)):
        print('  %s' % name)
    print('\nThe schema is the authority. Correct the document, or add the name to')
    print('ALLOW with a reason if it is deliberately not a column.')
    return 1


if __name__ == '__main__':
    sys.exit(main())
