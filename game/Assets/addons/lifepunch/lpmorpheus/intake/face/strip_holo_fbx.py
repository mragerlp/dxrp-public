"""Strip Morpheus product FBX to the floating Cavelux voxel logo.

Keeps cavelux_voxels_green / cavelux_voxels_white only. Applies parent
transforms so the empty spin node can go. Does not rebuild voxels.
"""

from __future__ import annotations

import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_SRC = os.path.normpath(
	os.path.join(HERE, "..", "..", "models", "cavelux_morpheus.fbx")
)
KEEP = ("cavelux_voxels_green", "cavelux_voxels_white")


def wipe() -> None:
	bpy.ops.object.select_all(action="SELECT")
	bpy.ops.object.delete(use_global=False)
	for block in (bpy.data.meshes, bpy.data.materials, bpy.data.armatures, bpy.data.objects):
		for item in list(block):
			block.remove(item)


def keep_name(name: str) -> bool:
	base = name.split(".")[0]
	return base in KEEP


def main() -> None:
	src = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else DEFAULT_SRC
	dst = src
	if not os.path.isfile(src):
		raise FileNotFoundError(src)

	wipe()
	bpy.ops.import_scene.fbx(filepath=src)

	kept: list[bpy.types.Object] = []
	for obj in list(bpy.data.objects):
		if obj.type == "MESH" and keep_name(obj.name):
			kept.append(obj)
		else:
			bpy.data.objects.remove(obj, do_unlink=True)

	if len(kept) != 2:
		raise RuntimeError(f"expected 2 voxel meshes, got {[o.name for o in kept]}")

	for obj in kept:
		if obj.parent is not None:
			world = obj.matrix_world.copy()
			obj.parent = None
			obj.matrix_world = world
		obj.name = obj.name.split(".")[0]

	bpy.ops.object.select_all(action="DESELECT")
	for obj in kept:
		obj.select_set(True)
		bpy.context.view_layer.objects.active = obj

	# Match build_cavelux_hologram.export_prop — FBX_SCALE_NONE blew the
	# meters→inches vmdl pin (logo went ~100x and laid flat).
	bpy.ops.export_scene.fbx(
		filepath=dst,
		use_selection=True,
		apply_scale_options="FBX_SCALE_ALL",
		bake_space_transform=True,
		bake_anim=True,
		add_leaf_bones=False,
		path_mode="COPY",
		embed_textures=True,
	)
	print(f"WROTE {dst}")
	print(f"KEPT {[o.name for o in kept]}")


if __name__ == "__main__":
	main()
