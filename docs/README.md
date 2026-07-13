# Contributing to the DXRP docs

Everything under this folder is published to [dxrp.net/docs](https://dxrp.net/docs). Open a PR against this folder to fix or add documentation, no access to the private site repo is needed.

## Structure

```
docs/
  manifest.json          # section order, title, and icon
  <section>/
    some-page.md          # one file per doc page
    images/
      some-page/
        screenshot.png     # images used by some-page.md
```

- Each top-level folder (`player/`, `operators/`, ...) is a **section** and must be listed in `manifest.json`.
- Adding a new section: add an entry to `manifest.json` and create the matching folder. `icon` is a [PrimeIcons](https://primeng.org/icons) class name (e.g. `pi pi-server`).
- Each `.md` file is one doc page, its filename (without `.md`) becomes the URL slug, e.g. `operators/setting-up-a-server.md` → `/docs/operators/setting-up-a-server`.

## Page format

Every page needs frontmatter with a `title` and `description`, and an optional `order` (pages within a section sort by `order` ascending, then alphabetically):

```markdown
---
title: Setting Up a Community Server
description: Create a server on the DXRP portal, configure a map, and go live on Windows or Linux.
order: 1
---

Page content starts here, standard GitHub-flavored markdown.
```

## Images

Put images in an `images/<page-slug>/` folder next to the page and reference them with a relative path, so they preview correctly on GitHub too:

```markdown
![Server status page](images/setting-up-a-server/06-server-status-page.png)
```

## Publishing

[dxrp.net](https://dxrp.net) fetches pages from this folder live, directly from GitHub, at page-load time. There's no build or deploy step here: once a PR merges to `main`, the change is live on [dxrp.net/docs](https://dxrp.net/docs) immediately. The `/docs` landing page itself is hand-coded and isn't generated from this folder.
