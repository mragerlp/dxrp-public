# lpbanker - DRAFTS for DRIVE-BANK-ASSETS-2 (prepared under DRIVE-MORNING-1 M6; nothing here is landed)

These vmdl / vmat / MANIFEST files are drafts in a prep worktree. They reference FBX and textures that are NOT copied yet.
Landing waits for Phase A and "go DRIVE-BANK-ASSETS-2" (the 2832 freeze).

Values (measured by WF-C wf_c4f4dc9c-87b; FBX file units = s&box units at import_scale 1.0, calibrated 4/4 on the bitcoin models):
- money_stacked_01..06: import_scale 0.4082 (modal note 15.04 cm -> 6.14 in). Ruling R-M6-1 (or plain cm->in 0.3937).
- money_bag_*: import_scale 0.4082 (option B, the stack family; a 45-47 in duffel). Option A (20-26 in bag) = 0.2093-0.2160 and shrinks the bundles inside to about 3.3 in. Ruling R-M6-2.
- safe_vault / safe_vault_door: import_scale 0.8567 (a 30 in floor safe). True size 0.3937 = 13.8 in. The asset is a small safe with its door, NOT a walk-in vault door. Ruling R-M6-3.
- import_rotation is [0,0,0] in every draft: the Unreal (Z-up) axis mapping is INFERRED and facing is unknown for all meshes. Measure Model.Bounds at landing and set rotation from a capture.
- import_filter keeps out the UCX_ collision hulls and LOD1-LOD4; the node names in exception_list must be re-checked against the importer at landing.
- vmat textures: the money ORM maps are to be split into _AO / _Roughness / _Metallic (assumed R/G/B order, UNVERIFIED); the two over-bar normals land derived (8-bit or 2K); Unreal normals may need a green flip (UNVERIFIED, prove visually).
