# DXRP Weapon Arsenal — FABLE Status Packet

**As of:** 2026-08-25
**Workspace:** `D:\Cavelux\dxrp-workbench`
**Project:** DXRP (`dxura / rp`)
**Portal target:** private development gamemode `lifepunch.dxrpdev` only
**Current safety state:** Play is live, so active game assets are frozen. No scene save, Portal submit, commit, or push is authorized by this packet.

The fresh 20:03 read-only native sensor identifies engine `26.08.19`, project
`DXRP` / `dxura.rp` at this exact workbench, `IsPlaying=true`,
`SceneHasUnsavedChanges=false`, `IsCompiling=false`,
`LastCompileSucceeded=true`, and `LastCompileErrors=0`, with 346 registered
tools. Direct native/Fobiat status is healthy. The guarded Cavelux-native,
Chromr Retargeter, and Chromr Animation Editor reads currently refuse because
their identity rule still requires the absent token `lifepunch`; no raw-port or
identity-guard bypass was used. Claude Bridge health was not freshly resensed in
this checkpoint. Current editor code-compile health is therefore **PASS** only
at the direct native sensor ceiling. Every new
candidate remains outside the game tree, so candidate asset compilation,
runtime behavior, and visual acceptance remain **UNVERIFIED** while Play is
live.

A subsequent read-only bridge re-sense reports Claude Bridge `connected=true`,
`roundTripOk=true`, aligned addon/server version `2.2.0`, and 278 handlers on the
same `DXRP` / `dxura.rp` workbench. Play remains active. The guarded
Cavelux-native status call still refuses on its stale `lifepunch` identity
requirement, so that guard was not bypassed and no newer guarded-native compile
claim is made.

The same continuation's native registry pass reports all 14 active custom
world/view prefabs compiled, current, and not failed. Their 14 `asset_files`
records report zero unresolved references. A failed-asset search across the
seven implemented custom families returned zero results. This is asset-registry
health only; it does not accept a rendered fit.

## Executive state

The donor-based weapon architecture is working. The remaining failures are no longer “missing model” problems; they are fit and ownership problems:

1. Shared-origin custom parts must be placed through a wrapper local computed as `inverse(animated donor parent) × accepted assembly transform`.
2. First-person hand seating and ADS must be accepted before final sight centering.
3. Third-person weapon placement and the left-hand IK grip are separate from first-person fit.
4. Bounds-derived transforms are useful candidates, not visual acceptance.
5. Audio and Portal publication remain separate provenance and delivery gates.

## Current arsenal

