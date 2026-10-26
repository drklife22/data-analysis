"""캐릭터 사진(PNG)의 배경을 지우고, 프로그램이 쓰는 이름으로 assets 폴더에 저장합니다.

사용법: python prepare_art.py
사진을 바꾸고 싶으면 아래 POSES 표에서 파일 이름만 바꾼 뒤 다시 실행하고 build.py를 실행하세요.
"""
from collections import deque
from pathlib import Path

from PIL import Image

here = Path(__file__).resolve().parent
photos = here.parent  # 사진은 moving character 폴더에 있습니다.
assets = here / "assets"

# 프로그램 안의 그림 이름 : (사진 파일, 좌우 뒤집기)
POSES = {
    "walk1.png": ("walking.PNG", False),       # 걷기
    "wake.png": ("standing.PNG", False),       # 기본으로 서 있기
    "dance.png": ("dancing.PNG", False),       # 몰래 하는 군무
    "sit.png": ("sit.PNG", False),             # 앉아 쉬기
    "eat2.png": ("drinking2.PNG", False),      # 간식 먹기
    "slide.png": ("lying.PNG", False),         # 미끄러지기 (머리가 왼쪽)
    "climb.png": ("climb.PNG", True),          # 벽·천장 타기 (머리가 오른쪽이 되게 뒤집기)
    "helicopter.png": ("falling.PNG", False),  # 떨어지기·던져졌을 때
    "cry_touch.png": ("crying1.PNG", False),   # 눌렀을 때 울기
    "cry_idle.png": ("crying2.PNG", False),    # 배고플 때 울기
    "held.png": ("teasing.PNG", False),        # 마우스로 들어 올렸을 때
    "chase.png": ("chasing.PNG", False),       # 물고기 쫓아 달리기 (왼쪽을 보고 있음)
}

SCALE = 4          # 작은 사진을 크게 키워서 프로그램에서 줄일 때 선이 부드럽게 보이게 합니다.
MIN_PART = 0.06    # 몸에서 떨어진 작은 장식(음표, 하트 등)은 가장 큰 덩어리의 6%보다 작으면 지웁니다.


def is_background(p, bg):
    r, g, b, a = p
    return a < 10 or (abs(r - bg[0]) <= 6 and abs(g - bg[1]) <= 6 and abs(b - bg[2]) <= 6)


def clean(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    px = im.load()
    bg = px[0, 0]
    # 1) 사진 가장자리에서부터 이어진 배경색만 투명하게 합니다. (몸 안의 흰색·눈 반짝임은 그대로 둡니다.)
    queue = deque((x, y) for x in range(w) for y in (0, h - 1))
    queue.extend((x, y) for y in range(h) for x in (0, w - 1))
    while queue:
        x, y = queue.popleft()
        if not (0 <= x < w and 0 <= y < h) or px[x, y][3] == 0 or not is_background(px[x, y], bg):
            continue
        px[x, y] = (0, 0, 0, 0)
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    # 2) 배경과 맞닿은 회색 테두리 픽셀은 검은 선과 배경이 섞인 것이므로 반투명한 검은색으로 바꿉니다.
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0 or max(r, g, b) - min(r, g, b) > 20:
                continue
            near = any(0 <= x + dx < w and 0 <= y + dy < h and px[x + dx, y + dy][3] == 0
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if near:
                v = (r + g + b) / 3
                alpha = max(0, min(255, round((bg[0] - v) / bg[0] * 255 * 1.15)))
                px[x, y] = (30, 24, 24, alpha)
    # 3) 떨어져 있는 작은 조각을 지웁니다.
    seen = [[False] * w for _ in range(h)]
    parts = []
    for y in range(h):
        for x in range(w):
            if seen[y][x] or px[x, y][3] < 40:
                continue
            part, queue = [], deque([(x, y)])
            seen[y][x] = True
            while queue:
                cx, cy = queue.popleft()
                part.append((cx, cy))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = cx + dx, cy + dy
                        if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and px[nx, ny][3] >= 40:
                            seen[ny][nx] = True
                            queue.append((nx, ny))
            parts.append(part)
    biggest = max(len(p) for p in parts)
    keep = Image.new("L", (w, h), 0)
    kp = keep.load()
    for part in parts:
        if len(part) >= biggest * MIN_PART:
            for x, y in part:
                kp[x, y] = 255
    # 남긴 덩어리 주변 1px의 옅은 테두리도 함께 남깁니다.
    from PIL import ImageFilter
    keep = keep.filter(ImageFilter.MaxFilter(3))
    kp = keep.load()
    for y in range(h):
        for x in range(w):
            if kp[x, y] == 0:
                px[x, y] = (0, 0, 0, 0)
    im = im.crop(im.getbbox())
    return im.resize((im.width * SCALE, im.height * SCALE), Image.LANCZOS)


def main():
    assets.mkdir(exist_ok=True)
    for name, (photo, flip) in POSES.items():
        im = clean(photos / photo)
        if flip:
            im = im.transpose(Image.FLIP_LEFT_RIGHT)
        # 가장자리에 여유를 둔 정사각형 캔버스의 바닥 가운데에 놓습니다.
        side = max(im.size) + 16 * SCALE
        canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        canvas.alpha_composite(im, ((side - im.width) // 2, side - im.height - 4 * SCALE))
        canvas.save(assets / name)
        print(name, "<-", photo, canvas.size)
    icon = clean(photos / POSES["walk1.png"][0])
    side = max(icon.size)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.alpha_composite(icon, ((side - icon.width) // 2, (side - icon.height) // 2))
    square.resize((256, 256), Image.LANCZOS).save(assets / "friend.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (256, 256)])
    print("friend.ico")


if __name__ == "__main__":
    main()
