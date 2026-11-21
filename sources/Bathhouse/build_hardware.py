"""Author the metric fittings for Bathhouse_02 using Blender's bundled Python.

Run Blender in background mode with --factory-startup --python-exit-code 1
--python sources/Bathhouse/build_hardware.py. Each mesh pivots at its mounting
point, ready to place repeatedly. All dimensions are in metres. Coordinates in
the modelling functions are (width, height, distance out from the wall).
"""

from math import cos, pi, sin
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "TidePool/Assets/_Game/Bathhouse/Models/BathhouseHardware.fbx"
# Blender swatches are linear. The scene builder remaps named slots to URP.
FINISHES = {
    "Steel": ((.42, .45, .45), .78, .29),
    "DarkMetal": ((.14, .16, .16), .65, .43),
    "BlackRubber": ((.022, .024, .025), 0, .7),
    "RedCap": ((.43, .026, .017), .06, .36),
    "BlueCap": ((.018, .084, .25), .06, .36),
    "MeterCase": ((.51, .49, .42), .08, .48),
    "Screen": ((.009, .014, .015), .12, .22),
    "RedLED": ((.96, .008, .004), 0, .28),
    "BenchWood": ((.40, .28, .17), .04, .47),
    "LampGlass": ((.77, .79, .74), 0, .28),
    "BlueSoap": ((.20, .48, .55), 0, .39),
    "VentDark": ((.055, .063, .06), .36, .61),
}


def in_blender(point):
    x, height, depth = point
    return Vector((x, -depth, height))


class Fitting:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.faces = []
        self.face_settings = []
        self.materials = []

    def face(self, indices, finish, smooth=False):
        if finish not in self.materials:
            self.materials.append(finish)
        self.faces.append(indices)
        self.face_settings.append((self.materials.index(finish), smooth))

    def box(self, center, sizes, finish, edge=.003):
        # Real bevels keep a narrow highlight on the reader casing and slats.
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        for vert in bm.verts:
            vert.co.x *= sizes[0]
            vert.co.y *= sizes[2]
            vert.co.z *= sizes[1]
        if edge:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(edge, min(sizes)*.42),
                            segments=3, affect='EDGES')
        bm.verts.index_update()
        start = len(self.vertices)
        position = in_blender(center)
        self.vertices.extend(position + vert.co for vert in bm.verts)
        for face in bm.faces:
            self.face([start + vert.index for vert in face.verts], finish)
        bm.free()

    def sweep(self, points, radii, finish, sides=14, smooth=True):
        centers = [in_blender(p) for p in points]
        if isinstance(radii, (float, int)):
            radii = [radii] * len(points)
        rings = []
        # Parallel transport keeps elbow joints untwisted along the pipe.
        guide = None
        for i, (center, radius) in enumerate(zip(centers, radii)):
            before = (center - centers[i-1]).normalized() if i else Vector()
            after = (centers[i+1] - center).normalized() if i + 1 < len(centers) else Vector()
            axis = (before + after).normalized()
            if guide is None:
                guide = min((Vector((1,0,0)), Vector((0,1,0)), Vector((0,0,1))),
                            key=lambda ref: abs(ref.dot(axis)))
            guide = (guide - axis * guide.dot(axis)).normalized()
            across = axis.cross(guide)
            ring = []
            for j in range(sides):
                angle = 2*pi*j/sides
                ring.append(len(self.vertices))
                self.vertices.append(center + radius*(cos(angle)*guide + sin(angle)*across))
            rings.append(ring)
        self.face(list(reversed(rings[0])), finish)
        for lower, upper in zip(rings, rings[1:]):
            for j in range(sides):
                k = (j+1) % sides
                self.face([lower[j], lower[k], upper[k], upper[j]], finish, smooth)
        self.face(rings[-1], finish)

    def rod(self, a, b, radius, finish="Steel", sides=14):
        self.sweep([a,b], radius, finish, sides)

    def finish(self):
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        assert not mesh.validate(), self.name
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        for name in self.materials:
            mesh.materials.append(bpy.data.materials[name])
        uv = mesh.uv_layers.new(name="UVMap")
        for face, (material, smooth) in zip(mesh.polygons, self.face_settings):
            face.material_index, face.use_smooth = material, smooth
            major = max(range(3), key=lambda i: abs(face.normal[i]))
            axes = ((1,2), (0,2), (0,1))[major]
            for loop in face.loop_indices:
                point = mesh.vertices[mesh.loops[loop].vertex_index].co
                uv.data[loop].uv = (point[axes[0]], point[axes[1]])
        mesh.update()
        return obj


