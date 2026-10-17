"""Generate the code-native pixel bell logo and quiet two-note audio cue."""
from pathlib import Path
import math, struct, wave
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
assets = root / 'src/QuestBell/Assets'
assets.mkdir(parents=True, exist_ok=True)
im = Image.new('RGBA', (32,32), (13,18,27,255))
d = ImageDraw.Draw(im)
for box, color in [((8,4,23,27),'#24483f'), ((13,5,18,7),'#f3cc7a'), ((10,8,21,10),'#b0ffe5'), ((8,11,23,22),'#6be8c5'), ((6,21,25,24),'#6be8c5'), ((13,25,18,27),'#f3cc7a'), ((10,12,12,19),'#c0ffe8'), ((20,13,23,22),'#329c8d'), ((24,5,26,7),'#bdaaff'), ((26,3,28,5),'#bdaaff')]:
    d.rectangle(box, fill=color)
im.save(assets / 'questbell.ico', sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)])
im.resize((256,256),Image.Resampling.NEAREST).save(assets / 'logo.png')
with wave.open(str(assets/'chime.wav'),'wb') as w:
    w.setparams((1,2,22050,0,'NONE','not compressed'))
    for i in range(22050):
        t=i/22050
        value=0.0
        for start,freq in [(0.0,660),(0.17,880)]:
            a=t-start
            if a>=0: value+=math.sin(2*math.pi*freq*a)*math.exp(-6*a)*min(1,a*120)*0.13
        w.writeframesraw(struct.pack('<h',int(max(-1,min(1,value))*32767)))
print('Native logo and chime created.')
