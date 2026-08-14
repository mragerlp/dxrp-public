# AK-47 Viewmodel Build (S&box)

---

## ⚡ STATUS — background rebuild done (2026-06-03), NO Blender required

The first-person viewmodel was rebuilt automatically using the **M4 rig as an
invisible driver** (the s&box first-person doc approach), so we don't need to add
a `camera` bone in Blender to ship a working FP view.

**What changed (DXRP project only — not yet synced to repo / published):**

- `equipment/vm_ak47/vm_ak47.prefab` was **replaced** with a clone of the working
  `vm_m4a1.prefab`. Old file backed up as `vm_ak47.prefab.preM4clone.bak`.
  - Component 1: `ViewModel` (camera bone read = M4's `camera`, animgraph = M4's).
  - Component 2: **M4 master** `v_m4a1.vmdl`, `CreateBoneObjects: true`,
    `UseAnimGraph: true`, `MaterialOverride = invisible.vmat` → drives everything
    but renders nothing.
  - Component 3: **arms** `v_first_person_arms_human.vmdl`, bonemerged to master.
  - Component 4: **textured AK** `v_ak47.vmdl`, bonemerged to master. The AK rig
    reuses the M4 bone names (`weapon_root`, `muzzle`, `stock`, `trigger`,
    `magazine`…) so it should ride the M4 skeleton.
- `equipment/vm_ak47/invisible.vmat` — new fully-transparent material (opacity 0 +
  tint alpha 0). Needed because `ViewModel.cs` forces the renderer Tint alpha back
  to 1 each frame, so the mesh must be hidden at the material level.
- `equipment/w_ak47/w_ak47.prefab` — `ViewModelPrefab` repointed from the M4
  placeholder back to `addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab`.

**Verify in editor (5 min), in this order:**

1. Open the DXRP project. Let it compile `invisible.vmat` + `vm_ak47.prefab`.
2. Open `vm_ak47.prefab`. You should see **arms + textured AK only** (no M4).
   - If the **M4 is still visible**: select the `v_m4a1.vmdl` renderer →
     `MaterialOverride` should be `invisible.vmat`. If it still shows, open
     `invisible.vmat` and confirm **Translucent** is on and opacity = 0.
