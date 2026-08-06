using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ArtBatchEncoder
{
    internal static class BatchManifestReader
    {
        public static BatchLoadResult LoadSingleTake(string jsonPath)
        {
            var batch = new BatchLoadResult();
            batch.SourcePath = Path.GetFullPath(jsonPath);
            batch.IsRecordingFolder = false;

            var take = ManifestReader.Load(jsonPath);
            batch.Takes.Add(take);
            batch.Sequences.AddRange(take.Sequences);
            foreach (var warning in take.Warnings)
                batch.Warnings.Add(take.TakeName + ": " + warning);

            return batch;
        }

        // Recursively evaluates every JSON file, then de-duplicates sequences by
        // their resolved numbered input pattern.
        public static BatchLoadResult LoadRecordingFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                throw new ArgumentException("Select the ART recording folder first.");
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("The selected ART recording folder does not exist: " + folderPath);

            var batch = new BatchLoadResult();
            batch.SourcePath = Path.GetFullPath(folderPath);
            batch.IsRecordingFolder = true;

            var jsonFiles = FindJsonFiles(batch.SourcePath);
            var candidates = new List<TakeLoadResult>();
            var rejectedJsonCount = 0;

            foreach (var jsonPath in jsonFiles)
            {
                try
                {
                    var take = ManifestReader.Load(jsonPath);
                    if (take.Sequences.Count == 0)
                    {
                        rejectedJsonCount++;
                        continue;
                    }
                    candidates.Add(take);
                }
                catch
                {
                    rejectedJsonCount++;
                }
            }

            // Prefer real manifest matches and shallower take-level JSON files before
            // unrelated JSON files that happen to live inside a pass directory.
            candidates = candidates
                .OrderByDescending(take => take.Sequences.Count(sequence =>
                    string.Equals(sequence.Source, "ART JSON", StringComparison.OrdinalIgnoreCase)))
                .ThenBy(take => GetPathDepth(take.JsonPath))
                .ThenByDescending(take => take.Sequences.Count)
                .ThenBy(take => take.JsonPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sequenceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var take in candidates)
            {
                var uniqueSequences = new List<SequenceItem>();
                foreach (var sequence in take.Sequences)
                {
                    var key = Path.GetFullPath(sequence.InputPatternPath);
                    if (sequenceKeys.Add(key))
                        uniqueSequences.Add(sequence);
                }

                // A nested or unrelated JSON may resolve to sequences that were already
                // connected by the actual take manifest. Do not add it as a duplicate take.
                if (uniqueSequences.Count == 0)
                    continue;

                batch.Takes.Add(take);
                batch.Sequences.AddRange(uniqueSequences);
                foreach (var warning in take.Warnings)
                    batch.Warnings.Add(take.TakeName + ": " + warning);
            }

            batch.Takes = batch.Takes
                .OrderBy(take => take.TakeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(take => take.JsonPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            batch.Sequences = batch.Sequences
                .OrderBy(sequence => sequence.TakeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(sequence => sequence.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(sequence => sequence.FolderPath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (jsonFiles.Count == 0)
            {
                batch.Warnings.Add("No .json files were found in the selected recording folder or its subfolders.");
            }
            else if (batch.Sequences.Count == 0)
            {
                batch.Warnings.Add("JSON files were found, but none resolved to numbered ART image sequences.");
            }
            else if (rejectedJsonCount > 0)
            {
                batch.Warnings.Add(rejectedJsonCount.ToString(CultureInfo.InvariantCulture) +
                    " unrelated or unusable JSON file(s) were ignored.");
            }

            return batch;
        }

        private static List<string> FindJsonFiles(string rootFolder)
        {
            var found = new List<string>();
            var pending = new Queue<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            pending.Enqueue(rootFolder);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                string fullCurrent;
                try
                {
                    fullCurrent = Path.GetFullPath(current);
                    var pathRoot = Path.GetPathRoot(fullCurrent);
                    if (!string.Equals(fullCurrent, pathRoot, StringComparison.OrdinalIgnoreCase))
                        fullCurrent = fullCurrent.TrimEnd(Path.DirectorySeparatorChar);
                }
                catch
                {
                    continue;
                }

                if (!visited.Add(fullCurrent))
                    continue;

                try
                {
                    foreach (var file in Directory.GetFiles(fullCurrent, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
                            found.Add(file);
                    }
                }
                catch
                {
                }

                try
                {
                    foreach (var child in Directory.GetDirectories(fullCurrent))
                    {
                        try
                        {
                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                                continue;
                        }
                        catch
                        {
                            continue;
                        }
                        pending.Enqueue(child);
                    }
                }
                catch
                {
                }
            }

            return found
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int GetPathDepth(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return int.MaxValue;

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (string.IsNullOrWhiteSpace(directory))
                    return 0;
                return directory.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries).Length;
            }
            catch
            {
                return int.MaxValue;
            }
        }
    }
}
