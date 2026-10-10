from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
directory=Path(__file__).parent/'preview'
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',24)
clips={name:[Image.open(directory/f'{name}-{i:02}.png').convert('RGB').resize((320,320)) for i in range(16)] for name in ['idle','run','attack']}
frames=[]
for index in range(40):
    frame=Image.new('RGB',(960,360),(32,39,49))
    draw=ImageDraw.Draw(frame)
    for slot,(name,label,period) in enumerate([('idle','待机',40),('run','跑步',16),('attack','顶撞',12)]):
        frame.paste(clips[name][int((index%period)/period*16)],(slot*320,40))
        draw.text((slot*320+160,18),label,font=font,fill='white',anchor='mm')
    frames.append(frame)
frames[0].save(directory/'NiuLai-actions.gif',save_all=True,append_images=frames[1:],duration=50,loop=0)
frames[5].save(directory/'NiuLai-actions.png')
