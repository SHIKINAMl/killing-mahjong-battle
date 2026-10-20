import fs from 'node:fs';
import {fileURLToPath} from 'node:url';
import {createPixelator} from 'image-to-pixel';
import {PNG} from 'pngjs';
import {palette,rgb,read,write,clone,editable,scale,put,polygon,canvas,paste,label} from './art.mjs';
import {verify} from './verify.mjs';

const here=new URL('./',import.meta.url);
const out=new URL('../../Assets/Resources/UI/Cutin/gothic_band.png',here);
const review=new URL(process.env.CUTIN_REVIEW_DIR ? process.env.CUTIN_REVIEW_DIR.replaceAll('\\','/').replace(/\/?$/,'/') : 'file:///C:/Unity/km-docs/screenshots/20261020/cutin_pixel_bg/v4_bat_blood/');
fs.mkdirSync(review,{recursive:true});
const before=read(new URL('gothic_band_before.png',here));
const source=scale(before,2);
// 窓の全画素を元の布の縦ひだで置換する。中央・斜めの縁は元画像のまま。
for(let y=0;y<600;y++) for(let x=0;x<800;x++) {
  const xx=Math.floor(x/2), yy=Math.floor(y/2);
  if(!editable(xx,yy)) continue;
  const phase=xx%24;
  put(source,x,y,rgb(palette[phase<5?3:phase<19?4:5]));
}
// 左右対称の2列。行ごとに雫とコウモリを入れ替える織物の繰り返し。
const bat=[[-9,-3],[-6,-1],[-3,-2],[-2,-5],[-.5,-3],[.5,-3],[2,-5],[3,-2],[6,-1],[9,-3],[8,1],[6,1],[5,3],[3,2],[1,5],[-1,5],[-3,2],[-5,3],[-6,1],[-8,1]];
const drop=[[0,-7],[1,-4],[3,-1],[5,2],[5,4],[3,6],[0,7],[-3,6],[-5,4],[-5,2],[-3,-1],[-1,-4]];
for(let row=0;row<9;row++) for(let col=0;col<2;col++) {
  const y=12+row*36+(col?18:0), x=14+col*24;
  const type=(row+col)%2?'drop':'bat';
  for(const cx of [x,399-x]) {
    const points=(type==='bat'?bat:drop).map(([dx,dy])=>[(cx+dx)*2,(y+dy)*2]);
    // 地色に近い一段明るい赤のみ。黒縁・目・光沢は描かない。
    polygon(source,points,rgb('#4E1B2C'));
  }
}
write(new URL('source_800x600.png',review),source);
const engine=await createPixelator({backend:'cpu'});
let result;
try {
  result=await engine.process({width:800,height:600,data:source.data},{
    size:{mode:'width',width:400,height:300,resample:'box'},
    palette:{source:'custom',colors:palette,distance:'oklab'},
    dither:{method:'bayer4',strength:0.18},
    outline:{mode:'none',color:'#000000',corners:true}
  });
} finally {engine.destroy();}
if(result.width!==400 || result.height!==300) throw new Error('変換寸法が不正です');
const after=clone(before);
for(let y=0;y<300;y++) for(let x=0;x<400;x++) {
  if(!editable(x,y)) continue;
  const i=(y*400+x)*4;
  after.data.set(result.data.subarray(i,i+3),i);
}
// 元のalphaを直接維持するため、減色の透明境界処理にマスク形状を依存させない。
const bytes=PNG.sync.write(after,{deflateLevel:9,deflateStrategy:0});
verify(before,after);
fs.writeFileSync(out,bytes);
fs.writeFileSync(new URL('gothic_band_after.png',review),bytes);
fs.copyFileSync(new URL('gothic_band_before.png',here),new URL('gothic_band_before.png',review));
for(const factor of [1,2]) {
  const a=scale(before,factor), b=scale(after,factor);
  const sheet=canvas(a.width*2+24,a.height+36);
  label(sheet,`BEFORE / ${factor}X`,8,8);
  label(sheet,`AFTER / ${factor}X`,a.width+16,8);
  paste(sheet,a,8,24); paste(sheet,b,a.width+16,24);
  write(new URL(`background_compare_${factor}x.png`,review),sheet);
  write(new URL(`background_${factor}x.png`,review),b);
}
const body=read(new URL('../../Assets/Resources/女の子/通常時身体.png',here));
const face=read(new URL('../../Assets/Resources/女の子/通常顔.png',here));
if(body.width!==face.width || body.height!==face.height) throw new Error('立ち絵差分の寸法が不一致です');
paste(body,face,0,0);
// 大きい原画像は整数の除数で間引く。色変更や補間をしない（元ファイルは変更しない）。
const divisor=Math.max(1,Math.ceil(Math.max(body.width,body.height)/720));
if(body.width%divisor || body.height%divisor) throw new Error('立ち絵を整数の除数で縮小できません');
const character=scale(body,1/divisor);
const mock=canvas(800,600,[11,8,13,255]);
const bg=scale(after,2), layer=canvas(800,600,[0,0,0,0]);
paste(mock,bg,0,0);
paste(layer,character,Math.round(210-character.width/2),Math.round(420-character.height/2));
for(let i=0;i<layer.data.length;i+=4) if(!bg.data[i+3]) layer.data[i+3]=0;
paste(mock,layer,0,0);
label(mock,'MOCK / COMPOSITED ASSETS / NOT A UNITY CAPTURE',16,16);
label(mock,'TEXT AREA',505,290,2);
write(new URL('character_composite_mock.png',review),mock);
const report=verify(before,after);
report.sourceSize=[800,600];
report.pipeline={tool:'Image-to-Pixel',version:'3.1.0',backend:'cpu',resample:'box',paletteDistance:'oklab',dither:'bayer4',strength:0.18,outline:'none'};
report.motifs={batNativeSize:[18,10],batDisplaySize:[36,20],dropNativeSize:[10,14],dropDisplaySize:[20,28],rowStep:36,columnStep:24,outline:false};
report.character={sourceSize:[body.width,body.height],displaySize:[character.width,character.height],nearestDivisor:divisor,sourceUnchanged:true,preview:'mock'};
report.pngBytes=bytes.length;
fs.writeFileSync(new URL('verification.json',review),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({output:fileURLToPath(out),review:fileURLToPath(review),...report},null,2));
