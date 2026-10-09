# Spike: native directory listers

Research and measurements behind the per-OS lister seam (Windows built, Linux and macOS not yet). Windows 11, Ryzen 7 5700 (8 cores, 16 threads), Defender real-time protection on. Sources were fetched and read; anything else is marked as a guess.

## Verdict

On Windows, a native lister ties .NET's `FileSystemEnumerator` on speed and wins on allocations (~30% less) and safety (child opens are relative to the parent handle with links refused). It's the Windows default. Linux and macOS listers are researched below but not built; expect the same speed story, so build them for the safety property, and only keep one if a bench on that OS shows it isn't slower.

## Windows measurements

Single-threaded, per directory, 47,821-dir corpus:

| step | µs |
|---|---|
| full listing, .NET enumerator | 169–200 |
| open + close by full path (`CreateFileW`) | 129–168 |
| open + close relative to the parent handle (`NtCreateFile`) | 95–120 |
| close alone | ~12 |

A full native listing (open, `NtQueryDirectoryFileEx` until `STATUS_NO_MORE_FILES`, close) came to ~173 µs and stayed within ~2% across every variant tried: 4/16/64 KB buffers, `NtQueryDirectoryFile` vs the `Ex` form, info class 1 vs 2, with or without `OBJ_DONT_REPARSE`, `FILE_OPEN_REPARSE_POINT`, `FILE_OPEN_FOR_BACKUP_INTENT` and `SL_RETURN_ON_DISK_ENTRIES_ONLY`. Two query calls per directory (one with entries, one ending), always.

With 16 workers both listers land at ~1.2 s for the corpus. Something below the walker serializes; guess: kernel or filter-driver contention. Confirming it needs an admin ETW trace.

## Windows choices

- Open: `NtCreateFile` with `RootDirectory` = parent handle and a single-component name, `FILE_LIST_DIRECTORY | SYNCHRONIZE`, share read/write/delete, `FILE_OPEN`, `FILE_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT | FILE_OPEN_REPARSE_POINT`, attributes `OBJ_DONT_REPARSE`. Rust's `remove_dir_all` uses the same pattern.
- Roots open by `\??\` path with `OBJ_CASE_INSENSITIVE`, which `CreateFileW` always passes. Children keep exact names, which also suits per-directory case-sensitive folders.
- No `FILE_OPEN_FOR_BACKUP_INTENT`: with the backup privilege enabled it skips ACL checks, and it bought nothing measurable.
- No `SL_RETURN_ON_DISK_ENTRIES_ONLY`: on virtualized roots it hides projected entries the .NET enumerator shows.
- No full-path fallback for a failed child open. A child that can't be opened relative to its parent is skipped like any other failed open, so no open ever re-parses a path a junction could hijack.
- Query: `NtQueryDirectoryFileEx`, `FileDirectoryInformation` (name at offset 64), 64 KB buffer per worker (the size git for Windows' fscache uses), parsed in place by `NextEntryOffset`, entries with `FILE_ATTRIBUTE_REPARSE_POINT` never descended.
- Defender scans synchronously at pre-create. The only documented relief is Dev Drive performance mode, an admin volume setting, so the app-side levers are fewer opens (pruning) and cheaper ones.

Sources: [NtQueryDirectoryFileEx](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/nf-ntifs-ntquerydirectoryfileex), [ZwCreateFile](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/wdm/nf-wdm-zwcreatefile), [OBJECT_ATTRIBUTES](https://learn.microsoft.com/en-us/windows/win32/api/ntdef/ns-ntdef-_object_attributes), [git fscache.c](https://raw.githubusercontent.com/git-for-windows/git/main/compat/win32/fscache.c), [Rust remove_dir_all](https://raw.githubusercontent.com/rust-lang/rust/master/library/std/src/sys/fs/windows/remove_dir_all.rs), [Defender performance mode](https://learn.microsoft.com/en-us/defender-endpoint/microsoft-defender-endpoint-antivirus-performance-mode).

## Linux (not built)

- Open: `openat(parent_fd, name, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC)`. Skip `O_NOATIME`, which returns `EPERM` unless the caller owns the file.
- List: `getdents64` (glibc wrapper since 2.30) into a reused 64–256 KB buffer (guess; glibc's own readdir uses `MIN(MAX(st_blksize, 32K), 1M)`). Record: `d_ino` u64 @0, `d_off` s64 @8, `d_reclen` u16 @16, `d_type` u8 @18, name NUL-terminated @19; advance by `d_reclen`. `DT_UNKNOWN` needs `fstatat(..., AT_SYMLINK_NOFOLLOW)`.
- io_uring has no getdents in mainline, so skip it. bfs 3.0 got its speed from I/O threads instead: 2.4 s vs fd 8.2 s vs find 7.0 s on 7.6M files.
- Constants differ per arch: x64 `O_DIRECTORY 0x10000`, `O_NOFOLLOW 0x20000`; arm64 `0x4000`, `0x8000`; `O_CLOEXEC 0x80000` on both; `AT_FDCWD -100`.

## macOS (not built)

- Open the same way; constants `O_NOFOLLOW 0x100`, `O_DIRECTORY 0x100000`, `O_CLOEXEC 0x1000000`, `AT_FDCWD -2`. `ELOOP` is 62, not Linux's 40.
- List with `fdopendir` on a `dup` of the fd plus `readdir`. One benchmark found plain readdir faster than `getattrlistbulk` on APFS for names and types (2019, re-measure). The 64-bit-inode `dirent` puts the name at offset 21 with `d_namlen` at 18 (guess, verify with a test).

## Both

- Keep each parent fd open while its children are queued (ref count, close at zero), cap open fds near half of `RLIMIT_NOFILE`, and skip rather than reopen by full path when the cap or `EMFILE` hits.
- Treat `EACCES`, `EPERM`, `ENOENT`, `ENOTDIR` and `ELOOP` as skip; retry `EINTR`.
- Names that aren't valid UTF-8 decode with U+FFFD for display; reopen by the original bytes.

Sources: [getdents(2)](https://man7.org/linux/man-pages/man2/getdents.2.html), [open(2)](https://man7.org/linux/man-pages/man2/open.2.html), [openat2(2)](https://man7.org/linux/man-pages/man2/openat2.2.html), [bfs 3.0](https://tavianator.com/2023/bfs_3.0.html), [bfs 1](https://tavianator.com/2016/bfs_1.html), [glibc opendir.c](https://raw.githubusercontent.com/bminor/glibc/master/sysdeps/unix/sysv/linux/opendir.c), [xnu fcntl.h](https://raw.githubusercontent.com/apple-oss-distributions/xnu/main/bsd/sys/fcntl.h), [getattrlistbulk](https://www.manpagez.com/man/2/getattrlistbulk/), [APFS readdir timings](http://blog.tempel.org/2019/04/dir-read-performance.html).
