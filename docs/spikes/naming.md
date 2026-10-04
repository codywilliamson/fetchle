# Naming spike: local file finder for agents

Checked 2026-10-03. Registry checks were HTTP status codes against the public APIs (404 = free), .dev via Google's registry RDAP (`pubapi.registry.google/rdap/domain/<n>.dev`), .sh/.app via rdap.org, GitHub via `gh api search/repositories` filtered to exact name, distro packages via Repology. Local collision check via `Get-Command` and `Get-Alias` in pwsh 7.

## The fwip problem

fwip is out, and not because of the registries. `fwip.dev` is registered (last changed 2026-02-19, expires 2027-03) and fwip.app sells "17 developer tools, $49 once, nothing sent to servers". That is a local-first, privacy-pitched dev tool brand. Same niche, same pitch, already live. PyPI `fwip` is also taken (a prompt optimizer). The GitHub user `fwip` is taken.

Snaff/snaffle are also dead: Snaffler is a well known C# pentest tool that finds juicy files on Windows shares. Exact same verb, same platform, wrong reputation.

## Brainstorm (75)

Speed sounds: fwip, fwoop, fwish, fwoosh, fwap, fwit, shwip, vwip, zwip, thwip, zoop, plip, blip, snick, flik, boop, boof, zing, yoink, yoik.

Sniffers and mascots: snoot, snout, snouf, snorf, snufl, snuffle, sniffl, sniffy, snuffy, snoofy, whiff, whiffle, trufl, truffl, trufo, oinky, piggle, pigsy, hogle, rootle, rootl, ferrit, frrt, fennec, magpy, nosey, nozl, beagle, wisp.

Retrieval verbs: filch, snarf, snaff, snaffle, nab, nabbit, pluk, fetchy, fetchit, fossick, grubble, spoor, rummy, yoinkle, lookit, spotto, tadah.

Diffle-style coined: fetchle, findle, foundle, pathle, piggle, whiffle, yoinkle, hogle, pippit.

