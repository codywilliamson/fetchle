namespace Fetchle.Core.Index;

// one subtree of the snapshot to search, spelled the way the caller passed its root
readonly record struct IndexScope(int Entry, string Path);
