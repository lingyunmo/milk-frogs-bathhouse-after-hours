"""Build the game's embedded font subsets. Place the upstream LXGW WenKai Regular and
Ma Shan Zheng TTF files in --art-fonts (sources and OFL notices in Docs/World/art-sources.md).
Use --fonts for the installed Noto Sans SC variable font.
"""
import argparse
import re
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / "Assets" / "Resources"

# Interface punctuation, figures and symbols that may only be assembled at runtime.
EXTRA = (
    "".join(chr(c) for c in range(0x20, 0x7F))
    + "，。、；：？！“”‘’（）《》【】—…·・％℃×→←↑↓　「」〔〕～－"
    + "ⅠⅡⅢⅣ①②③④"
)

# (source file, output, weight, character scope)
TYPEFACES = (
    ("NotoSansSC-VF.ttf", RESOURCES / "Typeface" / "AfterHours.ttf", 500, "all"),
    ("LXGWWenKai-Regular.ttf", RESOURCES / "Typeface" / "AfterHoursSerif.ttf", None, "all"),
    ("MaShanZheng-Regular.ttf", RESOURCES / "Interface" / "TitleTypeface.ttf", None, "打烊后奶娃浴场"),
)


def interface_characters() -> str:
    text = []
    for pattern in ("Assets/Scripts/**/*.cs", "Assets/Resources/Interface/*.uss"):
        for path in ROOT.glob(pattern):
            text.append(path.read_text(encoding="utf8"))
    # Room and floor names reach the map, area cards and save summaries from the scene hierarchy.
    for path in ROOT.glob("Assets/Scenes/*.unity"):
        for name in re.findall(r'm_Name: "((?:[^"\\]|\\.)*)"', path.read_text(encoding="utf8")):
            text.append(name.encode("ascii", "backslashreplace").decode("unicode_escape"))
    used = set("".join(text))
    # Keep only what the interface can print: CJK, full-width forms and the extras above.
    keep = {c for c in used if re.match(r"[\u2000-\u206f\u2150-\u218f\u2190-\u21ff\u2460-\u24ff"
                                        r"\u3000-\u303f\u3400-\u9fff\uff00-\uffef]", c)}
    return "".join(sorted(keep | set(EXTRA)))


def build(source: Path, output: Path, weight: int, characters: str) -> None:
    font = TTFont(source, recalcTimestamp=False)
    if weight:
        font = instancer.instantiateVariableFont(font, {"wght": weight}, updateFontNames=False)
        font["OS/2"].usWeightClass = weight
    else:
        # OFL subsets use project-specific family names; keep the original copyright and license.
        family = "After Hours Ledger" if characters != "打烊后奶娃浴场" else "After Hours Sign"
        for record in font["name"].names:
            if record.nameID in (1, 3, 4, 6, 16):
                value = family.replace(" ", "") if record.nameID == 6 else family
                record.string = value.encode(record.getEncoding())
    options = subset.Options()
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    options.notdef_outline = True
    options.glyph_names = False
    options.hinting = False
    options.desubroutinize = True
    subsetter = subset.Subsetter(options)
    subsetter.populate(text=characters)
    subsetter.subset(font)
    output.parent.mkdir(parents=True, exist_ok=True)
    font.recalcTimestamp = False
    font.save(output)
    print(f"{output.relative_to(ROOT)}: {len(characters)} characters, {output.stat().st_size:,} bytes")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fonts", default="C:/Windows/Fonts", type=Path)
    parser.add_argument("--art-fonts", default=ROOT / ".local" / "night-wall-sources", type=Path)
    args = parser.parse_args()
    everything = interface_characters()
    for source, output, weight, scope in TYPEFACES:
        build((args.fonts if weight else args.art_fonts) / source, output, weight, everything if scope == "all" else scope)


if __name__ == "__main__":
    main()