Backronyms tried: FWIP (files where i put 'em), PAWS (path answers without scanning), LOCI (local content index, method of loci), FIDO (files in, directory out; collides with FIDO2 auth), NOSE (taken by the python test runner), FLIP (find local files in place). .NET nods (dotf, sharpnose, netsy) all read as cringe or unsearchable and were dropped early.

## Cut fast, and why

- Overloaded or owned: zoop, wisp, fennec, yip, paws, loci, spoor, flik, fetchy, snick, boof, rootle (all have taken npm + crates + PyPI and/or .dev).
- Spelling fails when spoken: trufl/truffl/trufo (everyone types "truffle"), snouf, nozl, frrt, sniffl, magpy, rootl.
- Real-world baggage: thwip (Spider-Man sound, .dev taken), yoik (a Sami singing tradition, also "joik"), pigsy (Journey to the West), hogle (reads as Hoggle from Labyrinth), foundle (sounds like fondle), yoinkle (Urban Dictionary entry you do not want in a README), filch (means "steal"; terrible for a privacy tool, npm taken).
- Too long or no hook: fossick, whiffle, fwoosh, fetchit, lookit, tadah, zoomie, pippit.

## Scored shortlist (1-5, higher is better; collision is "low risk = 5")

| name    | type | memorable | vibe | mascot/video | collision | notes |
|---------|------|-----------|------|--------------|-----------|-------|
| fetchle | 3 | 5 | 5 | 4 | 5 | diffle sibling, nothing anywhere |
| fwoop   | 5 | 4 | 5 | 5 | 4 | fwip's surviving cousin |
| nabbit  | 4 | 5 | 4 | 5 | 3 | Nintendo has a rabbit named Nabbit |
| shwip   | 5 | 3 | 4 | 4 | 4 | small 2021 Steam game |
| oinky   | 5 | 4 | 3 | 5 | 4 | truffle pig, maybe too cute |
| spotto  | 4 | 4 | 4 | 3 | 3 | many consumer apps named Spotto |
| snorf   | 5 | 3 | 4 | 4 | 3 | crates taken, GH user active |
| snufl   | 4 | 2 | 4 | 4 | 5 | heard as "snuffle" |
| whiffle | 3 | 3 | 4 | 3 | 3 | whiffle ball, 7 letters |
| findle  | 3 | 2 | 3 | 2 | 4 | sounds like Kindle |
| pathle  | 3 | 2 | 3 | 2 | 5 | too derivative of diffle |
| filch   | 5 | 5 | 2 | 4 | 3 | "steal" meaning kills it |

## Availability for the finalists

| name    | NuGet | npm | brew | crates | PyPI | scoop | .dev | .sh | .app | GH user/org | exact GH repos | distro pkg |
|---------|-------|-----|------|--------|------|-------|------|-----|------|-------------|----------------|------------|
| fetchle | free | free | free | free | free | free | free | free | free | free | 0 | none |
| fwoop   | free | free | free | free | free | free | free | free | free | taken (idle) | 2 (max 1 star) | none |
| nabbit  | free | taken (2022 utils, dormant) | free | free | free | free | free | free | taken | taken | 11 (max 6 stars) | none |
| shwip   | free | free | free | free | free | free | free | free | taken | free | 1 (0 stars) | none |
| oinky   | free | free | free | free | free | free | free | free | taken | taken | 8 (max 5 stars) | none |
| spotto  | free | free | free | free | free | free | free | - | - | taken | 16 (max 1 star) | none |

None of the finalists resolve as a command or alias on this Windows box. pwsh ships `fw` (Format-Wide) and `fl` (Format-List) as aliases, so don't plan a two-letter short alias starting with those. No coreutils, BSD or Linux binary uses any of these names (Repology returns nothing).

## Top 5

### 1. fetchle

Said "FETCH-ul", rhymes with diffle on purpose. Story: it fetches, and it's from the same shop as diffle. Optional backronym: "find every thing, cache hot, local, eh".

Tagline: "Say what the file is. fetchle brings it back."

Hook: a scruffy terrier that drops the right path at your feet. Video beat: an agent throws a vague description, the dog bolts into a giant file tree, comes back in 4 ms wagging with one path in its mouth. Next to it, a `Get-ChildItem -Recurse` progress bar still crawling.

Weaknesses: 7 letters is one over the target. Spoken aloud some people will write "fetchel" (diffle has the same issue, so it's a known cost). "fetch" alone is generic in search, but "fetchle" is a zero-result word, which is what you want.

### 2. fwoop

Said "fwoop". The sound of something appearing instantly. It keeps everything you liked about fwip without walking into fwip.dev.

Tagline: "fwoop. there it is."

Hook: the name is the sound effect. Every video cut lands on a "fwoop" as the result pops in. Mascot can be anything fast: a little burst, a pneumatic tube.

Weaknesses: GitHub user `fwoop` is taken (idle), so the org would be `fwoop-dev` or similar. Heard aloud it could be "fwup". Close enough to fwip that people who know fwip.app may connect them.

### 3. nabbit

Said "NAB-it". Nab it, plus a rabbit, plus a rabbit pulled out of a hat.

Tagline: "Describe it. nabbit."

Hook: the strongest visual of the bunch. A magician's hat, the agent asks, a rabbit pops out holding the file.

Weaknesses: Nabbit is a Nintendo character (the purple thief rabbit in New Super Mario Bros. U). A rabbit mascot named Nabbit puts you right next to that. npm and .app are taken, GH user taken.

### 4. shwip

Said "shwip". The whip-crack/swipe sound of a fast grab.

Tagline: "find files at shwip speed."

Hook: a quick swipe sound and a motion smear in every result reveal.

Weaknesses: heard aloud it could be "schwip" or "shwhip". There's a small 2021 Steam game called Shwip. Less meaning on its own than the others.

### 5. oinky

Said "OINK-ee". A truffle pig that roots out exactly the file you want.

Tagline: "Roots out the file you meant."

Hook: a truffle pig snuffling through a folder tree, then an "oink" when it finds the match. Easily the cutest mascot and the most merch-able.

Weaknesses: silly enough that some people won't take it seriously next to an MCP server. The pig angle is a bit "dirt and mud" for a privacy tool. GH user and .app taken.

## Recommendation: fetchle

It's the only candidate that is free on every single surface checked, including the GitHub user/org and all three domains. It reads instantly (it fetches files, no explanation needed), and it makes diffle and fetchle a visible family, which is worth more for exposure than a slightly shorter command. The extra letter matters less than it looks, because the main typist is the agent, and humans have tab completion.

If the 7 letters bother you more than the family branding helps, take fwoop. It's the better sound for videos and is 5 letters, at the cost of a `fwoop-dev` GitHub org.
