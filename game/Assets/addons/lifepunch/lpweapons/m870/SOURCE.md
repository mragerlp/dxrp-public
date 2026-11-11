# M870 source custody

- Product/source title: `FREE SHOTGUN` on Fab; upstream Sketchfab title
  `Tactical Shotgun`.
- Creator/publisher: `Eemanuel3Dl` on Fab; `Otho (@eman8el.3di)` on
  Sketchfab.
- Fab listing: <https://www.fab.com/listings/108366de-4286-49bb-b71f-ec1b8449581c>
- Upstream Sketchfab listing:
  <https://sketchfab.com/3d-models/tactical-shotgun-300c082089ea42f69a6527e0aee27d00>
- License: [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/).
- Published: 2023-05-21; Fab listing last updated 2025-09-17.
- Fab listing flags: `Allows usage with AI: Yes`; `Generated with AI: No`.
- Custody status: `PROVENANCE-CLEARED` for the model and supplied textures;
  attribution remains required in any distributed build.
- Download archive: `tactical-shotgun.zip` (held locally by the project owner)
  - Bytes: `14381028`
  - SHA-256: `6D1BFF93EEF0DFBA4F844E2C8727BEC4D4F5648B9661A896CE00EF1778D6A0AC`
  - The duplicate `tactical-shotgun (1).zip` is byte-identical.

## Listing match evidence

- The Fab listing exposes the included file as `tactical-shotgun.zip`, size
  `13.71 MB`, matching the local archive name and 13.71 MiB byte size.
- The local archive's Windows `Zone.Identifier` records
  `ReferrerUrl=https://www.fab.com/` and the Epic/Fab content-delivery host.
- The upstream listing reports approximately 5.1k vertices and 9.8k
  triangles. The local FBX has 5,285 vertices, and the deterministic cleaned
  export has 9,764 triangulated faces.
- The Fab listing links the same ArtStation work (`aoL6rz`) as the upstream
  Sketchfab page, and the listing preview matches the local compiled model's
  wood furniture, sling, receiver, barrel, and pump silhouette.

See `ATTRIBUTION.md` for the distribution notice and modification summary.

All seven active native inputs below are byte-identical to their corresponding
archive members. This proves local custody only; it does not establish the
missing listing, publisher, entitlement, or redistribution terms.

## Native source

- `T-SHOTGUN.fbx`
  - Bytes: `322924`
  - SHA-256: `4387543BA93CBBE3AB495A8678CB88651FCFB05C9726A39E6DC34AFA3AFA1B7C`
  - One unrigged mesh, 5,285 vertices, 5,182 polygons, one material, and
    42 disconnected mesh islands.
- `textures/`
  - Original JPG files: `6`
  - Bytes: `14057172`
  - Ordinal manifest SHA-256: `8659F63D52C353911CFCF824BD0D1B86422ACBEBB2903C4AF0B749274B97550C`
  - Manifest rows: `filename<TAB>bytes<TAB>SHA256_UPPER<LF>`
  - Inputs: diffuse, normal, roughness, metalness, specular, and emission.

| Archive member | Bytes | SHA-256 |
|---|---:|---|
| `source/T-SHOTGUN.fbx` | 322924 | `4387543BA93CBBE3AB495A8678CB88651FCFB05C9726A39E6DC34AFA3AFA1B7C` |
| `textures/ROUGHNESS_SHOTGUN.jpg` | 3886379 | `76040A0AABE3C4B2383FC83B523CB87CC0537F6B004AD0F3073936CC4ABFDA62` |
| `textures/EMIT_SHOTGUN.jpg` | 449485 | `B30307D23DC1C2E643D196D9C9FBC00EB0000947C0B6D8F83EA565A6E31FFAB1` |
| `textures/METALNESS_SHOTGUN.jpg` | 531006 | `B0988D4DA67FBDAD5F0DB5FB6F52C9E6C0D21901940FEF0E0EDA6FEE60F40CE9` |
| `textures/NORMAL_SHOTGUN.jpg` | 3787223 | `B7428B13113B09B6B0E92FA30F17B9CD75AA4EBF92A90073DCF422165E5E98EE` |
| `textures/DIFFUSE_SHOTGUN.jpg` | 1827127 | `63B48076ACFBD5A8C99FBFFF6CDFFF82D574F6622743C5B0B0C60645F3579998` |
| `textures/SPECULAR_SHOTGUN.jpg` | 3575952 | `5E4F3E80E68782BCA19F8E8760B02A9D2D9915F3E2A941B868F573623F3A023F` |

## Clean local candidate

- `source/generated/m870_clean.fbx`
  - Bytes: `840284`
  - SHA-256: `7740C3C423EAF3F54081866833651EDC0B1FFBD9254A86937A1B4A40B3C661FD`
  - Binary FBX with explicit tangent/binormal layers.
  - `m870_body`: 5,004 vertices and 9,232 triangulated polygons.
  - `m870_pump`: 281 vertices and 532 triangulated polygons.
  - Both objects retain the common assembly origin.
- `materials/m870.vmat` maps the supplied diffuse, normal, roughness, and
  metalness textures. The separate specular map remains unused while the
  material follows the project's metalness workflow. The supplied emission map
  is retained as source evidence but self-illumination is disabled because a
  sampled audit found it effectively black with only sparse bright pixels.
- `models/m870_world.vmdl`, `models/m870_body.vmdl`, and
  `models/m870_pump.vmdl` read only the clean generated FBX, convert centimetres
  to Source inches with scale `0.3937008`, and rotate the native long axis to
  project-standard `+X`.
- Compiled engine bounds are approximately `39.713 × 2.214 × 13.681` Source
  inches with one real material and real render/physics geometry.
- `equipment/vm_m870/vm_m870.prefab` keeps the complete Spaghelli driver and
  first-person arms, hides only the donor gun, and renders the clean body and
  static pump at their shared origin.
- `equipment/w_m870/w_m870.prefab` uses the combined clean world model,
  `HoldType=Shotgun`, six-shell single reload, and semi-auto trigger semantics.
- The current geometry-only seeds are not final hand-fit values: first person
  `7.610142,0.556946,4.144161` at scale `1.018760`; third person
  `2.472998,0.556934,8.671473` at scale `1.018727`.
- The non-final world muzzle seed is `20.201894,0.554226,10.266229` in the
  donor Model object's local space. It is derived from the compiled model's
  +X barrel endpoint and the recorded third-person model transform; it still
  requires a firing-effect visual check before acceptance.

The clean model/material and prefabs are cleared for the private development
Portal with the attribution notice retained. Mechanical and visual acceptance
remain incomplete. Pump (SHOTGUN-1, 2026-11-11): the donor viewmodel has no pump bone, attachment or
animation, so the donor does not drive the pump. In first person the separate
`m870_pump` mesh is moved by code (`LpPumpSlideVisual`, fed by
`LpPumpShotgunCycle`); the carrier, bolt, release and hand-IK nodes are not used as
a pump driver. The first-person left hand does not follow the pump (no authored
animation exists). The third-person world model is one combined mesh and its pump
does not move. The world muzzle has a geometry-derived seed; its effect placement,
ejection placement, hand fit, ADS, third-person IK, runtime shell reload, pump
stroke length and direction, and all first-/third-person visuals are still
unverified. See `README.md` for the component list and the proof still owed.
