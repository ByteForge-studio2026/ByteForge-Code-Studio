#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
把 assets/icons/app-icon.png 转换成 Windows 可用的 app-icon.ico（多尺寸）。

会自动裁掉主体四周的白边 / 近白背景（只保留 logo 本体），再生成图标。

用法：
    python tools/make_icon.py
"""
import os
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "assets", "icons", "app-icon.png")
DST = os.path.join(ROOT, "assets", "icons", "app-icon.ico")

# Windows 图标常用尺寸（ICO 内嵌 PNG，保留透明通道）
SIZES = [(16, 16), (20, 20), (24, 24), (32, 32), (40, 40),
         (48, 48), (64, 64), (96, 96), (128, 128), (256, 256)]


def trim_white_margin(im: Image.Image) -> Image.Image:
    """裁掉主体四周的白边（近白且不透明的像素视为背景）。"""
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()

    min_x, min_y, max_x, max_y = w, h, -1, -1
    for y in range(0, h, 2):
        for x in range(0, w, 2):
            r, g, b, a = px[x, y]
            if a < 16:                      # 全透明
                continue
            if r > 245 and g > 245 and b > 245:   # 近白背景
                continue
            min_x, min_y = min(min_x, x), min(min_y, y)
            max_x, max_y = max(max_x, x), max(max_y, y)

    if max_x < 0:
        return im

    # 留 1% 呼吸空间，避免贴边
    pad = int(max(max_x - min_x, max_y - min_y) * 0.01)
    box = (max(0, min_x - pad), max(0, min_y - pad),
           min(w, max_x + pad + 1), min(h, max_y + pad + 1))
    return im.crop(box)


def main() -> None:
    src = Image.open(SRC)
    src = trim_white_margin(src)

    # 统一裁成正方形，避免非 1:1 源图被拉伸
    w, h = src.size
    side = min(w, h)
    left, top = (w - side) // 2, (h - side) // 2
    src = src.crop((left, top, left + side, top + side))

    base = src.resize((256, 256), Image.LANCZOS)
    base.save(DST, format="ICO", sizes=SIZES)
    print(f"{os.path.basename(DST)}: {os.path.getsize(DST)} bytes")


if __name__ == "__main__":
    main()
