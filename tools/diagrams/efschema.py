"""Read the SIMF database schema out of the EF Core model snapshots.

The snapshots are the generated record of the model that produced the database,
so the entity relationship sheets are drawn from the real schema rather than
from a diagram kept by hand. Nothing here is authored: every table, column,
type, key, index and foreign key comes out of the two snapshot files.

Used by fig5_erd_conceptual.py and fig6_erd_full.py.
"""
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MIGRATIONS = os.path.join(ROOT, "src", "Backend", "SIMF.Infrastructure",
                          "Persistence", "Migrations")
SNAPSHOTS = {
    "App": os.path.join(MIGRATIONS, "App", "SimfAppDbContextModelSnapshot.cs"),
    "Identity": os.path.join(MIGRATIONS, "Identity",
                             "SimfIdentityDbContextModelSnapshot.cs"),
}

_ENTITY = re.compile(r'modelBuilder\.Entity\("([^"]+)",\s*b\s*=>')
_PROP = re.compile(r'b\.Property<([^>]+)>\("([^"]+)"\)')
_HASKEY = re.compile(r'b\.HasKey\(([^)]*)\)')
_HASINDEX = re.compile(r'b\.HasIndex\(([^)]*)\)')
_TOTABLE = re.compile(r'b\.ToTable\("([^"]+)"')
_HASONE = re.compile(r'b\.HasOne\("([^"]+)"(?:,\s*"([^"]*)")?\)')

# Short forms so a column type fits inside an entity box.
_SHORT = {
    "uniqueidentifier": "guid", "datetime2": "datetime", "bit": "bool",
    "int": "int", "bigint": "bigint", "nvarchar(max)": "text",
    "float": "float", "decimal": "decimal", "varbinary(max)": "binary",
    "time": "time", "date": "date", "smallint": "smallint",
}


def short_type(sql, clr):
    if not sql:
        return clr.replace("System.", "").replace("Nullable<", "").rstrip(">").lower()
    if sql in _SHORT:
        return _SHORT[sql]
    m = re.match(r"nvarchar\((\d+)\)", sql)
    if m:
        return f"str({m.group(1)})"
    return sql


def short_name(clr):
    """Last segment of a CLR name, keeping a generic argument readable.

    'Microsoft.AspNetCore.Identity.IdentityUserToken<System.Guid>' becomes
    'IdentityUserToken<Guid>' rather than the 'Guid>' a plain split would give.
    """
    if "<" in clr:
        outer, arg = clr.split("<", 1)
        return f"{outer.split('.')[-1]}<{arg.rstrip('>').split('.')[-1]}>"
    return clr.split(".")[-1]


def _blocks(src):
    for m in _ENTITY.finditer(src):
        start, depth, i, started = m.end(), 0, m.end(), False
        while i < len(src):
            if src[i] == "{":
                depth += 1
                started = True
            elif src[i] == "}":
                depth -= 1
                if started and depth == 0:
                    break
            i += 1
        yield m.group(1), src[start:i]


def load():
    """Return (entities, relationships) read from both snapshots."""
    entities, rels, seen = {}, [], set()
    for db, path in SNAPSHOTS.items():
        src = open(path, encoding="utf-8").read()
        for clr, block in _blocks(src):
            short = short_name(clr)
            if "b.Property<" in block:
                props = []
                for pm in _PROP.finditer(block):
                    seg = block[pm.end():pm.end() + 420].split("b.Property<")[0]
                    ct = re.search(r'\.HasColumnType\("([^"]+)"\)', seg)
                    props.append({
                        "name": pm.group(2),
                        "clr": pm.group(1),
                        "sql": ct.group(1) if ct else "",
                        "required": ".IsRequired()" in seg,
                    })
                key = _HASKEY.search(block)
                idx = []
                for im in _HASINDEX.finditer(block):
                    cols = [c.strip().strip('"') for c in im.group(1).split(",")
                            if c.strip()]
                    tail = block[im.end():im.end() + 300].split("b.HasIndex")[0]
                    idx.append({"cols": cols, "unique": ".IsUnique()" in tail})
                tbl = _TOTABLE.search(block)
                entities[short] = {
                    "clr": clr, "db": db,
                    "table": tbl.group(1) if tbl else short,
                    "props": props,
                    "pk": [k.strip().strip('"') for k in key.group(1).split(",")]
                          if key else [],
                    "indexes": idx,
                }
            for hm in _HASONE.finditer(block):
                tail = block[hm.end():hm.end() + 700]
                fkm = re.search(r'\.HasForeignKey\("([^"]+)"\)', tail)
                fk = fkm.group(1) if fkm else None
                target = short_name(hm.group(1))
                sig = (short, target, fk)
                if sig in seen:
                    continue
                seen.add(sig)
                rels.append({
                    "from": short, "to": target, "fk": fk, "db": db,
                    "kind": "1-1" if ".WithOne(" in tail else "1-N",
                })
    return entities, rels


def columns_for(entity):
    """Return (marker, name, type) rows for an entity, keys first."""
    pk = set(entity["pk"])
    fks = {r["fk"] for r in RELATIONSHIPS if r["from"] == entity_name(entity) and r["fk"]}
    uniq = {c for i in entity["indexes"] if i["unique"] for c in i["cols"]}
    rows = []
    for p in entity["props"]:
        marker = "PK" if p["name"] in pk else ("FK" if p["name"] in fks else
                                               ("U" if p["name"] in uniq else ""))
        rows.append((marker, p["name"], short_type(p["sql"], p["clr"])))
    rows.sort(key=lambda r: {"PK": 0, "FK": 1, "U": 2, "": 3}[r[0]])
    return rows


def entity_name(entity):
    return short_name(entity["clr"])


ENTITIES, RELATIONSHIPS = load()

if __name__ == "__main__":
    import sys
    sys.stdout.reconfigure(encoding="utf-8")
    print(f"entities {len(ENTITIES)}  relationships {len(RELATIONSHIPS)}  "
          f"columns {sum(len(e['props']) for e in ENTITIES.values())}  "
          f"indexes {sum(len(e['indexes']) for e in ENTITIES.values())}")
