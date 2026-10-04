# Making demo videos

Demos are generated from the repo, so they never go stale and Claude can make a new one from a single prompt.

## Two layers

[VHS](https://github.com/charmbracelet/vhs) `.tape` files in `demo/tapes/` script terminal sessions and render them deterministically to GIF and MP4. They run on Linux in CI, which means pwsh-on-Linux for any `Get-ChildItem` comparison shot.

A small [Remotion](https://www.remotion.dev) project in `demo/video/` composes those clips into product videos: captions, zooms, and benchmark charts read straight from the committed `bench/` JSON. The video's numbers are the CI's numbers.

## Making one for a feature

1. Write `demo/tapes/<feature>.tape` showing the feature on the fixture corpus, not a real home dir, so no personal paths end up on screen.
2. Render it: `vhs demo/tapes/<feature>.tape`.
3. Add a Remotion composition in `demo/video/src/<feature>.tsx` that places the clip and any captions or charts.
4. Render: `pnpm --dir demo/video exec remotion render <Feature> out/<feature>.mp4`.

The repo skill `.claude/skills/make-demo/SKILL.md` encodes these steps plus the house style, so "make a video for v0.3" is one prompt.

## Rules

- Fixture corpus only. Never record a real home directory.
- Numbers on screen come from committed bench or eval JSON, never typed by hand.
- `demos.yml` re-renders every tape on release and attaches the GIFs, so the README always shows the current behavior.
