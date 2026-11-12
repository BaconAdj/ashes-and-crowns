"""Draws the generated plan of Wallingford (docs/wallingford-plan.png) from the CSV the test harness writes:
    TOWN_DUMP=/tmp/town.csv dotnet test Tools/dotnet-tests && python3 Tools/town_map.py /tmp/town.csv docs/wallingford-plan.png
North is up. One pixel is half a metre."""
import math, sys
from PIL import Image, ImageDraw

src, out = sys.argv[1], sys.argv[2]
rows = [l.rstrip("\n").split(",") for l in open(src)]
plan = [float(v) for v in next(r for r in rows if r[0] == "plan")[1:]]
hw, hd, rx0, rx1, mx, mz, mr, bx0, bx1 = plan
S = 2.0; pad = 190
W, H = int((2 * hw + 2 * pad + 120) * S), int((2 * hd + 2 * pad) * S)
def P(x, z): return ((x + hw + pad) * S, (hd + pad - z) * S)
img = Image.new("RGB", (W, H), (96, 110, 70)); d = ImageDraw.Draw(img)
d.rectangle([P(rx0, hd + pad), P(rx1, -hd - pad)], fill=(70, 92, 110))                       # Thames
d.rectangle([P(-hw - 29, hd + 29), P(hw + 14, -hd - 29)], fill=(74, 96, 112))                # wet ditch
d.rectangle([P(-hw - 14, hd + 14), P(hw + 14, -hd - 14)], fill=(88, 84, 60))                 # bank
d.rectangle([P(-hw, hd), P(hw + 14, -hd)], fill=(120, 126, 86))                              # inside
d.rectangle([P(rx0, hd + pad), P(rx1, -hd - pad)], fill=(70, 92, 110))
cols = {"the castle": (134, 126, 104), "the market place": (150, 138, 110), "Holy Trinity priory": (126, 134, 100), "the Kinecroft": (112, 132, 80), "the Bullcroft": (108, 128, 78)}
for r in rows:
    if r[0] == "rect":
        x0, z0, x1, z1 = map(float, r[2:6]); d.rectangle([P(x0, z1), P(x1, z0)], fill=cols.get(r[1], (130, 130, 100)))
        d.text(P(x0 + 6, z1 - 6), r[1], fill=(40, 40, 30))
d.ellipse([P(mx - mr, mz + mr), P(mx + mr, mz - mr)], fill=(100, 104, 70), outline=(60, 60, 40))
for r in rows:
    if r[0] == "street":
        x0, z0, x1, z1, w = map(float, r[2:7]); d.line([P(x0, z0), P(x1, z1)], fill=(150, 136, 104), width=int(w * S))
d.line([P(bx0, 0), P(bx1, 0)], fill=(110, 90, 60), width=int(5 * S))
kind_col = {"House": (92, 70, 44), "Cot": (120, 96, 60), "GreatHouse": (70, 48, 30), "Alehouse": (150, 80, 40), "ParishChurch": (60, 60, 66), "GreatChurch": (40, 40, 48),
            "PrioryRange": (70, 70, 60), "CastleHall": (70, 60, 50), "Keep": (50, 50, 54), "Gatehouse": (50, 50, 54), "Stable": (90, 76, 54)}
for r in rows:
    if r[0] != "building": continue
    kind = r[1]; x, z, f, hw_, hd_ = map(float, r[2:7])
    a = math.radians(f); fx, fz = math.sin(a), math.cos(a); rx_, rz_ = fz, -fx
    pts = [P(x + rx_ * sx * hw_ + fx * sz * hd_, z + rz_ * sx * hw_ + fz * sz * hd_) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    d.polygon(pts, fill=kind_col.get(kind, (80, 60, 40)))
    if kind in ("ParishChurch", "GreatChurch", "Keep", "CastleHall") and r[7]: d.text(P(x + 10, z + 4), r[7], fill=(20, 20, 24))
for r in rows:
    if r[0] == "street":
        x0, z0, x1, z1 = map(float, r[2:6]); d.text(P((x0 + x1) / 2 + 3, (z0 + z1) / 2 + 3), r[1], fill=(250, 244, 220))
t = next(r for r in rows if r[0] == "totals")
d.text((16, 14), "Wallingford, 1135 (generated plan). %s houses, %s townsfolk, %s souls with garrison and monks, %d parish churches. North is up; the grid of the ramparts is 550 m by 780 m." % (t[1], t[2], t[3], int(t[4]) - 1), fill=(255, 255, 255))
img.save(out); print("wrote", out, img.size)