| Weapon | Donor / structure | Current result | Private Portal | Next controlled act |
|---|---|---|---|---|
| AK-47 | M4 first-person driver; rifle world hold; left-hand IK component | Best current baseline. First-person scale/placement was saved from the principal's gizmo. A one-property TEMP candidate registers its existing `weapon_root` as `AdditionalRendererRoot`; third-person support hand and final ADS still require acceptance. | Content and shipment present | Regression capture: FP idle, ADS, TP idle, reload, audio |
| AKS-74U | MP5 driver; purchased native body and separate magazine; entitlement pin still pending | The integrated TEMP candidate preserves the existing magazine mapping and `AdditionalRendererRoot`, swaps the body renderer to body-minus-bolt geometry, and adds exactly one bolt renderer under the MP5 `bolt`. Magazine, selector, trigger, and stock are unchanged. The final candidate is 75,309 bytes at SHA-256 `786D6EE4…F8A0F`; its four required support assets are create-only and still absent from the product destinations. | Content and shipment present | After explicit Stop Play authorization, review the prefab and four support assets before promotion; then compile ModelDocs and run FP/TP/ADS/reload/bolt-motion acceptance. |
| AR-15 | Full M4 driver plus custom body and moving parts | The active prefab still places moving renderers directly on donor nodes and omits `AdditionalRendererRoot`. A fail-closed TEMP candidate gives all six moving renderers distinct wrapper locals and assigns the AR-local `weapon_root`, statically covering all seven custom renderers. This preserves the current body baseline but is not a visually accepted fit. | Content and shipment present | After explicit Stop Play authorization, review the TEMP diff before promotion; then prove FP/ADS/TP, visibility, magazine/bolt motion, and the VR-only charging-handle boundary |
| Desert Eagle | Full USP driver plus custom body, slide, and magazine | The current product custom-renderer split exposes exactly body, slide, and magazine geometry. The landed minimal TEMP candidate adds only one magazine bind-wrapper GUID and `AdditionalRendererRoot`; every existing body/slide/magazine local, GUID, reference, and component owner is preserved. The final is 74,373 bytes at SHA-256 `27CDBFD8…FFB6C7`; runtime/visual behavior remains unaccepted. | Content and shipment present | After explicit Stop Play authorization, review the narrow TEMP diff before promotion; then prove equip, slide, magazine, ADS, and TP behavior |
| SR-25 | M4 driver plus native body, scope, suppressor, and moving parts | The active prefab already has `AdditionalRendererRoot`; that visibility ownership is not a candidate-added feature. Its remaining structural defect is direct placement of shared-origin moving renderers on different animated parents. A fail-closed TEMP candidate preserves the existing renderer root while giving all seven moving renderers distinct compiled-bind wrapper locals and preserving scope, mount, suppressor, and the semi-only contract. A guarded post-process also reparents the existing ejection marker under `bolt_flap` with `W = inverse(P) * B`, preserving its GUID, ViewModel reference, and current baseline placement. Scoped ADS and rendered motion remain unaccepted. | Content and shipment present | After explicit Stop Play authorization, review the TEMP diff before promotion; then prove rifle hold/IK, scoped ADS, reload, moving parts, and animated ejection behavior |
| M1911 | USP driver plus body, slide, and magazine | The current product custom-renderer split exposes exactly body, slide, and magazine geometry. Active patch lacks a materialized root `ViewModel`, reproducing the runtime equip failure. The corrected full-document TEMP candidate inverse-composes those three parts against the exact compiled USP bind, reconstructs one assembly, and registers the custom-renderer root. The final candidate is 73,340 bytes at SHA-256 `D3316F70…BFC46`; its retained body seed remains explicitly unaccepted. The local source package has zero genuine texture images. | Content and shipment present | After explicit Stop Play authorization, review the TEMP diff before promotion and compile; separately redownload the authenticated PBR package and reconcile custody before product-quality material acceptance |
| M870 | Spaghelli shotgun driver plus clean native body and separate pump | Clean model closure is structurally valid, the view prefab has one root `ViewModel`, and its local paths already match the same fail-closed resolver pattern as the working custom weapons. A one-property TEMP candidate registers the existing `weapon_root` for visibility. The missing private package/content/equipment row is the equip blocker. The world prefab has no measured left grip or IK consumer. Pump is intentionally static: the deterministic donor audit finds no pump, fore-end, forend, or action node, and the donor's proven bolt/carrier reload channels are not semantic substitutes for a pump driver. | No private addon, content, or market row available | Preserve attribution; expose the private package and exact world/view content row; then measure the world left grip and use licensed replacement audio; keep pump motion/audio unwired until a real timing contract exists |
| AS VAL | Source not yet provided | No candidate can be built honestly yet. | Absent | Intake and provenance after source arrives |

## Audio map

| Weapon | Requested family | State |
|---|---|---|
| AK-47 | AK-47 | Existing local mapping retained |
| AKS-74U | AUG | Existing candidate mapping retained |
| AR-15 | suppressed M4A1 | Existing local mapping retained |
| Desert Eagle | Desert Eagle | Existing local mapping retained |
| M870 | Nova shot + pump | Exact TEMP mapping validated; pump deliberately unwired; product bytes barred |
| SR-25 | suppressed USP | Three-shot randomized TEMP event validated; product bytes barred |
| M1911 | Five-SeveN | One-shot TEMP event validated; product bytes barred |

The six selected M870/SR-25/M1911 WAV files decode successfully and match the
pinned local archive. The archive contains no embedded license, copyright, or
notice entry. BOARD ordinal 1980 classifies Sourcesounds as extracted Valve
audio for reference only and bars those bytes from the LIFEPUNCH repository.
The mappings are therefore technical references only until licensed
replacement files are supplied. A fresh verify-only pass preserves the archive
at SHA-256
`E8452275A5007670D367B4E870A8FC3E057B41560B3E7C93C5886BD96DE9BEB7`
with 6/6 PCM files valid and zero writes. An earlier 13-file TEMP stage remains at
`C:\Users\jared\AppData\Local\Temp\dxrp-csgo-audio-final-bf4ffa3f1fc4450597f56a9603119002`;
its 8,922-byte manifest is SHA-256
`7B8B18C9CD7410A294F81F71CB5CE660BCAE90F48C9E3F0BF8E7D147E8D2476A`.
The three one-field world-prefab wiring candidates are at
`C:\Users\jared\AppData\Local\Temp\dxrp-audio-wiring-final-49506c7708884798bab665e03ce0027b`;
their 2,492-byte manifest is SHA-256
`F842AA7FA04FDA65F139712E2F29C8904406904FF0DA6A789DB45F23AA66F19C`.
A fresh independently regenerated 13-file stage is at
`C:\\Users\\jared\\AppData\\Local\\Temp\\dxrp_weapon_audio_candidates_20260825_200256`;
its 8,922-byte manifest is SHA-256
`7B8B18C9CD7410A294F81F71CB5CE660BCAE90F48C9E3F0BF8E7D147E8D2476A`.
Fresh one-field wiring candidates are at
`C:\\Users\\jared\\AppData\\Local\\Temp\\dxrp_weapon_audio_wiring_20260825_200316`;
their 2,444-byte manifest is SHA-256
`8AB4344A9268D5767B3934E963F78F6AC8DFEEF589B345A671A0FD5D927072F9`.
All acceptance flags remain false and no game file was written.

