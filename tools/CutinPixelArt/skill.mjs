import fs from 'node:fs';
import {createPixelator} from 'image-to-pixel';
import {PNG} from 'pngjs';
import {read,write,clone,scale,canvas,paste,label} from './art.mjs';

// スキル発動のカットインの帯（2026-10-20、ユーザー指示）。
// 「べた塗りに本を作った時のドット感」「敵側は赤、主人公側は青」「外の枠は太い黒線」。
// 本（tools/RoomPixelArt の RoomBook*）と同じ作り方: 地色に狭い濃淡（1.06→0.94）を付け、
// 黒＋地色の 0.88/1/1.12 倍の4色へ、Image-to-Pixel 3.1.0 で半分に縮めながら bayer4 で減色する。
// 帯の形（透明なところ）はロンの gothic_band と同じにする。立ち絵の切り抜きの形を変えないため。

const here=new URL('./',import.meta.url);
const outDir=new URL('../../Assets/Resources/UI/Cutin/',here);
const review=new URL(process.env.SKILL_REVIEW_DIR ? 'file:///'+process.env.SKILL_REVIEW_DIR.replaceAll('\\','/').replace(/\/?$/,'/') : 'file:///C:/Unity/km-docs/screenshots/20261020/skill_cutin_bg/');
fs.mkdirSync(review,{recursive:true});

const sides={
  red:[168,28,36],   // 敵側
  blue:[34,78,170],  // 主人公側
};
const FrameWidth=4; // 1倍で4ドット（2倍表示で8単位）の黒い枠
const byte=n=>Math.max(0,Math.min(255,Math.round(n)));
const hex=c=>'#'+c.map(byte).map(v=>v.toString(16).padStart(2,'0')).join('');
// 帯の上の縁・下の縁（1倍の座標）。gothic_band と同じ式（art.mjs の edgeDistance）
const top=x=>66-Math.floor(x/4), bottom=x=>336-Math.floor(x/4);

const shape=read(new URL('gothic_band_before.png',here));
const engine=await createPixelator({backend:'cpu'});
const made={};
try {
  for(const [name,base] of Object.entries(sides)) {
    // 原形は2倍（800×600）。帯を横切る向きに、上 1.06 → 下 0.94 の濃淡
    const source=new PNG({width:800,height:600});
    for(let y=0;y<600;y++) for(let x=0;x<800;x++) {
      const t=Math.min(1,Math.max(0,(y/2-top(x/2))/(bottom(x/2)-top(x/2))));
      const shade=1.06-0.12*t, i=(y*800+x)*4;
      for(let c=0;c<3;c++) source.data[i+c]=byte(base[c]*shade);
      source.data[i+3]=255;
    }
    const colors=['#000000',...[0.88,1,1.12].map(k=>hex(base.map(v=>v*k)))];
    const result=await engine.process({width:800,height:600,data:source.data},{
      size:{mode:'width',width:400,height:300,resample:'box'},
      palette:{source:'custom',colors,distance:'oklab'},
      dither:{method:'bayer4',strength:0.18},
      outline:{mode:'none',color:'#000000',corners:true}
    });
    if(result.width!==400 || result.height!==300) throw new Error('変換寸法が不正です');
    const band=clone(shape);
    for(let y=0;y<300;y++) for(let x=0;x<400;x++) {
      const i=(y*400+x)*4;
      if(!shape.data[i+3]) { band.data.fill(0,i,i+4); continue; }
      const edge=Math.min(y-top(x),bottom(x)-y);
      if(edge<FrameWidth) band.data.set([0,0,0,255],i);
      else { band.data.set(result.data.subarray(i,i+3),i); band.data[i+3]=255; }
    }
    write(new URL(`skill_band_${name}.png`,outDir),band);
    write(new URL(`skill_band_${name}_2x.png`,review),scale(band,2));
    made[name]=band;
  }
} finally {engine.destroy();}

// 見本: 赤と青を2倍で並べる（ラベルは見本だけ）
const sheet=canvas(1624,636);
label(sheet,'ENEMY / RED / 2X',8,8);
label(sheet,'PLAYER / BLUE / 2X',816,8);
paste(sheet,scale(made.red,2),8,28);
paste(sheet,scale(made.blue,2),816,28);
write(new URL('skill_band_compare_2x.png',review),sheet);

// 検算: 透明度は gothic_band と1画素も違わない／色は各4色だけ
for(const [name,band] of Object.entries(made)) {
  let alphaDiff=0; const used=new Set();
  for(let i=0;i<band.data.length;i+=4) {
    if(band.data[i+3]!==shape.data[i+3]) alphaDiff++;
    if(band.data[i+3]) used.add(hex([band.data[i],band.data[i+1],band.data[i+2]]));
  }
  if(alphaDiff) throw new Error(`${name}: 透明度が ${alphaDiff} 画素違います`);
  console.log(JSON.stringify({name,alphaDiff,colors:[...used].sort()}));
}
