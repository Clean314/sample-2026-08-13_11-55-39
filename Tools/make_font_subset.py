"""
M PLUS 2 부분 폰트 다시 만들기.

일본어·유럽 언어·베트남어·터키어 문구는 M PLUS 2 로 그린다(GameFonts). 앱에는 원본(각 1.7MB) 대신
문구에 실제로 쓰는 글자만 남긴 부분 폰트(각 수십 KB)를 넣는다. 그래서 LocalizationManager.cs 의
문구를 고치거나 언어를 더하면 이 스크립트를 다시 돌려야 한다 — 안 그러면 새 글자가 폰에서 빈칸이 된다.

쓰는 법 (프로젝트 폴더에서):
    pip install fonttools
    python Tools/make_font_subset.py <원본 폰트 폴더>
  원본 폴더에 MPLUS2-Regular.ttf, MPLUS2-Bold.ttf 가 있어야 한다.
  결과는 Assets/Resources/Fonts/MPLUS2-Regular.ttf, MPLUS2-Bold.ttf 에 덮어쓴다.
"""
import sys, os, re
from fontTools import subset
from fontTools.ttLib import TTFont

SRC_DIR = sys.argv[1] if len(sys.argv) > 1 else '.'
LOC = 'Assets/Scripts/Game/LocalizationManager.cs'
OUT = 'Assets/Resources/Fonts'

src = open(LOC, encoding='utf-8').read()
enum = re.search(r'public enum Language \{([^}]*)\}', src).group(1)
langs = [x.strip() for x in enum.split(',')]
mplus = re.search(r'public static bool UsesMPlus\(Language l\) =>(.*?);', src, re.S).group(1)
use = [i for i, name in enumerate(langs) if 'Language.' + name in mplus]

chars = set(chr(c) for c in range(0x20, 0x7F)) | set('▶…')   # ▶ …
strpat = re.compile(r'"((?:[^"\\]|\\.)*)"')
for m in re.finditer(r'\{\s*"[a-z_0-9]+"\s*,\s*new\[\]\s*\{(.*?)\}\s*\},', src, re.S):
    strs = strpat.findall(m.group(1))
    for i in use:
        if i < len(strs):
            chars.update(strs[i].replace('\\n', ''))
for lang, name in re.findall(r'\(Language\.(\w+),\s*"([^"]*)"\)', src):
    if langs.index(lang) in use:
        chars.update(name)
text = ''.join(sorted(chars))

for fn in ['MPLUS2-Regular.ttf', 'MPLUS2-Bold.ttf']:
    opts = subset.Options()
    opts.name_IDs = ['*']
    opts.name_languages = ['*']
    opts.layout_features = ['*']
    opts.notdef_outline = True
    f = TTFont(os.path.join(SRC_DIR, fn))
    s = subset.Subsetter(opts)
    s.populate(text=text)
    s.subset(f)

    # 줄 높이를 SCDream 과 같게 맞춘다. M PLUS 2 는 줄 높이가 글자 크기의 1.45배라 SCDream(약 1.15배)보다
    # 훨씬 크다. 유니티 글자 상자는 줄이 상자 높이에 안 들어가면 글자를 아예 안 그린다 — 점수(110pt,
    # 상자 높이 130)처럼 SCDream 에 맞춰 빠듯하게 잡은 상자에서 숫자가 통째로 사라졌다.
    # 글자 모양은 그대로이고 줄 높이 정보만 바꾸므로, 모든 화면이 한국어일 때와 같은 자리에 놓인다.
    ref = TTFont(os.path.join('Assets/Resources/Fonts', 'SCDream8.otf'))
    for tag, attrs in (('hhea', ('ascent', 'descent', 'lineGap')),
                       ('OS/2', ('sTypoAscender', 'sTypoDescender', 'sTypoLineGap', 'usWinAscent', 'usWinDescent'))):
        for a in attrs:
            setattr(f[tag], a, getattr(ref[tag], a))

    dst = os.path.join(OUT, fn)
    f.save(dst)
    cm = TTFont(dst).getBestCmap()
    miss = ''.join(c for c in text if ord(c) not in cm)
    print(f'{fn}: {os.path.getsize(dst) // 1024} KB, {len(text)} chars' + (f', MISSING: {miss}' if miss else ''))
