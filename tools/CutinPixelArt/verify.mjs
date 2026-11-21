import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import fs from 'node:fs';
import {pathToFileURL} from 'node:url';
import {palette,read,editable,edgeDistance} from './art.mjs';

export function verify(before,after) {
  assert.equal(before.width,400); assert.equal(before.height,300);
  assert.equal(after.width,400); assert.equal(after.height,300);
  const masks=[Buffer.alloc(120000),Buffer.alloc(120000)];
  let alphaMismatch=0,changed=0,changedOutsideSides=0,changedFrame=0,opaque=0;
  const colors=new Map(), alpha=new Set();
  for(let y=0;y<300;y++) for(let x=0;x<400;x++) {
    const px=y*400+x, i=px*4;
    masks[0][px]=before.data[i+3]; masks[1][px]=after.data[i+3];
    if(before.data[i+3]!==after.data[i+3]) alphaMismatch++;
    alpha.add(after.data[i+3]);
    if(after.data[i+3]) {
      opaque++;
      const color='#'+after.data.subarray(i,i+3).toString('hex').toUpperCase();
      colors.set(color,(colors.get(color)??0)+1);
    }
    if(!before.data.subarray(i,i+4).equals(after.data.subarray(i,i+4))) {
      changed++;
      if(!editable(x,y)) changedOutsideSides++;
      if(edgeDistance(x,y)<15) changedFrame++;
    }
  }
  assert.equal(alphaMismatch,0,'透明境界が変化しています');
  assert.deepEqual([...alpha].sort((a,b)=>a-b),[0,255],'半透明画素があります');
  assert.equal(changedOutsideSides,0,'両端以外に差分があります');
  assert.equal(changedFrame,0,'外枠に差分があります');
  assert(changed>0,'変更がありません');
  assert([...colors.keys()].every(color=>palette.includes(color)),'パレット外の色があります');
  return {size:[400,300],opaquePixels:opaque,transparentPixels:120000-opaque,alphaValues:[...alpha].sort((a,b)=>a-b),alphaMismatch,changedPixels:changed,changedOutsideSides,changedFramePixels:changedFrame,opaqueColors:colors.size,palette:Object.fromEntries(colors),alphaSha256Before:createHash('sha256').update(masks[0]).digest('hex'),alphaSha256After:createHash('sha256').update(masks[1]).digest('hex')};
}
if(process.argv[1] && import.meta.url===pathToFileURL(process.argv[1]).href) {
  const before=read(new URL('gothic_band_before.png',import.meta.url));
  const after=read(new URL('../../Assets/Resources/UI/Cutin/gothic_band.png',import.meta.url));
  const report=verify(before,after);
  const hash=createHash('sha256').update(fs.readFileSync(new URL('../../Assets/Resources/UI/Cutin/gothic_band.png.meta',import.meta.url))).digest('hex');
  assert.equal(hash,'fc8906b9b9e8c536d9c3515fa2122099b499c3652e45bb7bbbca217d48f85e90','metaが着手時から変わっています');
  console.log(JSON.stringify({...report,metaSha256:hash},null,2));
}
