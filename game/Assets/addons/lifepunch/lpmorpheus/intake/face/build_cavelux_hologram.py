"""Build an original LifePunch Morpheus pedestal + living Cavelux hologram.

Run:
  blender --background --python build_cavelux_hologram.py

Units are meters. 1 inch = 0.0254 m. Meshy stills are silhouette only;
that mesh is not imported. The hologram face is cavelux_logo.png.
"""

from __future__ import annotations

import math
import os
from mathutils import Euler, Matrix, Vector

import bpy
import bmesh


INCH = 0.0254
HERE = os.path.dirname(os.path.abspath(__file__))
LOGO_PATH = os.path.join(HERE, "cavelux_logo.png")
BLEND_PATH = os.path.join(HERE, "cavelux_morpheus.blend")
FBX_PATH = os.path.join(HERE, "cavelux_morpheus.fbx")
PRODUCT_FBX_PATH = os.path.normpath(os.path.join(HERE, "..", "..", "models", "cavelux_morpheus.fbx"))
PREVIEW_PATH = os.path.join(HERE, "cavelux_morpheus_preview.png")

DISC_Z = 12.0 * INCH
DISC_RADIUS = 8.0 * INCH
BASE_RADIUS = 9.0 * INCH
BASE_H = 2.4 * INCH
FRAMES = 120


def inch(value: float) -> float:
    return value * INCH


def wipe_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.lights, bpy.data.cameras):
        for item in list(block):
            block.remove(item)


def new_object(name: str, mesh: bpy.types.Mesh) -> bpy.types.Object:
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def shade_smooth(obj: bpy.types.Object) -> None:
    for poly in obj.data.polygons:
        poly.use_smooth = True


def assign_mat(obj: bpy.types.Object, mat: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def mesh_from_bmesh(name: str, bm: bmesh.types.BMesh) -> bpy.types.Object:
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return new_object(name, mesh)


def add_cube(bm: bmesh.types.BMesh, loc: Vector, size: Vector, rot: Euler | None = None) -> None:
    matrix = Matrix.LocRotScale(loc, rot or Euler((0.0, 0.0, 0.0)), size)
    bmesh.ops.create_cube(bm, size=1.0, matrix=matrix)


def add_cylinder(
    bm: bmesh.types.BMesh,
    loc: Vector,
    radius: float,
    depth: float,
    segs: int,
    rot: Euler | None = None,
) -> None:
    matrix = Matrix.LocRotScale(loc, rot or Euler((0.0, 0.0, 0.0)), Vector((1.0, 1.0, 1.0)))
    bmesh.ops.create_cone(
        bm,
        cap_ends=True,
        cap_tris=False,
        segments=segs,
        radius1=radius,
        radius2=radius,
        depth=depth,
        matrix=matrix,
    )


def dark_metal() -> bpy.types.Material:
    mat = bpy.data.materials.new("lp_dark_metal")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (300, 0)
    principled = nt.nodes.new("ShaderNodeBsdfPrincipled")
    principled.location = (0, 0)
    set_socket(principled, "Base Color", (0.012, 0.013, 0.016, 1.0))
    set_socket(principled, "Metallic", 0.92)
    set_socket(principled, "Roughness", 0.38)
    nt.links.new(principled.outputs[0], out.inputs["Surface"])
    return mat


def cyan_led() -> bpy.types.Material:
    mat = bpy.data.materials.new("lp_cyan_led")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emission = nt.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.0, 1.0, 1.0, 1.0)
    emission.inputs["Strength"].default_value = 18.0
    nt.links.new(emission.outputs[0], out.inputs["Surface"])
    return mat


def voxel_white() -> bpy.types.Material:
    mat = bpy.data.materials.new("lp_voxel_white")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    principled = nt.nodes.new("ShaderNodeBsdfPrincipled")
    set_socket(principled, "Base Color", (0.82, 0.84, 0.88, 1.0))
    set_socket(principled, "Metallic", 0.05)
    set_socket(principled, "Roughness", 0.42)
    set_socket(principled, "Emission Color", (0.55, 0.58, 0.62, 1.0))
    set_socket(principled, "Emission Strength", 0.35)
    nt.links.new(principled.outputs[0], out.inputs["Surface"])
    return mat


