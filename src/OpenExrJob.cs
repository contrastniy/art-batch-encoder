using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ArtBatchEncoder
{
    internal sealed class OpenExrLayer
    {
        public SequenceItem Sequence { get; private set; }
        public string LayerName { get; private set; }

        public OpenExrLayer(SequenceItem sequence, string layerName)
        {
            Sequence = sequence;
            LayerName = layerName;
        }
    }

    internal sealed class OpenExrTakeJob
    {
        public string TakeName { get; set; }
        public string TakePath { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public int FrameCount { get; set; }
        public int Padding { get; set; }
        public string OutputFolder { get; set; }
        public string OutputPatternPath { get; set; }
        public List<OpenExrLayer> Layers { get; private set; }

        public OpenExrTakeJob()
        {
            Layers = new List<OpenExrLayer>();
        }

        public string OutputDisplayPath
        {
            get
            {
                return Path.Combine("EXR", Path.GetFileName(OutputPatternPath));
            }
        }

        public string GetOutputFramePath(int frameNumber)
        {
            var replacement = frameNumber.ToString("D" + Padding.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            return ReplacePrintfPattern(OutputPatternPath, replacement);
        }

        public bool AnyOutputExists()
        {
            for (var frame = StartFrame; frame <= EndFrame; frame++)
            {
                if (File.Exists(GetOutputFramePath(frame)))
                    return true;
            }
            return false;
        }

        public bool AllOutputsAreValid()
        {
            for (var frame = StartFrame; frame <= EndFrame; frame++)
            {
                var path = GetOutputFramePath(frame);
                if (!File.Exists(path))
                    return false;

                try
                {
                    if (new FileInfo(path).Length == 0)
                        return false;
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        private static string ReplacePrintfPattern(string pattern, string replacement)
        {
            var percentIndex = pattern.IndexOf('%');
            if (percentIndex < 0)
                return pattern;

            var dIndex = pattern.IndexOf('d', percentIndex);
            if (dIndex < 0)
                return pattern;

            return pattern.Substring(0, percentIndex) + replacement + pattern.Substring(dIndex + 1);
        }
    }

    internal static class OpenExrJobBuilder
    {
        public static List<OpenExrTakeJob> Build(IEnumerable<SequenceItem> selectedSequences)
        {
            if (selectedSequences == null)
                throw new ArgumentNullException("selectedSequences");

            var sequences = selectedSequences.Where(item => item != null).ToList();
            if (sequences.Count == 0)
                return new List<OpenExrTakeJob>();

            var jobs = new List<OpenExrTakeJob>();
            var groups = sequences
                .GroupBy(GetTakeKey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.First().TakeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
                jobs.Add(BuildJob(group.ToList()));

            return jobs;
        }

        public static string GetDisplayOutputPattern(SequenceItem sequence)
        {
            if (sequence == null)
                return string.Empty;

            var padding = Math.Max(4, sequence.Padding);
            return Path.Combine(
                "EXR",
                GetSafeTakeFileName(sequence) + "_%0" +
                padding.ToString(CultureInfo.InvariantCulture) + "d.exr");
        }

        public static string GetAbsoluteOutputPattern(SequenceItem sequence)
        {
            if (sequence == null || string.IsNullOrWhiteSpace(sequence.TakeFolderPath))
                return string.Empty;

            return Path.Combine(sequence.TakeFolderPath, GetDisplayOutputPattern(sequence));
        }

        public static bool ExpectedOutputExists(SequenceItem sequence)
        {
            if (sequence == null)
                return false;

            var pattern = GetAbsoluteOutputPattern(sequence);
            if (string.IsNullOrWhiteSpace(pattern))
                return false;

            var padding = Math.Max(4, sequence.Padding);
            var replacement = sequence.StartFrame.ToString("D" + padding.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            var percentIndex = pattern.IndexOf('%');
            var dIndex = percentIndex >= 0 ? pattern.IndexOf('d', percentIndex) : -1;
            if (percentIndex < 0 || dIndex < 0)
                return false;

            var firstFramePath = pattern.Substring(0, percentIndex) + replacement + pattern.Substring(dIndex + 1);
            return File.Exists(firstFramePath);
        }

        private static OpenExrTakeJob BuildJob(List<SequenceItem> sequences)
        {
            sequences = sequences
                .OrderBy(sequence => sequence.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(sequence => sequence.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var reference = sequences[0];
            if (string.IsNullOrWhiteSpace(reference.TakeFolderPath))
                throw new InvalidOperationException("Could not resolve the take folder for '" + reference.TakeName + "'.");

            foreach (var sequence in sequences)
            {
                if (!sequence.IsContiguous)
                    throw new InvalidOperationException("The sequence '" + sequence.Name + "' contains missing frame numbers.");

                if (sequence.StartFrame != reference.StartFrame ||
                    sequence.EndFrame != reference.EndFrame ||
                    sequence.FrameCount != reference.FrameCount)
                {
                    throw new InvalidOperationException(
                        "OpenEXR layers in take '" + reference.TakeName + "' must have the same frame range. " +
                        "Expected " + reference.FrameRangeText + ", but '" + sequence.Name + "' uses " + sequence.FrameRangeText + ".");
                }
            }

            var padding = Math.Max(4, sequences.Max(sequence => sequence.Padding));
            var outputFolder = Path.Combine(reference.TakeFolderPath, "EXR");
            var outputPattern = Path.Combine(
                outputFolder,
                GetSafeTakeFileName(reference) + "_%0" +
                padding.ToString(CultureInfo.InvariantCulture) + "d.exr");

            var job = new OpenExrTakeJob();
            job.TakeName = reference.TakeName;
            job.TakePath = reference.TakeFolderPath;
            job.StartFrame = reference.StartFrame;
            job.EndFrame = reference.EndFrame;
            job.FrameCount = reference.FrameCount;
            job.Padding = padding;
            job.OutputFolder = outputFolder;
            job.OutputPatternPath = outputPattern;

            var usedLayerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sequence in sequences)
            {
                var layerName = MakeUniqueLayerName(sequence.Name, usedLayerNames);
                job.Layers.Add(new OpenExrLayer(sequence, layerName));
            }

            return job;
        }

        private static string GetSafeTakeFileName(SequenceItem sequence)
        {
            var value = sequence == null ? null : sequence.TakeName;
            if (string.IsNullOrWhiteSpace(value) && sequence != null &&
                !string.IsNullOrWhiteSpace(sequence.TakeFolderPath))
            {
                value = new DirectoryInfo(sequence.TakeFolderPath).Name;
            }

            if (string.IsNullOrWhiteSpace(value))
                value = "take";

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var characters = value.Trim().ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                if (characters[index] == '%' || Array.IndexOf(invalidCharacters, characters[index]) >= 0)
                    characters[index] = '_';
            }

            var safeName = new string(characters).Trim(' ', '.');
            return string.IsNullOrWhiteSpace(safeName) ? "take" : safeName;
        }

        private static string GetTakeKey(SequenceItem sequence)
        {
            if (!string.IsNullOrWhiteSpace(sequence.TakeFolderPath))
                return Path.GetFullPath(sequence.TakeFolderPath);
            if (!string.IsNullOrWhiteSpace(sequence.TakeJsonPath))
                return Path.GetFullPath(sequence.TakeJsonPath);
            return sequence.TakeName ?? "take";
        }

        private static string MakeUniqueLayerName(string value, ISet<string> usedNames)
        {
            var builder = new StringBuilder();
            var source = string.IsNullOrWhiteSpace(value) ? "layer" : value.Trim();
            foreach (var character in source)
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                    builder.Append(character);
                else
                    builder.Append('_');
            }

            var baseName = CollapseUnderscores(builder.ToString()).Trim('_');
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "layer";
            if (char.IsDigit(baseName[0]))
                baseName = "layer_" + baseName;

            var candidate = baseName;
            var suffix = 2;
            while (!usedNames.Add(candidate))
            {
                candidate = baseName + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            return candidate;
        }

        private static string CollapseUnderscores(string value)
        {
            var builder = new StringBuilder(value.Length);
            var previousUnderscore = false;
            foreach (var character in value)
            {
                if (character == '_')
                {
                    if (previousUnderscore)
                        continue;
                    previousUnderscore = true;
                }
                else
                {
                    previousUnderscore = false;
                }
                builder.Append(character);
            }
            return builder.ToString();
        }
    }
}