## Fresh static evidence

- Tracked-tree `git diff --check`: **PASS**. The untracked pipeline and this
  packet are covered separately by the explicit whitespace/EOF scan named in
  the final evidence return.
- Prefab hierarchy contract: **PASS**, 7 prefabs and 28/28 mappings preserved.
- Asset closure: **PASS**, 14 prefabs and 670 unique GUIDs; no missing, dangling, colliding, or active Auto Rigger references.
- Existing audio closure: **PASS**, 22 events and 34/34 WAV files decoded.
- Dev roster contract: **EXPECTED FAIL**, exactly 1 defect. The active M1911
  patch does not materialize a direct root `Dxura.RP.Game.ViewModel`, matching
  the runtime equip failure. The other six command/path/preflight contracts
  remain clean.
- Ejection and IK source contracts: **PASS**.
- SR-25 pinned candidate manifest: **PASS**.
- Pipeline Python compilation: **PASS**, 39 files.
- Full pipeline unit suite: **PASS**, 161 run, 158 passed, 3 skipped. One AKS
  bolt-split test class is deliberately skipped unless its byte-pinned strict-
  TEMP input manifest is supplied; the real integrated bundle below exercises
  that path. The other two skips are Windows symlink/reparse negative controls
  unavailable without `WinError 1314` privilege. All runnable checks passed.
- Fresh real promotion bundle: **PASS / PROMOTION HOLD**. The bundle at
  `C:\Users\jared\AppData\Local\Temp\dxrp_weapon_promotion_bundle_ckpckl5o`
  contains exactly eight promotable prefab candidates plus four create-only AKS
  support assets. Its 134,546-byte report is SHA-256
  `3A8F4C684552B3ECED4D538B0905D7D72E639109FCE803C3622475CB2C813C52`.
  Mode is `TEMP_ONLY_REVIEW_BUNDLE_NO_PRODUCT_WRITE_MODE`; product, game, docs,
  editor, Portal, and Git writes are false.
- Real candidate validator against that exact bundle: **PASS**, 8/8 candidate
  pins, 8/8 destination-preimage pins, and 4/4 support-asset pins. It proves
  exact custody, closed prefab GUID/reference/visibility ancestry, moving-part
  ownership, and AKS create-only topology closure. It does not prove ModelDoc or
  game compilation, runtime animation, visual fit, ADS, reload, IK, audio,
  roster state, or Portal delivery. The focused promotion-bundle suite passes
  27/27; the focused validator passes 16/16 runnable checks with one expected
  symlink-privilege skip.
- Independent action-time promotion preflight: **PASS / HOLD**. The strict-TEMP
  bundle at
  `C:\Users\jared\AppData\Local\Temp\dxrp_promotion_preflight_25c31cc6616548a7bde88fe05be43399`
  contains an eight-prefab replacement manifest plus four create-only AKS
  support assets. Its 135,526-byte report is SHA-256
  `A8A844E286BC6672E4CBEE40C2DEBC46C5E2312D4320FFF499491BBB0D22DC2D`.
  A final race check proves all eight current destination preimages still match
  the pinned bundle and all four create-only destinations remain absent. AK-47
  and AR-15 are tracked-and-modified replacement targets; the other six prefab
  targets are untracked replacements. Any preimage mismatch or newly arrived
  create-only target must abort the attended promotion.
- M4 current-body renderer-root/wrapper suite: **PASS**, 9/9. M1911
  common-origin plus create-only race suite: **PASS**, 10/10. M1911 materializer:
  **PASS**, 7/7.
  AKS-74U common-origin: **PASS**, 7/7. Desert Eagle minimal correction:
  **PASS**, 8/8. World left-hand IK: **PASS**, 9/9. Audio wiring/staging: **PASS**,
  17/17. Shared TEMP/legacy guards: **PASS**, 7 passed and 1 skipped.
- Live donor-model inventory through the aligned s&box bridge (engine 26.08.19,
  bridge 2.2.0): **PASS**. M4 exposes 7 meshes, 80 bones, and 109 animations,
  including deploy, holster, three reload variants, fire deltas, and ironsights;
  MP5 exposes 4 meshes, 80 bones, and 120 animations, including normal/empty
  reload and ironsights; USP exposes 8 meshes, 77 bones, and 124 animations,
  with stock `slide`, `slide_catch`, and `magazine` nodes plus the stock reload
  contract; Spaghelli exposes 3 meshes, 76 bones, and 102 animations, including
  first-shell/entry/shell/exit reload phases and its stock `bolt`. This proves
  donor sequence/node availability, not that replacement geometry visibly
  follows those nodes at runtime.