def station():
    m = Fitting("ShowerStation")
    # Curved top arm and downward-facing bell. The outlet is about 38 cm off the wall.
    arm = [(0, .36, .063), (0, 2.045, .063)]
    for step in range(1, 9):
        a = .5*pi*step/8
        arm.append((0, 2.045 + .058*sin(a), .063 + .058*(1-cos(a))))
    arm.append((0, 2.103, .345))
    for step in range(1, 9):
        a = .5*pi*step/8
        arm.append((0, 2.103 - .043*(1-cos(a)), .345 + .043*sin(a)))
    arm.append((0, 2.047, .388))
    m.sweep(arm, .014, "Steel", 16)
    for height in (.40, .925, 1.57, 2.015):
        m.rod((0,height-.014,.063),(0,height+.014,.063),.019,"DarkMetal")
    for height in (1.58, 2.016):
        m.rod((0,height,.001),(0,height,.052),.009,"DarkMetal")
        m.rod((0,height,.001),(0,height,.009),.026,"Steel",24)
    m.sweep([(0,h,.388) for h in (2.062,2.040,2.032,2.020,2.010,2.001,1.994)],
            [.020,.024,.029,.045,.054,.061,.060], "Steel",32)
    m.sweep([(0,1.994,.388),(0,1.990,.388),(0,1.988,.388)],
            [.057,.054,.051], "BlackRubber",32)
    for radius, count in ((.014,6),(.030,11),(.044,16)):
        for i in range(count):
            a = 2*pi*i/count
            x, z = radius*cos(a), .388 + radius*sin(a)
            m.rod((x,1.988,z),(x,1.985,z),.0019,"DarkMetal",6)

    # Horizontal mixing body, escutcheons, hex unions and diverter lever.
    m.sweep([(-.126,.945,.09),(-.108,.945,.09),(.108,.945,.09),(.126,.945,.09)],
            [.024,.029,.029,.024], "Steel",22)
    m.rod((0,.945,.059),(0,.945,.093),.032)
    for x, cap in ((-.105,"RedCap"),(.105,"BlueCap")):
        m.sweep([(x,.945,z) for z in (.001,.004,.011,.017,.023)],
                [.038,.046,.045,.035,.023],"Steel",24)
        m.rod((x,.945,.020),(x,.945,.090),.018)
        m.rod((x,.945,.057),(x,.945,.077),.027,"DarkMetal",6)
        m.sweep([(x,.945,z) for z in (.102,.122,.138,.160,.164)],
                [.025,.028,.031,.030,.026],"Steel",14)
        m.rod((x,.945,.164),(x,.945,.167),.019,cap,18)
    m.rod((0,.945,.108),(0,.945,.139),.018,"DarkMetal")
    m.sweep([(0,.945,.140),(.016,.928,.143),(.043,.898,.146),(.056,.889,.147)],
            [.009,.009,.011,.011],"Steel",10)

    # Topped and bottomed elbow, union and mount marrying the horizontal feed to the riser.
    m.rod((-.039,.36,.063),(.039,.36,.063),.021,"DarkMetal",14)
    for x in (-.033,.033):
        m.rod((x-.006,.36,.063),(x+.006,.36,.063),.023,"Steel",8)

    # Small surface conduit and weather-sealed reader with recessed display and lit points.
    meter_x = -.245
    m.rod((meter_x,1.395,.016),(meter_x,2.87,.016),.008,"MeterCase",10)
    for h in (1.72,2.43):
        m.box((meter_x,h,.013),(.028,.013,.024),"MeterCase")
    m.box((meter_x,1.32,.041),(.165,.160,.076),"MeterCase",.010)
    m.box((meter_x,1.335,.081),(.138,.080,.008),"DarkMetal",.004)
    m.box((meter_x,1.337,.086),(.127,.064,.005),"Screen",.003)
    for x in (-.039,-.013,.013,.039):
        m.box((meter_x+x,1.34,.0895),(.016,.014,.002),"RedLED",.001)
    m.box((meter_x,1.265,.080),(.046,.005,.005),"DarkMetal",.001)
    for dx in (-.068,.068):
        for h in (1.258,1.382):
            m.rod((meter_x+dx,h,.077),(meter_x+dx,h,.081),.003,"Steel",8)
    return m.finish()


def basket():
    m = Fitting("SoapBasket")
    for x in (-.09,.09):
        m.rod((x,.060,.001),(x,.06,.024),.013)
        m.rod((x,.06,.001),(x,.06,.005),.019)
    for h in (.020,.077):
        rail = [(-.127,h,.023),(.127,h,.023),(.140,h,.034),(.140,h,.133),
                (.128,h,.145),(-.128,h,.145),(-.140,h,.133),(-.140,h,.034),(-.127,h,.023)]
        m.sweep(rail,.0042,"Steel",8)
    for x in (-.120,-.09,-.060,-.030,0,.030,.060,.09,.120):
        m.sweep([(x,.077,.024),(x,.026,.024),(x,.017,.033),(x,.017,.132),
                 (x,.026,.141),(x,.077,.141)],.0026,"Steel",8)
    return m.finish()


