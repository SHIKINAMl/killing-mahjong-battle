import fs from 'node:fs';
import {createPixelator} from 'image-to-pixel';
import {PNG} from 'pngjs';
import {write,scale,canvas,paste,label} from './art.mjs';

// 和了の点数表の地と見出しの帯（2026-10-25、ユーザー指示）。
// 「血だけではなく背景も作ってくれませんか？ デバッグ部屋にある本の模様を参考に」。
// 本（tools/RoomPixelArt の RoomBook*）と同じ作り方: 地色に上から下へ狭い濃淡（1.06→0.94）を付け、
// 黒＋地色の 0.88/1/1.12 倍の4色へ、Image-to-Pixel 3.1.0 で半分に縮めながら bayer4（強さ 0.18）で減色する。
// **本と違って黒い縁は付けない**（ユーザー「端っこの枠みたいなもんはいらない」）。
// 自分の和了＝青、相手の和了＝赤。地の色は点数表の GroundBlue/GroundRed、帯は HeadBlue/HeadRed（RonAnimationUI.cs）。
// 表の高さは役の数で 200〜340 に変わるので、地は一番高いとき（560x340 単位＝280x170 ドット）で作り、
// 表の上にそろえて置いて下を切る（伸ばすとドットが崩れる）。

const here=new URL('./',import.meta.url);
const outDir=new URL('../../Assets/Resources/UI/Settlement/',here);
const review=new URL('file:///C:/Unity/km-docs/screenshots/20261025/settlement_pixel/book_method/');
fs.mkdirSync(outDir,{recursive:true});
fs.mkdirSync(review,{recursive:true});

const sides={
  blue:{ground:[0x1C,0x2B,0x4A],head:[0x26,0x38,0x5E]},
  red:{ground:[0x4A,0x1C,0x28],head:[0x5E,0x24,0x32]},
};
const byte=n=>Math.max(0,Math.min(255,Math.round(n)));
const hex=c=>'#'+c.map(byte).map(v=>v.toString(16).padStart(2,'0')).join('');

const engine=await createPixelator({backend:'cpu'});
const made={};
async function bookLike(base,width,height){
  // 原形は2倍（width x height 単位）。上 1.06 → 下 0.94 の濃淡
  const source=new PNG({width,height});
  for(let y=0;y<height;y++) for(let x=0;x<width;x++) {
    const shade=1.06-0.12*y/Math.max(1,height-1), i=(y*width+x)*4;
    for(let c=0;c<3;c++) source.data[i+c]=byte(base[c]*shade);
    source.data[i+3]=255;
  }
  const colors=['#000000',...[0.88,1,1.12].map(k=>hex(base.map(v=>v*k)))];
  const result=await engine.process({width,height,data:source.data},{
    size:{mode:'width',width:width/2,height:height/2,resample:'box'},
    palette:{source:'custom',colors:[...new Set(colors)],distance:'oklab'},
    dither:{method:'bayer4',strength:0.18},
    outline:{mode:'none',color:'#000000',corners:true}
  });
  if(result.width!==width/2 || result.height!==height/2) throw new Error('変換寸法が不正です');
  const png=new PNG({width:result.width,height:result.height});
  png.data.set(result.data);
  return png;
}
try {
  for(const [name,side] of Object.entries(sides)) {
    const ground=await bookLike(side.ground,560,340);
    const head=await bookLike(side.head,560,40);
    write(new URL(`settlement_ground_${name}.png`,outDir),ground);
    write(new URL(`settlement_head_${name}.png`,outDir),head);
    made[name]={ground,head};
  }
} finally {engine.destroy();}

// 見本: 帯を地の上に重ねて2倍で並べる（ラベルは見本だけ）
const sheet=canvas(1144,380);
label(sheet,'PLAYER / BLUE / 2X',8,8);
label(sheet,'ENEMY / RED / 2X',584,8);
for(const [i,name] of ['blue','red'].entries()) {
  paste(sheet,scale(made[name].ground,2),8+i*576,28);
  paste(sheet,scale(made[name].head,2),8+i*576,28);
}
write(new URL('settlement_book_method_2x.png',review),sheet);

for(const [name,{ground,head}] of Object.entries(made)) {
  const used=new Set();
  for(const img of [ground,head]) for(let i=0;i<img.data.length;i+=4) used.add(hex([img.data[i],img.data[i+1],img.data[i+2]]));
  console.log(JSON.stringify({name,ground:[ground.width,ground.height],head:[head.width,head.height],colors:[...used].sort()}));
}
