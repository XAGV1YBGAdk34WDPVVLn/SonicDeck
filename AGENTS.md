# SonicDeck — Project Agent Instructions

## Mandatory End-to-End Release Pipeline

Whenever you finish making code changes to SonicDeck, **NEVER stop at `dotnet build`**.
The user launches SonicDeck via **`Win + R` → `sonicdeck`**, Start Menu, or `run-sonicdeck.bat`, all of which execute `publish\SonicDeck.exe`.

After any code change, always run the end-to-end release pipeline:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1
```

What `.\release.ps1` does automatically every time:
1. Locates `dotnet.exe` (works even when `dotnet` is only in `%USERPROFILE%\.dotnet\dotnet.exe`).
2. Verifies a clean `Release` build *before* touching any running instance.
3. Stops any running `SonicDeck` process so `publish\SonicDeck.exe` is never file-locked.
4. Publishes a fresh self-contained `win-x64` build to `.\publish\`.
5. Ensures Windows `HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\sonicdeck.exe` (`Win+R` → `sonicdeck`) and the Start Menu shortcut point to `publish\SonicDeck.exe`.
6. Relaunches `publish\SonicDeck.exe` so the user immediately sees the updated build (pass `-NoLaunch` only if explicitly requested).

If the user also asks to commit and push, pass `-Commit "<message>" -Push`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1 -Commit "Your commit message" -Push
```

## Environment & Git Notes

- **.NET SDK:** If `dotnet` is not on PATH, use `C:\Users\oz_co\.dotnet\dotnet.exe` (handled automatically by `release.ps1`).
- **Git:** Native Windows `git.exe` may not be installed; `wsl git` is available and used automatically by `release.ps1`.
- **Never commit `.agents/`:** Local agent metadata lives in `.agents/` and must never be staged or committed.

## Graphics / SkiaSharp 2.88.8 Architecture Note

- WPF `SKElement` renders on CPU raster surfaces.
- On Windows x64, `SkiaSharp 2.88.8` (`libSkiaSharp.dll` m88) crashes with `SEHException` when drawing `SKRuntimeEffect` (SkSL) shaders on CPU raster surfaces.
- For high-FPS shader-style visualizers (like `CymaticSingularityEngine` and `EventHorizonEngine`), use `unsafe` multithreaded `Parallel.For` pixel kernels writing directly to scaled `SKBitmap` buffers (`AllowUnsafeBlocks` is enabled in `SonicDeck.csproj`), combined with full-resolution Skia vector/additive layers (`SKBlendMode.Plus`) instead of expensive full-screen CPU `SKMaskFilter.CreateBlur`.
