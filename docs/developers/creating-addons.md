---
title: Creating Addons
description: Extend a DXRP server with custom code, distributed through the portal.
order: 1
---

Addons let you extend a DXRP server with custom code and assets, distributed through the DXRP portal and pulled down automatically by every server on your network via the [server launcher](/docs/operators/launching-server-with-addons).

An addon has two parts, each packaged separately:

- **Code**: extracted into `game/Code/Addons/<networkIdentifier>/<addonIdentifier>/` and compiled as part of the game project by s&box.
- **Assets**: extracted into `game/Assets/addons/<networkIdentifier>/<addonIdentifier>/`.

`<networkIdentifier>` is your network's (tenant's) identifier, and `<addonIdentifier>` is the addon's identifier. Both are lowercase letters, numbers, and hyphens only, up to 64 characters. Addon identifiers are unique across the whole platform, not just within your network, so pick something specific.

## 1. Create the addon on the portal

In the portal, go to **... → Addons → New Addon** and fill in:

| Field | Notes |
| --- | --- |
| **Name** | Display name, shown in the marketplace and portal |
| **Identifier** | Lowercase letters, numbers, hyphens only. Can't be changed after creation |
| **Description** | Optional short description |

The addon starts private and free. You can fill in the rest (about page, price, visibility, screenshots, video) later from the addon's manage page.

## 2. Develop the addon locally

1. Clone the [DXRP repository](https://github.com/dxura/dxrp) and open `rp.sbproj` in s&box.
2. Create your code inside `game/Code/Addons/<networkIdentifier>/<addonIdentifier>/` and any assets inside `game/Assets/addons/<networkIdentifier>/<addonIdentifier>/`, using your own network and addon identifiers from step 1. These folders aren't checked into the DXRP repo, they're specific to your addon and get zipped up for upload in the next step.
3. Build and test against your own network as you normally would in s&box.

Component-based addon services registered under `Code/Addons` are picked up automatically at runtime. You don't need to manually register them anywhere else in the game project.

## 3. Publish a revision

Back on the addon's manage page in the portal, open **New Revision**. This is where the code and assets you wrote in step 2 actually get uploaded:

1. Under **Code**, select your `game/Code/Addons/<networkIdentifier>/<addonIdentifier>/` folder. Under **Assets**, select `game/Assets/addons/<networkIdentifier>/<addonIdentifier>/`. Either can be left empty and inherited from the previous revision if you're only updating one side.
2. The portal zips the *contents* of each selected folder client-side. The folder itself isn't preserved, only what's inside it. Make sure you select the `<addonIdentifier>` folder itself, not its parent, or the code will extract one level too deep on the server and silently fail to compile.
3. Set a changelog and, if needed, a target s&box version.
4. Publish. Uploads must finish within a few minutes of creating the revision. If you leave the dialog open too long before uploading, you'll need to start a new revision.

Size limits: code packages up to 50MB, asset packages up to 300MB.

Only the latest revision of an addon can receive new package uploads. Once a newer revision exists, the older one is locked.

## 4. Add content entries (optional)

If your addon defines placeable content (entities, items, etc.) that game modes can reference, add them as content entries on the revision: name, type, description, and references into your code/assets, plus an optional base config (JSON) applied by default wherever the content is used.

## 5. Go live

Once you're happy with a revision, set the addon's **Visibility** to Public from its manage page so networks can find and install it. You can also set a price (whole dollars, 0 = free), a video, and whether your source code is visible to installers.

Once a network installs your addon, it'll show up for their servers automatically the next time the [server launcher](/docs/operators/launching-server-with-addons) runs.

## Reference

The [API Documentation](/docs/api/api-documentation) covers the portal API if you want to script addon publishing instead of using the UI. Questions are welcome in the [Discord](https://discord.gg/uBwQ2QHP2D).
