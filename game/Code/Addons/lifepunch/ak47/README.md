# AK47 Code

This is the clean code lane for the LifePunch AK47 addon package.

Do not import old scratch code directly. Build runtime behavior from a known DXRP weapon reference, preferably the official M4A1 pattern, then keep only LifePunch-specific implementation here.

Initial scope:

- Weapon identity and package wiring.
- Core `AK47.cs` definition for mounted paths and baseline stats.
- Compile-safe `AK47Weapon.cs` runtime contract for ammo state and first-pass weapon timing.
- Primary weapon behavior.
- World model and viewmodel hooks.
- Damage, fire rate, recoil, spread, reload, ammo, and inventory behavior after the DXRP weapon base class is confirmed.

Current runtime boundary:

- `AK47Weapon.cs` intentionally compiles as a LifePunch-owned `Sandbox.Component`.
- Do not change it to inherit the DXRP/base weapon class until that class is available to `addons.csproj` and builds locally.

Non-goals for the first core pass:

- Server economy changes.
- Store/VIP/EVIP behavior.
- Gamemode attachment or market rows.
- Development server sync.
