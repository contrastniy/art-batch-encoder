using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArtBatchEncoder
{
    internal sealed class ExrCompressionProfile
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string OiiotoolName { get; private set; }
        public string Description { get; private set; }
        public bool UsesLevel { get; private set; }
        public decimal MinimumLevel { get; private set; }
        public decimal MaximumLevel { get; private set; }
        public decimal DefaultLevel { get; private set; }
        public int DecimalPlaces { get; private set; }
        public decimal Increment { get; private set; }

        public ExrCompressionProfile(
            string id,
            string name,
            string oiiotoolName,
            string description,
            bool usesLevel,
            decimal minimumLevel,
            decimal maximumLevel,
            decimal defaultLevel,
            int decimalPlaces,
            decimal increment)
        {
            Id = id;
            Name = name;
            OiiotoolName = oiiotoolName;
            Description = description;
            UsesLevel = usesLevel;
            MinimumLevel = minimumLevel;
            MaximumLevel = maximumLevel;
            DefaultLevel = defaultLevel;
            DecimalPlaces = decimalPlaces;
            Increment = increment;
        }

        public string BuildArgument(decimal level)
        {
            if (!UsesLevel)
                return OiiotoolName;

            var clamped = Math.Max(MinimumLevel, Math.Min(MaximumLevel, level));
            var format = DecimalPlaces > 0 ? "0." + new string('#', DecimalPlaces) : "0";
            return OiiotoolName + ":" + clamped.ToString(format, CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal static class ExrCompressionCatalog
    {
        public static List<ExrCompressionProfile> CreateAll()
        {
            return new List<ExrCompressionProfile>
            {
                new ExrCompressionProfile("zip", "ZIP (lossless)", "zip",
                    "Lossless ZIP compression in 16-scanline blocks. Good general-purpose default.",
                    true, 1m, 9m, 4m, 0, 1m),
                new ExrCompressionProfile("zips", "ZIPS (lossless)", "zips",
                    "Lossless ZIP compression one scanline at a time. Better for partial scanline access.",
                    true, 1m, 9m, 4m, 0, 1m),
                new ExrCompressionProfile("piz", "PIZ (lossless)", "piz",
                    "Lossless wavelet compression. Often effective for grainy or high-detail imagery.",
                    false, 0m, 0m, 0m, 0, 1m),
                new ExrCompressionProfile("rle", "RLE (lossless)", "rle",
                    "Lossless run-length compression. Fast and useful for large flat-color areas.",
                    false, 0m, 0m, 0m, 0, 1m),
                new ExrCompressionProfile("pxr24", "PXR24 (lossy float)", "pxr24",
                    "Lossy compression for 32-bit float channels; integer and half channels remain lossless.",
                    false, 0m, 0m, 0m, 0, 1m),
                new ExrCompressionProfile("b44", "B44 (lossy half)", "b44",
                    "Fixed-rate lossy compression intended for half-float channels.",
                    false, 0m, 0m, 0m, 0, 1m),
                new ExrCompressionProfile("b44a", "B44A (lossy half)", "b44a",
                    "B44 variant with improved compression for uniform image regions.",
                    false, 0m, 0m, 0m, 0, 1m),
                new ExrCompressionProfile("dwaa", "DWAA (lossy)", "dwaa",
                    "Lossy DCT compression in 32-scanline blocks. Lower level means higher quality.",
                    true, 0m, 1000m, 45m, 1, 1m),
                new ExrCompressionProfile("dwab", "DWAB (lossy)", "dwab",
                    "Lossy DCT compression in 256-scanline blocks. Efficient for full-frame playback.",
                    true, 0m, 1000m, 45m, 1, 1m),
                new ExrCompressionProfile("none", "None", "none",
                    "Uncompressed OpenEXR output.",
                    false, 0m, 0m, 0m, 0, 1m)
            };
        }

        public static ExrCompressionProfile FindById(IEnumerable<ExrCompressionProfile> profiles, string id)
        {
            if (profiles == null)
                return null;

            return profiles.FirstOrDefault(profile =>
                string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
