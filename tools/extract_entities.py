"""
FF Pixel Remaster offline entity-label extractor (FF2 / FF3 / FF4 / FF5).

Generalised from the FF5 mod's tools/extract_entities.py. Reads every map_*.bundle under
the game's Addressables tree, walks the Tiled-format entity data (entity_default TextAssets
+ base64 `inline` entity[] groups in each map's `package` manifest), and collects the raw
Japanese developer labels that the mod translates at runtime (EntityTranslator.Translate
on PropertyEntity.Name).

What is new compared with the FF5 original:
  * GAME selects the install path and, more importantly, a faithful Python mirror of THAT
    mod's EntityTranslator lookup. Each mod strips prefixes/suffixes differently, so
    "is this label already covered?" can only be answered per game.
  * `missing` reports only the labels the mod would actually fail to translate, reduced to
    the key the lookup will try last (so ①/②/③ variants collapse into one entry).
  * `gamedict` builds a Japanese -> {lang: text} dictionary from the game's own message
    tables (story_cha = speaker names, system = names of characters/places/items). A
    label that exactly matches a game string is filled with the official localisation;
    partial matches are listed as glossary hints for whoever translates the rest.

Usage:
  python extract_entities.py sample [N]        # tally + sample from N bundles (default 3)
  python extract_entities.py full <outfile>    # every label, merged into <outfile> (FF5 format)
  python extract_entities.py missing <outfile> # labels the current translation.json misses
  python extract_entities.py gamedict <outfile># official JP -> {lang} dictionary

Environment:
  FFPR_GAME     overrides GAME below (FF2 / FF3 / FF4 / FF5)
  FFPR_AA_PATH  overrides the Addressables folder
"""
import glob, os, json, base64, sys, re
from collections import Counter, defaultdict
import UnityPy

GAME = os.environ.get("FFPR_GAME", "FF5")

STEAM = r"D:\Games\SteamLibrary\steamapps\common"
INSTALL = {
    "FF2": (r"FINAL FANTASY II PR", r"FINAL FANTASY II_Data"),
    "FF3": (r"FINAL FANTASY III PR", r"FINAL FANTASY III_Data"),
    "FF4": (r"FINAL FANTASY IV PR", r"FINAL FANTASY IV_Data"),
    "FF5": (r"FINAL FANTASY V PR", r"FINAL FANTASY V_Data"),
}
AA = os.environ.get(
    "FFPR_AA_PATH",
    os.path.join(STEAM, INSTALL[GAME][0], INSTALL[GAME][1], r"StreamingAssets\aa\StandaloneWindows64"),
)

# The mod repo this script lives in (tools/..).
MOD = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# MapConstants.ObjectType — identical numbering in FF2/3/4/5 (FF3 adds 27, FF5 adds 27-28)
OBJ_TYPE_NAME = {
    0: "PointIn", 1: "TileAnimation", 2: "Event", 3: "GotoMap", 4: "ToLayer",
    5: "Entity", 6: "NPC", 7: "TreasureBox", 8: "OpenTrigger", 9: "SavePoint",
    10: "AnimEntity", 11: "EffectEntity", 12: "ScreenEffect", 13: "MoveArea",
    14: "Polyline", 15: "ChangeOffset", 16: "RelayInteraction", 17: "ShopNPC",
    18: "MinimapIcon", 19: "CollisionEntity", 20: "TelepoPoint",
    21: "TransportationEventAction", 22: "IgnoreRoute", 23: "NonEncountArea",
    24: "TimeSwitchingGimmickArea", 25: "SlidingFloorGimmickArea",
    26: "DamageFloorGimmickArea", 27: "MapRange", 28: "RandomEvent",
}

# Never named to the player by any of the mods: non-interactive geometry/effects, plus
# TreasureBox and SavePoint (mod-constant names). GotoMap is resolved from the Map/Area
# master at runtime in every mod, so its dev label is never spoken.
EXCLUDE_TYPES = {0, 1, 3, 7, 8, 9, 11, 12, 13, 14, 15, 19, 22, 23, 24, 25, 26, 27}

# LanguageCodeMap minus 'ja' (identity).
LANGS = ["en", "fr", "it", "de", "es", "ko", "zht", "zhc", "ru", "th", "pt"]
ALL_LANGS = ["ja"] + LANGS


def contains_japanese(text):
    for c in text:
        o = ord(c)
        if (0x3040 <= o <= 0x309F) or (0x30A0 <= o <= 0x30FF) or (0x4E00 <= o <= 0x9FFF):
            return True
    return False