def bar():
    m = Fitting("SoapBar")
    # Oval, pressed mould profile with flatter top and rounded rims.
    rings = []
    for height, scale in ((.001,.84),(.004,.94),(.009,.99),(.022,1),(.027,.96),(.031,.84),(.033,.61)):
        ring = []
        for i in range(32):
            a = 2*pi*i/32
            x = .050*scale*cos(a)
            z = .029*scale*sin(a)
            ring.append(len(m.vertices))
            m.vertices.append(in_blender((x,height,z)))
        rings.append(ring)
    m.face(rings[0], "BlueSoap")
    for low, high in zip(rings,rings[1:]):
        for i in range(32):
            j = (i+1)%32
            m.face([low[i],high[i],high[j],low[j]],"BlueSoap",True)
    m.face(list(reversed(rings[-1])), "BlueSoap")
    return m.finish()


def bench():
    m = Fitting("SlattedBench")
    # A municipal changing-room bench with bowed tubular legs, slats and recessed bolts.
    for x in (-.57,.57):
        m.sweep([(x,.018,.19),(x,.365,.19),(x,.388,.18),(x,.400,.162),
                 (x,.400,-.162),(x,.388,-.18),(x,.365,-.19),(x,.018,-.19)],
                .018,"DarkMetal",14)
        m.sweep([(x,.38,-.17),(x,.44,-.175),(x,.81,-.216)],.014,"DarkMetal",12)
        for z in (-.19,.19):
            m.sweep([(x,.0,z),(x,.006,z),(x,.024,z)],[.023,.024,.021],"BlackRubber",12)
    m.rod((-.57,.155,-.18),(.57,.155,-.18),.012,"DarkMetal")
    for z in (-.151,-.051,.051,.151):
        m.box((0,.425,z),(1.5,.032,.089),"BenchWood",.006)
        for x in (-.57,.57):
            m.rod((x,.441,z),(x,.443,z),.0045,"DarkMetal",8)
    for h,z in ((.584,-.172),(.690,-.186),(.796,-.200)):
        m.box((0,h,z),(1.5,.087,.029),"BenchWood",.005)
        for x in (-.57,.57):
            m.rod((x,h,z+.014),(x,h,z+.017),.0045,"DarkMetal",8)
    return m.finish()


def lamp():
    m = Fitting("DomeLamp")
    m.sweep([(0,h,0) for h in (0,-.012,-.025,-.036,-.046)],
            [.127,.141,.142,.133,.123],"DarkMetal",40)
    m.sweep([(0,h,0) for h in (-.043,-.052,-.067,-.083,-.100,-.112,-.116)],
            [.126,.128,.123,.108,.080,.041,.008],"LampGlass",40)
    for x in (-.135,.135):
        m.rod((x,-.020,0),(x,-.032,0),.0035,"Steel",8)
    return m.finish()


def grille():
    m = Fitting("VentGrille")
    m.box((0,-.007,0),(.41,.012,.31),"DarkMetal",.002)
    m.box((0,-.015,0),(.36,.010,.26),"VentDark",.002)
    for i in range(-5,6):
        m.box((0,-.022,i*.023),(.35,.013,.008),"DarkMetal",.002)
    for x in (-.19,.19):
        for z in (-.14,.14):
            m.rod((x,-.013,z),(x,-.016,z),.0035,"Steel",8)
    return m.finish()


def drain():
    m = Fitting("DrainSection")
    for z in (-.047,.047):
        m.box((0,-.007,z),(1,.014,.011),"DarkMetal",.001)
    # Deep bed fills the apertures, avoiding an infinite hole through the scene.
    m.box((0,-.016,0),(1,.007,.109),"VentDark",.001)
    for i in range(40):
        m.box((-.4875 + i*.025,-.006,0),(.012,.012,.092),"Steel",edge=0)
    return m.finish()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    for name,(color,metal,roughness) in FINISHES.items():
        material = bpy.data.materials.new(name)
        material.diffuse_color = (*color,1)
        material.use_nodes = True
        shader = material.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = (*color,1)
        shader.inputs["Metallic"].default_value = metal
        shader.inputs["Roughness"].default_value = roughness
        if name == "RedLED":
            shader.inputs["Emission Color"].default_value = (*color,1)
            shader.inputs["Emission Strength"].default_value = 1.5
    objects = [build() for build in (station,basket,bar,bench,lamp,grille,drain)]
    bpy.context.view_layer.update()
    for obj in objects:
        obj.select_set(True)
        obj.data.calc_loop_triangles()
        print(f"{obj.name}: {len(obj.data.loop_triangles)} triangles; {len(obj.data.materials)} finishes")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT), use_selection=True, object_types={'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             mesh_smooth_type='FACE', bake_anim=False, add_leaf_bones=False,
                             use_custom_props=False, path_mode='STRIP')
    print("Exported", OUTPUT)


if __name__ == "__main__":
    main()
