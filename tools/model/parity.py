"""regenerates tests/Fetchle.Tests/Core/Embedding/parity.json: what Python model2vec says about a fixed set of
strings, so the C# implementation can be held to it.

each case has the token ids model2vec's tokenizer produces (special tokens off, [UNK] dropped, like
StaticModel.encode does) and the f32 vector model2vec embeds the text to, rounded to f16 and base64 encoded
(null when the text has no known tokens). first16 repeats the head of the vector as plain numbers so a diff is readable.

usage: uv run --with model2vec python -I tools/model/parity.py
"""
import base64
import json
from pathlib import Path

import numpy as np
from huggingface_hub import snapshot_download
from model2vec import StaticModel

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tests/Fetchle.Tests/Core/Embedding/parity.json"
UNK_ID = 1

# the three the tokenizer unit tests also pin
PINNED = [
    "Microsoft.WindowsTerminal_8wekyb3d8bbwe",
    "ConsoleHost_history.txt",
    "claude.exe",
]

PHRASES = """
Program Files (x86)
Documents and Settings
Users
AppData
Roaming
LocalAppData
Local Settings
Application Data
node_modules
.git
.gitignore
.gitattributes
.editorconfig
.vscode
settings.json
launch.json
tasks.json
extensions.json
package.json
package-lock.json
pnpm-lock.yaml
tsconfig.json
tsconfig.base.json
vite.config.ts
webpack.config.js
eslint.config.mjs
README.md
CHANGELOG.md
LICENSE
CONTRIBUTING.md
Dockerfile
docker-compose.yml
Makefile
CMakeLists.txt
build.gradle.kts
pom.xml
Cargo.toml
Cargo.lock
go.mod
requirements.txt
pyproject.toml
setup.py
__init__.py
__pycache__
site-packages
venv
.venv
Fetchle.Core.csproj
Fetchle.Tests.dll
Directory.Build.props
Directory.Packages.props
global.json
packages.lock.json
appsettings.Development.json
Program.cs
StaticModel.cs
SnapshotWriter.cs
FastWalker.cs
bin
obj
Debug
Release
net10.0
win-x64
linux-x64
publish
artifacts
dist
Google Chrome
Chrome User Data
Default
Cookies
Bookmarks
History
Login Data
Web Data
Preferences
Local State
Cache_Data
Code Cache
GPUCache
ShaderCache
Service Worker
Mozilla Firefox
Profiles
places.sqlite
prefs.js
key4.db
extensions.json
Microsoft Edge
Microsoft.PowerShell_profile.ps1
PSReadLine
Windows PowerShell
WindowsPowerShell
Modules
Microsoft.VisualStudio.Code
Code - Insiders
User
globalStorage
workspaceStorage
state.vscdb
keybindings.json
snippets
JetBrains
Rider2025.2
IntelliJIdea2025.1
options
recentProjects.xml
Slack
slack-desktop
Discord
app-1.0.9034
Spotify
Steam
steamapps
common
libraryfolders.vdf
SteamLibrary
Epic Games
Battle.net
Downloads
Desktop
Pictures
Screenshots
Screenshot 2026-03-14 142233.png
IMG_20240615_183045.jpg
DSC_0042.JPG
Videos
Captures
Music
OneDrive
OneDrive - Contoso
Dropbox
Google Drive
iCloudDrive
Documents
My Games
Saved Games
Projects
source
repos
dev
work
notes
Obsidian Vault
Daily Notes
Meeting Notes 2026-09-30.md
Invoice_2026-08_final_v2.pdf
Resume - John Smith.docx
Budget 2026.xlsx
Q3 Planning Deck.pptx
tax_return_2025.pdf
taxes
receipts
backup
backups
old
tmp
temp
Temp
Windows
System32
SysWOW64
drivers
etc
hosts
WinSxS
Fonts
Microsoft.NET
Framework64
v4.0.30319
dotnet
sdk
shared
Microsoft.NETCore.App
Microsoft.AspNetCore.App
nuget
.nuget
packages
.cargo
.rustup
.npm
.pnpm-store
.cache
.config
.ssh
id_ed25519.pub
known_hosts
.bashrc
.zshrc
.profile
.bash_history
.python_history
.lesshst
.claude
.claude.json
CLAUDE.md
AGENTS.md
claude-code
codex
usr
local
bin
lib
lib64
opt
var
log
syslog
nginx
access.log
error.log
docker
containers
overlay2
mnt
home
Windows Terminal
LocalState
settings.json.bak
WindowsApps
Microsoft.WindowsTerminal_1.21.2361.0_x64__8wekyb3d8bbwe
Microsoft.WindowsStore_8wekyb3d8bbwe
MicrosoftTeams
Teams
Zoom
Notion
Obsidian
Postman
Figma
Blender
Unity
UnrealEngine
Godot
Android
Sdk
platform-tools
Xcode
DerivedData
Library
Application Support
Caches
Preferences
com.apple.Safari
DataGrip
SQLServer
mysql
postgresql
data
pgdata
redis
mongodb
terraform
.terraform
kubernetes
.kube
helm
ansible
playbook.yml
inventory.ini
""".strip().splitlines()

# accents, scripts and symbols the normalizer has to handle
SPECIAL = [
    "café menu",
    "Ünïcödé Fïlé",
    "naïve résumé",
    "Ångström",
    "São Paulo",
    "Łódź",
    "Müller_Übersicht_2026.docx",
    "Crème brûlée recipe",
    "ñandú señor",
    "Ελληνικά αρχεία",
    "Привет мир",
    "Tiếng Việt",
    "日本語のファイル",
    "文档 我的",
    "한국어 문서",
    "📁 Projects 🚀",
    "report ✅ final",
    "tab\tseparated\nlines",
    "  leading and trailing   ",
    "UPPER_CASE_CONSTANT",
    "camelCaseIdentifier",
    "snake_case_identifier",
    "kebab-case-identifier",
    "v1.2.3-beta.4+build.5",
    "C:\\Users\\Cody\\source\\repos",
    "/usr/local/share/doc",
    "a" * 120,
    "$HOME/~backup/#tmp/%temp%",
    "🔥🔥🔥",
    "\u200b\u200b",
]


def vector_cell(vector: np.ndarray) -> str:
    return base64.b64encode(vector.astype(np.float16).tobytes()).decode("ascii")


def main() -> None:
    revision = next(line.split()[1] for line in (ROOT / "tools/model/source.txt").read_text().splitlines() if line.startswith("revision "))
    directory = snapshot_download("minishlab/potion-retrieval-32M", revision=revision)
    model = StaticModel.from_pretrained(directory)

    texts = list(dict.fromkeys([*PINNED, *PHRASES, *SPECIAL]))
    cases = []
    for text in texts:
        ids = [i for i in model.tokenizer.encode(text, add_special_tokens=False).ids if i != UNK_ID]
        vector = model.encode([text])[0].astype(np.float32) if ids else None
        cases.append({
            "text": text,
            "ids": ids,
            "first16": None if vector is None else [round(float(v), 6) for v in vector[:16]],
            "vector": None if vector is None else vector_cell(vector),
        })

    assert any(c["vector"] is None for c in cases), "need at least one text with no known tokens"
    OUT.write_text(json.dumps({"revision": revision, "dims": int(model.dim), "cases": cases}, ensure_ascii=False, indent=0) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {OUT} ({len(cases)} cases, {OUT.stat().st_size:,} bytes)")


main()