def voxel_green() -> bpy.types.Material:
    mat = bpy.data.materials.new("lp_cavelux_green")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emission = nt.nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (0.15, 1.0, 0.12, 1.0)
    emission.inputs["Strength"].default_value = 22.0
    nt.links.new(emission.outputs[0], out.inputs["Surface"])
    return mat


def set_socket(node: bpy.types.Node, name: str, value) -> None:
    if name in node.inputs:
        node.inputs[name].default_value = value


def hologram_material(logo: bpy.types.Image) -> bpy.types.Material:
    mat = bpy.data.materials.new("lp_cavelux_hologram")
    mat.use_nodes = True
    if hasattr(mat, "blend_method"):
        mat.blend_method = "BLEND"
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (720, 0)

    emission = nt.nodes.new("ShaderNodeEmission")
    emission.location = (360, 80)
    emission.inputs["Strength"].default_value = 14.0
    emission.name = "HoloEmission"

    transparent = nt.nodes.new("ShaderNodeBsdfTransparent")
    transparent.location = (360, -80)

    mix = nt.nodes.new("ShaderNodeMixShader")
    mix.location = (540, 0)

    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.location = (-220, 80)
    tex.image = logo
    tex.interpolation = "Closest"

    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.location = (-420, 80)
    mapping.name = "HoloMapping"

    texcoord = nt.nodes.new("ShaderNodeTexCoord")
    texcoord.location = (-620, 80)

    sep = nt.nodes.new("ShaderNodeSeparateColor")
    sep.location = (0, 160)

    rgb2bw = nt.nodes.new("ShaderNodeRGBToBW")
    rgb2bw.location = (0, 40)

    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.location = (-220, -220)
    wave.wave_type = "BANDS"
    wave.bands_direction = "Y"
    wave.inputs["Scale"].default_value = 42.0
    wave.inputs["Distortion"].default_value = 0.35
    wave.name = "ScanWave"

    wave_map = nt.nodes.new("ShaderNodeMapping")
    wave_map.location = (-420, -220)
    wave_map.name = "ScanMapping"

    wave_coord = nt.nodes.new("ShaderNodeTexCoord")
    wave_coord.location = (-620, -220)

    scan_mult = nt.nodes.new("ShaderNodeMath")
    scan_mult.location = (0, -160)
    scan_mult.operation = "MULTIPLY"
    scan_mult.inputs[1].default_value = 0.55

    scan_add = nt.nodes.new("ShaderNodeMath")
    scan_add.location = (160, -80)
    scan_add.operation = "ADD"
    scan_add.inputs[1].default_value = 0.45

    lum_gt = nt.nodes.new("ShaderNodeMath")
    lum_gt.location = (160, 40)
    lum_gt.operation = "GREATER_THAN"
    lum_gt.inputs[1].default_value = 0.04

    pulse = nt.nodes.new("ShaderNodeValue")
    pulse.location = (160, 200)
    pulse.name = "HoloPulse"
    pulse.outputs[0].default_value = 1.0

    emit_mul = nt.nodes.new("ShaderNodeMath")
    emit_mul.location = (360, 220)
    emit_mul.operation = "MULTIPLY"
    emit_mul.inputs[0].default_value = 16.0

    nt.links.new(texcoord.outputs["UV"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    nt.links.new(tex.outputs["Color"], sep.inputs["Color"])
    nt.links.new(tex.outputs["Color"], rgb2bw.inputs["Color"])
    nt.links.new(tex.outputs["Color"], emission.inputs["Color"])
    nt.links.new(wave_coord.outputs["UV"], wave_map.inputs["Vector"])
    nt.links.new(wave_map.outputs["Vector"], wave.inputs["Vector"])
    nt.links.new(wave.outputs["Color"], scan_mult.inputs[0])
    nt.links.new(scan_mult.outputs["Value"], scan_add.inputs[0])
    nt.links.new(scan_add.outputs["Value"], emit_mul.inputs[0])
    nt.links.new(pulse.outputs["Value"], emit_mul.inputs[1])
    nt.links.new(emit_mul.outputs["Value"], emission.inputs["Strength"])
    nt.links.new(rgb2bw.outputs["Val"], lum_gt.inputs[0])
    nt.links.new(lum_gt.outputs["Value"], mix.inputs["Fac"])
    nt.links.new(transparent.outputs[0], mix.inputs[1])
    nt.links.new(emission.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])

    mat["holo_pulse_node"] = "HoloPulse"
    mat["scan_mapping_node"] = "ScanMapping"
    return mat


def animate_hologram(mat: bpy.types.Material) -> None:
    nt = mat.node_tree
    pulse = nt.nodes.get("HoloPulse")
    scan = nt.nodes.get("ScanMapping")
    if pulse is None or scan is None:
        return
    for frame in range(1, FRAMES + 1):
        t = (frame - 1) / FRAMES
        pulse.outputs[0].default_value = 0.72 + 0.38 * abs(math.sin(t * math.tau * 2.0))
        pulse.outputs[0].keyframe_insert("default_value", frame=frame)
        scan.inputs["Location"].default_value[1] = t * 1.6
        scan.inputs["Location"].keyframe_insert("default_value", frame=frame, index=1)


def build_pedestal(metal: bpy.types.Material, led: bpy.types.Material) -> list[str]:
    names: list[str] = []

    base_bm = bmesh.new()
    add_cylinder(base_bm, Vector((0.0, 0.0, BASE_H * 0.5)), BASE_RADIUS, BASE_H, 6)
    add_cylinder(base_bm, Vector((0.0, 0.0, BASE_H + inch(0.28))), BASE_RADIUS * 0.78, inch(0.55), 6)
    base = mesh_from_bmesh("pedestal_base", base_bm)
    assign_mat(base, metal)
    names.append(base.name)

    arm_h = inch(11.2)
    arm_z = BASE_H + arm_h * 0.5 + inch(0.2)
    spread = inch(6.4)
    lean = math.radians(7.0)

    for side, name in ((-1.0, "arm_L"), (1.0, "arm_R")):
        arm_bm = bmesh.new()
        add_cube(
            arm_bm,
            Vector((side * spread, inch(0.15), arm_z)),
            Vector((inch(1.15), inch(1.45), arm_h)),
            Euler((0.0, side * -lean, 0.0)),
        )
        arm = mesh_from_bmesh(name, arm_bm)
        assign_mat(arm, metal)
        names.append(arm.name)

        led_bm = bmesh.new()
        add_cube(
            led_bm,
            Vector((side * (spread - inch(0.62)), inch(0.05), arm_z + inch(0.4))),
            Vector((inch(0.14), inch(0.22), arm_h * 0.72)),
            Euler((0.0, side * -lean, 0.0)),
        )
        strip = mesh_from_bmesh(f"led_{name[-1]}", led_bm)
        assign_mat(strip, led)
        names.append(strip.name)

    return names


def build_cave_rim(metal: bpy.types.Material) -> str:
    bm = bmesh.new()
    chunks = 20
    for i in range(chunks):
        t = (i / chunks) * math.tau
        jitter = 0.16 * math.sin(i * 2.7) + 0.07 * math.cos(i * 5.1)
        r = DISC_RADIUS + inch(1.15) + inch(jitter * 3.2)
        loc = Vector(
            (
                math.cos(t) * r,
                inch(1.1) + inch(0.25 * math.sin(i)),
                DISC_Z + inch(0.12 * math.sin(i * 1.7)),
            )
        )
        size = Vector(
            (
                inch(1.0 + (i % 3) * 0.28),
                inch(0.85 + (i % 2) * 0.2),
                inch(1.25 + (i % 4) * 0.35),
            )
        )
        rot = Euler((math.radians(8 * math.sin(i)), math.radians(12 * math.cos(i)), t))
        add_cube(bm, loc, size, rot)
    obj = mesh_from_bmesh("cave_rim", bm)
    assign_mat(obj, metal)
    return obj.name


def build_slats(metal: bpy.types.Material) -> str:
    bm = bmesh.new()
    count = 5
    span = DISC_RADIUS * 1.85
    for i in range(count):
        y = -inch(1.35)
        z = DISC_Z + (i - (count - 1) / 2.0) * inch(2.35)
        add_cube(bm, Vector((0.0, y, z)), Vector((span, inch(0.14), inch(0.22))))
    obj = mesh_from_bmesh("crt_slats", bm)
    assign_mat(obj, metal)
    return obj.name


def build_hologram_plate(mat: bpy.types.Material) -> str:
    bm = bmesh.new()
    size = DISC_RADIUS
    verts = [
        bm.verts.new((-size, 0.0, -size)),
        bm.verts.new((-size, 0.0, size)),
        bm.verts.new((size, 0.0, size)),
        bm.verts.new((size, 0.0, -size)),
    ]
    face = bm.faces.new(verts)
    uv_layer = bm.loops.layers.uv.new()
    for loop in face.loops:
        x, _y, z = loop.vert.co
        loop[uv_layer].uv = ((x / size) * 0.5 + 0.5, (z / size) * 0.5 + 0.5)
    obj = mesh_from_bmesh("hologram_plate", bm)
    obj.location = Vector((0.0, inch(0.22), DISC_Z))
    assign_mat(obj, mat)
    return obj.name


def sample_logo_voxels(logo: bpy.types.Image) -> tuple[bmesh.types.BMesh, bmesh.types.BMesh]:
    width, height = logo.size
    pixels = list(logo.pixels)
    samples = 64
    step_x = max(1, width // samples)
    step_y = max(1, height // samples)
    cell = (DISC_RADIUS * 2.0) / samples
    cube = cell * 0.62
    white_bm = bmesh.new()
    green_bm = bmesh.new()
    half = DISC_RADIUS

    for sy in range(0, height, step_y):
        for sx in range(0, width, step_x):
            idx = (sy * width + sx) * 4
            r, g, b = pixels[idx], pixels[idx + 1], pixels[idx + 2]
            lum = 0.2126 * r + 0.7152 * g + 0.0722 * b
            if lum < 0.07:
                continue
            u = sx / (width - 1)
            v = sy / (height - 1)
            x = (u - 0.5) * 2.0 * half
            z = (v - 0.5) * 2.0 * half
            if (x * x + z * z) > (half * 1.02) ** 2:
                continue
            depth = inch(0.12) * (0.4 + lum)
            loc = Vector((x, depth, z))
            size = Vector((cube, cube * 0.7, cube))
            is_green = g > 0.38 and g > r + 0.18 and g > b + 0.18
            if is_green:
                add_cube(green_bm, loc, size)
            else:
                add_cube(white_bm, loc, size)
    return white_bm, green_bm


def build_voxels(logo: bpy.types.Image, white_mat: bpy.types.Material, green_mat: bpy.types.Material) -> tuple[str, str, str]:
    white_bm, green_bm = sample_logo_voxels(logo)
    white = mesh_from_bmesh("cavelux_voxels_white", white_bm)
    green = mesh_from_bmesh("cavelux_voxels_green", green_bm)
    assign_mat(white, white_mat)
    assign_mat(green, green_mat)

    spin = bpy.data.objects.new("holo_spin", None)
    bpy.context.collection.objects.link(spin)
    spin.empty_display_type = "PLAIN_AXES"
    spin.location = Vector((0.0, inch(0.55), DISC_Z))
    white.parent = spin
    green.parent = spin
    white.location = Vector((0.0, 0.0, 0.0))
    green.location = Vector((0.0, 0.0, 0.0))

    yaw = spin.driver_add("rotation_euler", 2)
    yaw.driver.type = "SCRIPTED"
    yaw.driver.expression = f"(2.0 * pi) * (frame - 1) / {FRAMES}"

    return white.name, green.name, spin.name


def setup_preview() -> None:
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = FRAMES
    scene.frame_current = 24
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.filepath = PREVIEW_PATH
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("lp_void")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.0, 0.0, 0.0, 1.0)
        bg.inputs["Strength"].default_value = 0.0

    cam_data = bpy.data.cameras.new("preview_cam")
    cam_data.lens = 50
    cam = bpy.data.objects.new("preview_cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = Vector((inch(12.0), inch(-38.0), inch(11.5)))
    track = cam.constraints.new(type="TRACK_TO")
    target = bpy.data.objects.new("look_target", None)
    bpy.context.collection.objects.link(target)
    target.location = Vector((0.0, 0.0, DISC_Z))
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    scene.camera = cam

    key = bpy.data.lights.new("key_fill", "AREA")
    key.energy = 40.0
    key.size = inch(18.0)
    key.color = (0.75, 0.82, 1.0)
    key_obj = bpy.data.objects.new("key_fill", key)
    bpy.context.collection.objects.link(key_obj)
    key_obj.location = Vector((inch(10.0), inch(-16.0), inch(20.0)))

    holo = bpy.data.lights.new("holo_point", "POINT")
    holo.energy = 90.0
    holo.color = (0.2, 1.0, 0.25)
    holo.shadow_soft_size = inch(3.0)
    holo_obj = bpy.data.objects.new("holo_point", holo)
    bpy.context.collection.objects.link(holo_obj)
    holo_obj.location = Vector((0.0, inch(-2.0), DISC_Z))

    rim = bpy.data.lights.new("cyan_rim", "POINT")
    rim.energy = 35.0
    rim.color = (0.0, 0.9, 1.0)
    rim_obj = bpy.data.objects.new("cyan_rim", rim)
    bpy.context.collection.objects.link(rim_obj)
    rim_obj.location = Vector((0.0, inch(4.0), inch(6.0)))

    floor_bm = bmesh.new()
    add_cube(floor_bm, Vector((0.0, 0.0, -inch(0.2))), Vector((inch(48.0), inch(48.0), inch(0.4))))
    floor = mesh_from_bmesh("preview_floor", floor_bm)
    floor_mat = bpy.data.materials.new("lp_floor")
    floor_mat.use_nodes = True
    nt = floor_mat.node_tree
    principled = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    set_socket(principled, "Base Color", (0.004, 0.004, 0.005, 1.0))
    set_socket(principled, "Roughness", 0.85)
    assign_mat(floor, floor_mat)

    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 24


def export_prop(names: list[str], filepath: str) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for name in names:
        obj = bpy.data.objects.get(name)
        if obj is not None:
            obj.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True,
        bake_anim=True,
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=True,
    )


def main() -> None:
    if not os.path.isfile(LOGO_PATH):
        raise FileNotFoundError(LOGO_PATH)

    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    wipe_scene()

    logo = bpy.data.images.load(LOGO_PATH)
    logo.pack()
    logo.colorspace_settings.name = "sRGB"

    metal = dark_metal()
    led = cyan_led()
    white_mat = voxel_white()
    green_mat = voxel_green()
    holo_mat = hologram_material(logo)
    animate_hologram(holo_mat)

    names = build_pedestal(metal, led)
    names.append(build_cave_rim(metal))
    names.append(build_slats(metal))
    names.append(build_hologram_plate(holo_mat))
    white_name, green_name, spin_name = build_voxels(logo, white_mat, green_mat)
    names.extend([white_name, green_name, spin_name])

    setup_preview()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_prop(names, FBX_PATH)
    export_prop([white_name, green_name], PRODUCT_FBX_PATH)
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print(f"WROTE {BLEND_PATH}")
    print(f"WROTE {FBX_PATH}")
    print(f"WROTE {PRODUCT_FBX_PATH}")
    print(f"WROTE {PREVIEW_PATH}")


if __name__ == "__main__":
    main()