- Deterministic compiled-donor animation auditor: **PASS**. The sealed,
  TEMP-only auditor is 27,762 bytes at SHA-256
  `29FC5F387BF89F459B5CA497D99D580CDFD41201685F0B0D19745DD45AFA1CCC`;
  its 12-test suite is 10,465 bytes at SHA-256
  `26A59DC3567DE76D51B55B974F70EC47FBB724133D7CD423A5AC4C8A5A207C96`.
  A fresh run emitted 37,756 deterministic report bytes at SHA-256
  `ACD6A33C62F9E6E424F365D5B899ADEAD47ED1F2F8693BE315D518B606C526F3`
  and removed its strict-TEMP export before reporting. It proves these selected
  compiled-donor channels: M4 magazine translation in `Reload_Throw`,
  `Reload_Pull`, and `Reload_Empty`, bolt translation in `Reload_Empty` and
  `Trigger_Fire_delta`, and trigger rotation in `Trigger_Fire_delta`; MP5
  magazine translation in `Reload`/`Reload_Empty` and bolt translation in all
  nine `Fire_01_delta` through `Fire_09_delta` clips; USP magazine translation
  in all four one-/two-hand reload variants and slide translation in both empty
  reloads, four one-/two-hand fire clips, and `Fire_GoesEmpty`; Spaghelli bolt
  translation in `Reload_FirstShell` and carrier rotation in `Reload_Shell`.
  Spaghelli exposes no dedicated pump/fore-end/forend/action node among the 80
  scanned nodes. The proof ceiling is compiled glTF channel data only: it does
  not prove custom-part wiring, runtime animation, visual fit, reload timing,
  IK, audio, editor health, or Portal delivery.
- ViewModel visibility-root suite: **PASS**, 10/10. The exact AK-47 and M870
  active preimages and the generated AKS-74U, Desert Eagle, and M1911 inputs
  each produce a deterministic create-only TEMP candidate with exactly one
  semantic property added and zero product writes.
- Approved audio archive selection: **PASS**, 6/6 selected WAVs verified; product writes: **0**.
- Read-only editor thumbnail audit: **PASS at the asset-preview ceiling**. The
  active AK-47, AKS-74U, AR-15, Desert Eagle, and SR-25 view prefabs render
  their custom weapon geometry. The active M1911 preview is blank, and the
  active M870 preview renders donor arms without the shotgun. This reproduces
  the two active view-prefab defects without changing Play, the pawn, or the
  scene. All seven active world-prefab thumbnails render their custom weapon
  geometry. These are asset previews only, not first-person, third-person hold,
  runtime, or animation proof.
- M1911 common-origin full-document TEMP intermediate: **PASS**, one direct root
  `ViewModel`, the complete 8-key prefab envelope, both external package
  references, 73,205 bytes, SHA-256
  `099EF7340861DB3305E045238BADD0918BC59A518C6E67FC7CC8CCC64732C451`.
  The 1,986-byte compiled-bind manifest is
  `2271B467C02423158352714FD1E53D0F489E2CF59D17D2F1ED6D6869754E6FAC`.
  Body, slide, and magazine reconstruct one common assembly with maximum
  round-trip delta `8.8817841970012523e-16`; active-game writes: **0**. The
  create-only writer also rejects and preserves an externally arrived final
  destination instead of replacing it. The final visibility-root stage is
  73,340 bytes at SHA-256
  `D3316F70750F9BBF355D73EF3885F8C712D59F472A415E169A754FC3841BFC46`.
  An independent TEMP mirror replacing only the active M1911 view prefab with
  this final makes the identical dev-roster contract pass 7/7. That proves the
  exact roster/root defect is closed statically. The separate animation
  hierarchy check still rejects three old local-transform expectations, so the
  final is not visually or animation-accepted and has not been promoted.
