"""Assemble real Unity captures; check palette relationships and original-alpha joints.
Run after MonsterArtAcceptance.ExportTemplates and verify-monster-system.ps1.
No source art is changed. Pillow is needed only for the review sheets.
"""
import argparse, json, math, re, sys
sys.stdout.reconfigure(encoding="utf-8")
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageChops

ROOT = Path(__file__).resolve().parents[1]
NAMES = ['双头虫足','露脑缀眼','双星连体','双头触须','多眼星团','露脑圆身','猫耳缝合','横卧翼体','钉饰短肢','独角黑肢','须角白肢','蝠耳触手']
BOXES = [(20,10,410,305),(470,0,780,315),(25,305,430,505),(480,320,825,590),(20,545,380,790),(470,610,696,820),(700,590,895,800),(30,820,410,990),(475,820,680,1020),(680,795,865,1025),(865,795,1052,1025),(1045,750,1380,1025)]
def lab(hexcode):
    rgb = [int(hexcode[i:i+2],16)/255 for i in (0,2,4)]
    r,g,b = [x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in rgb]
    l=(.4122214708*r+.5363325363*g+.0514459929*b)**(1/3)
    m=(.2119034982*r+.6806995451*g+.1073969566*b)**(1/3)
    s=(.0883024619*r+.2817188376*g+.6299787005*b)**(1/3)
    return (.2104542553*l+.793617785*m-.0040720468*s,1.9779984951*l-2.428592205*m+.4505937099*s,.0259040371*l+.7827717662*m-.808675766*s)
def hue(c):
    _,a,b=lab(c); return math.degrees(math.atan2(b,a))%360

