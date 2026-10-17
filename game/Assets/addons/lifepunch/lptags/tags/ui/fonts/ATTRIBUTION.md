# ATTRIBUTION

Folder: `addons\lifepunch\lptags\tags\ui\fonts`

| File | Bytes | SHA-256 | Origin | Licence |
|------|------:|---------|--------|---------|
| RobotoMono-Regular.ttf | 125748 | AF0BFF7599C3DF3831755C16E39B3C496DF74B8C8D8A1161B14DC8461BE17CB4 | Roboto Mono, Google Fonts (`googlefonts/robotomono`) | SIL Open Font License 1.1 |
| RobotoMono-LICENSE-OFL-1.1.txt | 4488 | FFD9F334604A352AD52EE0A7159C31BDAFAF5F8465E5B9F9EB5C1F4924343183 | Upstream `OFL.txt`, stored with CRLF line endings | SIL Open Font License 1.1 |

## Licence verification (own-sensed 2026-08-28)

`TAGS_RUNTIME_GATES.md` (FONT-SHIPPING) requires the staged licence to equal upstream
`googlefonts/robotomono/OFL.txt` at SHA-256
`50AB8DD54680D3473F649C9DB86FECE88434D097C7834475C1C72D2F8C429215` / 4395 B, and states that
substring matching is not an admissible licence gate.

As stored the file is 4488 B and does NOT match that pin. It has 93 lines and exactly 93 CR
bytes, so it is the same document carrying CRLF line endings. Normalised to LF it is
**4395 bytes** with SHA-256
**50AB8DD54680D3473F649C9DB86FECE88434D097C7834475C1C72D2F8C429215** -- an exact whole-file
equality with the pin of record, not a substring match. The 93-byte delta is exactly the CR
count, which is the signature of serialization drift rather than an altered document.

Sensors: `wc -c`, `wc -l`, `tr -cd CR | wc -c`, `tr -d CR | sha256sum`.

Scope of the claim: this proves the staged bytes equal the pin RECORDED IN
`TAGS_RUNTIME_GATES.md`. It does not re-fetch upstream -- this lane has no web access -- so
"equals upstream" rests on that recorded pin, not on a live comparison.

## Font shipping status

- **Package inclusion: covered.** `game/rp.sbproj` Resources includes
  `addons/lifepunch/lptags/**`.
- **SCSS lookup: present.** `LpTagsMenu.razor.scss:32` and `:87` request
  `font-family: "Roboto Mono", "Courier New", monospace`.
- **Engine consumption: UNVERIFIED.** No `@font-face` rule exists anywhere in the tree
  (sensor: `grep -rn '@font-face' game --include=*.scss --include=*.css` -> 0 hits, with
  `font-family` matches present in the same sheets as a positive control). There is therefore
  no in-tree precedent for registering a custom font family, and none was invented here.
  Until an editor pass proves the face resolves, the menu should be expected to fall back to
  Courier New / monospace.