- AKS-74U bolt-split pipeline: **LANDED IN TOOLS / TEMP CANDIDATE PASS**. The
  35,401-byte builder is SHA-256
  `5468DABC89C22A48921EA6DA65AFE1525858BA90CF4E5653DE18129721CA22E3`;
  the 25,602-byte Blender worker is
  `F3130E5459233DC7DC651E60C9A047BC89F03120BE2252A05D25AB66B4E9A8FE`;
  and the 10,908-byte test is
  `AAE230AB06F84B4D22B6B684D7CB75833B76900566BD43DB1072F5525CDCBDE4`.
  The final integrated first-person prefab is 75,309 bytes at SHA-256
  `786D6EE46BF6A96CDA6336582B9B4040C30A0275262089D8CB27B7291A9F8A0F`.
  Static validation proves exactly one new bolt renderer under the MP5 `bolt`,
  the body-minus-bolt model swap, preservation of `AdditionalRendererRoot`, and
  unchanged magazine, selector, trigger, and stock. The four create-only support
  assets and intended product destinations are:

  - 1,684,556-byte `aks74u_body_minus_bolt.fbx`, SHA-256
    `427C43D4410B0330BDAAE9C3DC606B65898622B64530BDA74A80C10426234C0C`,
    to `game/Assets/addons/lifepunch/aks74u/source/bolt_split/aks74u_body_minus_bolt.fbx`;
  - 81,148-byte `aks74u_bolt.fbx`, SHA-256
    `06DE3C4729D039691804467EE698E1532175F429083DA8ECF443FB60EA48C95F`,
    to `game/Assets/addons/lifepunch/aks74u/source/bolt_split/aks74u_bolt.fbx`;
  - 1,259-byte `aks74u_body_minus_bolt.vmdl`, SHA-256
    `EF3A1285977F4D335D4D54B06510224E499D52A82D54B68F697B2A2F529730ED`,
    to `game/Assets/addons/lifepunch/aks74u/aks74u_body_minus_bolt.vmdl`;
  - 1,237-byte `aks74u_bolt.vmdl`, SHA-256
    `FDC8F6EE449FD4AC5F1BE79AC202F6A6ED0FE412CAAD416955FC089F42C8D93C`,
    to `game/Assets/addons/lifepunch/aks74u/aks74u_bolt.vmdl`.

  All four product destinations remain absent and no product write occurred.
  ModelDoc/game compile, rendered bolt motion, hand fit, ADS, TP IK, runtime,
  visuals, and Portal delivery remain unverified.
- Desert Eagle minimal correction: **PASS**. The 29,478-byte promotion builder
  is SHA-256
  `786A843C7A68113B36475F55E0F2CF00909C091F2C5A9945AB85EE8F30398585`;
  the 12,168-byte test is
  `461DE8C712D58B284BAC342D0AD8BA5AF31E4F7C3E7B3BF06E48CB8C4AC5A6E1`.
  Its final TEMP candidate is 74,373 bytes at SHA-256
  `27CDBFD846AF3A08637A05C8577428BC3C30B4420259DD5CDE5502C4E4FFB6C7`.
  Relative to the 73,212-byte active prefab, it adds exactly one magazine
  bind-wrapper GUID plus `AdditionalRendererRoot`; existing body, slide, and
  magazine locals, GUIDs, references, component owners, and root name remain
  unchanged. `build_deserteagle_common_origin_candidate.py` is now only the
  pinned bind-math/indexing helper, not a promotable broad-rewrite producer.
  Compile, animation, runtime, and visual fit remain unverified.
- AR-15/SR-25 compiled-bind TEMP candidates: **PASS**. The 22,429-byte pinned
  M4 DATA sample is
  `12338DBDE712B9ED754CD192538F7F180DF1E35864353E241EEBF946C780D7B2`.
  The AR-15 candidate is 84,627 bytes at
  `92BF6A0E7ECC94A1A8F0E1391253CF2B67724D3EFDE5EDD8FFC52EB96DEE5B82`;
  the SR-25 candidate is 94,773 bytes at
  `9378D22F99F81BEDC83043B99C6FCCAA9C7C6E20D4EFFED0BD1137BCDCEE694D`;
  All 13 serialized moving-part wrappers satisfy `P * W = B` with maximum
  delta `4.90490943e-09`. Here `B` is explicitly the current byte-pinned
  identity-staged body baseline, not a measured or visually accepted fit. The
  AR candidate additionally assigns `AdditionalRendererRoot` to its prefab-local
  `weapon_root` GUID and proves all seven custom renderer GUIDs remain beneath
  it. The SR-25 candidate additionally uses the exact 74,922-byte native M4
  prefab to prove that `EjectionPort` is owned by `bolt_flap`, then reparents
  the existing marker under the SR-25 `bolt_flap` at local
  `0.97884475,8.24603638,0.02181413` with serialized round-trip delta
  `4.484870613774561e-09`; its GUID and ViewModel reference are unchanged.
  Product writes, compile, runtime motion, hand fit, ADS, and TP remain
  unverified.
- Consolidated promotion review bundle: **PASS / PROMOTION HOLD**. The current
  real bundle is
  `C:\Users\jared\AppData\Local\Temp\dxrp_weapon_promotion_bundle_ckpckl5o`.
  Its 134,546-byte report is SHA-256
  `3A8F4C684552B3ECED4D538B0905D7D72E639109FCE803C3622475CB2C813C52`.
  It contains exactly eight promotable prefab candidates, eight pinned active
  destination preimages, and four pinned create-only AKS support assets. The
  current 56,425-byte orchestrator is SHA-256
  `DF0CFFFBB18AFA751FCD45A1D51DE38C974905FA31C35767EB1BDB329FAC2461`.
  Final prefabs are confined to `candidates/`, evidence-only construction files
  to `evidence/intermediates/`, and support files to
  `support_assets/create_only/`; all measured, visual, runtime, and Portal
  acceptance flags remain false.
