# Git LFS

This repository stores large binary assets with Git LFS. Source code, package
locks, and small text files stay in normal Git.

## Setup

Install Git LFS once on each machine:

```powershell
git lfs install
```

After cloning:

```powershell
git lfs pull
```

## Tracked Assets

Rules live in the root `.gitattributes`. They cover common media, design files,
archives, documents, fonts, binary data files, and root-level standalone HTML
exports such as wireframes.

Check whether a file is managed by LFS:

```powershell
git check-attr -a -- "path/to/file"
```

List LFS-managed files already committed:

```powershell
git lfs ls-files
```

## Adding Large Files

Add files normally after the matching LFS rule exists:

```powershell
git add .gitattributes
git add "path/to/large-file"
git commit
```

If a large file was already committed without LFS, do not rewrite shared history
without coordinating with the team. For a local branch that has not been shared,
use:

```powershell
git lfs migrate import --include="path/to/large-file"
```
