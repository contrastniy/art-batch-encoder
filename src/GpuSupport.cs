using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace ArtBatchEncoder
{
    internal static class GpuBackends
    {
        public const string Auto = "Auto";
        public const string Nvidia = "NVIDIA NVENC";
        public const string Amd = "AMD AMF";
        public const string Intel = "Intel QSV";

        public static object[] All
        {
            get
            {
                return new object[] { Auto, Nvidia, Amd, Intel };
            }
        }
    }

    internal static class GpuHardwareDetector
    {
        public static List<string> GetControllerNames()
        {
            var names = new List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject controller in results)
                    {
                        var name = Convert.ToString(controller["Name"]);
                        if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                            names.Add(name.Trim());
                    }
                }
            }
            catch
            {
            }
            return names;
        }

        public static List<string> GetPreferredBackends()
        {
            var names = GetControllerNames();
            var backends = new List<string>();

            if (names.Any(name => name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0))
                backends.Add(GpuBackends.Nvidia);
            if (names.Any(name =>
                name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("RADEON", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("ATI", StringComparison.OrdinalIgnoreCase) >= 0))
                backends.Add(GpuBackends.Amd);
            if (names.Any(name => name.IndexOf("INTEL", StringComparison.OrdinalIgnoreCase) >= 0))
                backends.Add(GpuBackends.Intel);

            return backends;
        }

        public static string SummarizeControllers()
        {
            var names = GetControllerNames();
            return names.Count == 0 ? "Unknown" : string.Join("; ", names.ToArray());
        }
    }

    internal sealed class FfmpegCapabilities
    {
        public string VersionLine { get; set; }
        public HashSet<string> Encoders { get; private set; }
        public string RawOutput { get; set; }

        public FfmpegCapabilities()
        {
            Encoders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public bool HasEncoder(string encoder)
        {
            return !string.IsNullOrWhiteSpace(encoder) && Encoders.Contains(encoder);
        }
    }

    // Reads encoder capabilities once and maps the selected GPU backend to the
    // corresponding FFmpeg encoder exposed by the user's build.
    internal static class FfmpegProbe
    {
        private static readonly Regex EncoderLineRegex = new Regex(
            "^\\s*[VAS\\.][A-Z\\.]{5}\\s+([^\\s]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static FfmpegCapabilities Probe(string ffmpegPath)
        {
            if (string.IsNullOrWhiteSpace(ffmpegPath) || !File.Exists(ffmpegPath))
                throw new FileNotFoundException("ffmpeg.exe was not found.", ffmpegPath);

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = ffmpegPath;
            startInfo.Arguments = "-encoders";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new InvalidOperationException("Could not start FFmpeg.");

                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(15000))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("FFmpeg capability detection timed out.");
                }

                var combined = standardOutput + Environment.NewLine + standardError;
                var capabilities = new FfmpegCapabilities();
                capabilities.RawOutput = combined;

                using (var reader = new StringReader(combined))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(capabilities.VersionLine) &&
                            line.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase))
                            capabilities.VersionLine = line.Trim();

                        var match = EncoderLineRegex.Match(line);
                        if (match.Success)
                            capabilities.Encoders.Add(match.Groups[1].Value.Trim());
                    }
                }

                if (process.ExitCode != 0 && capabilities.Encoders.Count == 0)
                    throw new InvalidOperationException("FFmpeg returned exit code " + process.ExitCode + ".\r\n" + standardError.Trim());

                return capabilities;
            }
        }

        public static string ResolveGpuBackend(
            CodecProfile codec,
            string requestedBackend,
            FfmpegCapabilities capabilities)
        {
            if (codec == null || capabilities == null)
                return null;

            if (!string.Equals(requestedBackend, GpuBackends.Auto, StringComparison.OrdinalIgnoreCase))
            {
                var requestedEncoder = codec.GetEncoderName(true, requestedBackend);
                return capabilities.HasEncoder(requestedEncoder) ? requestedBackend : null;
            }

            foreach (var preferredBackend in GpuHardwareDetector.GetPreferredBackends())
            {
                if (capabilities.HasEncoder(codec.GetEncoderName(true, preferredBackend)))
                    return preferredBackend;
            }

            var availableBackends = new List<string>();
            if (capabilities.HasEncoder(codec.NvidiaEncoder))
                availableBackends.Add(GpuBackends.Nvidia);
            if (capabilities.HasEncoder(codec.AmdEncoder))
                availableBackends.Add(GpuBackends.Amd);
            if (capabilities.HasEncoder(codec.IntelEncoder))
                availableBackends.Add(GpuBackends.Intel);

            return availableBackends.Count == 1 ? availableBackends[0] : null;
        }

        public static string SummarizeGpuEncoders(FfmpegCapabilities capabilities)
        {
            if (capabilities == null)
                return "Not tested";

            var detected = new List<string>();
            if (capabilities.Encoders.Any(name => name.EndsWith("_nvenc", StringComparison.OrdinalIgnoreCase)))
                detected.Add("NVENC");
            if (capabilities.Encoders.Any(name => name.EndsWith("_amf", StringComparison.OrdinalIgnoreCase)))
                detected.Add("AMF");
            if (capabilities.Encoders.Any(name => name.EndsWith("_qsv", StringComparison.OrdinalIgnoreCase)))
                detected.Add("QSV");

            return detected.Count == 0 ? "No supported GPU encoders found" : string.Join(", ", detected.ToArray());
        }
    }
}
