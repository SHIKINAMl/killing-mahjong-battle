import fs from 'node:fs';
import {PNG} from 'pngjs';
import {createPixelator} from 'image-to-pixel';

const parts=JSON.parse(fs.readFileSync(new URL('parts.json',import.meta.url)));
const output=new URL('../../Assets/Resources/Room/PixelParts/',import.meta.url);
fs.mkdirSync(output,{recursive:true});
const engine=await createPixelator({backend:'cpu'});
const byte=n=>Math.max(0,Math.min(255,Math.round(n)));
const hex=rgb=>'#'+rgb.map(byte).map(c=>c.toString(16).padStart(2,'0')).join('');
const bayer=[[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]];
const results=[];
for(const p of parts){
  const glow=p.name.endsWith('Glow');
  const width=Math.max(4,Math.round(p.width)),height=Math.max(4,Math.round(p.height));
  const image=new PNG({width,height});
  const rgb=glow?[255,255,255]:p.color.slice(0,3).map(v=>v*255);
  // 現在の矩形・色を原形にし、狭い濃淡だけを付けて減色する。
  for(let y=0;y<height;y++)for(let x=0;x<width;x++){
    const at=(y*width+x)*4;
    const shade=glow?1:1.06-0.12*y/Math.max(1,height-1);
    for(let c=0;c<3;c++)image.data[at+c]=byte(rgb[c]*shade);
    image.data[at+3]=255;
  }
  const palette=glow?['#ffffff']:['#000000',...([0.88,1,1.12].map(k=>hex(rgb.map(v=>v*k))))];
  const result=await engine.process({width,height,data:image.data},{
    size:{mode:'width',width:Math.max(2,Math.round(width/2)),height:Math.max(2,Math.round(height/2)),resample:'box'},
    palette:{source:'custom',colors:[...new Set(palette)],distance:'oklab'},
    dither:{method:'bayer4',strength:0.18},
    outline:{mode:glow?'none':'inner',color:'#000000',corners:true}
  });
  if(!glow){
    // 完全不透明の矩形には透明境界がないため、キャンバス端も明示的に黒縁にする。
    for(let y=0;y<result.height;y++)for(let x=0;x<result.width;x++){
      if(x!==0&&y!==0&&x!==result.width-1&&y!==result.height-1)continue;
      const at=(y*result.width+x)*4;
      result.data[at]=result.data[at+1]=result.data[at+2]=0;
    }
  }
  if(glow){
    // 光は黒縁を付けず、端を透過ドットにする。元のImageの色とalphaで明滅させる。
    for(let y=0;y<result.height;y++)for(let x=0;x<result.width;x++){
      const edge=Math.min(x,y,result.width-1-x,result.height-1-y);
      const coverage=Math.min(1,(edge+1)/6);
      result.data[(y*result.width+x)*4+3]=(bayer[y%4][x%4]+0.5)/16<coverage?255:0;
    }
  }
  fs.writeFileSync(new URL(p.name+'.png',output),await result.toPNG({width,height}));
  results.push({name:p.name,width,height,colors:result.usedColors().length,glow});
}
engine.destroy();
console.log(JSON.stringify({count:results.length,parts:results},null,2));
