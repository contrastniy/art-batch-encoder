FFmpeg runtime folder
=====================

Place ffmpeg.exe and any accompanying FFmpeg runtime files in this folder.

Expected path:
  ffmpeg\ffmpeg.exe

During build, the complete contents of this directory are copied into the
release package under ffmpeg\. If no runtime files are present, the packaged
folder contains only this README.
