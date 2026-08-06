# Third-party software

ART Batch Encoder invokes FFmpeg for video output and OpenImageIO `oiiotool` for multilayer OpenEXR output.

These runtimes are not stored in the repository by default. During packaging, any files present under `ffmpeg\` and `openimageio\` are copied into the release archive. The person creating a release is responsible for verifying the licenses and redistribution terms of the runtime builds they include.