3. `lp_give_ak` in play mode → first person should sit lower-right like the M4
   (NOT giant/filling screen). Arms should grip it.
   - If the **AK mesh is distorted** (bonemerge mismatch), switch component 4 from
     bonemerge to a static child: parent it under the `weapon_root` bone object and
     zero its local transform, then nudge to align. (Mesh won't animate on reload
     but won't distort.)
   - If the AK is **offset from the hands**, nudge the AK renderer's local
     position/rotation a little until it overlaps where the M4 sat.
4. Muzzle flash / ejection already use the M4 `muzzle` / `bolt_flap` bone objects —
   should be correct out of the box.

**Third-person hold (separate, still TODO — needs live editor tweak):**
`w_ak47.prefab` → `Model` child currently `Rotation: 0,0,1,0` (180° flip) and
`Muzzle` at `-459 X` (clearly wrong). Reference values from working M4 `w_m4a1.prefab`:
- Model child rotation: `0.0000000056,0.00000012,0.1164025,0.9932021` (~13° roll, no flip)
- Muzzle: `20.07571,0,7.212921`  •  EjectionPort: `1.774985,-0.4210945,7.672482`
(Held position already matches M4: `2.888188,1.646453,-6.576477`.)

**After editor verification passes:** sync DXRP → repo, remove the `lp_give_ak`
dev command, then publish a new AK revision and re-pin it on the LifePunch gamemode.

---

**New to S&box?** Use the full click-by-click guide first:

```text
lifepunchaddons/docs/AK47_SBOX_BEGINNER_GUIDE.md
```

Do **not** publish another DXRP revision until this checklist is done and first person looks correct in a local play test.

## Why

DXRP `ViewModel` reads a **`camera` bone** from the viewmodel every frame (`ViewModel.cs` → `GetBoneLocalTransform("camera")`). Official rifles use a dedicated viewmodel (for example `v_m4a1.vmdl`), not the world model.

`vm_ak47.prefab` currently points at `w_ak47.vmdl` with manual child offsets. That will stay broken no matter how many portal revisions you publish.

## Open project

1. Open S&box.
2. Open project: `lifepunchaddons/addons.sbproj`
3. Keep the Asset Browser scoped to `addons/lifepunch/ak47`.

## Reference (DXRP M4A1)

Full links, comparison tables, and shipment vs first-person notes:

```text
lifepunchaddons/docs/M4A1_REFERENCE.md
```

Study these official files side by side while you work:

| Piece | Path in `dxrp-public` |
|--------|------------------------|
| Viewmodel prefab | `game/Assets/gameplay/equipment/weapons/m4a1/vm_m4a1.prefab` |
| Viewmodel model | `models/weapons/sbox_assault_m4a1/v_m4a1.vmdl` |
| World prefab | `game/Assets/gameplay/equipment/weapons/m4a1/w_m4a1.prefab` |

M4A1 viewmodel prefab pattern:

- Root `vm_*` has tags `player,viewmodel`.
- `ViewModel` component on root; `ModelRenderer` and `Arms` on **root** renderers (not a scaled child).
- Weapon `SkinnedModelRenderer` uses **`v_*` model**, `CreateBoneObjects: true`, `UseAnimGraph: true`, overlay render.
- Second renderer: `models/first_person/v_first_person_arms_human.vmdl` with **bone merge** to the weapon renderer.
- Child hierarchy includes a bone object named **`camera`** (created from the model skeleton).
- `Muzzle` / ejection port sit on weapon bones, not huge world-space offsets.

## Target outputs (LifePunch)

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/v_ak47/v_ak47.vmdl
Assets/addons/lifepunch/ak47/equipment/vm_ak47/vm_ak47.prefab   (rebuilt in editor)
```

Mounted path after publish:

```text
addons/lifepunch/ak47/models/lifepunch/ak47/v_ak47/v_ak47.vmdl
```

## Step 1 — Source orientation (Blender recommended)

The world FBX may be correct for crates but wrong for first person.

1. Open `w_ak47/source/ak47.fbx` in Blender (or your DCC).
2. Orient the rifle the way DXRP first-person weapons sit (compare a screenshot of M4A1 first person).
3. Add a bone named exactly **`camera`** at the rear sight / eye line.
4. **Orient the `camera` bone (required):** DXRP does `camera.LocalRotation *= bone.Rotation` every frame. A default Blender bone (pointing up) causes the “giant stock filling the screen” bug.
   - In Blender **Edit Mode**, select the **`camera`** bone.
   - Rotate the bone (**R**) so the bone’s **length axis points down the barrel** (stock → muzzle), same idea as the official M4A1 `camera` bone.
   - The bone **head** sits near the player eye / rear sight; the **tail** points toward the muzzle.
   - Do **not** fix facing with `vm_ak47` root rotation — M4A1 uses **`Rotation 0,0,0,1`** on the prefab root.
5. Optional but ideal: add a simple weapon skeleton (trigger, magazine, bolt) if you want reload animation later.
6. Export FBX to:

```text
Assets/addons/lifepunch/ak47/models/lifepunch/ak47/v_ak47/source/ak47_vm.fbx
```

Do not overwrite `w_ak47/source/ak47.fbx` unless you also re-tune the world model.

## Step 2 — Create `v_ak47.vmdl` in S&box

1. In Asset Browser, go to `models/lifepunch/ak47/v_ak47/`.
2. Create a new **Model** (`v_ak47.vmdl`).
3. Import mesh from `source/ak47_vm.fbx` (or duplicate `w_ak47` setup and swap mesh).
4. Use the same material: `../w_ak47/materials/ak47_body.vmat`.
5. Set import scale so the viewmodel is **weapon-sized in the model preview**, not crate-sized.
   - World model uses `import_scale = 0.039` in `w_ak47.vmdl`; viewmodel scale will differ — tune in the model editor.
6. Compile and confirm the model opens with **no errors**.
7. In the skeleton/bone list, confirm a bone named **`camera`** exists.

## Step 3 — Rebuild `vm_ak47.prefab` (match M4A1)

1. Open `equipment/vm_ak47/vm_ak47.prefab`.
2. **Remove** the temporary child `Model` object with manual position/rotation/scale (the Rev 31–33 workaround).
3. On the **root** `vm_ak47` object:
   - Add `SkinnedModelRenderer` → Model = `addons/lifepunch/ak47/models/lifepunch/ak47/v_ak47/v_ak47.vmdl`
   - Enable **Create Bone Objects**
   - Render: Overlay layer, Render Type Off, Use Anim Graph on (when the model supports it)
4. Add second `SkinnedModelRenderer` on root for arms:
   - Model = `models/first_person/v_first_person_arms_human.vmdl`
   - Bone Merge Target = weapon renderer
   - Same overlay render settings as M4A1
5. `ViewModel` component:
   - `ModelRenderer` → weapon renderer on root
   - `Arms` → arms renderer on root
   - `Muzzle` / `EjectionPort` → drag from bone children (after Create Bone Objects), not world-scale numbers from `w_ak47`
6. Save prefab.

## Step 4 — Local play test (before publish)

1. Run the addons project or join **development** with the **local** addon mount if your setup supports it.
2. Equip AK-47 first person:
   - Gun should sit in the lower-right like M4A1, not fill the screen.
   - Arms visible (if arms renderer wired).
3. Fire / reload once — check muzzle flash position.
4. Drop weapon / shipment — world model should still use `w_ak47` via `w_ak47.prefab` (unchanged).

## Step 5 — Repo + DXRP publish (only after Step 4 passes)

```powershell
cd lifepunchaddons
.\scripts\validate-layout.ps1
.\scripts\prepare-publish.ps1 -Addon ak47
```

Portal:

1. Publish new revision (Code + Assets from `.dxrp-publish/upload`).
2. Content JSON unchanged except keep `iconPath`: `/addons/lifepunch/ak47/ui/ak47_killfeed.png`
3. Pin revision on LifePunch gamemode → Save → sync **development** server → restart.

## Done criteria

- [ ] `v_ak47.vmdl` compiles in S&box
- [ ] Skeleton contains `camera` bone
- [ ] `vm_ak47.prefab` uses `v_ak47.vmdl` on root (M4A1 layout)
- [ ] First person matches M4A1 presentation closely enough to playtest
- [ ] Muzzle / ejection align when firing
- [ ] World/shipment still uses `w_ak47` and looks correct
