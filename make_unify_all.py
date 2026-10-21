# -*- coding: utf-8 -*-
# v1.6.5 素材脚本：统一所有奶蛙本体大小（等高309，画布213x309，无空白）
# 素材.nwp（原版422帧）裁空白；drag.nwp（GIF 120帧）与 feed.nwp（沮丧表情）等比缩放到等高后居中贴入
import zipfile, io, json, os, shutil
from PIL import Image
import numpy as np

gif = r"C:\Users\Admin\Desktop\oABgBKB8iA4XOA0gAY6IABdZOwVEIGAfQ1gsHi.png.gif"
webp = r"C:\Users\Admin\Desktop\oAAeWABB0BP0P81iiAAJ4AE0ZAmRvyaQNYIrCt.webp"
folder = r"C:\Users\Admin\Desktop\奶蛙桌宠"
outdir = os.path.join(folder, "eye_extract")
os.makedirs(outdir, exist_ok=True)

U = 213   # 统一画布宽
V = 309   # 统一本体高
T0, T1 = 45.0, 70.0
BG = np.array([252, 252, 255])


def crop_paste(src, w, h, x, y):
    c = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    c.paste(src, (x, y), src)
    return c


def content_bounds(img):
    a = np.array(img)
    alpha = a[..., 3] > 10
    if not alpha.any():
        return None
    ys, xs = np.where(alpha)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


# ---------- 1. 素材.nwp：原版 422 帧统一裁剪 ----------
src_nwp = os.path.join(folder, "素材.nwp")
shutil.copy2(src_nwp, os.path.join(folder, "素材.nwp.bak"))
z = zipfile.ZipFile(src_nwp)
meta = json.loads(z.read('metadata.json'))
b = (37, 47, 250, 356)   # 素材内容并集边界（已采样验证）
print("素材裁剪边界:", b, "->", U, "x", V)

with zipfile.ZipFile(os.path.join(folder, "素材_new.nwp"), "w", zipfile.ZIP_DEFLATED) as oz:
    m2 = dict(meta); m2['width'] = U; m2['height'] = V
    oz.writestr("metadata.json", json.dumps(m2))
    count = 0
    for name in z.namelist():
        if name == "metadata.json":
            continue
        raw = z.read(name)
        im = Image.open(io.BytesIO(raw)).convert('RGBA').crop(b)
        buf = io.BytesIO(); im.save(buf, "PNG")
        oz.writestr(name, buf.getvalue())
        count += 1
print("素材.nwp 重打包完成:", count, "帧, %.1f KB" % (os.path.getsize(os.path.join(folder, "素材_new.nwp")) / 1024))

# ---------- 2. drag.nwp：GIF 抠图 → 等高 309 → 居中贴入 ----------
im = Image.open(gif)
N = im.n_frames
cut = N
for i in range(N):
    im.seek(i)
    if im.info.get('duration', 0) == 0:
        cut = i
        break
if cut == N:
    cut = 120
frames = []
for i in range(cut):
    im.seek(i)
    rgb = np.array(im.convert('RGB')).astype(int)
    d = np.linalg.norm(rgb - BG, axis=2)
    alpha = np.clip((d - T0) / (T1 - T0), 0, 1) * 255
    rgba = np.dstack([rgb, alpha.astype(np.uint8)]).astype(np.uint8)
    src = Image.fromarray(rgba, 'RGBA')
    # v1.6.6：变形缩放到与原版奶蛙完全同尺寸（U x V），保证所有奶蛙本体大小一致
    src = src.resize((U, V), Image.LANCZOS)
    frames.append(crop_paste(src, U, V, 0, 0))
for i in (0, 30, 70, 110, cut - 1):
    frames[i].save(os.path.join(outdir, "drag_u2_%03d.png" % i))

meta_drag = {"width": U, "height": V, "frames": cut, "fps": 15, "duration": cut / 15}
with zipfile.ZipFile(os.path.join(folder, "drag_new.nwp"), "w", zipfile.ZIP_DEFLATED) as oz:
    oz.writestr("metadata.json", json.dumps(meta_drag))
    for i, fr in enumerate(frames):
        buf = io.BytesIO(); fr.save(buf, "PNG")
        oz.writestr("drag/%04d.png" % i, buf.getvalue())
print("drag.nwp 已生成:", meta_drag, "%.1f KB" % (os.path.getsize(os.path.join(folder, "drag_new.nwp")) / 1024))

# ---------- 3. feed.nwp：webp 抠图去水印 → 等高 309 → 居中贴入 ----------
wp = Image.open(webp).convert('RGB')
wprgb = np.array(wp).astype(int)
d = np.linalg.norm(wprgb - BG, axis=2)
alpha = np.clip((d - T0) / (T1 - T0), 0, 1) * 255
rgba = np.dstack([wprgb, alpha.astype(np.uint8)]).astype(np.uint8)
feed = Image.fromarray(rgba, 'RGBA')
wpx = np.array(feed)
wm_l, wm_t, wm_r, wm_b = 262, 466, 336, 504
wmask = np.zeros((wpx.shape[0], wpx.shape[1]), bool)
wmask[wm_t:wm_b, wm_l:wm_r] = True
wmask &= wpx[..., 3] > 30
ring = 14
for yy, xx in np.argwhere(wmask):
    y0, y1 = max(0, yy - ring), min(wpx.shape[0], yy + ring + 1)
    x0, x1 = max(0, xx - ring), min(wpx.shape[1], xx + ring + 1)
    patch = wpx[y0:y1, x0:x1]
    sel = patch[(patch[..., 3] > 30) & (~wmask[y0:y1, x0:x1])]
    if len(sel):
        wpx[yy, xx, :3] = np.median(sel[:, :3], axis=0).astype(np.uint8)
feed = Image.fromarray(wpx, 'RGBA')
b_f = content_bounds(feed)
# v1.6.6：变形缩放到与原版奶蛙完全同尺寸（U x V）
feed = feed.crop(b_f).resize((U, V), Image.LANCZOS)
feed_u = crop_paste(feed, U, V, 0, 0)
feed_u.save(os.path.join(outdir, "feed_unified2.png"))
meta_feed = {"width": U, "height": V, "frames": 1}
buf = io.BytesIO(); feed_u.save(buf, "PNG")
with zipfile.ZipFile(os.path.join(folder, "feed_new.nwp"), "w", zipfile.ZIP_DEFLATED) as oz:
    oz.writestr("metadata.json", json.dumps(meta_feed))
    oz.writestr("feed/0000.png", buf.getvalue())
print("feed.nwp 已生成:", meta_feed, "%.1f KB" % (os.path.getsize(os.path.join(folder, "feed_new.nwp")) / 1024))
print("完成。新包: 素材_new.nwp / drag_new.nwp / feed_new.nwp")
