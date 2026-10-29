"""Battle Cities reusable pickup cube. Blender Z-up -> glTF Y-up, one unit.
Run with Blender --background --python build.py. No Unity files are changed.
"""
import bpy, bmesh, math, json, hashlib, shutil
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parent
for folder in ('textures', 'previews', 'references'):
    (ROOT/folder).mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1
S=1/.9
parts=[]

def linear(c): return c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4
def mat(name, rgb, metallic, roughness):
    m=bpy.data.materials.new(name); m.use_nodes=True; m.use_backface_culling=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*[linear(c) for c in rgb],1)
    p.inputs['Metallic'].default_value=metallic; p.inputs['Roughness'].default_value=roughness
    m.diffuse_color=(*[linear(c) for c in rgb],1)
    return m
gold=mat('Powerup_Gold_Frame',(1,.71,.055),.62,.27)
blue=mat('Powerup_Cobalt_Panels',(.018,.30,.84),.12,.32)
decal=mat('Powerup_Icon_Transparent',(1,1,1),0,.60)
decal.surface_render_method='DITHERED'
im=bpy.data.images.new('transparent-icon-placeholder',width=4,height=4,alpha=True)
im.pixels=[1,1,1,0]*16; im.filepath_raw=str(ROOT/'textures/transparent-icon-placeholder.png');im.file_format='PNG';im.save();im.pack()
tex=decal.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im
p=decal.node_tree.nodes.get('Principled BSDF')
decal.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
decal.node_tree.links.new(tex.outputs['Alpha'],p.inputs['Alpha'])

def finish(o,material):
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
    for f in o.data.polygons:
        axis=max(range(3),key=lambda k:abs(f.normal[k]));ax=[k for k in range(3) if k!=axis]
        for li in f.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co
            uv.data[li].uv=(v[ax[0]]+.5,v[ax[1]]+.5)
    o.data.materials.append(material);parts.append(o)
    return o

def box(name,at,dim,material,bevel):
    bpy.ops.mesh.primitive_cube_add(size=1,location=tuple(v*S for v in at));o=bpy.context.object;o.name=name
    o.dimensions=tuple(v*S for v in dim);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=o.modifiers.new('Single-segment structural bevel','BEVEL');mod.width=bevel*S;mod.segments=1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,material)

# Blue core is a genuine closed, chamfered volume, set behind all 12 gold rails.
box('Recessed cobalt core',(0,0,.45),(.77,.77,.77),blue,.045)
# Eight visibly larger gold caps meet the twelve slender rails.
for x in (-.36,.36):
    for y in (-.36,.36):
        for z in (.09,.81):box('Gold corner cap',(x,y,z),(.18,.18,.18),gold,.031)

def rail(axis,at):
    # An eight-sided chamfered rectangle extruded only along its length: 28 triangles.
    r=.057;c=.016;l=.276
    cross=[(-r+c,-r),(r-c,-r),(r,-r+c),(r,r-c),(r-c,r),(-r+c,r),(-r,r-c),(-r,-r+c)]
    v=[]
    others=[i for i in range(3) if i!=axis]
    for side in (-l,l):
        for a,b in cross:
            q=list(at);q[axis]+=side;q[others[0]]+=a;q[others[1]]+=b;v.append(tuple(n*S for n in q))
    faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
    mesh=bpy.data.meshes.new('Rail');mesh.from_pydata(v,[],faces);mesh.update()
    o=bpy.data.objects.new('Gold structural rail',mesh);bpy.context.collection.objects.link(o);finish(o,gold)
for axis in range(3):
    others=[i for i in range(3) if i!=axis]
    for a in (-.378,.378):
        for b in (-.378,.378):
            at=[0,0,.45];at[others[0]]+=a;at[others[1]]+=b;rail(axis,at)

# Preserve all editable components before producing the optimized runtime join.
root=bpy.data.objects.new('PowerupCube',None);bpy.context.collection.objects.link(root)
root['unitsPerTile']=64;root['nominalSize']=1.0;root['recommendedUniformScale']=.9
for o in parts:o.parent=root
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'powerup-cube-components.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='Cube_Body'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
tri=body.modifiers.new('Runtime triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
body['role']='opaque reusable blue and gold shell'

# glTF coordinate contract: all five decals at the requested outer planes.
# Side UV-up is world-up. Top UV-up points north (+glTF Z / Blender -Y).
faces=[
 ('Top',(0,0,1.001),(-1,0,0),(0,-1,0)),
 ('South',(0,.501,.5),(-1,0,0),(0,0,1)),
 ('North',(0,-.501,.5),(1,0,0),(0,0,1)),
 ('East',(.501,0,.5),(0,1,0),(0,0,1)),
 ('West',(-.501,0,.5),(0,-1,0),(0,0,1)),
]
# Top raster top corresponds to Blender -Y (north); retain outward winding separately.
decal_v=[];decal_f=[];decal_uv=[];anchors=[]
for name,center,u,up in faces:
    if name=='Top':up=(0,-1,0)
    n=Vector(u).cross(Vector(up));desired=Vector(center)-Vector((0,0,.5))
    reverse=n.dot(desired)<0
    start=len(decal_v);r=.32
    for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]:decal_v.append(tuple(Vector(center)+Vector(u)*a*r+Vector(up)*b*r))
    decal_f.append(tuple(start+i for i in ([3,2,1,0] if reverse else [0,1,2,3])))
    anchors.append({'name':'Decal_'+name,'position':[center[0],center[2],-center[1]],'right':[u[0],u[2],-u[1]],'up':[up[0],up[2],-up[1]],'size':[.64,.64]})
    empty=bpy.data.objects.new('Decal_'+name,None);bpy.context.collection.objects.link(empty);empty.parent=root;empty.location=center
    empty.empty_display_type='ARROWS';empty.empty_display_size=.1;empty['right_glTF']=anchors[-1]['right'];empty['up_glTF']=anchors[-1]['up'];empty['decalWidth']=.64