- The current 53,460-byte validator at SHA-256
  `CACDB57F80693C17074F011BD3CE2707DA784FDA52659CE6B15B04942C253136`
  returns **PASS** against that exact real bundle: 8/8 candidate pins, 8/8
  destination-preimage pins, and 4/4 support-asset pins. It additionally proves
  the Desert Eagle delta is only one wrapper GUID plus
  `AdditionalRendererRoot`, and the AKS delta is only a body-minus-bolt model
  swap plus one bolt child while preserving its existing visibility root and
  magazine/selector/trigger/stock state. ModelDoc/game compile, runtime,
  rendered part motion, visual fit, ADS, reload, IK, audio, roster behavior, and
  Portal delivery remain unverified. No product, game, docs, editor, scene,
  Portal, or Git write path fired.
- Anchor ownership audit: **PASS**, `ANCHOR_RESOLUTION_OK files=12 refs=24
  unresolved=0`. AR-15, Desert Eagle, and corrected M1911 retain coherent
  donor-node ownership for moving ejection geometry. AKS-74U geometry seeds are
  intentionally unwired and unaccepted. The active SR-25 ejection marker remains
  a static sibling, while the exact TEMP candidate corrects its ownership under
  M4 `bolt_flap`; AK-47 and M870 remain structural baselines only.
- SR-25 Blender 5 source comparison: **NO ACTIONABLE DELTA**. Both sources
  contain 20 objects, 18 meshes, 14 materials, zero armatures, and zero actions;
  the only added image datablock is `Render Result`.

## AK-47 visual reference

`C:\Users\jared\Downloads\ak47test.mp4` (30,177,300 bytes, SHA-256
`604F7B95FF760970D6CBF208F854A59D03AE9A6191E5AB4EF2307161793A52B3`) is a
5.456-second, 3840×2160 reference recorded on 2026-08-25. A frame-by-frame
contact sheet shows one coherent first-person AK during idle and firing, with
the support hand on the fore-end and no duplicate weapon assembly. It does not
show ADS, reload, third-person IK, or current post-restart parity, so those
remain open acceptance items.

## Current runtime observation

An earlier same-run live camera hierarchy contained an enabled `vm_m1911` GameObject with no
components, and the principal's `hold_R` hierarchy contains no `w_m1911`
equipment instance. That directly reproduces the active patch-format failure;
it does not contradict the green compile sensor because the JSON is valid while
the instantiated prefab shape is incomplete. The current console also records
exact-row/path failures for M870 and Desert Eagle. These are runtime defects,
not C# compiler failures.

The pinned 2026-08-25 19:36:58 `lp_weapon_roster_status` read-only run gives a
sharper roster ceiling. Its nine-line, 3,779-byte log block is pinned at SHA-256
`EC14FCFA775B719F160320A3B56403B6FC884BD84443B01F29927221F8D19F74`
and reports `candidates=8 equipReady=5 marketReady=5`. AK-47, AKS-74U, AR-15,
Desert Eagle, and SR-25 are ready. M1911 resolves its row/content/market but
reports `viewAsset=False`; M870 and AS VAL resolve no usable roster entry. Fresh
20:06:26 and 20:42:26 read-only reruns report the same eight rows and unchanged
`equipReady=5 marketReady=5` summaries.
Later give logs
show AR-15, AKS-74U, Desert Eagle, and SR-25 resources created with
`resourceValid=True` but `active=False`. Those lines prove roster/resource
creation only, not successful equip, first-person rendering, or visual fit.
Shipment spawn logs exist for AKS-74U, Desert Eagle, AR-15, and M1911; they do
not prove the contained weapon can equip.

A fresh read-only Portal UI census on 2026-08-25 (America/New_York) confirms the
target is the private `lifepunch.dxrpdev` gamemode. Addons, Content, and Market
each contain Rev 1 entries for AK-47, AKS-74U, AR-15, Desert Eagle, M1911, and
SR-25. M870 and AS VAL/ASVAL are absent from all three surfaces. This was census
only: no Portal edit, sync, purchase, save, submit, or publish action occurred.

The newest read-only 1280x720 camera capture shows the principal idle in third
person with no weapon drawn. A component read identifies the active equipment as
`w_hands` with `HoldType=None`; this makes the frame explicitly invalid for any
weapon hold, ADS, reload, moving-part, or audio acceptance.

The latest 19:10-19:11 read-only sensor confirms the same host session remains
live and unpaused with `Bloodwave` alive as a non-debug, non-proxy principal
pawn. The pawn's synchronized AFK duration was roughly 7,726 seconds. This
proves the session and pawn exist, not that the principal is currently present
at the keyboard, and it authorizes no runtime or perspective mutation.

