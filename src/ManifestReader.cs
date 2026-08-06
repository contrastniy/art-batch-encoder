using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ArtBatchEncoder
{
    internal static class ManifestReader
    {
        private static readonly string[] SupportedImageExtensions =
        {
            ".tga", ".png", ".bmp", ".jpg", ".jpeg", ".tif", ".tiff", ".exr", ".dpx"
        };

        private static readonly Regex NumberedFileRegex = new Regex(
            "^(.*?)(\\d+)(\\.[^.]+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PrintfFrameRegex = new Regex(
            "%0?(\\d*)d",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static TakeLoadResult Load(string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
                throw new ArgumentException("Select an ART JSON file first.");
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("The selected ART JSON does not exist.", jsonPath);

            var result = new TakeLoadResult();
            result.JsonPath = Path.GetFullPath(jsonPath);

            Dictionary<string, object> root = null;
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 16 * 1024 * 1024;
                root = serializer.DeserializeObject(File.ReadAllText(jsonPath)) as Dictionary<string, object>;
            }
            catch (Exception exception)
            {
                result.Warnings.Add("The JSON could not be parsed; folder scanning was used instead: " + exception.Message);
            }

            result.TakeFolder = ResolveTakeFolder(jsonPath, root);
            result.TakeName = ReadTakeName(root, result.TakeFolder);
            result.FrameRate = ReadFrameRate(root);

            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root != null)
                ReadManifestPasses(root, result, knownKeys);

            // The folder scan keeps older or partially written manifests usable.
            ScanForAdditionalSequences(result.TakeFolder, result, knownKeys);

            foreach (var sequence in result.Sequences)
            {
                sequence.TakeName = result.TakeName;
                sequence.TakeJsonPath = result.JsonPath;
                sequence.TakeFolderPath = result.TakeFolder;
                sequence.FrameRate = result.FrameRate;
            }

            result.Sequences = result.Sequences
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (result.Sequences.Count == 0)
                result.Warnings.Add("No numbered ART image sequences were found beside the selected JSON.");

            return result;
        }

        private static string ResolveTakeFolder(string jsonPath, Dictionary<string, object> root)
        {
            var jsonFolder = Path.GetDirectoryName(Path.GetFullPath(jsonPath));
            var take = GetDictionary(root, "take");
            var absoluteFolder = GetString(take, "absolute_folder");
            if (!string.IsNullOrWhiteSpace(absoluteFolder) && Directory.Exists(absoluteFolder))
                return Path.GetFullPath(absoluteFolder);
            return jsonFolder;
        }

        private static string ReadTakeName(Dictionary<string, object> root, string takeFolder)
        {
            var take = GetDictionary(root, "take");
            var name = GetString(take, "name");
            if (!string.IsNullOrWhiteSpace(name))
                return name.Trim();

            if (!string.IsNullOrWhiteSpace(takeFolder))
                return new DirectoryInfo(takeFolder).Name;

            return "take";
        }

        private static double ReadFrameRate(Dictionary<string, object> root)
        {
            var capture = GetDictionary(root, "capture");
            var value = GetDouble(capture, "frame_rate", 0.0);
            if (value <= 0.0)
                value = GetDouble(capture, "fps", 0.0);
            if (value <= 0.0)
                value = GetDouble(root, "frame_rate", 0.0);
            if (value <= 0.0)
                value = 60.0;
            return value;
        }

        private static void ReadManifestPasses(
            Dictionary<string, object> root,
            TakeLoadResult result,
            HashSet<string> knownKeys)
        {
            object passesObject;
            if (!root.TryGetValue("passes", out passesObject) || passesObject == null)
                return;

            var enumerable = passesObject as IEnumerable;
            if (enumerable == null)
                return;

            foreach (var rawPass in enumerable)
            {
                var pass = rawPass as Dictionary<string, object>;
                if (pass == null)
                    continue;

                var directory = GetString(pass, "directory");
                var pattern = GetString(pass, "filename_pattern");
                var name = GetString(pass, "name");

                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(pattern))
                    continue;

                var folder = Path.IsPathRooted(directory)
                    ? directory
                    : Path.Combine(result.TakeFolder, NormalizeRelativePath(directory));

                var item = BuildFromManifestPattern(folder, pattern, name);
                if (item == null)
                    continue;

                item.Source = "ART JSON";
                AddSequence(result, knownKeys, item);
            }
        }

        private static SequenceItem BuildFromManifestPattern(string folder, string pattern, string passName)
        {
            if (!Directory.Exists(folder))
                return null;

            var marker = PrintfFrameRegex.Match(pattern);
            if (!marker.Success)
                return null;

            var requestedPadding = 0;
            if (!string.IsNullOrEmpty(marker.Groups[1].Value))
                int.TryParse(marker.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out requestedPadding);

            var prefix = pattern.Substring(0, marker.Index);
            var suffix = pattern.Substring(marker.Index + marker.Length);
            var numberPattern = requestedPadding > 0
                ? "(\\d{" + requestedPadding.ToString(CultureInfo.InvariantCulture) + "})"
                : "(\\d+)";
            var expression = new Regex(
                "^" + Regex.Escape(prefix) + numberPattern + Regex.Escape(suffix) + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            var matches = new List<FrameMatch>();
            foreach (var file in Directory.GetFiles(folder))
            {
                var match = expression.Match(Path.GetFileName(file));
                if (!match.Success)
                    continue;

                int frame;
                if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
                    continue;
                matches.Add(new FrameMatch(file, frame, match.Groups[1].Value.Length));
            }

            if (matches.Count == 0)
                return null;

            matches.Sort(delegate(FrameMatch left, FrameMatch right) { return left.Number.CompareTo(right.Number); });
            var padding = requestedPadding > 0 ? requestedPadding : matches[0].Padding;
            var safeName = MakeSafeFileName(!string.IsNullOrWhiteSpace(passName)
                ? passName
                : DeriveSequenceName(prefix, new DirectoryInfo(folder).Name));

            return CreateSequenceItem(folder, pattern, safeName, matches, padding);
        }

        private static void ScanForAdditionalSequences(
            string takeFolder,
            TakeLoadResult result,
            HashSet<string> knownKeys)
        {
            var folders = new List<string>();
            if (Directory.Exists(takeFolder))
            {
                folders.Add(takeFolder);
                try
                {
                    folders.AddRange(
                        Directory.GetDirectories(takeFolder)
                            .Where(folder => !string.Equals(
                                Path.GetFileName(folder),
                                "EXR",
                                StringComparison.OrdinalIgnoreCase)));
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            foreach (var folder in folders)
            {
                List<string> files;
                try
                {
                    files = Directory.GetFiles(folder)
                        .Where(IsSupportedImage)
                        .ToList();
                }
                catch (Exception)
                {
                    continue;
                }

                var groups = new Dictionary<string, List<FrameMatch>>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var match = NumberedFileRegex.Match(fileName);
                    if (!match.Success)
                        continue;

                    int frame;
                    if (!int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
                        continue;

                    var extension = match.Groups[3].Value.ToLowerInvariant();
                    if (!SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var prefix = match.Groups[1].Value;
                    var padding = match.Groups[2].Value.Length;
                    var key = prefix + "\n" + padding.ToString(CultureInfo.InvariantCulture) + "\n" + extension;
                    List<FrameMatch> group;
                    if (!groups.TryGetValue(key, out group))
                    {
                        group = new List<FrameMatch>();
                        groups.Add(key, group);
                    }
                    group.Add(new FrameMatch(file, frame, padding));
                }

                foreach (var pair in groups)
                {
                    var group = pair.Value;
                    if (group.Count == 0)
                        continue;
                    if (string.Equals(folder, takeFolder, StringComparison.OrdinalIgnoreCase) && group.Count < 2)
                        continue;

                    group.Sort(delegate(FrameMatch left, FrameMatch right) { return left.Number.CompareTo(right.Number); });
                    var firstName = Path.GetFileName(group[0].Path);
                    var firstMatch = NumberedFileRegex.Match(firstName);
                    if (!firstMatch.Success)
                        continue;

                    var prefix = firstMatch.Groups[1].Value;
                    var extension = firstMatch.Groups[3].Value;
                    var padding = firstMatch.Groups[2].Value.Length;
                    var pattern = prefix + "%0" + padding.ToString(CultureInfo.InvariantCulture) + "d" + extension;
                    var folderName = new DirectoryInfo(folder).Name;
                    var sequenceName = DeriveSequenceName(prefix, folderName);
                    var item = CreateSequenceItem(folder, pattern, MakeSafeFileName(sequenceName), group, padding);
                    item.Source = "folder scan";
                    AddSequence(result, knownKeys, item);
                }
            }
        }

        private static SequenceItem CreateSequenceItem(
            string folder,
            string pattern,
            string name,
            List<FrameMatch> matches,
            int padding)
        {
            var start = matches[0].Number;
            var end = matches[matches.Count - 1].Number;
            var uniqueFiles = matches
                .GroupBy(item => item.Number)
                .Select(group => group.First())
                .OrderBy(item => item.Number)
                .ToList();

            var frameFiles = uniqueFiles.Select(item => Path.GetFullPath(item.Path)).ToList();
            var outputName = string.IsNullOrWhiteSpace(name) ? new DirectoryInfo(folder).Name : name;

            return new SequenceItem
            {
                Name = outputName,
                FolderPath = Path.GetFullPath(folder),
                FileNamePattern = pattern,
                InputPatternPath = Path.Combine(Path.GetFullPath(folder), pattern),
                StartFrame = start,
                EndFrame = end,
                FrameCount = uniqueFiles.Count,
                Padding = padding,
                IsContiguous = uniqueFiles.Count == end - start + 1,
                FrameFiles = frameFiles
            };
        }

        private static void AddSequence(
            TakeLoadResult result,
            HashSet<string> knownKeys,
            SequenceItem item)
        {
            var key = Path.GetFullPath(item.FolderPath).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant()
                + "|" + item.FileNamePattern.ToLowerInvariant();
            if (knownKeys.Add(key))
                result.Sequences.Add(item);
        }

        private static string DeriveSequenceName(string prefix, string folderName)
        {
            var candidate = (prefix ?? string.Empty).Trim(' ', '_', '-', '.');
            if (string.IsNullOrWhiteSpace(candidate) ||
                string.Equals(candidate, "frame", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate, "image", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate, "shot", StringComparison.OrdinalIgnoreCase))
                candidate = folderName;
            return candidate;
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "sequence";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray();
            var result = new string(chars).Trim(' ', '.');
            return string.IsNullOrWhiteSpace(result) ? "sequence" : result;
        }

        private static bool IsSupportedImage(string path)
        {
            var extension = Path.GetExtension(path);
            return SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeRelativePath(string value)
        {
            return value.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> dictionary, string key)
        {
            if (dictionary == null)
                return null;
            object value;
            if (!dictionary.TryGetValue(key, out value))
                return null;
            return value as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            if (dictionary == null)
                return null;
            object value;
            if (!dictionary.TryGetValue(key, out value) || value == null)
                return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static double GetDouble(Dictionary<string, object> dictionary, string key, double fallback)
        {
            if (dictionary == null)
                return fallback;
            object value;
            if (!dictionary.TryGetValue(key, out value) || value == null)
                return fallback;
            double result;
            return double.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result)
                ? result
                : fallback;
        }

        private sealed class FrameMatch
        {
            public string Path { get; private set; }
            public int Number { get; private set; }
            public int Padding { get; private set; }

            public FrameMatch(string path, int number, int padding)
            {
                Path = path;
                Number = number;
                Padding = padding;
            }
        }
    }
}
