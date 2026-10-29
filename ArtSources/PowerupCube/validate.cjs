const fs=require('fs'),path=require('path'),crypto=require('crypto'),validator=require('gltf-validator');
const root=__dirname;
(async()=>{
 const bytes=fs.readFileSync(path.join(root,'powerup-cube.glb')),jl=bytes.readUInt32LE(12),d=JSON.parse(bytes.subarray(20,20+jl)),bin=28+jl;
 const checks=await validator.validateBytes(bytes),issues=[];
 function values(ai){const a=d.accessors[ai],v=d.bufferViews[a.bufferView],n={SCALAR:1,VEC2:2,VEC3:3,VEC4:4}[a.type],size={5123:2,5125:4,5126:4}[a.componentType],read={5123:'readUInt16LE',5125:'readUInt32LE',5126:'readFloatLE'}[a.componentType],stride=v.byteStride??n*size,start=bin+(v.byteOffset??0)+(a.byteOffset??0);return Array.from({length:a.count},(_,i)=>Array.from({length:n},(_,k)=>bytes[read](start+i*stride+k*size)));}
 const meshes=[];
 for(const node of d.nodes.filter(n=>n.mesh!==undefined)){
  const min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];let triangles=0,vertices=0,zeroArea=0;
  if(node.translation||node.rotation||node.scale||node.matrix)issues.push(node.name+' has a nonidentity transform');
  for(const p of d.meshes[node.mesh].primitives){
   const pos=values(p.attributes.POSITION),ind=values(p.indices).flat();triangles+=ind.length/3;vertices+=pos.length;
   for(const v of pos)for(let k=0;k<3;k++){min[k]=Math.min(min[k],v[k]);max[k]=Math.max(max[k],v[k]);}
   for(const key of ['NORMAL','TANGENT']){if(p.attributes[key]===undefined)issues.push('Missing '+key+' '+node.name);else if(values(p.attributes[key]).some(v=>!v.every(Number.isFinite)||Math.abs(Math.hypot(...v.slice(0,3))-1)>.002))issues.push('Invalid '+key);}
   if(values(p.attributes.TEXCOORD_0).some(v=>!v.every(Number.isFinite)))issues.push('Invalid UV');
   for(let i=0;i<ind.length;i+=3){const a=pos[ind[i]],b=pos[ind[i+1]],c=pos[ind[i+2]],u=b.map((v,k)=>v-a[k]),v=c.map((v,k)=>v-a[k]);const n=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]];if(Math.hypot(...n)<1e-9)zeroArea++;}
  }
  if(zeroArea)issues.push(node.name+' has degenerate triangles');
  meshes.push({name:node.name,triangles,vertices,materialSlots:d.meshes[node.mesh].primitives.length,bounds:{min,max}});
 }
 const body=meshes.find(x=>x.name==='Cube_Body'),overlay=meshes.find(x=>x.name==='DecalSurfaces');
 const close=(a,b)=>Math.abs(a-b)<1e-5;
 if(!body.bounds.min.every((v,k)=>close(v,[-.5,0,-.5][k]))||!body.bounds.max.every((v,k)=>close(v,[.5,1,.5][k])))issues.push('Shell must be unit sized and grounded');
 if(body.triangles>800||overlay.triangles!==10)issues.push('Triangle budget');
 const anchors=d.nodes.filter(n=>n.name.startsWith('Decal_'));
 if(anchors.length!==5)issues.push('Missing anchor');
 for(const n of anchors){const expected={Top:[0,1.001,0],North:[0,.5,.501],South:[0,.5,-.501],East:[.501,.5,0],West:[-.501,.5,0]}[n.name.slice(6)];if(!n.translation.every((v,k)=>close(v,expected[k])))issues.push('Anchor placement '+n.name);}
 if(d.materials.length!==3||d.materials.find(m=>m.name==='Powerup_Icon_Transparent').alphaMode!=='BLEND')issues.push('Material contract');
 if(d.animations?.length||d.extensions?.KHR_lights_punctual||d.cameras?.length)issues.push('Unexpected preview-only contents');
 const result={file:path.join(root,'powerup-cube.glb'),bytes:bytes.length,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),meshes,totalTriangles:meshes.reduce((n,m)=>n+m.triangles,0),materials:d.materials,anchors,errors:checks.issues.numErrors+issues.length,warnings:checks.issues.numWarnings,issues:[...issues,...checks.issues.messages.filter(x=>x.severity<2)]};
 fs.writeFileSync(path.join(root,'validation.json'),JSON.stringify(result,null,2));
 const manifest=JSON.parse(fs.readFileSync(path.join(root,'asset-manifest.json')));Object.assign(manifest,{status:result.errors||result.warnings?'validation requires review':'GLB validated; Unity integration review pending',sha256:result.sha256,validation:'validation.json',meshCount:meshes.length,totalTriangles:result.totalTriangles,measuredShellBounds:body.bounds});fs.writeFileSync(path.join(root,'asset-manifest.json'),JSON.stringify(manifest,null,2));
 console.log(JSON.stringify({triangles:result.totalTriangles,meshes:meshes.map(x=>({name:x.name,triangles:x.triangles,bounds:x.bounds})),errors:result.errors,warnings:result.warnings,issues:result.issues},null,2));if(result.errors||result.warnings)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1});
