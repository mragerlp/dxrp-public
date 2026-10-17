"""Export voxel-only product FBX from the intake blend (no voxel rebuild)."""

from __future__ import annotations

import os

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
PRODUCT_FBX_PATH = os.path.normpath(os.path.join(HERE, "..", "..", "models", "cavelux_morpheus.fbx"))
KEEP = ("cavelux_voxels_green", "cavelux_voxels_white")


def main() -> None:
	kept = [obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name.split(".")[0] in KEEP]
	if len(kept) != 2:
		raise RuntimeError(f"expected 2 voxel meshes in blend, got {[o.name for o in kept]}")

	bpy.ops.object.select_all(action="DESELECT")
	for obj in kept:
		obj.select_set(True)
		bpy.context.view_layer.objects.active = obj

	bpy.ops.export_scene.fbx(
		filepath=PRODUCT_FBX_PATH,
		use_selection=True,
		apply_scale_options="FBX_SCALE_ALL",
		bake_space_transform=True,
		bake_anim=True,
		add_leaf_bones=False,
		path_mode="COPY",
		embed_textures=True,
	)
	print(f"WROTE {PRODUCT_FBX_PATH}")
	print(f"KEPT {[o.name for o in kept]}")


if __name__ == "__main__":
	main()
