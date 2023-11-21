import json
from PIL import Image, ImageDraw, ImageFont
from sys import argv, stdout
import os
import base64


def GenerateImage(text: str, fontSize: int, fontPath: str) -> Image.Image:
    stroke_width = int(fontSize * 6/58)
    spacing = 0
    fontFile = ImageFont.truetype(fontPath, fontSize)

    img = Image.new(
        "RGBA", (int(fontSize*1.5*len(text)), int(fontSize*2)), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    textbbox = draw.textbbox((10, 10), text,
                             font=fontFile,
                             stroke_width=stroke_width,
                             spacing=spacing)
    draw.text(
        (10, 10), text,
        fill=(255, 255, 255, 255),
        font=fontFile,
        stroke_width=stroke_width,
        stroke_fill=(40,40,65,255),
        spacing=spacing
    )
    img = img.crop(textbbox)
    # img = ChopTransparent(img)
    return img


if __name__ == "__main__":
    if not argv[1]:
        raise Exception("No name given")
    info = base64.b64decode(argv[1]).decode("utf-8")
    data = json.loads(info)

    text: str = data["str"]
    width: int = data["imgWidth"]
    height: int = data["imgHeight"]
    font: str = data["font"]

    if width / height > 16 / 9:
        fontsize = int(height * 0.043)
    else:
        fontsize = int(width * 0.024)
    fontsize *= 5
    img = GenerateImage(text, fontsize, font)
    b64name = base64.b64encode(text.encode(
        "utf-8")).decode("utf-8").replace('\\', "_").replace('/', "_")
    imgType = "EB" if font.endswith("EB.otf") else "DB"

    filepath = os.path.join(
        os.getcwd(), "patterns", f"{width}x{height}", imgType, f"{b64name}.png")
    if os.name == "nt":
        filepath = filepath.replace("/", "\\")
    if not os.path.exists(os.path.dirname(filepath)):
        os.makedirs(os.path.dirname(filepath), exist_ok=True)
    img.save(filepath)
    print(base64.b64encode(filepath.encode("utf-8")).decode("utf-8"), file=stdout)
