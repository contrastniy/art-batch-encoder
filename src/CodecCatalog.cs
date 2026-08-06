using System.Collections.Generic;

namespace ArtBatchEncoder
{
    internal static class CodecCatalog
    {
        public static List<CodecProfile> CreateAll()
        {
            return new List<CodecProfile>
            {
                CreateProRes("prores_proxy", "Apple ProRes 422 Proxy", 0, "yuv422p10le", false,
                    "Small ProRes editing proxy. CPU encoding only."),
                CreateProRes("prores_lt", "Apple ProRes 422 LT", 1, "yuv422p10le", false,
                    "Lightweight 10-bit ProRes for editing. CPU encoding only."),
                CreateProRes("prores_422", "Apple ProRes 422", 2, "yuv422p10le", false,
                    "Standard 10-bit ProRes 422 editing master. CPU encoding only."),
                CreateProRes("prores_422_hq", "Apple ProRes 422 HQ", 3, "yuv422p10le", false,
                    "High-quality 10-bit ProRes 422. Recommended default for ART passes."),
                CreateProRes("prores_4444", "Apple ProRes 4444", 4, "yuva444p10le", true,
                    "10-bit ProRes 4444 with alpha support. CPU encoding only."),
                CreateProRes("prores_4444_xq", "Apple ProRes 4444 XQ", 5, "yuva444p10le", true,
                    "Highest-quality ProRes 4444 profile with alpha support. CPU encoding only."),

                new CodecProfile(
                    "dnxhr_hq",
                    "Avid DNxHR HQ",
                    ".mov",
                    "dnxhd",
                    "-c:v dnxhd -profile:v dnxhr_hq -pix_fmt yuv422p",
                    null, null, null, null, null, null,
                    "High-quality 8-bit 4:2:2 intermediate for editing. CPU encoding only.",
                    false),

                new CodecProfile(
                    "dnxhr_hqx",
                    "Avid DNxHR HQX 10-bit",
                    ".mov",
                    "dnxhd",
                    "-c:v dnxhd -profile:v dnxhr_hqx -pix_fmt yuv422p10le",
                    null, null, null, null, null, null,
                    "10-bit 4:2:2 DNxHR intermediate for compositing and editing. CPU encoding only.",
                    false),

                new CodecProfile(
                    "h264_hq",
                    "H.264 High Quality",
                    ".mp4",
                    "libx264",
                    "-c:v libx264 -preset slow -crf 16 -pix_fmt yuv420p -movflags +faststart",
                    "h264_nvenc",
                    "-c:v h264_nvenc -preset p6 -tune hq -rc vbr -cq 18 -b:v 0 -pix_fmt yuv420p -movflags +faststart",
                    "h264_amf",
                    "-c:v h264_amf -quality quality -rc cqp -qp_i 18 -qp_p 18 -pix_fmt yuv420p -movflags +faststart",
                    "h264_qsv",
                    "-c:v h264_qsv -preset slow -global_quality 18 -pix_fmt nv12 -movflags +faststart",
                    "Compact delivery MP4. GPU encoding is available through NVENC, AMF, or Intel QSV.",
                    false),

                new CodecProfile(
                    "hevc_hq",
                    "H.265 / HEVC 10-bit High Quality",
                    ".mp4",
                    "libx265",
                    "-c:v libx265 -preset slow -crf 18 -pix_fmt yuv420p10le -tag:v hvc1 -movflags +faststart",
                    "hevc_nvenc",
                    "-c:v hevc_nvenc -preset p6 -tune hq -rc vbr -cq 20 -b:v 0 -pix_fmt p010le -tag:v hvc1 -movflags +faststart",
                    "hevc_amf",
                    "-c:v hevc_amf -quality quality -rc cqp -qp_i 20 -qp_p 20 -pix_fmt yuv420p -tag:v hvc1 -movflags +faststart",
                    "hevc_qsv",
                    "-c:v hevc_qsv -preset slow -global_quality 20 -pix_fmt p010le -tag:v hvc1 -movflags +faststart",
                    "Compact 10-bit HEVC delivery file. GPU encoding is available when supported by FFmpeg and the installed GPU.",
                    false),

                new CodecProfile(
                    "av1_hq",
                    "AV1 10-bit High Quality",
                    ".mkv",
                    "libsvtav1",
                    "-c:v libsvtav1 -preset 6 -crf 20 -pix_fmt yuv420p10le",
                    "av1_nvenc",
                    "-c:v av1_nvenc -preset p6 -tune hq -rc vbr -cq 22 -b:v 0 -pix_fmt p010le",
                    "av1_amf",
                    "-c:v av1_amf -quality quality -rc cqp -qp_i 22 -qp_p 22 -pix_fmt yuv420p",
                    "av1_qsv",
                    "-c:v av1_qsv -preset slow -global_quality 22 -pix_fmt p010le",
                    "Efficient modern 10-bit codec in MKV. Requires an FFmpeg build with SVT-AV1 or a supported GPU encoder.",
                    false),

                new CodecProfile(
                    "ffv1_rgba",
                    "FFV1 16-bit RGBA Lossless",
                    ".mkv",
                    "ffv1",
                    "-c:v ffv1 -level 3 -coder 1 -context 1 -g 1 -slicecrc 1 -pix_fmt gbrap16le",
                    null, null, null, null, null, null,
                    "Mathematically lossless 16-bit RGBA archive codec. Very large files; CPU encoding only.",
                    true)
            };
        }

        private static CodecProfile CreateProRes(
            string id,
            string name,
            int profile,
            string pixelFormat,
            bool preservesAlpha,
            string description)
        {
            return new CodecProfile(
                id,
                name,
                ".mov",
                "prores_ks",
                "-c:v prores_ks -profile:v " + profile + " -pix_fmt " + pixelFormat + " -vendor apl0",
                null, null, null, null, null, null,
                description,
                preservesAlpha);
        }
    }
}