def normalize_halfwidth(s):
    out = []
    for c in s:
        o = ord(c)
        if 0xFF01 <= o <= 0xFF5E:
            out.append(chr(o - 0xFEE0))
        elif o == 0x3000:
            out.append(" ")
        else:
            out.append(c)
    return "".join(out)


def is_placeholder(name):
    """Labels no mod ever speaks (superset of the per-mod placeholder filters)."""
    if not name or name == "Unknown":
        return True
    norm = normalize_halfwidth(name)
    if norm.startswith("Default ") or norm.startswith("Default_"):
        return True
    if name.startswith("汎用"):
        return True
    if "セーブポイント" in name:
        return True
    return False


# ─────────────────────────────────────────────────────────────────────────────
#  Per-game mirrors of Utils/EntityTranslator.cs Translate().
#  Each returns the ordered list of keys the mod tries. The last one is the most
#  reduced form, which is what a new dictionary entry should be keyed on.
# ─────────────────────────────────────────────────────────────────────────────
CIRCLED = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳"
_PREFIX_SIMPLE = re.compile(r"^((?:SC)?\d+:)", re.IGNORECASE)          # FF3 / FF4 / FF5
_PREFIX_FF2 = re.compile(r"^((?:SC\s*E?\s*)?\d+:)", re.IGNORECASE)      # FF2
_TRAIL_DIGITS = re.compile(r"([0-9０-９]+)$")                            # FF2


def _strip(rx, s):
    m = rx.match(s)
    return (m.group(1), s[len(m.group(1)):]) if m else (None, s)


def _strip_digits(s):
    m = _TRAIL_DIGITS.search(s)
    return (m.group(1), s[: len(s) - len(m.group(1))]) if m else (None, s)


def candidates_ff2(name):
    keys = [name]
    prefix, after = _strip(_PREFIX_FF2, name)
    if prefix:
        keys.append(after)
    suffix, base = _strip_digits(name)
    if suffix:
        keys.append(base)
    if prefix:
        s2, b2 = _strip_digits(after)
        if s2:
            keys.append(b2)
    if name[:1] in CIRCLED:
        rest = name[1:]
        keys.append(rest)
        s3, b3 = _strip_digits(rest)
        if s3:
            keys.append(b3)
    if len(name) > 1 and name[0] == "真":
        rest = name[1:]
        keys.append(rest)
        s4, b4 = _strip_digits(rest)
        if s4:
            keys.append(b4)
    return keys


def candidates_ff3(name):
    keys = [name]
    prefix, after = _strip(_PREFIX_SIMPLE, name)
    if prefix:
        keys.append(after)
    return keys


def candidates_ff4_ff5(name):
    keys = [name]
    prefix, after = _strip(_PREFIX_SIMPLE, name)
    if prefix:
        keys.append(after)
    if name and name[-1] in CIRCLED:
        keys.append(name[:-1])
    if prefix and after and after[-1] in CIRCLED:
        keys.append(after[:-1])
    return keys


CANDIDATES = {"FF2": candidates_ff2, "FF3": candidates_ff3,
              "FF4": candidates_ff4_ff5, "FF5": candidates_ff4_ff5}[GAME]


def proposed_key(name):
    """The key a new entry should use: the most reduced form the lookup will try.
    Never reduce to a string with no Japanese left (e.g. a bare prefix)."""
    keys = [k for k in CANDIDATES(name) if k and contains_japanese(k)]
    return keys[-1] if keys else name


# ─────────────────────────────────────────────────────────────────────────────
#  Bundle walking (unchanged from the FF5 original)
# ─────────────────────────────────────────────────────────────────────────────
def get_text(obj):
    data = obj.read()
    s = getattr(data, "m_Script", b"")
    if isinstance(s, str):
        return s.encode("utf-8", "surrogateescape").decode("utf-8", "replace")
    return bytes(s).decode("utf-8", "replace")


def object_type_of(tiled_obj):
    for p in tiled_obj.get("properties", []):
        if p.get("name") == "object_type":
            try:
                return int(p.get("value"))
            except (TypeError, ValueError):
                return None
    return None


