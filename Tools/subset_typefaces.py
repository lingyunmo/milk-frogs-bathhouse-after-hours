"""Rebuild the static Noto subsets that the interface ships with.

The sources are the Noto Sans SC and Noto Serif SC variable fonts (SIL OFL 1.1) that ship with
Windows 11 under C:/Windows/Fonts. Each subset is pinned to one weight and keeps every character
that appears in the game scripts and style sheets, plus the punctuation in EXTRA below.

    python Tools/subset_typefaces.py [--fonts C:/Windows/Fonts]
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
    ("NotoSerifSC-VF.ttf", RESOURCES / "Typeface" / "AfterHoursSerif.ttf", 600, "all"),
    ("NotoSerifSC-VF.ttf", RESOURCES / "Interface" / "TitleTypeface.ttf", 650, "打烊后"),
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
    if output.exists():
        with TTFont(output) as previous:
            carried = {chr(c) for c in previous.getBestCmap()}
        if set(characters) <= carried:
            print(f"{output.relative_to(ROOT)}: already covers {len(characters)} characters")
            return
        # Never drop a glyph an earlier subset carried. Some text is only assembled at runtime.
        characters = "".join(sorted(set(characters) | carried))
    font = TTFont(source)
    axis = next(a for a in font["fvar"].axes if a.axisTag == "wght")
    font = instancer.instantiateVariableFont(font, {"wght": max(axis.minValue, min(axis.maxValue, weight))},
                                             updateFontNames=False)
    font["OS/2"].usWeightClass = weight
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
    font.save(output)
    print(f"{output.relative_to(ROOT)}: {len(characters)} characters, {output.stat().st_size:,} bytes")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--fonts", default="C:/Windows/Fonts", type=Path)
    args = parser.parse_args()
    everything = interface_characters()
    for source, output, weight, scope in TYPEFACES:
        build(args.fonts / source, output, weight, everything if scope == "all" else scope)


if __name__ == "__main__":
    main()
