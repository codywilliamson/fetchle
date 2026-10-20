namespace Fetchle.Fixtures;

// vocab for the home corpus. "settings" is deliberately absent so the query only hits the landmarks
static class HomeWords
{
    public static readonly string[] CodeDirs =
    [
        "src", "lib", "app", "core", "utils", "components", "services", "models", "views", "tests", "docs", "scripts", "config",
        "common", "shared", "api", "pages", "hooks", "types", "assets", "styles", "helpers", "middleware", "controllers",
        "handlers", "plugins", "bin", "obj", "build", "dist", "examples", "fixtures", "migrations", "templates", "data",
    ];

    public static readonly string[] AppDirs =
    [
        "Cache", "Code Cache", "GPUCache", "Local Storage", "Session Storage", "IndexedDB", "blob_storage", "Network", "Crashpad",
        "logs", "User Data", "Default", "Profiles", "Extensions", "databases", "Service Worker", "CacheStorage", "ScriptCache",
        "Dictionaries", "Platform", "plugins", "updates", "resources", "locales", "state", "storage", "backups", "temp",
    ];

    public static readonly string[] MediaDirs =
    [
        "2019", "2020", "2021", "2022", "2023", "2024", "holiday", "work", "school", "taxes", "receipts", "projects", "archive",
        "scans", "family", "invoices", "drafts", "reports", "albums", "screenshots", "wallpapers", "exports", "backup", "misc",
    ];

    public static readonly string[] NpmDirs =
    [
        "lib", "dist", "esm", "cjs", "src", "types", "bin", "build", "core", "utils", "internal", "runtime", "dom", "node",
        "browser", "plugin", "parser", "loader", "compat", "polyfill", "shim", "stream", "async", "lodash", "babel", "react",
        "webpack", "eslint", "chalk", "debug", "semver", "glob", "minimatch", "yargs", "express", "axios", "uuid", "tslib",
    ];

    public static readonly string[] CacheDirs = ["thumbs", "http", "objects", "blobs", "v1", "v2", "tmp", "index", "chunks", "layers"];

    public static readonly string[] Vendors = ["Adobe", "JetBrains", "Microsoft", "Mozilla", "Google", "Steam", "Slack", "Zoom", "Docker", "Postman", "Figma", "Notion", "Epic", "Valve", "Brave", "Opera", "Telegram", "Signal"];
    public static readonly string[] Products = ["Shared", "Launcher", "Client", "Studio", "Desktop", "Runtime", "Updater", "Helper", "Browser", "Suite", "Tools", "Engine"];
    public static readonly string[] RepoNames = ["portal", "billing", "dashboard", "gateway", "mobile", "docs-site", "infra", "analytics", "scheduler", "notifier", "auth", "search", "importer", "reports", "admin", "storefront", "sdk", "cli", "worker"];
    public static readonly string[] CacheApps = ["pip", "huggingface", "yarn", "pnpm", "typescript", "mesa_shader_cache", "thumbnails", "go-build", "nuget", "torch", "electron"];
    public static readonly string[] PackageRoots = [".nuget/packages", ".vscode/extensions", ".cargo/registry/src", "go/pkg/mod", ".rustup/toolchains", ".m2/repository", ".conda/pkgs"];
    public static readonly string[] MediaRoots = ["Documents", "Documents", "Downloads", "Pictures", "Music", "Videos"];

    public static readonly string[] Stems =
    [
        "alpha", "build", "delta", "event", "frame", "graph", "index", "layer", "model", "parse", "query", "route", "store", "token",
        "video", "audio", "chunk", "sheet", "theme", "panel", "cursor", "buffer", "socket", "worker", "render", "scene", "shader",
        "mesh", "texture", "input", "output", "format", "reader", "writer", "client", "server", "session", "account", "profile",
        "report", "export", "import", "filter", "mapper", "loader", "cache", "queue", "timer", "logger", "metric", "policy",
    ];

    public static readonly string[] NpmStems = ["index", "package", "README", "LICENSE", "CHANGELOG", "types", "utils", "helper", "main", "core", "parser", "stream", "format", "compat", "browser", "node", "async", "loader"];

    public static readonly string[] Dotfiles = [".gitignore", ".editorconfig", ".eslintrc.json", ".prettierrc", ".env", ".npmrc", ".DS_Store", ".babelrc", ".dockerignore", ".gitattributes"];

    // home dir dotfiles, always present
    public static readonly string[] HomeDotfiles = [".bashrc", ".gitconfig", ".npmrc", ".viminfo", ".bash_history", ".profile", ".lesshst"];

    // weight is relative within a table
    public static readonly (string Ext, int Weight)[] CodeExts = [(".cs", 22), (".ts", 18), (".js", 16), (".json", 12), (".md", 8), (".py", 6), (".css", 5), (".html", 4), (".xml", 4), (".txt", 3), (".yml", 2)];
    public static readonly (string Ext, int Weight)[] NpmExts = [(".js", 40), (".json", 14), (".md", 10), (".ts", 8), (".map", 12), (".d.ts", 10), (".mjs", 3), (".cjs", 3)];
    public static readonly (string Ext, int Weight)[] DataExts = [(".dat", 14), (".db", 8), (".log", 12), (".json", 14), (".ldb", 10), (".pak", 6), (".dll", 10), (".exe", 3), (".png", 8), (".xml", 6), (".bin", 5), (".txt", 4)];
    public static readonly (string Ext, int Weight)[] MediaExts = [(".jpg", 38), (".png", 22), (".mp4", 6), (".mp3", 12), (".gif", 5), (".webp", 5), (".heic", 4), (".mov", 3), (".svg", 5)];
    public static readonly (string Ext, int Weight)[] DocumentExts = [(".pdf", 34), (".docx", 18), (".xlsx", 12), (".txt", 12), (".md", 6), (".pptx", 6), (".csv", 8), (".zip", 4)];
}
