# Held-out failure table

Every held-out query whose expected path wasn't ranked first, with the stage that lost it. The ranks come from the same corpus run three ways: fused (what ships), lexical only and semantic only (`--lexical-only`, `--semantic-only`), so a row can say which retriever had it and which one cost it. `-` means not in the top 10.

Fixes go to classes, never to one query, and every fix has to hold on dev first (docs/decisions.md, 2026-10-21). Kinds: name, partial, descriptive.

## Current: v0.2 at the held-out baseline

Held-out, 81 queries: top-1 79.0%, top-3 93.8%, MRR 0.864. Lexical alone on the same queries: 81.5%, 93.8%, 0.875. Semantic alone: 14.8%, 23.5%, 0.214.

| id | query | kind | fused | lexical | semantic | stage | why |
|---|---|---|---|---|---|---|---|
| b-002 | keyboard shortcuts i set in vs code | descriptive | - | - | - | semantic | keybindings.json shares no word with the query, and its path centroid (keybindings, User, Code, Roaming) doesn't come near "keyboard shortcuts" at 512 sign bits. "vs code" splits into vs + code, so lexical only finds Code/ itself |
| b-007 | thunderbird email search index | descriptive | - | - | - | semantic, noise | thunderbird matches the dir; "index" matches thousands of Index.* filler files that outrank the profile's global-messages-db.sqlite, whose name says neither email nor search. semantic doesn't connect messages db with email index |
| b-037 | how the ticket agent sends push notifications | descriptive | - | - | - | tokenizer, semantic | the file is ntfy.ts: an abbreviation the WordPiece vocabulary splits into junk, so neither retriever reads it as notify. lexical finds the notify/ dir and its other file costAlert.ts |
| b-058 | script behind the pet whistle item | descriptive | 2 | 2 | 1 | fusion | semantic had whistle.js first ("script"), lexical had items/whistle.json first ("item"); lexical's rank 1 won the fusion |
| m-017 | ticket-prep skill qs-main | partial | 3 | 3 | 3 | depth | the ticket-prep dirs (.agents and .claude copies) match every word and sit one level above SKILL.md, which is what was wanted |
| m-052 | attest workspace detail page | descriptive | 4 | 1 | 8 | fusion | lexical had page.tsx first; semantic voted for other workspace files, and three of them collected both votes |
| m-066 | service that records review decisions | descriptive | 4 | 1 | - | fusion | lexical first; semantic's candidates were monocle's reviewRecords.ts copies, which also had lexical rank, so fused they passed the target |
| m-087 | accordion script on qs-main product pages | descriptive | 2 | 1 | 4 | fusion | product-accordion.php and .js tie on words; semantic preferred the .php |
| m-091 | monocle-review-hook | name | 3 | 3 | - | copies | three dirs with this exact name: a plugin cache copy and a plugins/ copy, both shallower than the integrations/ source |
| m-098 | monocle walkthrough video | descriptive | 3 | 5 | 5 | copies, depth | walkthrough.mp4 at the repo root (a flattened copy, not in the expected set) and a .gif rank above the three accepted copies |
| m-102 | CommentEditor.svelte | name | 2 | 2 | - | copies, depth | a flattened copy of the file at the repo root is four levels shallower than the real one |
| m-110 | brindlee.mdx | name | 2 | 2 | 5 | copies, depth | same: a root-level copy beats src/data/blogs/brindlee.mdx on depth |
| m-134 | qs-main compiled app css | descriptive | 2 | 2 | 2 | depth | app.css at the theme root and css/app.css tie on words; nothing in the query says compiled output lives in css/ |
| m-161 | rts s1 review html | partial | 2 | 2 | 3 | copies, depth | a copy at source/spectacl3/ outranks the two accepted copies on depth |
| m-167 | rts-l2-larger.html | name | 2 | 2 | 2 | copies, depth | same as m-161 |
| m-201 | event notifications scss | partial | 2 | 2 | 1 | copies, depth | css/admin/events/... and app/static/admin/css/admin/events/... hold the same file; the shallower one isn't the accepted one |
| m-221 | potion embeddings spike | descriptive | 2 | 1 | - | fusion | lexical had the Spike/ dir first; a file inside it (paths-pruned.txt) took both votes |

### By class

| class | rows | top-3 misses | what would fix the class |
|---|---|---|---|
| copies and depth | 9 | 1 (m-098) | knowing which copy is canonical: recency, git roots, or "is this inside a repo's src". depth alone prefers the flattened copy |
| fusion displaces a lexical first place | 5 | 1 (m-052) | semantic adds nothing measurable at the top on this eval (see below); a fusion that only lets semantic add candidates traded 1.8 points of dev top-1 for 0.9 of top-3, so it wasn't taken |
| semantic vocabulary gap | 3 | 3 | a stronger model or query expansion; the 512-bit codes cost a little here too (float centroids rank some of these higher) |

## Round 1: before the class fixes

Read once after dev tuning stopped improving. Held-out was top-1 75.3%, top-3 90.1%, MRR 0.832. Two classes stood out and both were checked on dev before held-out was read again.

| class | rows | fix | dev before -> after |
|---|---|---|---|
| a query that says "folder" wants a dir | m-045 (fused -, lexical 1), m-168 (8, 1) | folder/directory/dir become intent: files score half and the word isn't a term | 77.2 / 88.2 / 0.831 -> 78.1 / 89.0 / 0.840 |
| semantic outvotes a dir that matched every word | m-152 (4, 1), m-221 (2, 1) | a dir with full coverage counts as "lexical covered" again, which turns semantic off for the query | no change on dev; it undid a dev-neutral rule added earlier |

After both: held-out 79.0 / 93.8 / 0.864.