A lawful read-only compiled-bind route is now pinned through
`Source2Viewer-CLI.exe` version
`19.2.6339+c72208352f5bf62f1482447ed166c548f303f8fa` at SHA-256
`36D8C9208EEFA61DD695BD577E49618BB161569941318F629294A4E4AF00EDC0`.
The AKS-74U candidate pins the 1,602,903-byte MP5 compiled model at
`87B8E0757BB7AE6B8B784A261449DABCD9A37125A0AC8B420C115244F65B4F24`;
the Desert Eagle candidate pins the 1,394,438-byte USP compiled model at
`439F8B2B676F310318EC4080D577DC16EA3549249564BADDDCD0B0CDE4A520F5`.
The AR-15/SR-25 candidate pins the 2,019,792-byte M4 compiled model at
`8726559C336098469AAA7C9A9A5018FE4825D3C6375EF406AB849C3EC82AABE4`.
All three compiled-bind builders reject arbitrary transforms, altered evidence
pins, and changed parser/model bytes. This is compiled skeleton-bind evidence
only, not an observed `IdlePose`, rendered animation, or visual fit.

The current session log contains one stale missing-USP path event for
`models/weapons/sbox_usp/v_usp.vmdl_c`; a full workspace search found no
persistent reference to that path. The only old M870 Auto Rigger source
reference is inside `t_shotgun.vmdl.disabled`; its orphan compiled artifact is
not reachable from the active prefabs.

A fresh bounded read-only intake found no AS VAL/ASVAL/VSS/Vintorez/9x39
source among 7,366 workbench files, 101 Downloads files, all 25 Downloads ZIPs
(10,685 central-directory entries), Desktop/Documents ingress roots, or CAVELUX
comms. The only positive text hits were existing status/dev placeholders and
old DayZ spawn rows, not source assets. AS VAL remains intake-only.

The M1911 source audit found 44 files but zero texture images. The pinned
`source.zip` is 876,596 bytes at SHA-256
`714297EB2970A966523527CFBD425B717E47BF58319466FA8AD2986D0F74FE25`
and contains exactly four FBXs, no textures. The 73,205-byte file is the
non-promotable common-origin intermediate. The promotable hardened final is
73,340 bytes at SHA-256
`D3316F70750F9BBF355D73EF3885F8C712D59F472A415E169A754FC3841BFC46`;
it has one root `ViewModel`, 81/81 unique object GUIDs, 6/6 component GUIDs,
hidden USP geometry, retained arms, and three distinct compiled-bind locals that
reconstruct one assembly. Its product-quality material gate
remains open. `SOURCE.md` is stale for `build_m1911_graybox.py` and
`pipeline_io.py`; generated asset hashes still match their recorded rows. Do not
rewrite the game-tree custody note while Play is live.

A fresh fail-closed material closure audit passes at the static disk ceiling:
14/14 active FP/TP prefab sources and compiled sidecars exist; 54 explicit
renderer records resolve; 35/35 custom ModelDocs and their FBX edges resolve;
35/35 referenced VMATs are present and compiled; 108/108 distinct custom
texture dependencies resolve; and all 9 stock donor models have compiled cloud
assets. AK-47, AKS-74U, AR-15, Desert Eagle, M870, and SR-25 have complete
active PBR inputs. M1911 is the sole material-quality blocker: its six materials
are explicitly mechanical graybox surfaces with zero custom maps, and its native
source supplies only a UV-grid placeholder. No newer raw source drop was found.
This is path/compile closure, not rendered lighting or appearance acceptance.

The M870 local parity audit passes: 2 prefabs, 87 objects, 17 components, and 12
active local assets with zero missing/dangling/duplicate GUID or active Auto
Rigger references. Its exact world/view paths already match `M870DevGive.cs`.
There is no missing local package manifest to repair; the remaining roster
blocker is private Portal/content metadata, followed by supervised fit and audio.

The M870 pump remains intentionally static after a fresh signal audit. The
owner-local `OnWeaponShot` event is a usable start edge, but the current network
proxy path does not repost it, reload exposes no per-shell phase edge, and neither
the prefab nor donor defines pump delay, travel, curve, cancellation, or TP
replication. The first-person pump is separable; the world pump is not. A measured
pump-motion contract and separable world geometry are required before a driver
can be implemented without inventing behavior.

Both downloaded tactical-shotgun ZIP payloads are exact duplicates: 14,381,028
bytes at SHA-256
`6D1BFF93EEF0DFBA4F844E2C8727BEC4D4F5648B9661A896CE00EF1778D6A0AC`.
Their seven members match the current native M870 source inputs 7/7. The source
FBX is 322,924 bytes at SHA-256
`4387543BA93CBBE3AB495A8678CB88651FCFB05C9726A39E6DC34AFA3AFA1B7C`;
it contains one mesh/material, 42 disconnected topology islands, no deformers,
pose, skeleton, animation stack/layer/curve, or named take, and no separately
named body, pump, magazine, or shell object. The differing 313-byte NTFS
`Zone.Identifier` streams only record different download-token metadata; the
ZIP `$DATA` streams compare byte-for-byte equal. Neither archive supplies the
missing action driver. The current separate first-person pump remains a static
sibling renderer, and the current world source remains combined.

