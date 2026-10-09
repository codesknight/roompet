# Background Music — Sources & Licenses

This document records the provenance and licensing of the third-party background-music
tracks shipped in `UnityMCPProject/Assets/Resources/Music/`.

**These files are not original work.** They are third-party audio assets used under the
licenses stated by their authors on the source pages listed below. Each entry was checked
individually on the source page; the license name written here is the one that page
declares, and nothing in this document is inferred or assumed.

## Summary

- **All 11 tracks are CC0 1.0 Universal (Public Domain Dedication).**
- **Attribution is NOT legally required for any of them.** CC0 places the work in the
  public domain, so no credit obligation attaches to the license.
- Several authors nonetheless *ask* to be credited (marked as **credit requested** below).
  Providing that credit is a courtesy, not a license condition — but we do it anyway.
- No NonCommercial (NC), NoDerivatives (ND), "personal use only", or unlicensed material
  was used. Nothing here requires a paid license or a share-alike obligation.
- All 11 tracks were sourced from a single repository,
  [OpenGameArt.org](https://opengameart.org/), which renders the exact license deed for
  each submission on its own page — so every license claim below was read off the page
  rather than guessed from a search result.

### Source repository

All tracks were published on **OpenGameArt.org** at the URLs listed per track. Downloads
were taken from the direct file URLs published on those same pages
(`https://opengameart.org/sites/default/files/...`).

### License used

| License | Deed | Attribution required? | Tracks |
|---|---|---|---|
| CC0 1.0 Universal | https://creativecommons.org/publicdomain/zero/1.0/ | No | all 11 |

### Verification performed

For every file below, after download we verified:

1. The file's leading magic bytes really are audio — `OggS` for `.ogg`, `ID3`/`0xFF 0xFB`
   for `.mp3`. No HTML error page was saved under an audio extension.
2. The byte size on disk (recorded per track).
3. The decoded duration, parsed from the container itself (Ogg granule position /
   MPEG frame headers), not from the page text.

Total added size: **22,872,852 bytes (21.8 MiB)** across 11 files.

---

## Track index

| Unity file | Scene | Title | Author | License | Bytes |
|---|---|---|---|---|---|
| `menu_theme.mp3` | 开始界面 | A New Town (RPG Theme) | cynicmusic (The Cynic Project) | CC0 1.0 | 766,291 |
| `pet_room.ogg` | 宠物房间 | Napping on a Cloud | congusbongus | CC0 1.0 | 1,595,355 |
| `garden.ogg` | 花园 | Flowerbed Fields [Loop] | Zane Little Music | CC0 1.0 | 1,724,097 |
| `terrace.ogg` | 夜晚露台 | Somewhere in the Elevator | You're Perfect Studio (music by Peachtea) | CC0 1.0 | 986,848 |
| `runner_forest.mp3` | 森林奔跑 | Free Run [8 bit(ish)] | TAD | CC0 1.0 | 2,002,777 |
| `fly_bird.ogg` | 小鸟飞行 | Funky Disco Beats to Boogie/Woogie to | Fupi | CC0 1.0 | 2,675,602 |
| `jump_quest.mp3` | 跳一跳 | Party Sector | Joth | CC0 1.0 | 1,925,141 |
| `catch_fruit.ogg` | 接果子 | Raspberry Jam | congusbongus | CC0 1.0 | 2,921,627 |
| `slice_fruit.mp3` | 切水果 | The Rush | tebruno99 | CC0 1.0 | 3,356,965 |
| `angry_birds.ogg` | 弹弓小鸟 | Toy Soldiers | Zane Little Music | CC0 1.0 | 2,512,878 |
| `puzzle.mp3` | 拼图 / 记忆配对 | Contemplation | Joth | CC0 1.0 | 2,405,271 |

---

## Per-track detail

### `menu_theme.mp3` — 开始界面 (title screen)

- **Title:** A New Town (RPG Theme)
- **Author:** cynicmusic (The Cynic Project)
- **Source page:** https://opengameart.org/content/a-new-town-rpg-theme
- **Direct file:** https://opengameart.org/sites/default/files/025_A_New_Town.mp3
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No. **Credit requested** — the author's page says
  "Please credit — The Cynic Project / pixelsphere.org / cynicmusic.com". We honour this,
  but it is a request, not a CC0 condition.
- **Format:** MP3 (starts with `ID3`) · **Bytes:** 766,291 · **Duration:** ~47.6 s
- **Why it fits:** The source describes it as a "classic harp solo theme for discovering a
  new town … ethereal, minimal and regal" — a soft, harp-led welcome that reads as warm,
  gentle and quietly hopeful, exactly the tone a title screen wants.

### `pet_room.ogg` — 宠物房间 (cozy indoor pet room)

- **Title:** Napping on a Cloud
- **Author:** congusbongus
- **Source page:** https://opengameart.org/content/napping-on-a-cloud
- **Direct file:** https://opengameart.org/sites/default/files/napping_on_a_cloud.ogg
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** Ogg Vorbis (starts with `OggS`) · **Bytes:** 1,595,355 · **Duration:** ~248.4 s
- **Why it fits:** Described by the author as "chill happy chiptune"; at over four minutes it
  is by far the longest track here, so it can sit under the pet room for a long session
  without the loop seam becoming noticeable, and its restraint keeps it unobtrusive.

### `garden.ogg` — 花园 (sunny garden)

- **Title:** Flowerbed Fields [Loop]
- **Author:** Zane Little Music
- **Source page:** https://opengameart.org/content/flowerbed-fields-loop
- **Direct file:** https://opengameart.org/sites/default/files/flowerbed_fields.ogg
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** Ogg Vorbis (starts with `OggS`) · **Bytes:** 1,724,097 · **Duration:** ~105.9 s
- **Why it fits:** Published explicitly as a loop — "a cute chiptune loop to get an adventure
  started" — bright, light and playful, and deliberately written to repeat seamlessly.

### `terrace.ogg` — 夜晚露台 (night terrace, upscale lounge)

- **Title:** Somewhere in the Elevator
- **Author:** You're Perfect Studio (track contributed by Peachtea)
- **Source page:** https://opengameart.org/content/somewhere-in-the-elevator
- **Direct file:** https://opengameart.org/sites/default/files/Peachtea%20-%20Somewhere%20in%20the%20Elevator_1.ogg
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
  *(chosen by us)*. The source page offers this same track under **four** licenses:
  CC0, CC-BY 4.0, CC-BY 3.0 and OGA-BY 3.0. We take it under **CC0**, which carries no
  attribution obligation. If you prefer to credit it anyway, credit
  "You're Perfect Studio / Peachtea".
- **Attribution required:** No (under the CC0 option we selected).
- **Format:** Ogg Vorbis (starts with `OggS`) · **Bytes:** 986,848 · **Duration:** ~61.4 s
- **Why it fits:** The author calls it a "cute 1 minute loopable tune"; it is literal
  elevator/lounge music — mellow, elegant and understated, which is precisely the
  upscale-night-lounge register the terrace scene is going for. It is also authored as a
  seamless loop.

### `runner_forest.mp3` — 森林奔跑 (endless runner, forest)

- **Title:** Free Run [8 bit(ish)]
- **Author:** TAD
- **Source page:** https://opengameart.org/content/free-run-8-bitish
- **Direct file:** https://opengameart.org/sites/default/files/Project%202%20marioish_0.mp3
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** MP3 (starts with `ID3`) · **Bytes:** 2,002,777 · **Duration:** ~83.3 s
- **Why it fits:** A bright, forward-moving chiptune run theme that keeps a happy, upbeat
  pulse without turning aggressive — the right energy for an endless runner while staying
  family-friendly.

### `fly_bird.ogg` — 小鸟飞行 (flappy-bird style)

- **Title:** Funky Disco Beats to Boogie/Woogie to
- **Author:** Fupi
- **Source page:** https://opengameart.org/content/funky-disco-beats-to-boogiewoogie-to
- **Direct file:** https://opengameart.org/sites/default/files/funkydiscobeatstoboogieslashwoogieto_0.ogg
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** Ogg Vorbis (starts with `OggS`) · **Bytes:** 2,675,602 · **Duration:** ~133.1 s
- **Why it fits:** This is the row that needed **动次打次** — a clear percussive groove. The
  author describes it as "a funky disco loop", i.e. a four-on-the-floor drum-driven backing
  with an unbroken beat, which gives the flap cadence something steady and rhythmic to lock
  onto rather than a melody-led cue.

### `jump_quest.mp3` — 跳一跳 (hold-to-charge hopping)

- **Title:** Party Sector
- **Author:** Joth
- **Source page:** https://opengameart.org/content/party-sector
- **Direct file:** https://opengameart.org/sites/default/files/Party%20Sector.mp3
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** MP3 (starts with `ID3`) · **Bytes:** 1,925,141 · **Duration:** ~96.1 s
- **Why it fits:** The author describes it as "a dancy space tune" — a light, bouncy,
  uncluttered arcade tune with an obvious pulse, which suits a simple charge-and-hop
  mechanic without competing with it.

### `catch_fruit.ogg` — 接果子 (catch falling fruit)

- **Title:** Raspberry Jam
- **Author:** congusbongus
- **Source page:** https://opengameart.org/content/raspberry-jam
- **Direct file:** https://opengameart.org/sites/default/files/raspberry_jam.ogg
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** Ogg Vorbis (starts with `OggS`) · **Bytes:** 2,921,627 · **Duration:** ~174.5 s
- **Why it fits:** A "new jack swing style funky tune" — busy, bright and cheerful, with
  enough rhythmic activity to match a screen full of falling fruit, and long enough
  (~3 minutes) that a long catching session does not sit on the same bar.

### `slice_fruit.mp3` — 切水果 (fruit ninja style slicing)

- **Title:** The Rush
- **Author:** tebruno99
- **Source page:** https://opengameart.org/content/the-rush
- **Direct file:** https://opengameart.org/sites/default/files/The%20Rush_2.mp3
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No. **Request:** the author asks that the file's existing ID3
  tags be left intact — **we have not rewritten or stripped the tags**, the file is copied
  byte-for-byte from the source.
- **Format:** MP3 (starts with `ID3`) · **Bytes:** 3,356,965 · **Duration:** ~139.7 s
- **Why it fits:** The author describes it as a "fun little tune maybe for some high action
  scene" — fast, energetic and playful, matching the quick-slice pacing of a fruit-slicing
  game.

### `angry_birds.ogg` — 弹弓小鸟 (slingshot physics puzzle)

- **Title:** Toy Soldiers
- **Author:** Zane Little Music
- **Source page:** https://opengameart.org/content/toy-soldiers
- **Direct file:** https://opengameart.org/sites/default/files/toy_soldiers.ogg
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** Ogg Vorbis (starts with `OggS`) · **Bytes:** 2,512,878 · **Duration:** ~154.2 s
- **Why it fits:** "Toy Soldiers march onward!" — a toy-sized march: rhythmic and
  processional like a military advance, but light and playful rather than grim, which maps
  onto a slingshot siege waged by cartoon birds.

### `puzzle.mp3` — 拼图 / 记忆配对 (in-room puzzle panels)

- **Title:** Contemplation
- **Author:** Joth
- **Source page:** https://opengameart.org/content/contemplation-0
- **Direct file:** https://opengameart.org/sites/default/files/Contemplation.mp3
- **License:** CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
- **Attribution required:** No.
- **Format:** MP3 (starts with `ID3`) · **Bytes:** 2,405,271 · **Duration:** ~120.1 s
- **Why it fits:** The author describes it plainly: "just an ambient track … no real melody
  or focus here, just ambience." Sparse and quiet by construction, so it stays out of the
  way while the player thinks.

---

## Rows that could not be filled

**None.** All 11 scene rows are filled with a track that passed every check (audio magic
bytes confirmed, ≥ 30 s, ≥ 100 KB, license explicitly permitting commercial use and
redistribution inside a game).

## Note for redistributors

Everything here is CC0, so you may reuse, modify and redistribute these files — including
commercially — with no attribution obligation. The credit block below is included as a
courtesy to the authors, several of whom asked for it, and because crediting free-asset
authors is good practice for a public repository.

### Suggested in-game / README credit block

```
Background music (all tracks licensed CC0 1.0 Universal, public domain):
  "A New Town (RPG Theme)"        — cynicmusic / The Cynic Project (pixelsphere.org, cynicmusic.com)
  "Napping on a Cloud"            — congusbongus
  "Flowerbed Fields [Loop]"       — Zane Little Music
  "Somewhere in the Elevator"     — You're Perfect Studio (music by Peachtea)
  "Free Run [8 bit(ish)]"         — TAD
  "Funky Disco Beats to Boogie/Woogie to" — Fupi
  "Party Sector"                  — Joth
  "Raspberry Jam"                 — congusbongus
  "The Rush"                      — tebruno99
  "Toy Soldiers"                  — Zane Little Music
  "Contemplation"                 — Joth
Source: https://opengameart.org/
```
