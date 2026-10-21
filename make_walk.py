# -*- coding: utf-8 -*-
# v1.8 行走动画打包：walk_01~08 → walk.nwp（426x618 画布、8 帧、10fps）
import zipfile, io, json, os
from PIL import Image

d = r"H:\豆包文件\行走帧_透明PNG\行走帧_透明PNG"
out = r"C:\Users\Admin\Desktop\奶蛙桌宠\walk_new.nwp"
U, V = 426, 618
files = sorted(os.listdir(d))

# 统一内容边界
bounds = None
for f in files:
    im = Image.open(os.path.join(d, f)).convert("RGBA")
    a = im.getchannel("A")
    bbox = a.getbbox()
    bounds = bbox if bounds is None else (min(bounds[0], bbox[0]), min(bounds[1], bbox[1]), max(bounds[2], bbox[2]), max(bounds[3], bbox[3]))
print("统一内容边界:", bounds, "尺寸:", bounds[2]-bounds[0], "x", bounds[3]-bounds[1])

frames = []
for f in files:
    im = Image.open(os.path.join(d, f)).convert("RGBA")
    crop = im.crop(bounds)
    sw = int(round(crop.width * V / crop.height))
    c = Image.new("RGBA", (U, V), (0, 0, 0, 0))
    c.paste(crop.resize((sw, V), Image.LANCZOS), ((U - sw) // 2, 0), crop.resize((sw, V), Image.LANCZOS))
    frames.append(c)

meta = {"width": U, "height": V, "frames": len(frames), "fps": 10, "duration": len(frames) / 10}
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    z.writestr("metadata.json", json.dumps(meta))
    for i, fr in enumerate(frames):
        buf = io.BytesIO(); fr.save(buf, "PNG")
        z.writestr("walk/%04d.png" % i, buf.getvalue())
print("walk.nwp 完成: %.1f KB, 帧数 %d, 10fps" % (os.path.getsize(out) / 1024, len(frames)))
