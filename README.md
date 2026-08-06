<p align="center">
  <img src="docs/art_batch_encoder.png" width="144" alt="ART Batch Encoder icon">
</p>

# ART Batch Encoder v1.0

Windows GUI for scanning ART JSON manifests and encoding numbered image sequences as videos or multilayer OpenEXR sequences.

## Features

- Single JSON or recursive all-takes folder scanning; JSON filenames are unrestricted.
- Video output: ProRes, DNxHR, H.264, HEVC, AV1, and FFV1.
- Multilayer OpenEXR output: selected passes become named channel layers in one sequence per take.
- EXR compression: ZIP, ZIPS, PIZ, RLE, PXR24, B44, B44A, DWAA, DWAB, or uncompressed.
- Adjustable ZIP/ZIPS and DWA compression levels.
- Optional NVENC, AMF, and Intel QSV video acceleration; GPU mode is off by default.
- Per-take FPS or **Override FPS** for video output.
- Sequence removal, batch progress, cancellation, and transactional screenshot deletion.
- Portable settings in `artbe_settings.ini` beside the executable.

## Run

1. For video, place `ffmpeg.exe` in `ffmpeg`.
2. For multilayer EXR, place `oiiotool.exe` and its OpenImageIO DLL files in `openimageio`.
3. Run `ART Batch Encoder.bat`.
4. Select one JSON file or an ART recording root and click **Scan**.
5. Select **Video files** or **OpenEXR multilayer sequence**, configure the output, and click **Encode selected**.

Video files are written beside their source sequences. Multilayer EXR output is written to:

```text
%take_path%\EXR\%take%_%0Nd.exr
```

`%take%` is replaced with the take name from the ART manifest, with filename-unsafe characters normalized.

All selected layers in one take must have the same contiguous frame range. The generated `EXR` folder is excluded from later ART source scans.

## Executable search order

FFmpeg:

1. `<exe>\ffmpeg\ffmpeg.exe`
2. Saved or manually selected path
3. Beside the executable
4. `tools\ffmpeg.exe`
5. Windows `PATH`

OpenImageIO:

1. `<exe>\openimageio\oiiotool.exe`
2. `<exe>\openimageio\bin\oiiotool.exe`
3. Saved or manually selected path
4. Beside the executable
5. `tools\oiiotool.exe`
6. Windows `PATH`

Runtime binaries are optional source-local dependencies. Put them in the directories above before building when they should be included in the packaged release.

## Build and package

Run `build.bat`. It compiles the application, stages the local runtimes, and creates the final release in `dist`:

```text
dist\ART_Batch_Encoder_v1.0\
dist\ART_Batch_Encoder_v1.0.zip
```

Packaging rules:

- Everything under `ffmpeg\` is copied to the release `ffmpeg\` folder.
- Everything under `openimageio\` is copied to the release `openimageio\` folder.
- When either source runtime is absent, its release folder contains only `README.txt`.
- `bin\` is intermediate compiler output; `dist\` is the final build folder.

Release builds from `ARTBatchEncoder.sln` also run `package.ps1` automatically. The script can be run again manually after a successful compile when only the archive needs to be refreshed.

The project targets .NET Framework 4.8 and does not use NuGet packages.

## Source layout

- `MainForm.*.cs` — interface, discovery, list actions, video/EXR encoding, and lifecycle.
- `ManifestReader.cs`, `BatchManifestReader.cs` — manifest and recursive batch discovery.
- `Codec*`, `GpuSupport.cs` — video codecs and hardware encoder selection.
- `OpenExrJob.cs`, `ExrCompressionProfile.cs`, `OpenImageIoSupport.cs` — multilayer EXR jobs and OpenImageIO integration.
- `SettingsStore.cs` — portable INI persistence.
