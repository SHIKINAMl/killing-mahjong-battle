import fs from 'node:fs';
import {PNG} from 'pngjs';

export const palette = ['#000000','#160F18','#1E0707','#3E1522','#461827','#4E1B2C','#663931','#786E55'];
export const rgb = hex => hex.slice(1).match(/../g).map(v => parseInt(v, 16));
export const read = path => PNG.sync.read(fs.readFileSync(path));
export const write = (path, im) => fs.writeFileSync(path, PNG.sync.write(im,{deflateLevel:9,deflateStrategy:0}));
export function clone(im) {
  const out=new PNG({width:im.width,height:im.height});
  im.data.copy(out.data);
  return out;
}
export const edgeDistance = (x,y) => Math.min(y-(66-Math.floor(x/4)), 336-Math.floor(x/4)-y);
export const editable = (x,y) => Math.min(x,399-x)<50 && edgeDistance(x,y)>=15;
export function put(im,x,y,color) {
  if(x<0 || x>=im.width || y<0 || y>=im.height) return;
  im.data.set(color.length===4?color:[...color,255],(y*im.width+x)*4);
}
export function canvas(width,height,color=[28,28,32,255]) {
  const im=new PNG({width,height});
  for(let i=0;i<im.data.length;i+=4) im.data.set(color,i);
  return im;
}
export function scale(im,factor) {
  const out=new PNG({width:im.width*factor,height:im.height*factor});
  for(let y=0;y<out.height;y++) for(let x=0;x<out.width;x++) {
    const i=(Math.floor(y/factor)*im.width+Math.floor(x/factor))*4;
    put(out,x,y,im.data.subarray(i,i+4));
  }
  return out;
}
export function paste(out,im,dx,dy) {
  for(let y=0;y<im.height;y++) for(let x=0;x<im.width;x++) {
    const xx=dx+x, yy=dy+y;
    if(xx<0 || xx>=out.width || yy<0 || yy>=out.height) continue;
    const i=(y*im.width+x)*4, j=(yy*out.width+xx)*4, a=im.data[i+3]/255;
    const b=out.data[j+3]/255, alpha=a+b*(1-a);
    if(!alpha) continue;
    for(let c=0;c<3;c++) out.data[j+c]=Math.round((im.data[i+c]*a+out.data[j+c]*b*(1-a))/alpha);
    out.data[j+3]=Math.round(alpha*255);
  }
}
// 2倍原形の画素中心で面を判定。ぼかし・補間・黒い輪郭は付けない。
export function polygon(im,points,color) {
  const minX=Math.floor(Math.min(...points.map(p=>p[0]))), maxX=Math.ceil(Math.max(...points.map(p=>p[0])));
  const minY=Math.floor(Math.min(...points.map(p=>p[1]))), maxY=Math.ceil(Math.max(...points.map(p=>p[1])));
  for(let y=minY;y<maxY;y++) for(let x=minX;x<maxX;x++) {
    let inside=false;
    for(let i=0,j=points.length-1;i<points.length;j=i++) {
      const [xi,yi]=points[i], [xj,yj]=points[j];
      if((yi>y+.5)!==(yj>y+.5) && x+.5<(xj-xi)*(y+.5-yi)/(yj-yi)+xi) inside=!inside;
    }
    if(inside) put(im,x,y,color);
  }
}
// 見本の英字ラベル専用。ゲーム素材へは描かない。
const glyphs={
  A:'01110/10001/10001/11111/10001/10001/10001', B:'11110/10001/10001/11110/10001/10001/11110',
  C:'01111/10000/10000/10000/10000/10000/01111', D:'11110/10001/10001/10001/10001/10001/11110',
  E:'11111/10000/10000/11110/10000/10000/11111', F:'11111/10000/10000/11110/10000/10000/10000',
  G:'01111/10000/10000/10111/10001/10001/01111', H:'10001/10001/10001/11111/10001/10001/10001',
  I:'111/010/010/010/010/010/111', J:'00111/00010/00010/00010/10010/10010/01100',
  K:'10001/10010/10100/11000/10100/10010/10001', L:'10000/10000/10000/10000/10000/10000/11111',
  M:'10001/11011/10101/10101/10001/10001/10001', N:'10001/11001/10101/10011/10001/10001/10001',
  O:'01110/10001/10001/10001/10001/10001/01110', P:'11110/10001/10001/11110/10000/10000/10000',
  Q:'01110/10001/10001/10001/10101/10010/01101', R:'11110/10001/10001/11110/10100/10010/10001',
  S:'01111/10000/10000/01110/00001/00001/11110', T:'11111/00100/00100/00100/00100/00100/00100',
  U:'10001/10001/10001/10001/10001/10001/01110', V:'10001/10001/10001/10001/10001/01010/00100',
  W:'10001/10001/10001/10101/10101/10101/01010', X:'10001/10001/01010/00100/01010/10001/10001',
  Y:'10001/10001/01010/00100/00100/00100/00100', Z:'11111/00001/00010/00100/01000/10000/11111',
  0:'01110/10001/10011/10101/11001/10001/01110', 1:'010/110/010/010/010/010/111',
  2:'01110/10001/00001/00110/01000/10000/11111', 3:'11110/00001/00001/01110/00001/00001/11110',
  4:'10010/10010/10010/11111/00010/00010/00010', 5:'11111/10000/10000/11110/00001/00001/11110',
  6:'01110/10000/10000/11110/10001/10001/01110', 7:'11111/00001/00010/00100/01000/01000/01000',
  8:'01110/10001/10001/01110/10001/10001/01110', 9:'01110/10001/10001/01111/00001/00001/01110',
  '/':'00001/00001/00010/00100/01000/10000/10000', '-':'00000/00000/00000/11111/00000/00000/00000'
};
export function label(im,text,x,y,factor=1) {
  for(const char of text.toUpperCase()) {
    const rows=(glyphs[char]??'000/000/000/000/000/000/000').split('/');
    for(let yy=0;yy<7;yy++) for(let xx=0;xx<rows[yy].length;xx++) if(rows[yy][xx]==='1') {
      for(let sy=0;sy<factor;sy++) for(let sx=0;sx<factor;sx++) put(im,x+xx*factor+sx,y+yy*factor+sy,[224,215,204]);
    }
    x+=(rows[0].length+1)*factor;
  }
}