def walk_entity_doc(doc, raw_counter, kept, bundle_tag):
    if not isinstance(doc, dict):
        return
    for layer in doc.get("layers", []):
        objs = layer.get("objects")
        if not isinstance(objs, list):
            continue
        for o in objs:
            if not isinstance(o, dict):
                continue
            name = o.get("name") or ""
            ot = object_type_of(o)
            raw_counter[ot] += 1
            if ot is None or ot in EXCLUDE_TYPES:
                continue
            if not name or not contains_japanese(name):
                continue
            if is_placeholder(name):
                continue
            kept.append((name, ot, bundle_tag))


def iter_entity_docs(bundle_path):
    env = UnityPy.load(bundle_path)
    text_by_name = defaultdict(list)
    package_txt = None
    for obj in env.objects:
        if obj.type.name != "TextAsset":
            continue
        data = obj.read()
        nm = getattr(data, "m_Name", "")
        txt = get_text(obj)
        text_by_name[nm].append(txt)
        if nm == "package":
            package_txt = txt

    for txt in text_by_name.get("entity_default", []):
        try:
            yield json.loads(txt)
        except Exception:
            pass

    if package_txt:
        try:
            pkg = json.loads(package_txt)
        except Exception:
            pkg = None
        if pkg:
            for m in pkg.get("map", []) or []:
                for elem in (m.get("entity") or []):
                    inline = elem.get("inline")
                    if inline:
                        try:
                            yield json.loads(base64.b64decode(inline).decode("utf-8"))
                        except Exception:
                            pass
                    else:
                        leaf = (elem.get("asset") or "").split("/")[-1]
                        for txt in text_by_name.get(leaf, []):
                            try:
                                yield json.loads(txt)
                            except Exception:
                                pass


def bundle_tag(path):
    base = os.path.basename(path)
    return base.split("_assets_all_")[0]


def sweep(bundles):
    raw_counter = Counter()
    kept = []
    errors = []
    for i, b in enumerate(bundles, 1):
        try:
            for doc in iter_entity_docs(b):
                walk_entity_doc(doc, raw_counter, kept, bundle_tag(b))
        except Exception as e:
            errors.append((os.path.basename(b), str(e)))
        if i % 40 == 0:
            print(f"  ...{i}/{len(bundles)} bundles")
    return raw_counter, kept, errors


def all_map_bundles():
    return sorted(glob.glob(os.path.join(AA, "map_*_assets_all_*.bundle")))


# ─────────────────────────────────────────────────────────────────────────────
#  Game message tables -> official localisation
# ─────────────────────────────────────────────────────────────────────────────
_MARKUP = re.compile(r"<[^>]*>")


def clean_msg(s):
    return _MARKUP.sub("", s).replace("\\n", " ").strip()


def load_message_tables():
    """{table: {lang: {msg_id: text}}} for every localised table in the message bundle."""
    b = glob.glob(os.path.join(AA, "message_assets_all_*.bundle"))
    if not b:
        return {}
    env = UnityPy.load(b[0])
    tables = defaultdict(dict)
    for obj in env.objects:
        if obj.type.name != "TextAsset":
            continue
        nm = obj.read().m_Name
        m = re.match(r"^(.*)_(ja|en|fr|it|de|es|ko|zht|zhc|ru|th|pt)$", nm)
        if not m:
            continue
        table, lang = m.group(1), m.group(2)
        rows = {}
        for line in get_text(obj).splitlines():
            if "\t" not in line:
                continue
            k, v = line.split("\t", 1)
            rows[k] = v
        tables[table][lang] = rows
    return tables


def build_gamedict(tables):
    """Japanese text -> {lang: official text}. Only whole strings; conflicting
    localisations for the same Japanese text keep the most common one."""
    votes = defaultdict(lambda: defaultdict(Counter))
    for table in ("story_cha", "system"):
        langs = tables.get(table, {})
        ja = langs.get("ja", {})
        for mid, jtext in ja.items():
            jt = clean_msg(jtext)
            if not jt or not contains_japanese(jt) or len(jt) > 40:
                continue
            for lang in LANGS:
                t = clean_msg(langs.get(lang, {}).get(mid, ""))
                if t:
                    votes[jt][lang][t] += 1
    out = {}
    for jt, per_lang in votes.items():
        entry = {}
        for lang in LANGS:
            if per_lang.get(lang):
                entry[lang] = per_lang[lang].most_common(1)[0][0]
        if entry.get("en"):
            out[jt] = entry
    return out


# ─────────────────────────────────────────────────────────────────────────────
#  Commands
# ─────────────────────────────────────────────────────────────────────────────
def load_translation():
    p = os.path.join(MOD, "translation.json")
    with open(p, encoding="utf-8-sig") as f:
        return json.load(f)


