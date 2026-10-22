# -*- coding: utf-8 -*-
# v1.8.17 双击头部三阶段动作素材：seq.nwp（3 帧）
# 帧1=武术起势(webp) 帧2=红色愤怒(png去水印) 帧3=打电话(png去水印)
# 统一画布 618x618（显示 309x309），内容等比 contain 完整显示居中；白底抠透明
import zipfile, io, json, os
from PIL import Image
import numpy as np

out = r"C:\Users\Admin\Desktop\奶蛙桌宠\seq_new.nwp"
sources = [
    r"C:\Users\Admin\Desktop\owefEEEVBAAIoCj5AIuReAbBl1SGSLRoqEWVKl.webp",
    r"H:\豆包文件\红色小人-愤怒配色(绿眼版).png",
    r"H:\豆包文件\黄色小人-打电话全身_1.png",
]
TW, TH = 618, 618

frames = []
for idx, src in enumerate(sources):
    im = Image.open(src).convert("RGBA")
    arr = np.array(im)
    h, w = arr.shape[:2]
    r, g, b = arr[:, :, 0].astype(int), arr[:, :, 1].astype(int), arr[:, :, 2].astype(int)
    # 1) 白底/白晕硬切透明
    mn = arr[:, :, :3].astype(np.int16).min(axis=2)
    alpha = np.where(mn >= 240, 0, arr[:, :, 3]).astype(np.uint8)
    arr2 = np.copy(arr); arr2[:, :, 3] = alpha
    # 2) 右下角"豆包AI生成"水印（灰色文字）→ 透明
    y0, y1, x0, x1 = int(h*0.90), int(h*0.998), int(w*0.76), int(w*0.998)
    mask = np.zeros((h, w), bool); mask[y0:y1, x0:x1] = True
    grayish = (np.abs(r - g) < 25) & (np.abs(g - b) < 25) & mask & (arr2[:, :, 3] > 8)
    arr2[grayish, 3] = 0
    im2 = Image.fromarray(arr2, "RGBA")
    # 3) 内容 bbox
    ys, xs = np.where(arr2[:, :, 3] > 8)
    bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1) if len(ys) else (0, 0, 1, 1)
    crop = im2.crop(bbox)
    # 4) contain 到 618x618（等比完整显示、居中）
    scale = min(TW / crop.width, TH / crop.height)
    cw, ch = max(1, int(round(crop.width * scale))), max(1, int(round(crop.height * scale)))
    resized = crop.resize((cw, ch), Image.LANCZOS)
    c = Image.new("RGBA", (TW, TH), (0, 0, 0, 0))
    c.paste(resized, ((TW - cw) // 2, (TH - ch) // 2), resized)
    frames.append(c)
    print("帧%d bbox%s -> %dx%d 居中于 %dx%d, 非零alpha %.0f%%" % (idx + 1, bbox, cw, ch, TW, TH,
          float((np.array(c)[:, :, 3] > 8).sum()) / TW / TH * 100))

meta = {"width": TW, "height": TH, "frames": 3, "fps": 15, "duration": 1 / 15}
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    z.writestr("metadata.json", json.dumps(meta))
    for i, c in enumerate(frames):
        buf = io.BytesIO(); c.save(buf, "PNG")
        z.writestr("seq/%04d.png" % i, buf.getvalue())
print("seq.nwp 完成: %.1f KB" % (os.path.getsize(out) / 1024))

# 拼图预览
import math
cols = 3
W2 = Image.new("RGBA", (TW * cols, TH), (255, 255, 255, 255))
for i, c in enumerate(frames):
    W2.paste(c, (i * TW, 0), c)
W2.save(r"C:\Users\Admin\Desktop\奶蛙桌宠\self-test\seq_grid.png")
print("拼图预览: self-test/seq_grid.png")
