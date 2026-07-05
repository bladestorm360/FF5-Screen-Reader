"""
FF5 PR offline entity-label extractor.

Reads every map_*.bundle under the game's Addressables tree, walks the Tiled-format
entity data (entity_default TextAssets + base64 `inline` entity[] groups in each map's
`package` manifest), and collects the raw Japanese developer labels that the screen
reader mod translates at runtime (EntityTranslator.Translate on PropertyEntity.Name).

Mirrors the mod's per-map dump filter (Utils/EntityTranslator.cs CollectDumpableEntities
+ Field/EntityFactory.cs) so the bulk output is consistent with the existing hotkey dump,
just across all 238 maps at once.

Usage:
  python extract_entities.py sample [N]     # tally + sample from N bundles (default 3)
  python extract_entities.py full <outfile> # sweep all bundles, write translation.generated.json
"""
import glob, os, json, base64, sys
from collections import Counter, defaultdict
import UnityPy

# Path to the game's Addressables bundle folder. Override with env var FF5_AA_PATH
# if your Steam library lives elsewhere.
AA = os.environ.get(
    "FF5_AA_PATH",
    r"D:\Games\SteamLibrary\steamapps\common\FINAL FANTASY V PR\FINAL FANTASY V_Data\StreamingAssets\aa\StandaloneWindows64",
)

# MapConstants.ObjectType (dump.cs 262213)
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

# Excluded exactly as the mod's dump collector does:
#   TreasureBox(7), SavePoint(9), GotoMap(3)  + IsNonInteractiveType(...)
EXCLUDE_TYPES = {
    0,   # PointIn
    1,   # TileAnimation
    3,   # GotoMap (resolved via Map/Area master at runtime)
    7,   # TreasureBox (mod constant name)
    8,   # OpenTrigger
    9,   # SavePoint (auto-named)
    11,  # EffectEntity
    12,  # ScreenEffect
    13,  # MoveArea
    14,  # Polyline
    15,  # ChangeOffset
    19,  # CollisionEntity
    22,  # IgnoreRoute
    23,  # NonEncountArea
    24,  # TimeSwitchingGimmickArea
    25,  # SlidingFloorGimmickArea
    26,  # DamageFloorGimmickArea
    27,  # MapRange
}

# Target languages = LanguageCodeMap minus 'ja' (Utils/EntityTranslator.cs)
LANGS = ["en", "fr", "it", "de", "es", "ko", "zht", "zhc", "ru", "th", "pt"]


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
    if not name or name == "Unknown":
        return True
    norm = normalize_halfwidth(name)
    if norm.startswith("Default ") or norm.startswith("Default_"):
        return True
    if name.startswith("汎用"):
        return True
    if "セーブポイント" in name:  # save-point duplicate of SavePointEntity
        return True
    return False


import re
# Mirror EntityTranslator's EntityPrefixRegex + EntitySuffixRegex
_PREFIX_RE = re.compile(r"^((?:SC)?\d+:)", re.IGNORECASE)
_CIRCLED = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳"


def normalize_key(name):
    """Reduce a raw label to the base the mod's 4-tier lookup collapses to:
    strip a leading (SC)?\\d+: prefix and a trailing circled-number suffix."""
    base = name
    m = _PREFIX_RE.match(base)
    if m:
        base = base[len(m.group(1)):]
    if base and base[-1] in _CIRCLED:
        base = base[:-1]
    return base


def get_text(obj):
    data = obj.read()
    s = getattr(data, "m_Script", b"")
    if isinstance(s, str):
        return s.encode("utf-8", "surrogateescape").decode("utf-8", "replace")
    return bytes(s).decode("utf-8", "replace")


def object_type_of(tiled_obj):
    """Extract object_type int from a Tiled object's properties[] list."""
    for p in tiled_obj.get("properties", []):
        if p.get("name") == "object_type":
            try:
                return int(p.get("value"))
            except (TypeError, ValueError):
                return None
    return None


def walk_entity_doc(doc, raw_counter, kept):
    """A Tiled entity doc: {layers:[{objects:[{name, properties:[...]}]}]}."""
    if not isinstance(doc, dict):
        return
    for layer in doc.get("layers", []):
        objs = layer.get("objects")
        if not isinstance(objs, list):
            continue  # tile-data layer, not an object layer
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
            kept.append((name, ot))


