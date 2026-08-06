using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ArtBatchEncoder
{
    internal sealed class SequenceItem
    {
        public string TakeName { get; set; }
        public string TakeJsonPath { get; set; }
        public string TakeFolderPath { get; set; }
        public string Name { get; set; }
        public string FolderPath { get; set; }
        public string FileNamePattern { get; set; }
        public string InputPatternPath { get; set; }
        public int StartFrame { get; set; }
        public int EndFrame { get; set; }
        public int FrameCount { get; set; }
        public int Padding { get; set; }
        public bool IsContiguous { get; set; }
        public double FrameRate { get; set; }
        public List<string> FrameFiles { get; set; }
        public string Source { get; set; }

        public SequenceItem()
        {
            FrameFiles = new List<string>();
            IsContiguous = true;
            FrameRate = 60.0;
        }

        public string FrameRangeText
        {
            get
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}-{1}", StartFrame, EndFrame);
            }
        }

        public string GetOutputPath(string extension)
        {
            var normalizedExtension = string.IsNullOrWhiteSpace(extension) ? ".mov" : extension.Trim();
            if (!normalizedExtension.StartsWith(".", StringComparison.Ordinal))
                normalizedExtension = "." + normalizedExtension;

            return Path.Combine(FolderPath, MakeSafeFileName(Name) + normalizedExtension);
        }

        private static string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "sequence";

            var invalid = Path.GetInvalidFileNameChars();
            var characters = value.Trim().ToCharArray();
            for (var index = 0; index < characters.Length; index++)
            {
                if (Array.IndexOf(invalid, characters[index]) >= 0)
                    characters[index] = '_';
            }

            var result = new string(characters).Trim(' ', '.');
            return string.IsNullOrWhiteSpace(result) ? "sequence" : result;
        }
    }

    internal sealed class TakeLoadResult
    {
        public string JsonPath { get; set; }
        public string TakeFolder { get; set; }
        public string TakeName { get; set; }
        public double FrameRate { get; set; }
        public List<SequenceItem> Sequences { get; set; }
        public List<string> Warnings { get; set; }

        public TakeLoadResult()
        {
            FrameRate = 60.0;
            Sequences = new List<SequenceItem>();
            Warnings = new List<string>();
        }
    }

    internal sealed class BatchLoadResult
    {
        public string SourcePath { get; set; }
        public bool IsRecordingFolder { get; set; }
        public List<TakeLoadResult> Takes { get; set; }
        public List<SequenceItem> Sequences { get; set; }
        public List<string> Warnings { get; set; }

        public BatchLoadResult()
        {
            Takes = new List<TakeLoadResult>();
            Sequences = new List<SequenceItem>();
            Warnings = new List<string>();
        }
    }
}