def paste_fit(out,im,box):
    x,y,w,h=box; im=im.copy();im.thumbnail((w,h),Image.Resampling.LANCZOS);out.paste(im,(x+(w-im.width)//2,y+(h-im.height)//2))

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--directory',default='docs/audit/2026-10-20/templates');args=parser.parse_args()
    directory=ROOT/args.directory;directory.mkdir(parents=True,exist_ok=True)
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
    overview=Image.new('RGB',(1400,1110),'#d4cbbd');draw=ImageDraw.Draw(overview)
    reference=Image.open(ROOT/'Assets/示意图/怪物实例.jpg').convert('RGB')
    comparison=Image.new('RGB',(1800,1360),'#d4cbbd');cd=ImageDraw.Draw(comparison)
    for t,name in enumerate(NAMES):
        im=Image.open(directory/f'template-{t}.png').convert('RGB')
        paste_fit(overview,im,(t%4*350,t//4*370,350,335));draw.text((t%4*350+25,t//4*370+339),f'{t:02} {name}',font=font,fill='#241c16')
        x=t%3*600;y=t//3*340
        paste_fit(comparison,reference.crop(BOXES[t]),(x+10,y+20,280,265))
        paste_fit(comparison,im.crop((101,131,659,729)),(x+305,y+20,280,265))
        cd.text((x+20,y+295),f'{t:02} {name}   参考 / Unity 实际画像',font=font,fill='#241c16')
    overview.save(directory/'template-overview.png');comparison.save(directory/'reference-comparison.png')
    for p in range(6):
        sheet=Image.new('RGB',(1400,1110),'#d4cbbd');d=ImageDraw.Draw(sheet)
        for t,name in enumerate(NAMES):
            im=Image.open(directory/f'palette-{p}-template-{t}.png').convert('RGB')
            paste_fit(sheet,im,(t%4*350,t//4*370,350,335));d.text((t%4*350+25,t//4*370+339),f'{t:02} {name}',font=font,fill='#241c16')
        sheet.save(directory/f'colorway-{p}.png')
    checks=[]
    if (directory/'colorways.json').exists():
        colors=json.loads((directory/'colorways.json').read_text(encoding='utf-8-sig'))
        sheet=Image.new('RGB',(1000,6*115),'#efe6d6');d=ImageDraw.Draw(sheet)
        for i,p in enumerate(colors):
            dl=lab(p['body'])[0]-lab(p['pattern'])[0]
            dh=abs((hue(p['body'])-hue(p['pattern'])+180)%360-180)
            different=lambda key:abs((hue(p['body'])-hue(p[key])+180)%360-180)>40
            ok=.09<dl<.15 and dh<6 and different('eye') and different('mouth')
            checks.append(dict(name=p['name'],bodyPatternLightnessDifference=dl,bodyPatternHueDifference=dh,passed=ok))
            d.text((20,i*115+40),p['name'],font=font,fill='#241c16')
            for j,(key,label) in enumerate([('body','身体'),('pattern','花纹'),('eye','眼部'),('mouth','嘴部')]):
                x=140+j*210;d.rounded_rectangle((x,i*115+10,x+190,i*115+66),8,fill='#'+p[key]);d.text((x,i*115+73),label+' #'+p[key],font=font,fill='#241c16')
        sheet.save(directory/'color-cards.png')
    joints=[]
    if (directory/'template-geometry.json').exists():
        face_conflicts=[]
        def paint(l):
            im=Image.open(ROOT/l['source']).convert('RGBA');x,y,w,h=l['ink'];im=im.crop((round(x),round(im.height-y-h),round(x+w),round(im.height-y)))
            im=im.resize(tuple(max(1,round(v*4)) for v in l['size']),Image.Resampling.BICUBIC)
            if l['mirror']:im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            mask=im.getchannel('A').point(lambda a:255 if a>64 else 0).rotate(l['angle'],resample=Image.Resampling.NEAREST,expand=True)
            field=Image.new('L',(1800,1400));field.paste(mask,(round((l['at'][0]+220)*4-mask.width/2),round((320-l['at'][1])*4-mask.height/2)))
            field.info["bounds"]=field.getbbox()
            return field
        def overlap(a,b):
            x,y=a.info['bounds'],b.info['bounds'];box=(max(x[0],y[0]),max(x[1],y[1]),min(x[2],y[2]),min(x[3],y[3]))
            return 0 if box[0]>=box[2] or box[1]>=box[3] else ImageChops.multiply(a.crop(box),b.crop(box)).histogram()[255]
        for template in json.loads((directory/'template-geometry.json').read_text(encoding='utf-8-sig')):
            layers=[l for l in template['layers'] if re.search('Core|Cross|Head|Star(?:Front|Rear|Left|Right)|Stalk|Feel|Foot|Arm|Tentacle|Tendril|Wing|Ear|Horn|Rivet|Spine|Brain|Accessory',l['name']) and not re.search('StarMark|Seam',l['name'])]
            alpha=[paint(l) for l in layers];adjacency=[]
            for i,l in enumerate(layers):
                adjacency.append([other['name'] for j,other in enumerate(layers) if i!=j and overlap(alpha[i],alpha[j])>=8])
            root=next((l['name'] for l in layers if l['name']=='Core'),next((l['name'] for l in layers if l['name'] in ('StarFront','CrossLeft','StarLeft')),layers[0]['name']))
            reached={root}
            while True:
                new=reached|{name for i,l in enumerate(layers) if l['name'] in reached for name in adjacency[i]}
                if new==reached:break
                reached=new
            for i,l in enumerate(layers):
                joints.append(dict(template=template['template'],head=template.get('head',0),back=template.get('back',0),layer=l['name'],neighbors=adjacency[i],connected=l['name'] in reached))
            for accessory in [l for l in template['layers'] if l['name']=='HeadAccessory']:
                for eye in [l for l in template['layers'] if l.get('expression')]:
                    area=overlap(paint(accessory),paint(eye))/16
                    if area>1:face_conflicts.append(dict(template=template['template'],head=template.get('head',0),back=template.get('back',0),eye=eye['name'],overlapArea=area))
        (directory/'accessory-eye-clearance.json').write_text(json.dumps(dict(passed=not face_conflicts,conflicts=face_conflicts),ensure_ascii=False,indent=2),encoding='utf-8')
        (directory/'alpha-joints.json').write_text(json.dumps(dict(kind='BASELINE_AND_ALL_COMPATIBLE_ACCESSORIES_ORIGINAL_ALPHA_CONNECTIVITY',rows=joints,disconnected=[j for j in joints if not j['connected']]),ensure_ascii=False,indent=2),encoding='utf-8')
    motion=sorted(directory.glob('motion-[025].png'))
    if len(motion)==3:
        frames=[]
        for p in motion:
            im=Image.open(p).convert('RGB');im.thumbnail((960,540));frames.append(im)
        frames[0].save(directory/'motion-preview.gif',save_all=True,append_images=frames[1:],duration=240,loop=0)
    (directory/'color-checks.json').write_text(json.dumps(dict(passed=bool(checks) and all(c['passed'] for c in checks),checks=checks),ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(colorChecks=checks,disconnectedJoints=[(j['template'],j['head'],j['back'],j['layer']) for j in joints if not j['connected']],directory=str(directory)),ensure_ascii=False))
    if checks and not all(c['passed'] for c in checks):raise SystemExit('Palette relationships failed')
    if any(not j['connected'] for j in joints):raise SystemExit('Original-alpha attachment connectivity failed')
    if 'face_conflicts' in locals() and face_conflicts:raise SystemExit('Accessory covers expression eye: '+str(face_conflicts))
if __name__=='__main__':main()
