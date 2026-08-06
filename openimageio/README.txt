OpenImageIO runtime folder
==========================

Multilayer OpenEXR output requires a Windows OpenImageIO runtime.
Place oiiotool.exe and every DLL or support file distributed with it here.

Expected path:
  openimageio\oiiotool.exe

Also supported:
  openimageio\bin\oiiotool.exe

During build, the complete contents of this directory are copied into the
release package under openimageio\. If no runtime files are present, the
packaged folder contains only this README.