One custody-path defect remains: `SOURCE.md:55` records
`source/T-SHOTGUN.fbx`, but that path is absent. The pinned source actually
resides at `m870/T-SHOTGUN.fbx` with the same 322,924-byte size and SHA-256 above.
Do not call the M870 custody record exact until that documentation-only path is
corrected after Play stops.

A pinned compiled-donor `Fire_01` audit also rejects every apparent fallback.
Across 36 frames at 40 FPS, `trigger`, `carrier`, `bolt`, `release`, both shells,
the muzzle, and both weapon-hand IK nodes are exactly static relative to
`weapon_root_children`. The only motion is whole-gun recoil plus sub-0.01-unit
hand/IK settling; body-to-pump relative motion remains zero. No pump, fore-end,
action, handguard, or rack node exists. Keep the pump static until an authored
motion/timing contract is supplied.

## M870 private Portal payload — prepared, not submitted

The read-only Portal audit found no current global `lifepunch.m870` addon. The
structural private-development payload is prepared as follows:

- Private/free addon identifier `lifepunchm870`, s&box identifier
  `lifepunch.m870`, initial revision 1.
- One `Equipment` content entry named `M870`, world prefab
  `addons/lifepunch/m870/equipment/w_m870/w_m870.prefab`, view prefab
  `addons/lifepunch/m870/equipment/vm_m870/vm_m870.prefab`, world model
  `addons/lifepunch/m870/models/m870_world.vmdl`, scale 1, grouping
  `Secondary`, and base config `{}`.
- Attach revision 1 to private `lifepunch.dxrpdev`. The equipment row's
  `GameModeAddonContentId` must be the generated game-mode content-row ID, not
  the global content or addon-attachment ID. Limit remains unlimited (`0`).
- Market type `Equipment`; `ReferenceId` must be the generated game-mode
  equipment-row ID. Shipment grouping, sort 0, quantity 5, white color, and Gun
  Dealer access mirror the other custom weapons. The proposed $3,500 cost is an
  inference from the existing Spaghelli shotgun row, not an accepted value.

This is not yet a fully serializable submission packet: the price is unaccepted,
the serialized white-color value and Gun Dealer job identifier/tag array are not
pinned, generated IDs do not yet exist, and optional icon/display overrides are
unspecified. All generated Portal IDs remain unknowable before creation. Save, submit,
private sync, and market creation each remain held for action-time principal
confirmation; no Portal mutation occurred.

## Proof ceiling

Static closure does **not** prove hotload, runtime spawn, first-person fit,
third-person fit, ADS, reload part motion, sound playback, or Portal delivery.
The direct native editor sensor currently reports DXRP, Play live/unpaused, a
clean scene, no active compile, `LastCompileSucceeded=true`, and zero compile
errors. Guarded Cavelux-native and Chromr calls still refuse because their
identity rule requires a missing `lifepunch` token; that guard was not bypassed,
and Claude Bridge was not freshly resensed. These facts prove only the direct
native compiler/session ceiling. Runtime and visual proofs must be collected after
Play is explicitly stopped, a separate reviewed product-promotion act applies
the smallest accepted fixes, and the normal scene is restarted without saving
it.

## Required acceptance order

For each weapon:

1. Structural invariant and pinned-input check.
2. Positive editor compile sensor plus zero fresh code errors.
3. First-person idle: exactly one complete weapon, both hands seated.
4. Fire: only the correct moving part responds.
5. Reload: magazine path and any supported bolt/slide/pump motion.
6. ADS after hand seating; iron/scope line centered.
7. Third-person idle/fire/reload with correct hold and left-hand IK.
8. Deploy/holster visibility and audio playback.
9. Private Portal roster/spawn verification only after local acceptance.
10. AK-47 regression before declaring the arsenal ready.

## HOLD conditions

- Do not save `game.scene`.
- Do not mutate active assets while Play is live.
- Do not use Auto Rigger outputs under the current provenance gate.
- Do not copy or publish Sourcesounds/CS:GO bytes; BOARD ordinal 1980 makes the
  current archive reference-only. Use licensed replacement audio.
- Do not publish M870 until its CC BY 4.0 attribution is preserved and an
  authorized private addon package plus Portal rows exist. Its requested Valve
  audio mapping remains reference-only and cannot be bundled.
- Do not invent final transforms; retain bounds results as candidates until a visual pass accepts them.
- Do not commit, push, or operate production Portal from this lane.
