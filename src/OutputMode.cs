namespace ArtBatchEncoder
{
    internal static class OutputModes
    {
        public const string Video = "Video files";
        public const string OpenExrMultilayer = "OpenEXR multilayer sequence";

        public static readonly string[] All =
        {
            Video,
            OpenExrMultilayer
        };
    }
}
