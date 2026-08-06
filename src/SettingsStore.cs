using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ArtBatchEncoder
{
    internal sealed class AppSettings
    {
        public bool RememberLastFolder { get; set; }
        public string LastSourceMode { get; set; }
        public string LastTakeJson { get; set; }
        public string LastRecordingFolder { get; set; }
        public string LastFfmpegPath { get; set; }
        public string LastOiiotoolPath { get; set; }
        public string LastOutputMode { get; set; }
        public string LastCodecId { get; set; }
        public bool UseGpu { get; set; }
        public string GpuBackend { get; set; }
        public bool OverrideFrameRate { get; set; }
        public double OverrideFrameRateValue { get; set; }
        public bool OverwriteOutputs { get; set; }
        public bool DeleteFrames { get; set; }
        public string ExrCompressionId { get; set; }
        public double ExrCompressionLevel { get; set; }

        public AppSettings()
        {
            RememberLastFolder = true;
            LastSourceMode = "take";
            LastTakeJson = string.Empty;
            LastRecordingFolder = string.Empty;
            LastFfmpegPath = string.Empty;
            LastOiiotoolPath = string.Empty;
            LastOutputMode = OutputModes.Video;
            LastCodecId = "prores_422_hq";
            UseGpu = false;
            GpuBackend = GpuBackends.Auto;
            OverrideFrameRate = false;
            OverrideFrameRateValue = 60.0;
            OverwriteOutputs = true;
            DeleteFrames = false;
            ExrCompressionId = "zip";
            ExrCompressionLevel = 4.0;
        }
    }

    // Portable INI persistence. The file always lives beside the executable.
    internal static class SettingsStore
    {
        private const string SettingsFileName = "artbe_settings.ini";

        public static string SettingsPath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFileName);
            }
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings();

            try
            {
                if (!File.Exists(SettingsPath))
                {
                    Save(settings);
                    return settings;
                }

                var values = ReadIni(SettingsPath);
                settings.RememberLastFolder = ReadBool(values, "RememberLastFolder", settings.RememberLastFolder);
                settings.LastSourceMode = ReadString(values, "LastSourceMode", settings.LastSourceMode);
                settings.LastTakeJson = ReadString(values, "LastTakeJson", settings.LastTakeJson);
                settings.LastRecordingFolder = ReadString(values, "LastRecordingFolder", settings.LastRecordingFolder);
                settings.LastFfmpegPath = ReadString(values, "LastFfmpegPath", settings.LastFfmpegPath);
                settings.LastOiiotoolPath = ReadString(values, "LastOiiotoolPath", settings.LastOiiotoolPath);
                settings.LastOutputMode = ReadString(values, "LastOutputMode", settings.LastOutputMode);
                settings.LastCodecId = ReadString(values, "LastCodecId", settings.LastCodecId);
                settings.UseGpu = ReadBool(values, "UseGpu", settings.UseGpu);
                settings.GpuBackend = ReadString(values, "GpuBackend", settings.GpuBackend);
                settings.OverrideFrameRate = ReadBool(values, "OverrideFrameRate", settings.OverrideFrameRate);
                settings.OverrideFrameRateValue = ReadDouble(values, "OverrideFrameRateValue", settings.OverrideFrameRateValue);
                settings.OverwriteOutputs = ReadBool(values, "OverwriteOutputs", settings.OverwriteOutputs);
                settings.DeleteFrames = ReadBool(values, "DeleteFrames", settings.DeleteFrames);
                settings.ExrCompressionId = ReadString(values, "ExrCompressionId", settings.ExrCompressionId);
                settings.ExrCompressionLevel = ReadDouble(values, "ExrCompressionLevel", settings.ExrCompressionLevel);
            }
            catch
            {
                // Keep defaults when the portable INI is missing, locked, or malformed.
            }

            return settings;
        }

        public static void Save(AppSettings settings)
        {
            if (settings == null)
                return;

            var builder = new StringBuilder();
            builder.AppendLine("; ART Batch Encoder v1.0 portable settings");
            builder.AppendLine("; This file is stored beside ARTBatchEncoder.exe.");
            builder.AppendLine();
            builder.AppendLine("[General]");
            Append(builder, "RememberLastFolder", settings.RememberLastFolder);
            Append(builder, "LastSourceMode", settings.LastSourceMode);
            Append(builder, "LastTakeJson", settings.LastTakeJson);
            Append(builder, "LastRecordingFolder", settings.LastRecordingFolder);
            Append(builder, "LastFfmpegPath", settings.LastFfmpegPath);
            Append(builder, "LastOiiotoolPath", settings.LastOiiotoolPath);
            builder.AppendLine();
            builder.AppendLine("[Encoding]");
            Append(builder, "LastOutputMode", settings.LastOutputMode);
            Append(builder, "LastCodecId", settings.LastCodecId);
            Append(builder, "UseGpu", settings.UseGpu);
            Append(builder, "GpuBackend", settings.GpuBackend);
            Append(builder, "OverrideFrameRate", settings.OverrideFrameRate);
            Append(builder, "OverrideFrameRateValue", settings.OverrideFrameRateValue);
            Append(builder, "OverwriteOutputs", settings.OverwriteOutputs);
            Append(builder, "DeleteFrames", settings.DeleteFrames);
            Append(builder, "ExrCompressionId", settings.ExrCompressionId);
            Append(builder, "ExrCompressionLevel", settings.ExrCompressionLevel);

            File.WriteAllText(SettingsPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static Dictionary<string, string> ReadIni(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal) ||
                    (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal)))
                    continue;

                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                if (key.Length > 0)
                    values[key] = value;
            }
            return values;
        }

        private static string ReadString(IDictionary<string, string> values, string key, string fallback)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : fallback;
        }

        private static bool ReadBool(IDictionary<string, string> values, string key, bool fallback)
        {
            string value;
            bool parsed;
            if (!values.TryGetValue(key, out value) || !bool.TryParse(value, out parsed))
                return fallback;
            return parsed;
        }

        private static double ReadDouble(IDictionary<string, string> values, string key, double fallback)
        {
            string value;
            double parsed;
            if (!values.TryGetValue(key, out value) ||
                !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return fallback;
            return parsed;
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder.Append(key).Append('=').AppendLine(value ?? string.Empty);
        }

        private static void Append(StringBuilder builder, string key, bool value)
        {
            Append(builder, key, value ? "true" : "false");
        }

        private static void Append(StringBuilder builder, string key, double value)
        {
            Append(builder, key, value.ToString("0.###", CultureInfo.InvariantCulture));
        }
    }
}
