---
title: Editor Libraries
description: Why game/Libraries and the local editor and agent configuration are not tracked, and how a fresh clone gets what it needs.
order: 3
---

`game/Libraries/` holds the s&box editor libraries a developer has installed. Git ignores everything in it except `.gitkeep` and [`MANIFEST.md`](../../game/Libraries/MANIFEST.md), which lists what the editor expects: folder name, package version where one exists, whether the library ships a licence file, whether it is house or third-party, and whether the editor loaded it.

## Why they are not tracked

- **Licences.** Of the 38 libraries itemised in the manifest, 26 carry no licence file and 12 carry an MIT licence file. This repository is public; it does not republish bytes it has no licence to republish.
- **They are the editor's, not ours.** The editor's library manager installs and updates them. Update bursts rewrite many files at once, and tracking those copies only records the editor's churn as local changes.
- **Size.** About 6,700 files and 164 MiB on a developer machine, most of it unchanged third-party code.
- **Nothing in `game/Code` uses them.** A sweep of the game code found no type or namespace from any library. They are tooling around the editor, not dependencies of the game. Use from assets, scenes or shaders was not swept, and a build from a clean clone without them has not been run.

Do not force-add library files (`git add -f`) to get around the ignore rule; add a row to the manifest instead.

## How a fresh clone gets its libraries

1. Clone the repository and open `rp.sbproj` in s&box.
2. For each library you want, install it from the editor's library manager (the names in the manifest are sbox.game package identifiers, apart from the folders marked `-` for version). A library loads by its folder being present under `game/Libraries/`; `rp.sbproj` lists no package references.
3. Only the libraries you use need to be present. A missing one should cost you the tooling it provides; nothing in `game/Code` references them.

## The house organs

Two libraries are specific to how this workbench is operated:

- **`local.cvlxobservatory`** is authored for this workbench. It writes the editor heartbeat and the liveness of the editor bridges, and shows a read-only dock in the editor. Its source is kept in the maintainer's own tooling repository, outside this one; the developer copies its `editor/` folder into `game/Libraries/local.cvlxobservatory/` (a copy, not a symlink).
- **`sboxskinsgg.claudebridge`** is the in-editor Claude Bridge. It is a third-party package from sbox.game; no licence file ships with it, so it is installed from the editor and never redistributed from here.

The two editor bridge libraries (`notpointless.chomnr_mcp` and `kitsupanic.sbox_mcp_plus`) are ordinary third-party packages that the house tooling talks to.

## Local tooling that is also ignored

These are per-machine editor and agent configuration. They can hold machine paths, endpoints and the names of credentials, so they stay out of the repository. Never commit a credential value to any of them.

- `.mcp.json`
- `.codex/`
- `.cursor/hooks/state/`
- `exceptions.txt`
