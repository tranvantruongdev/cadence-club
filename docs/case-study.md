# Cadence Club: case study

A match-3 in the mould of Royal Match, built solo in Unity 6.3 from my own
[mobile template](https://github.com/tranvantruongdev/unity-mobile-template). The twist: your boosters are riders
from your cycling club, charged by matching their colour. This page covers the three parts I care most about,
then the smaller things and the numbers.

## 1. A board model that emits events, replayed by the view

The whole game lives in pure C# (`Assets/_Game/Scripts/Core`, no `UnityEngine`). A move goes in, and a list of
**board events** comes out:

```
Swapped (3,3)↔(4,3)
StepStarted 0
  Cleared (4,1) … SpecialActivated (4,3) RocketH … CrateHit (5,4) hits left 1 … SpecialCreated (2,2) Bomb
  Fell (2,6)→(2,3) … Spawned (2,7) from above
StepStarted 1            ← a cascade
  …
StepStarted 0            ← the board's own turn: oil spreads, a trophy drops in
  OilSpread (0,7)→(1,7)
```

`BoardView` replays them step by step: clears and specials together, then every piece falls with one tween. Then
it compares itself with the Core board. If they differ, it logs a warning and resyncs, and the PlayMode smoke test
fails on that warning. The smoke test plays five levels with a bot through the real controller, including the ones
with oil and trophies.

What this bought me:

- **Tests without Unity.** The rules, every special and combo, gravity with diagonal slides under crates, the
  shuffle when no move is left: all of it is tested with `dotnet test`, and the same files run as EditMode tests in
  Unity.
- **Determinism.** A seeded PCG32 generator drives refills and shuffles: same seed and same moves, same game. The
  first three levels use a designed seed, and a test checks that level 2's first hint makes a rocket and level 3's a
  bomb. If a rule change breaks that, the failure message names a seed that works.
- **One invariant kept on purpose:** each step settles gravity once, so each piece moves with a single tween. When I
  added trophies (they leave the board at the bottom of their column), collecting them at the end of a step would
  have needed a second gravity pass. Instead, a trophy that has arrived leaves at the *start* of the next step,
  together with that step's matches.

## 2. Thirty levels tuned by a bot

A greedy bot scores each legal move: cells cleared, goal pieces, specials made, crates or oil next to the match,
cells under a trophy. A `LevelSimulator` plays a level 200 times with different seeds.

The trick that makes tuning cheap: **the bot never looks at moves left**, so a run with a limit of 15 plays exactly
the first 15 moves of a run with no limit. One uncapped pass per seed records how many moves each win took. That
gives the win rate for *every* move limit at once, and `FitMoves` picks the limit whose win rate sits in the band.

The bands follow a sawtooth: easy levels 1–6 and a breather after each bump, a hard bump every fifth level from 10,
and a finale at 30.

![Greedy-bot win rate per level against its target band](win-rates.svg)

The check runs in CI on every push (`LevelFileBandTests`). When a level drifts out of its band, the failure says
which move limit fits; that's how levels 22 (oil) and 26 (trophies) got their 17 and 25 moves. These simulations
were about ten times slower inside Unity's editor (242 s against 24 s at the time), so they moved to a dotnet-only
test project. All 30 levels × 200 games now take about two and a half minutes there.

The level editor (an editor window) paints the shape, sets goals, and runs the same simulator in place.

## 3. Gacha with pity, rates on screen, and a reveal you can skip

The same kind of system I ship at work, rebuilt from scratch:

- **Rates:** R 80%, SR 17%, SSR 3%, read from CSV, with half of SSRs going to the featured rider.
- **Pity:** an SSR is guaranteed by pull 60, and the counter is on the Recruit screen ("SSR guaranteed within N
  pulls").
- **10-pull floor:** every 10-pull has at least one SR.
- **Duplicates:** a rider you already have becomes shards, which level riders up to 5. That's a sink, not a dead pull.
- **Rates screen:** every rider's individual chance, plus the rules in plain words. Gems come only from playing;
  the shop is a demo with a "no real purchases" banner.
- **Reveal:** bikes roll in, a flash in the best rarity's colour teases the batch, and the cards flip one by one.
  An SSR flips slowly with a burst and a line from the rider. **Skip** jumps to the summary.

The roller is pure C#, with seeded tests:
- the rates over many pulls;
- pity: with SSRs removed from the table, exactly every 60th pull is one; with the real rates, no run without an
  SSR is longer than 59;
- the 10-pull floor;
- the featured share.

## Smaller things

- **First session:** a new player boots straight into level 1 (the template's boot flow got a hook for this). The
  hint comes after 1.5 s with a fingertip sliding along the swap. The win card only says "Next level", and after
  level 3 it says "Continue" and opens Home, where a fingertip points at the first task to restore.
- **Home as a place:** each area is a flat illustration built from UI shapes (rounded boxes, ellipses, rings). Every
  renovation task is its own drawing in the scene: a faint ghost with a ★ bubble until it's built, then it pops in.
  The last task of an area tells its story beat.
- **Coin sinks:** pre-level boosters (rockets, bomb, disco placed on the board), and "+5 moves" for 300 coins when
  out of moves, with the life charged only if you leave.
- **Localization (EN/VI/JA):** one table in CSV, keyed by the English text, so code reads `Loc.T("Shop")`. A test
  scans the source for every `Loc.T`/`Loc.F` literal, the master data shown on screen, and the Settings labels, and
  fails if one has no Vietnamese or Japanese row. Japanese uses M PLUS Rounded 1c as a fallback atlas built from
  exactly the characters the table uses.
- **Colour-blind check:** the six piece colours go through a protanopia, deuteranopia and tritanopia simulation
  ([images](colour-blind)). The hues merge, but every colour has its own shape, so no pair depends on colour alone.
- **Game feel:** a victory lap (moves left become rockets and fire before the end card), a bounce and buzz on a
  swap that makes no match, punchy goal counters, rising pitch on cascades.
- **Tests that leave no trace:** the smoke test starts from a fresh save, like a first install, and puts the
  developer's save back afterwards. It captures every screen at 9:16, 20:9 and 4:3, and switches the game to
  Japanese and Vietnamese. The trailer is recorded by a test at a fixed 30 fps (`Time.captureFramerate`), so it
  comes out the same every time.

## Numbers

| | |
|---|---|
| Tests | 134 EditMode in Unity; under dotnet, 104 game tests and 36 template tests; 1 PlayMode smoke test |
| Content | 30 levels, 12 riders (5 powers), 5 areas × 6 tasks, 5 obstacles (crate, ice, chain, oil, trophy), 3 languages |
| Level tuning | 30 × 200 bot games per CI run; every level inside its band (chart above) |
| Frame rate during combos, APK size | Not measured yet: they need the phone and the v1.0.0 release build |

*Inspired by Royal Match. Original art, characters, levels and the rider-booster twist are mine.*
