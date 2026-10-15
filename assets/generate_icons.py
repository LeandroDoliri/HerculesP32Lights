import os
from PIL import Image, ImageDraw, ImageFilter

out_dir = os.path.dirname(os.path.abspath(__file__))
parent_dir = os.path.dirname(out_dir)

def create_badge_icon(is_on=True):
    size = 512
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    margin = 32
    bg_color = (24, 28, 36, 255) if not is_on else (20, 24, 40, 255)
    border_color = (80, 85, 95, 255) if not is_on else (0, 220, 255, 255)

    if is_on:
        glow = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        gdraw = ImageDraw.Draw(glow)
        gdraw.ellipse([margin - 16, margin - 16, size - margin + 16, size - margin + 16], fill=(0, 200, 255, 120))
        glow = glow.filter(ImageFilter.GaussianBlur(24))
        img = Image.alpha_composite(glow, img)
        draw = ImageDraw.Draw(img)

    draw.ellipse([margin, margin, size - margin, size - margin], fill=bg_color, outline=border_color, width=16)

    grid_start_x = 140
    grid_start_y = 140
    pad_size = 46
    pad_gap = 16

    for r in range(4):
        for c in range(4):
            x0 = grid_start_x + c * (pad_size + pad_gap)
            y0 = grid_start_y + r * (pad_size + pad_gap)
            x1 = x0 + pad_size
            y1 = y0 + pad_size

            if is_on:
                palette = [
                    (255, 50, 80, 255),
                    (0, 180, 255, 255),
                    (190, 50, 255, 255),
                    (0, 240, 180, 255)
                ]
                pad_color = palette[(r + c) % 4]
                pad_border = (255, 255, 255, 220)
            else:
                pad_color = (48, 52, 60, 255)
                pad_border = (70, 75, 85, 255)

            draw.rounded_rectangle([x0, y0, x1, y1], radius=8, fill=pad_color, outline=pad_border, width=4)

    dot_color = (0, 255, 130, 255) if is_on else (100, 105, 115, 255)
    draw.ellipse([240, 410, 272, 442], fill=dot_color, outline=(255, 255, 255, 200) if is_on else (60, 65, 75, 255), width=4)

    return img

sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)]

img_on = create_badge_icon(True)
img_off = create_badge_icon(False)
img_app = create_badge_icon(True)

for target in [out_dir, parent_dir]:
    img_on.save(os.path.join(target, 'icon_on.ico'), format='ICO', sizes=sizes)
    img_off.save(os.path.join(target, 'icon_off.ico'), format='ICO', sizes=sizes)
    img_app.save(os.path.join(target, 'app.ico'), format='ICO', sizes=sizes)

print('Icons generated successfully in', out_dir, 'and', parent_dir)