def has_entry(d, key):
    v = d.get(key)
    return bool(v) and any(v.get(l) for l in LANGS)


def cmd_sample(n):
    bundles = all_map_bundles()
    real = [b for b in bundles if "nighteffect" not in os.path.basename(b)]
    picks = real[:n] if real else bundles[:n]
    raw_counter, kept, _ = sweep(picks)
    print("\n=== raw object_type histogram ===")
    for ot, cnt in sorted(raw_counter.items(), key=lambda kv: -kv[1]):
        excl = "  [EXCLUDED]" if ot in EXCLUDE_TYPES else ""
        print(f"  {OBJ_TYPE_NAME.get(ot, f'?{ot}'):<26} ({ot}): {cnt}{excl}")
    print(f"\n=== kept: {len(kept)} raw, {len({k[0] for k in kept})} unique ===")


def cmd_full(outfile):
    raw_counter, kept, errors = sweep(all_map_bundles())
    names = sorted({k[0] for k in kept})
    existing = {}
    if os.path.exists(outfile):
        with open(outfile, encoding="utf-8") as f:
            existing = json.load(f)
    merged = dict(existing)
    for n in names:
        merged.setdefault(n, {l: "" for l in LANGS})
    with open(outfile, "w", encoding="utf-8") as f:
        json.dump(merged, f, ensure_ascii=False, indent=2, sort_keys=True)
    print(f"{len(names)} unique labels; wrote {outfile} ({len(merged)} keys)")
    if errors:
        print(f"{len(errors)} bundle errors, first: {errors[:3]}")


def cmd_missing(outfile):
    bundles = all_map_bundles()
    print(f"{GAME}: sweeping {len(bundles)} map bundles under {AA}")
    raw_counter, kept, errors = sweep(bundles)
    tr = load_translation()

    labels = defaultdict(lambda: {"types": set(), "maps": set()})
    for name, ot, tag in kept:
        labels[name]["types"].add(OBJ_TYPE_NAME.get(ot, str(ot)))
        labels[name]["maps"].add(tag)

    hit, miss = 0, {}
    for name, info in labels.items():
        if any(has_entry(tr, k) for k in CANDIDATES(name)):
            hit += 1
            continue
        key = proposed_key(name)
        slot = miss.setdefault(key, {"raw": set(), "types": set(), "maps": set()})
        slot["raw"].add(name)
        slot["types"] |= info["types"]
        slot["maps"] |= info["maps"]

    tables = load_message_tables()
    gd = build_gamedict(tables)
    terms = sorted((t for t in gd if len(t) >= 2), key=len, reverse=True)

    out = {}
    official = 0
    for key in sorted(miss):
        slot = miss[key]
        rec = {
            "raw": sorted(slot["raw"]),
            "types": sorted(slot["types"]),
            "maps": sorted(slot["maps"]),
        }
        if key in gd:
            rec["official"] = gd[key]
            official += 1
        else:
            hints, covered = {}, key
            for t in terms:
                if t in covered:
                    hints[t] = gd[t]
                    covered = covered.replace(t, "\0")
            if hints:
                rec["glossary"] = hints
        out[key] = rec

    with open(outfile, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)

    print(f"unique raw labels: {len(labels)}  covered by translation.json: {hit}")
    print(f"missing keys: {len(out)}  (exact official match: {official}, "
          f"with glossary hints: {sum(1 for r in out.values() if 'glossary' in r)})")
    by_type = Counter(t for r in out.values() for t in r["types"])
    print("missing by type:", dict(by_type))
    if errors:
        print(f"{len(errors)} bundle errors, first: {errors[:3]}")
    print(f"wrote {outfile}")


def cmd_gamedict(outfile):
    gd = build_gamedict(load_message_tables())
    with open(outfile, "w", encoding="utf-8") as f:
        json.dump(gd, f, ensure_ascii=False, indent=2, sort_keys=True)
    print(f"{len(gd)} official strings -> {outfile}")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "sample":
        cmd_sample(int(sys.argv[2]) if len(sys.argv) > 2 else 3)
    elif cmd in ("full", "missing", "gamedict") and len(sys.argv) > 2:
        {"full": cmd_full, "missing": cmd_missing, "gamedict": cmd_gamedict}[cmd](sys.argv[2])
    else:
        print(__doc__)
        sys.exit(1)
