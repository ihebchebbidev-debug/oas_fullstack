#!/usr/bin/env python3
"""Static inventory of ASP.NET controllers in Backend/.
Zero inference: only literal attribute text is reported."""
import re, os, sys, json, collections

ROOT = "Backend"
HTTP = re.compile(r'^\[Http(Get|Post|Put|Patch|Delete|Head|Options)(?:\("([^"]*)"\))?')
ROUTE = re.compile(r'^\[Route\("([^"]*)"\)\]')
CLASS = re.compile(r'\b(?:public|internal)\s+(?:partial\s+)?class\s+(\w+Controller)\b')
METHOD = re.compile(r'^(?:public|internal|protected|private)\s+.*\b(\w+)\s*\(')
PERM = re.compile(r'^\[RequirePermission\(([^)]*)\)\]')

def files():
    for root, _, fs in os.walk(ROOT):
        for f in sorted(fs):
            if f.endswith("Controller.cs") or (f.endswith(".cs") and "Controllers" in root):
                yield os.path.join(root, f)

def parse(path):
    lines = open(path, encoding="utf-8", errors="ignore").read().split("\n")
    cls, route, actions = None, None, []
    pending, perms, auth = [], [], None
    class_auth, class_perm = None, None
    for i, raw in enumerate(lines, 1):
        s = raw.strip()
        if s.startswith("//"):
            continue
        m = ROUTE.match(s)
        if m and cls is None:
            route = m.group(1); continue
        m = CLASS.search(s)
        if m and cls is None:
            cls = m.group(1); continue
        m = HTTP.match(s)
        if m:
            pending.append((m.group(1).upper(), m.group(2) or "")); continue
        m = PERM.match(s)
        if m:
            if cls is None: class_perm = m.group(1).replace('"', '')
            else: perms.append(m.group(1).replace('"', ''))
            continue
        if s.startswith("[Authorize"):
            if cls is None: class_auth = "Authorize"
            else: auth = "Authorize"
            continue
        if s.startswith("[AllowAnonymous"):
            if cls is None: class_auth = "AllowAnonymous"
            else: auth = "AllowAnonymous"
            continue
        if s.startswith("["):
            continue
        if pending and s:
            mm = METHOD.match(s)
            name = mm.group(1) if mm else s[:60]
            actions.append({"line": i, "name": name, "verbs": pending,
                            "perm": perms[-1] if perms else None, "auth": auth})
            pending, perms, auth = [], [], None
    return cls, route, actions, class_auth, class_perm

def main():
    mods = collections.defaultdict(list)
    for p in files():
        txt = open(p, encoding="utf-8", errors="ignore").read()
        if "ControllerBase" not in txt and "[Http" not in txt:
            continue
        cls, route, actions, class_auth, class_perm = parse(p)
        if not actions and not cls:
            continue
        parts = p.split(os.sep)
        mod = parts[2] if len(parts) > 2 and parts[1] == "Modules" else "(racine)"
        mods[mod].append({"path": p, "class": cls, "route": route, "actions": actions,
                          "classAuth": class_auth, "classPerm": class_perm})
    return mods

def emit_markdown(mods, out):
    nf = sum(len(v) for v in mods.values())
    na = sum(len(c["actions"]) for v in mods.values() for c in v)
    nv = sum(len(a["verbs"]) for v in mods.values() for c in v for a in c["actions"])
    L = []
    L.append("# Annexe — Inventaire exhaustif des contrôleurs backend\n")
    L.append("> Généré par `scripts/inventory_controllers.py` (analyse statique de `Backend/**/Controllers/*.cs`).")
    L.append("> Aucune route inférée : uniquement le texte littéral des attributs. Régénérer avec :")
    L.append("> `python3 scripts/inventory_controllers.py --markdown`\n")
    L.append(f"- Fichiers contrôleurs : **{nf}**")
    L.append(f"- Actions HTTP (méthodes portant au moins un attribut `[Http*]`) : **{na}**")
    L.append(f"- Mappings de route (attributs `[Http*]`, une méthode pouvant en porter plusieurs) : **{nv}**")
    L.append(f"- Modules : **{len(mods)}**\n")
    L.append("## Récapitulatif par module\n")
    L.append("| Module | Fichiers | Actions |")
    L.append("|---|---:|---:|")
    for m in sorted(mods):
        L.append(f"| {m} | {len(mods[m])} | {sum(len(c['actions']) for c in mods[m])} |")
    L.append(f"| **TOTAL** | **{nf}** | **{na}** |\n")
    L.append("## Détail par contrôleur\n")
    for m in sorted(mods):
        L.append(f"### {m}\n")
        for c in sorted(mods[m], key=lambda x: x["path"]):
            base = c["route"] or "(aucun [Route] de classe)"
            L.append(f"#### `{c['path']}` — `{c['class']}`\n")
            L.append(f"- Route de classe : `{base}`")
            ca = c["classAuth"] or "—"
            if c["classPerm"]: ca += f" + RequirePermission({c['classPerm']})"
            L.append(f"- Autorisation de classe : {ca}")
            L.append(f"- Actions : {len(c['actions'])}\n")
            if not c["actions"]:
                L.append("")
                continue
            L.append("| Ligne | Verbe(s) | Template | Méthode | Autorisation |")
            L.append("|---:|---|---|---|---|")
            for a in c["actions"]:
                verbs = " / ".join(v for v, _ in a["verbs"])
                tpl = " / ".join(t or "(vide)" for _, t in a["verbs"])
                perm = f"RequirePermission({a['perm']})" if a["perm"] else (a["auth"] or "—")
                L.append(f"| {a['line']} | {verbs} | `{tpl}` | `{a['name']}` | {perm} |")
            L.append("")
    open(out, "w", encoding="utf-8").write("\n".join(L) + "\n")

if __name__ == "__main__":
    mods = main()
    nf = sum(len(v) for v in mods.values())
    na = sum(len(c["actions"]) for v in mods.values() for c in v)
    nv = sum(len(a["verbs"]) for v in mods.values() for c in v for a in c["actions"])
    if "--markdown" in sys.argv:
        emit_markdown(mods, "docs/ANNEXE-INVENTAIRE-CONTROLEURS.md")
        print("docs/ANNEXE-INVENTAIRE-CONTROLEURS.md regenerated")
    if "--json" in sys.argv:
        print(json.dumps(mods, indent=1)); sys.exit()
    print(f"files={nf} actions={na} routeMappings={nv} modules={len(mods)}")
    for m in sorted(mods):
        print(f"  {m:20} ctrl={len(mods[m]):2} act={sum(len(c['actions']) for c in mods[m]):4}")