def iter_entity_docs(bundle_path):
    """Yield every entity Tiled-doc (dict) for a map bundle: entity_default TextAssets
    plus base64 inline entity[] groups from the package manifest."""
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

    # 1. entity_default TextAssets (one per sub-map)
    for txt in text_by_name.get("entity_default", []):
        try:
            yield json.loads(txt)
        except Exception:
            pass

    # 2. inline entity[] groups from the package manifest (event-conditional variants)
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
                        # fallback: asset ref -> TextAsset by leaf name
                        asset = elem.get("asset") or ""
                        leaf = asset.split("/")[-1]
                        for txt in text_by_name.get(leaf, []):
                            try:
                                yield json.loads(txt)
                            except Exception:
                                pass


def process_bundle(bundle_path, raw_counter, kept):
    for doc in iter_entity_docs(bundle_path):
        walk_entity_doc(doc, raw_counter, kept)


def cmd_sample(n):
    bundles = sorted(glob.glob(os.path.join(AA, "map_*_assets_all_*.bundle")))
    # prefer "real" numbered maps over nighteffect variants for the sample
    real = [b for b in bundles if "nighteffect" not in os.path.basename(b)]
    picks = real[:n] if real else bundles[:n]
    raw_counter = Counter()
    kept = []
    for b in picks:
        print("BUNDLE:", os.path.basename(b))
        process_bundle(b, raw_counter, kept)

    print("\n=== raw object_type histogram (all objects seen) ===")
    for ot, cnt in sorted(raw_counter.items(), key=lambda kv: (-kv[1])):
        label = OBJ_TYPE_NAME.get(ot, f"?{ot}")
        excl = "  [EXCLUDED]" if (ot in EXCLUDE_TYPES) else ""
        print(f"  {label:<26} ({ot}): {cnt}{excl}")

    print(f"\n=== kept labels: {len(kept)} raw, {len(set(kept))} unique (name,type) ===")
    by_type = defaultdict(set)
    for name, ot in kept:
        by_type[ot].add(name)
    for ot in sorted(by_type):
        names = sorted(by_type[ot])
        print(f"\n  -- {OBJ_TYPE_NAME.get(ot, ot)} ({ot}): {len(names)} unique --")
        for nm in names[:25]:
            print(f"       {nm}")
        if len(names) > 25:
            print(f"       ... (+{len(names)-25} more)")


def cmd_full(outfile):
    bundles = sorted(glob.glob(os.path.join(AA, "map_*_assets_all_*.bundle")))
    raw_counter = Counter()
    kept = []
    errors = []
    for i, b in enumerate(bundles, 1):
        try:
            process_bundle(b, raw_counter, kept)
        except Exception as e:
            errors.append((os.path.basename(b), str(e)))
        if i % 40 == 0:
            print(f"  ...{i}/{len(bundles)} bundles, {len(set(n for n,_ in kept))} unique labels so far")

    unique_names = sorted({name for name, ot in kept})
    by_type = defaultdict(set)
    for name, ot in kept:
        by_type[ot].add(name)

    normalized = {normalize_key(n) for n in unique_names}
    print(f"\n=== SWEEP COMPLETE: {len(bundles)} bundles ===")
    print(f"unique RAW labels:        {len(unique_names)}")
    print(f"unique NORMALIZED bases:  {len(normalized)}  (what actually needs translating)")
    print("per object_type (unique names):")
    for ot in sorted(by_type):
        print(f"  {OBJ_TYPE_NAME.get(ot, ot):<26} ({ot}): {len(by_type[ot])}")
    if errors:
        print(f"\n{len(errors)} bundle errors:")
        for name, e in errors[:20]:
            print(f"  {name}: {e}")

    # Merge with existing translation file (preserve any filled values)
    existing = {}
    if os.path.exists(outfile):
        try:
            with open(outfile, "r", encoding="utf-8") as f:
                existing = json.load(f)
        except Exception:
            existing = {}

    merged = dict(existing)
    added = 0
    for name in unique_names:
        if name not in merged:
            merged[name] = {lang: "" for lang in LANGS}
            added += 1
        else:
            for lang in LANGS:
                merged[name].setdefault(lang, "")

    with open(outfile, "w", encoding="utf-8") as f:
        json.dump(merged, f, ensure_ascii=False, indent=2, sort_keys=True)

    print(f"\nwrote {outfile}: {len(merged)} total keys ({added} new, {len(existing)} preexisting)")


if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in ("sample", "full"):
        print(__doc__)
        sys.exit(1)
    if sys.argv[1] == "sample":
        cmd_sample(int(sys.argv[2]) if len(sys.argv) > 2 else 3)
    else:
        if len(sys.argv) < 3:
            print("full mode needs an output file path")
            sys.exit(1)
        cmd_full(sys.argv[2])
