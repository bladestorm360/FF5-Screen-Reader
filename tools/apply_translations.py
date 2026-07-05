"""
Merge authored translation batches into translation.generated.json (and mirror to
translation.json for embedding). A batch file is a JSON object { jpName: {lang: value, ...} }.
Only non-empty values overwrite; existing filled values are preserved unless the batch
provides a value. Unknown keys (not already in the dictionary) are reported, not added.
"""
import json, sys, os

MOD = r"D:\Games\Dev\Unity\FFPR\FF5\ff5-screen-reader"
GEN = os.path.join(MOD, "translation.generated.json")
# All 12 localization languages (LanguageCodeMap). 'ja' is identity (= the key itself);
# the other 11 are authored. 'en' is the runtime fallback for any missing language.
LANGS = ["ja", "en", "fr", "it", "de", "es", "ko", "zht", "zhc", "ru", "th", "pt"]
AUTHORED = [l for l in LANGS if l != "ja"]


def load(p):
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def save(p, d):
    with open(p, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2, sort_keys=True)


def apply_batch(batch_path):
    d = load(GEN)
    batch = load(batch_path)
    unknown, applied = [], 0
    for k, langs in batch.items():
        if k not in d:
            unknown.append(k)
            d[k] = {}
        # ensure all 12 slots exist; ja is identity
        for l in LANGS:
            d[k].setdefault(l, "")
        d[k]["ja"] = k
        for l in AUTHORED:
            v = langs.get(l, "")
            if v:
                d[k][l] = v
                applied += 1
    save(GEN, d)
    # coverage report
    filled_en = sum(1 for v in d.values() if v.get("en"))
    fully = sum(1 for v in d.values() if all(v.get(l) for l in AUTHORED))
    print(f"applied {applied} values from {os.path.basename(batch_path)}")
    if unknown:
        print(f"  WARNING {len(unknown)} unknown keys (added): {unknown[:5]}")
    print(f"  coverage: en={filled_en}/{len(d)}, all-11-authored={fully}/{len(d)}")


def status():
    d = load(GEN)
    filled_en = sum(1 for v in d.values() if v.get("en"))
    fully = sum(1 for v in d.values() if all(v.get(l) for l in AUTHORED))
    missing_en = [k for k, v in d.items() if not v.get("en")]
    print(f"total keys: {len(d)}")
    print(f"en filled : {filled_en}")
    print(f"all-11-authored: {fully}")
    if missing_en:
        print(f"first missing-en keys: {missing_en[:20]}")


def promote():
    """Copy filled generated dict into embedded translation.json, keeping only entries
    that have at least an English value (Initialize() ignores blank entries)."""
    d = load(GEN)
    out = {}
    for k, v in d.items():
        if v.get("en"):
            v.setdefault("ja", k)
            v["ja"] = k
            out[k] = {l: v.get(l, "") for l in LANGS}
    save(os.path.join(MOD, "translation.json"), out)
    print(f"promoted {len(out)} entries into translation.json")


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "status"
    if cmd == "apply":
        apply_batch(sys.argv[2])
    elif cmd == "promote":
        promote()
    else:
        status()