mesh=bpy.data.meshes.new('Five shared UV icon planes');mesh.from_pydata(decal_v,[],decal_f);mesh.update()
icons=bpy.data.objects.new('DecalSurfaces',mesh);bpy.context.collection.objects.link(icons);icons.parent=root;icons.data.materials.append(decal)
uv=mesh.uv_layers.new(name='IconUV')
for poly in mesh.polygons:
    start=(poly.vertices[0]//4)*4
    for li in poly.loop_indices:
        k=mesh.loops[li].vertex_index-start;uv.data[li].uv=[(0,0),(1,0),(1,1),(0,1)][k]
tri=icons.modifiers.new('Two triangles per icon','TRIANGULATE');bpy.context.view_layer.objects.active=icons;bpy.ops.object.modifier_apply(modifier=tri.name)
icons['role']='Optional transparent icon overlay; one material shared by top and four sides'
icons['castShadows']=False
root['decalContract']='Top y=1.001; four sides at x/z +/-0.501; side center y=0.5; UV 0..1; width 0.64'
bpy.ops.object.select_all(action='DESELECT')
for o in [root,body,icons]+[bpy.data.objects['Decal_'+x] for x in ['Top','South','North','East','West']]:o.select_set(True)
bpy.context.view_layer.objects.active=body
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'powerup-cube.blend'))
bpy.ops.export_scene.gltf(filepath=str(ROOT/'powerup-cube.glb'),export_format='GLB',use_selection=True,
    export_yup=True,export_apply=True,export_texcoords=True,export_normals=True,export_tangents=True,
    export_materials='EXPORT',export_extras=True,export_animations=False,export_cameras=False,export_lights=False)

# Grounded studio renders are inspection artifacts; lights/floor are excluded from export/source.
icons.hide_render=True
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.render.resolution_x=900;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.world.color=(.10,.10,.10)
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=0
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.001));floor=bpy.context.object;floor.name='Preview floor'
floor.data.materials.append(mat('Studio charcoal',(.16,.20,.25),0,.8))
for name,at,power,size in [('Key',(-3,-4,6),350,4),('Fill',(4,-1,3),180,3),('Rim',(1,4,5),450,3)]:
    bpy.ops.object.light_add(type='AREA',location=at);o=bpy.context.object;o.name=name;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.4))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();camera=bpy.context.object;camera.data.type='ORTHO';camera.data.ortho_scale=1.9;scene.camera=camera
for name,at in [('three-quarter',(3.2,4.5,3.4)),('top',(0,0,6)),('front',(0,6,.5))]:
    camera.location=at;camera.rotation_euler=(Vector((0,0,.5))-camera.location).to_track_quat('-Z','Y').to_euler()
    if name=='top':camera.rotation_euler=(0,0,math.pi)
    scene.render.filepath=str(ROOT/'previews'/f'cube-{name}.png');bpy.ops.render.render(write_still=True)

materials={'Powerup_Gold_Frame':{'baseColorSRGB':[1,.71,.055],'metallic':.62,'roughness':.27},'Powerup_Cobalt_Panels':{'baseColorSRGB':[.018,.30,.84],'metallic':.12,'roughness':.32},'Powerup_Icon_Transparent':{'baseColorFactor':[1,1,1,1],'metallic':0,'roughness':.6,'alphaMode':'BLEND','texture':'textures/transparent-icon-placeholder.png','castShadows':False}}
(ROOT/'materials.json').write_text(json.dumps(materials,indent=2)+'\n')
manifest={'id':'powerup-cube','status':'authored; validation and Unity import review pending','unitsPerTile':64,'nominalSize':1,'runtimeScale':.9,'visualFootprintSimulationUnits':57.6,'pivot':'ground-center','axis':'Y up; north +Z',
    'model':'powerup-cube.glb','source':'powerup-cube.blend','componentSource':'powerup-cube-components.blend','bodyTriangles':len(body.data.polygons),'decalTriangles':len(icons.data.polygons),'bodyMaterialSlots':len(body.data.materials),'totalMaterialSlots':3,'decals':anchors,
    'colliderSuggestion':{'type':'Box','center':[0,.5,0],'size':[1,1,1],'note':'Integration owns pickup trigger sizing; render shell must not block tanks.'},
    'iconInstructions':'Replace DecalSurfaces shared material with existing transparent shop illustration material, or disable DecalSurfaces and use the five anchors. Do not use both. UV is full 0..1 on every face. No powerup icons or halo are baked into the model.',
    'provenance':{'authoring':'Procedural Blender mesh, authored specifically for Battle Cities from user-supplied visual references; no external mesh or texture downloads.','tool':bpy.app.version_string,'references':[]},'previews':['previews/cube-three-quarter.png','previews/cube-top.png','previews/cube-front.png']}
for src in ['C:/Users/tassa/AppData/Local/Temp/codex-clipboard-f530beb4-83a7-4bd1-933a-ca8f79b61231.png','C:/Users/tassa/AppData/Local/Temp/codex-clipboard-3bc97f20-58c9-4ebf-8510-cf1ba381477a.jpg']:
    p=Path(src)
    if p.exists():shutil.copy2(p,ROOT/'references'/p.name);manifest['provenance']['references'].append({'originalPath':src,'localPath':'references/'+p.name,'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'rights':'User-supplied reference; source rights not independently verified.'})
(ROOT/'asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('POWERUP_CUBE_BUILT '+json.dumps({'triangles':len(body.data.polygons)+len(icons.data.polygons),'model':str(ROOT/'powerup-cube.glb')}))
