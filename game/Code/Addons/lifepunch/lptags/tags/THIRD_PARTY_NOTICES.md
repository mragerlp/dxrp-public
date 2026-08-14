<!--
PROPRIETARY & CONFIDENTIAL â€” Â© 2026 lifepunch.co. All rights reserved.

"LIFEPUNCHâ„¢ Tags for DXRP" (s&box ident: lifepunch.tags Â· addon ident: lptags) is the sole-owned
intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
sublicensing, copying, or reuse by ANY person or entity â€” including DXRP and
LifePunch staff, contributors, or community â€” EXCEPT the owner (lifepunch.co).
Third-party material identified below remains governed exclusively by its stated license;
this notice does not relicense that third-party material.
Presence in this repository or on the DXRP portal grants no rights to anyone else.

Author account: mrragerlp Â· Public alias (in-game Â· Steam Â· Discord): Bloodwave
-->
# Third-party notices

## Bundled assets

| Asset | Copyright | License | Source |
| --- | --- | --- | --- |
| Roboto Mono | Copyright 2015 The Roboto Mono Project Authors | SIL Open Font License 1.1 | <https://github.com/googlefonts/robotomono> |

## lifepunchwawcolortags

Repository: <https://github.com/mragerlp/lifepunchwawcolortags>  
Pinned commit: `dba9dca76d008ac6b59f62fcd8bc3bce9bcac9bf`  
Pinned source: <https://github.com/mragerlp/lifepunchwawcolortags/commit/dba9dca76d008ac6b59f62fcd8bc3bce9bcac9bf>  
Review date: 2026-07-31

The following source behavior is adapted for the LifePunch foundation:

| Upstream | Foundation destination |
| --- | --- |
| `js/cycl.js` | `LpTagEffectEvaluator.EvaluateColorWipe` |
| `js/rain.js` | `LpTagEffectEvaluator.EvaluateRainbowWave` |
| `js/cyln.js` | `LpTagEffectEvaluator.EvaluateRedScanner` |
| `js/move.js` | `LpTagEffectEvaluator.EvaluateSlide` |
| `js/dots.js` | `LpTagEffectEvaluator.EvaluateBouncingDot` |
| `js/stars.js` | `LpTagEffectEvaluator.EvaluateBouncingPlus` |
| `js/colors.js` | static catalog colors |

LifePunch modifications: color, scan, and slide effects apply to the complete
player name; Slide is a bounded pixel transform; Dot and Plus are prefix
decorations; text is segmented by grapheme; animated effects use a shared
phase; viewer overrides can reduce or disable animation/decorations; and output
is rendered by an s&box presenter rather than browser demo markup.

MIT License

Copyright (c) 2026 Zeljko Vranjes

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
