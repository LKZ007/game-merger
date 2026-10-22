import sys, time, os
from playwright.sync_api import sync_playwright
from PIL import Image
OUT = sys.argv[1]
js = open("/home/claude/game-merger/gamemerger/web/brain.js").read()
html = """<html><head><style>
body{margin:0;background:transparent;font-family:'DejaVu Sans',sans-serif}
#icon{position:absolute;left:0;top:0;width:512px;height:512px;border-radius:112px;
 background:radial-gradient(circle at 50% 38%,#3b1f73 0%,#1d0f3a 55%,#0c0912 100%);box-shadow:inset 0 0 0 10px rgba(167,139,250,.55)}
#icon canvas{position:absolute;left:36px;top:30px;width:440px;height:452px}
#small{position:absolute;left:0;top:520px;width:512px;height:512px;border-radius:96px;overflow:hidden;
 background:radial-gradient(circle at 45% 35%,#7c4ddb 0%,#3b1f73 60%,#1d0f3a 100%)}
#small canvas{position:absolute;left:-20px;top:-10px;width:600px;height:620px}
#wel{position:absolute;left:600px;top:0;width:164px;height:314px;background:linear-gradient(180deg,#24124a 0%,#110a22 60%,#0c0912 100%);overflow:hidden}
#wel canvas{position:absolute;left:12px;top:40px;width:140px;height:150px}
#wel .t{position:absolute;left:0;right:0;top:214px;text-align:center;color:#ece8f5;font-weight:700;font-size:16px;letter-spacing:.5px}
#wel .t span{color:#a78bfa}
#wel .s{position:absolute;left:0;right:0;top:244px;text-align:center;color:#b9b0cf;font-size:10px}
#hdr{position:absolute;left:800px;top:0;width:150px;height:57px;background:#ffffff}
#hdr canvas{position:absolute;left:96px;top:3px;width:50px;height:51px}
</style></head><body>
<div id=icon><canvas data-b=1></canvas></div>
<div id=wel><canvas data-b=2></canvas><div class=t>FRANKEN<span>STEIN</span></div><div class=s>stitch your games together</div></div>
<div id=small><canvas data-b=1></canvas></div>
<div id=hdr><canvas data-b=3></canvas></div>
<script>JSHERE</script>
<script>document.querySelectorAll('canvas').forEach(c=>LogoBrain.mount(c,{dense:c.dataset.b==1?1100:700,angle:1.25,speed:0}));</script>
</body></html>""".replace("JSHERE", js)
with sync_playwright() as p:
    b = p.chromium.launch(executable_path="/opt/pw-browsers/chromium")
    pg = b.new_page(viewport={"width": 1000, "height": 1040}, device_scale_factor=2)
    pg.set_content(html); time.sleep(1.5)
    pg.screenshot(path=OUT + "/icon-hi.png", omit_background=True, clip={"x": 0, "y": 0, "width": 512, "height": 512})
    pg.screenshot(path=OUT + "/small-hi.png", omit_background=True, clip={"x": 0, "y": 520, "width": 512, "height": 512})
    pg.screenshot(path=OUT + "/welcome-hi.png", clip={"x": 600, "y": 0, "width": 164, "height": 314})
    pg.screenshot(path=OUT + "/header-hi.png", clip={"x": 800, "y": 0, "width": 150, "height": 57})
    b.close()
ic = Image.open(OUT + "/icon-hi.png").convert("RGBA").resize((256, 256), Image.LANCZOS)
sm = Image.open(OUT + "/small-hi.png").convert("RGBA")
big = [ic.resize((n, n), Image.LANCZOS) for n in (40, 48, 64, 128)]
small = [sm.resize((n, n), Image.LANCZOS) for n in (16, 20, 24, 32)]
ic.save(OUT + "/Frankenstein.ico", format="ICO", append_images=small + big, sizes=[(n, n) for n in (16, 20, 24, 32, 40, 48, 64, 128, 256)])
ic.save(OUT + "/icon256.png")
Image.open(OUT + "/welcome-hi.png").convert("RGB").resize((164, 314), Image.LANCZOS).save(OUT + "/welcome.bmp")
Image.open(OUT + "/header-hi.png").convert("RGB").resize((150, 57), Image.LANCZOS).save(OUT + "/header.bmp")
# preview sheet
sheet = Image.new("RGB", (900, 360), (20, 14, 32))
sheet.paste(Image.open(OUT + "/welcome-hi.png").convert("RGB").resize((164, 314)), (20, 23))
sheet.paste(ic, (210, 52), ic)
for i, sz in enumerate((64, 48, 32, 24, 16)):
    src = sm if sz <= 32 else ic
    sheet.paste(src.resize((sz, sz), Image.LANCZOS), (500 + i * 72, 150), src.resize((sz, sz), Image.LANCZOS))
sheet.paste(Image.open(OUT + "/header.bmp"), (500, 260))
sheet.save(OUT + "/preview.png")
