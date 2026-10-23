# AKS-74U audio custody

These local evaluation cues were extracted from `csgo-master.zip` downloaded from
<https://github.com/sourcesounds/csgo/tree/master/sound/weapons>.

Archive SHA-256:
`E8452275A5007670D367B4E870A8FC3E057B41560B3E7C93C5886BD96DE9BEB7`

Upstream mapping:

- `aug/aug_01.wav` through `aug/aug_04.wav` -> randomized close shots
- `aug/aug_draw.wav` -> deploy
- `aug/aug_clipout.wav` -> magazine out
- `aug/aug_clipin.wav` -> magazine in
- `clipempty_rifle.wav` -> dry fire

The upstream archive does not include a redistribution license. These files are
for local evaluation only and must not be included in a public or private Portal
publish until redistribution permission is documented. Bolt, clip-hit, and
distant-tail variants remain intentionally unwired until animation and range
timing are measured.
