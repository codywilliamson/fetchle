# Changelog

## 1.0.0 (2026-10-09)


### Features

* add cli shell with command stubs and output mode detection ([59bb96b](https://github.com/codywilliamson/fetchle/commit/59bb96b128507bf3eeb434113ef730951bf386a1))
* add naive end-to-end search behind IFileSearch ([b9b4f1e](https://github.com/codywilliamson/fetchle/commit/b9b4f1e759f4f8a2ff4fdd3987042428ea716da0))
* add remotion hype video for fetchle ([3ef68a0](https://github.com/codywilliamson/fetchle/commit/3ef68a00bd41bb8a2aaef5b6705b34ad1f0abadd))
* add single-threaded fast walker ([6c73e61](https://github.com/codywilliamson/fetchle/commit/6c73e618bf0ee76d509cc363d302538ff0cd7f2d))
* add stdio mcp server with find_files and index_status ([eb59eb8](https://github.com/codywilliamson/fetchle/commit/eb59eb8e10d08ee6d4f0ca0eaedf95abb6a8faa3))
* add synthesized soundtrack to hype video ([5c642fe](https://github.com/codywilliamson/fetchle/commit/5c642fe71fec8325f8a006107afb819176e45a2b))
* **core:** prune more vcs, package-cache and os-trash directories ([ea05086](https://github.com/codywilliamson/fetchle/commit/ea050865190275bb5a3774411b1c67be6c0d71a4))
* default to the native lister on windows ([61cedb3](https://github.com/codywilliamson/fetchle/commit/61cedb39d2cbbdb0e3558a8341441d78e4f1940e))
* log bench and evals through microsoft.extensions.logging ([c020050](https://github.com/codywilliamson/fetchle/commit/c020050d9fcfa0eab86c7004d36c2db60e4bc493))


### Bug Fixes

* **bench:** don't crash when benchmarkdotnet leaves no results folder ([dd8174a](https://github.com/codywilliamson/fetchle/commit/dd8174add3d7e66be847d5ec7e5418cb0e2dfc12))
* **bench:** fail the external bench on unexpected exit codes ([8c28c0b](https://github.com/codywilliamson/fetchle/commit/8c28c0b1852bb1d2023c1e33d2a47187dc8d29c2))
* **bench:** give every corpus directory exactly its share of files ([de78479](https://github.com/codywilliamson/fetchle/commit/de7847923a6deaca6c7e2fba6557af3b9dff1925))
* **bench:** point the missing-exe hint at dotnet build.cs publish ([e978247](https://github.com/codywilliamson/fetchle/commit/e978247334372e33a277cf29e2fc2b7b474269a1))
* **build:** detect windows in build.ps1 without $IsWindows ([254e74c](https://github.com/codywilliamson/fetchle/commit/254e74c40697945514fbd2c8d10be5b98c065ded))
* cap deadlines against the timestamp range, not a TimeSpan guess ([c95ee08](https://github.com/codywilliamson/fetchle/commit/c95ee085bceb97b8c8181217ea6373872f5cae48))
* **cli:** clamp --since cutoffs older than DateTimeOffset.MinValue ([48fcb8a](https://github.com/codywilliamson/fetchle/commit/48fcb8a26e1aaf7704a9582758e08d35d06eb3e4))
* **cli:** treat durations too large for TimeSpan as usage errors ([feab855](https://github.com/codywilliamson/fetchle/commit/feab85571ffd9a67313629f53ecd4c7014d2edc1))
* list every entry the .NET lister would, including virtualized ones ([7902017](https://github.com/codywilliamson/fetchle/commit/7902017c733c000dc54a19b41c8b814a37b72892))
* **mcp:** pass the request cancellation token into search ([3745be9](https://github.com/codywilliamson/fetchle/commit/3745be9782ac1672076f19e1d33685a3f4696a36))
* **mcp:** reject explicit null arguments instead of using defaults ([bf1bbce](https://github.com/codywilliamson/fetchle/commit/bf1bbceda3da9e3cca066635c820365452ea1b32))
* **mcp:** reject unknown tool arguments as invalid params ([4363052](https://github.com/codywilliamson/fetchle/commit/4363052d44a180ae88086650c72f875ea6a55192))
* open native walk roots case-insensitively like CreateFileW ([22e5bd5](https://github.com/codywilliamson/fetchle/commit/22e5bd5c2929b78a57c86bdd1380988649151ffd))
* reject cli --limit below 1 as a usage error ([58abda3](https://github.com/codywilliamson/fetchle/commit/58abda37addbf9ad4c216956beb18b4f195e5a1e))
* report an expired budget on empty roots ([7ad5674](https://github.com/codywilliamson/fetchle/commit/7ad5674d00de864e3366a9311d94744b7593ae60))
* skip dirs the native lister can't open relatively instead of reopening by full path ([4b805ec](https://github.com/codywilliamson/fetchle/commit/4b805ec9b1e0c175e8fd4bff944b9a166b8c240a))
* stop the .NET lister visiting entries after the budget expires ([1bd8b07](https://github.com/codywilliamson/fetchle/commit/1bd8b076eceaa23fd954d664f2e66ba2c8c63bc0))
* **test:** don't assert 0ms in the budget footer ([5fcd396](https://github.com/codywilliamson/fetchle/commit/5fcd3962ecc072f28fb2d09c92dba1bb67138070))
* **test:** resolve symlinks in fixture root so macos /var matches the server cwd ([fea26ee](https://github.com/codywilliamson/fetchle/commit/fea26eef92b5531ad85411ab460615fc72f54a32))
* turn invalid root syntax into a usage or invalid-params error ([d57c83c](https://github.com/codywilliamson/fetchle/commit/d57c83ccab81308fee994fb8380bca0c9c092689))


### Performance Improvements

* back work deque with a ring buffer ([c864b9d](https://github.com/codywilliamson/fetchle/commit/c864b9d1b200dff2df1472156897722ed399aa24))
* keep only the top limit hits during the naive walk ([bf55df0](https://github.com/codywilliamson/fetchle/commit/bf55df073246900db833ac7ab933c7c04df2c3a5))
* list dirs with a FileSystemEnumerator subclass ([8f1c929](https://github.com/codywilliamson/fetchle/commit/8f1c9298b37ad0fe0b0446050f0d08b02b82eb38))
* list windows dirs with relative nt opens ([521a1b0](https://github.com/codywilliamson/fetchle/commit/521a1b0b2282902c277be5ee364ab2a07e178fb9))
* search with the parallel walker and per-worker top-k ([b259a57](https://github.com/codywilliamson/fetchle/commit/b259a577966598caa5903dec5480d5f9d3b8465b))
* walk dirs in parallel with work-stealing deques ([f958f45](https://github.com/codywilliamson/fetchle/commit/f958f4528bcd585319e601a9cd2a43b85eb8722b))
