using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ArtBatchEncoder
{
    internal sealed partial class MainForm
    {
        // OpenImageIO combines each selected ART pass into a named channel layer.
        private async Task PrepareAndEncodeOpenExrAsync(List<DataGridViewRow> selectedRows)
        {
            var oiiotoolPath = ResolveOiiotoolPath(_oiiotoolPathTextBox.Text.Trim());
            if (oiiotoolPath == null)
            {
                MessageBox.Show(
                    this,
                    "oiiotool.exe was not found. Place the OpenImageIO runtime in the openimageio folder beside the application, or select oiiotool.exe in Settings.",
                    "OpenImageIO is required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ShowPage(_settingsPage, _settingsNavButton);
                return;
            }

            List<OpenExrTakeJob> jobs;
            try
            {
                Cursor = Cursors.WaitCursor;
                _oiiotoolPathTextBox.Text = oiiotoolPath;
                _oiiotoolStatusLabel.Text = OpenImageIoProbe.Probe(oiiotoolPath);
                jobs = OpenExrJobBuilder.Build(
                    selectedRows
                        .Select(row => row.Tag as SequenceItem)
                        .Where(sequence => sequence != null));
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    exception.Message,
                    "OpenEXR validation failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            if (jobs.Count == 0)
            {
                MessageBox.Show(this, "No OpenEXR take jobs could be created.", "Nothing to encode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            await EncodeOpenExrBatchAsync(selectedRows, oiiotoolPath, jobs);
        }

        private async Task EncodeOpenExrBatchAsync(
            List<DataGridViewRow> selectedRows,
            string oiiotoolPath,
            List<OpenExrTakeJob> jobs)
        {
            SetEncodingState(true);
            _cancelRequested = false;
            _progressBar.Value = 0;

            var completed = 0;
            var failed = 0;
            var skipped = 0;
            var processed = 0;
            var compression = GetSelectedExrCompression();
            var compressionArgument = compression.BuildArgument(_exrCompressionLevelControl.Value);

            AppendLog(string.Format(
                CultureInfo.InvariantCulture,
                "Starting multilayer OpenEXR batch: {0} take(s), {1} selected layer sequence(s), compression {2}.",
                jobs.Count,
                selectedRows.Count,
                compressionArgument));

            foreach (var job in jobs)
            {
                if (_cancelRequested)
                    break;

                var jobRows = GetRowsForOpenExrJob(selectedRows, job);
                if (job.AnyOutputExists() && !_overwriteCheckBox.Checked)
                {
                    skipped++;
                    processed++;
                    SetRowsStatus(jobRows, "Skipped", ArtTheme.Warning);
                    AppendLog("Skipped take because EXR output already exists: " + job.OutputFolder);
                    SetOverallProgress(processed, jobs.Count, 0.0, "Skipped " + job.TakeName);
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(job.OutputFolder);
                    SetRowsStatus(jobRows, "Encoding", ArtTheme.Info);
                    foreach (var row in jobRows)
                    {
                        row.Cells[OutputColumnIndex].Value = job.OutputDisplayPath;
                        row.Cells[OutputColumnIndex].ToolTipText = job.OutputPatternPath;
                    }

                    AppendLog(string.Format(
                        CultureInfo.InvariantCulture,
                        "Encoding take {0}: {1} layer(s), frames {2}-{3} -> {4}",
                        job.TakeName,
                        job.Layers.Count,
                        job.StartFrame,
                        job.EndFrame,
                        job.OutputPatternPath));

                    var exitCode = await RunOiiotoolAsync(
                        oiiotoolPath,
                        job,
                        compressionArgument,
                        processed,
                        jobs.Count);

                    if (_cancelRequested)
                    {
                        SetRowsStatus(jobRows, "Cancelled", ArtTheme.Warning);
                        break;
                    }

                    if (exitCode != 0 || !job.AllOutputsAreValid())
                    {
                        failed++;
                        processed++;
                        SetRowsStatus(jobRows, "Failed", ArtTheme.Error);
                        AppendLog(string.Format(
                            CultureInfo.InvariantCulture,
                            "OpenImageIO failed for take {0} with exit code {1}.",
                            job.TakeName,
                            exitCode));
                        SetOverallProgress(processed, jobs.Count, 0.0, "Failed " + job.TakeName);
                        continue;
                    }

                    completed++;
                    processed++;
                    SetRowsStatus(jobRows, "Done", ArtTheme.Success);
                    SetOverallProgress(processed, jobs.Count, 0.0, job.TakeName + " complete");
                    AppendLog("Completed multilayer sequence: " + job.OutputPatternPath);
                }
                catch (Exception exception)
                {
                    failed++;
                    processed++;
                    SetRowsStatus(jobRows, "Failed", ArtTheme.Error);
                    AppendLog("OpenEXR encoding failed for " + job.TakeName + ": " + exception.Message);
                    SetOverallProgress(processed, jobs.Count, 0.0, "Failed " + job.TakeName);
                }
            }

            var allSucceeded = !_cancelRequested && failed == 0 && skipped == 0 && completed == jobs.Count;
            if (allSucceeded && _deleteFramesCheckBox.Checked)
            {
                try
                {
                    var deleted = DeleteSourceFrames(selectedRows);
                    AppendLog(string.Format(CultureInfo.InvariantCulture, "Deleted {0} source screenshot(s).", deleted));
                }
                catch (Exception exception)
                {
                    allSucceeded = false;
                    AppendLog("OpenEXR files were encoded, but screenshot deletion stopped: " + exception.Message);
                    MessageBox.Show(
                        this,
                        "All OpenEXR sequences were encoded successfully, but not all screenshots could be deleted.\r\n\r\n" + exception.Message,
                        "Deletion incomplete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            if (_cancelRequested)
            {
                _progressLabel.Text = "Cancelled";
                _headerStatusLabel.Text = "CANCELLED";
                AppendLog("OpenEXR batch cancelled. Source screenshots were not deleted.");
            }
            else if (allSucceeded)
            {
                _progressBar.Value = _progressBar.Maximum;
                _progressLabel.Text = "Batch complete";
                _headerStatusLabel.Text = "DONE #" + completed.ToString("0000", CultureInfo.InvariantCulture);
                AppendLog("Multilayer OpenEXR batch completed successfully.");
            }
            else
            {
                _progressLabel.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Complete: {0} done / {1} failed / {2} skipped",
                    completed,
                    failed,
                    skipped);
                _headerStatusLabel.Text = failed > 0 ? "ERROR" : "INCOMPLETE";
                AppendLog(string.Format(
                    CultureInfo.InvariantCulture,
                    "OpenEXR batch finished: {0} done, {1} failed, {2} skipped. Source screenshots were not deleted.",
                    completed,
                    failed,
                    skipped));

                if (failed > 0)
                {
                    MessageBox.Show(
                        this,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "OpenEXR batch finished with {0} successful, {1} failed, and {2} skipped take(s).\r\n\r\nSource screenshots were not deleted.",
                            completed,
                            failed,
                            skipped),
                        "OpenEXR encoding error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            SetEncodingState(false);
            RefreshOutputColumn();
        }

        private async Task<int> RunOiiotoolAsync(
            string oiiotoolPath,
            OpenExrTakeJob job,
            string compressionArgument,
            int processedJobs,
            int totalJobs)
        {
            var arguments = BuildOiiotoolArguments(job, compressionArgument);
            AppendLog("oiiotool " + arguments);

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = oiiotoolPath;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = Encoding.UTF8;
            startInfo.StandardErrorEncoding = Encoding.UTF8;

            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                _currentProcess = process;

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs)
                {
                    if (string.IsNullOrWhiteSpace(eventArgs.Data))
                        return;

                    const string progressPrefix = "ARTBE_FRAME ";
                    if (eventArgs.Data.StartsWith(progressPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        int frameNumber;
                        if (int.TryParse(
                            eventArgs.Data.Substring(progressPrefix.Length).Trim(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out frameNumber))
                        {
                            var completedFrames = frameNumber - job.StartFrame + 1;
                            var fraction = job.FrameCount > 0
                                ? Math.Min(1.0, Math.Max(0.0, completedFrames / (double)job.FrameCount))
                                : 0.0;
                            try
                            {
                                BeginInvoke(new Action(delegate
                                {
                                    SetOverallProgress(
                                        processedJobs,
                                        totalJobs,
                                        fraction,
                                        string.Format(
                                            CultureInfo.InvariantCulture,
                                            "{0}: {1}/{2}",
                                            job.TakeName,
                                            Math.Min(Math.Max(completedFrames, 0), job.FrameCount),
                                            job.FrameCount));
                                }));
                            }
                            catch
                            {
                            }
                        }
                        return;
                    }

                    try
                    {
                        BeginInvoke(new Action(delegate { AppendLog(eventArgs.Data); }));
                    }
                    catch
                    {
                    }
                };

                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs)
                {
                    if (string.IsNullOrWhiteSpace(eventArgs.Data))
                        return;
                    try
                    {
                        BeginInvoke(new Action(delegate { AppendLog(eventArgs.Data); }));
                    }
                    catch
                    {
                    }
                };

                try
                {
                    if (!process.Start())
                        throw new InvalidOperationException("Could not start oiiotool.exe.");

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await Task.Run(delegate { process.WaitForExit(); });
                    process.WaitForExit();
                    return process.ExitCode;
                }
                finally
                {
                    _currentProcess = null;
                }
            }
        }

        private static string BuildOiiotoolArguments(OpenExrTakeJob job, string compressionArgument)
        {
            var builder = new StringBuilder();
            builder.Append("--threads 0 ");
            builder.Append("--frames ")
                .Append(job.StartFrame.ToString(CultureInfo.InvariantCulture))
                .Append('-')
                .Append(job.EndFrame.ToString(CultureInfo.InvariantCulture))
                .Append(' ');

            foreach (var layer in job.Layers)
            {
                builder.Append(QuoteArgument(layer.Sequence.InputPatternPath)).Append(' ');
                AppendLayerChannelRenameArguments(builder, layer.LayerName);
            }

            if (job.Layers.Count > 1)
            {
                builder.Append("--chappend:n=")
                    .Append(job.Layers.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(' ');
            }

            builder.Append("-d half ");
            builder.Append("--compression ").Append(QuoteArgument(compressionArgument)).Append(' ');
            builder.Append("--scanline ");
            builder.Append("-o ").Append(QuoteArgument(job.OutputPatternPath)).Append(' ');
            builder.Append("--echo ").Append(QuoteArgument("ARTBE_FRAME {FRAME_NUMBER}"));
            return builder.ToString();
        }

        // ART captures are normally RGB or RGBA. Conditional renaming also keeps
        // grayscale and two-channel sources valid while limiting unusual inputs to RGBA.
        private static void AppendLayerChannelRenameArguments(StringBuilder builder, string layerName)
        {
            builder.Append("--if ").Append(QuoteArgument("{TOP.nchannels == 1}")).Append(' ');
            builder.Append("--ch 0 --chnames ").Append(QuoteArgument(layerName + ".Y")).Append(' ');
            builder.Append("--else --if ").Append(QuoteArgument("{TOP.nchannels == 2}")).Append(' ');
            builder.Append("--ch 0,1 --chnames ").Append(QuoteArgument(layerName + ".R," + layerName + ".G")).Append(' ');
            builder.Append("--else --if ").Append(QuoteArgument("{TOP.nchannels == 3}")).Append(' ');
            builder.Append("--ch 0,1,2 --chnames ").Append(QuoteArgument(layerName + ".R," + layerName + ".G," + layerName + ".B")).Append(' ');
            builder.Append("--else --ch 0,1,2,3 --chnames ")
                .Append(QuoteArgument(layerName + ".R," + layerName + ".G," + layerName + ".B," + layerName + ".A"))
                .Append(" --endif --endif --endif ");
        }

        private static List<DataGridViewRow> GetRowsForOpenExrJob(
            IEnumerable<DataGridViewRow> selectedRows,
            OpenExrTakeJob job)
        {
            var sequences = new HashSet<SequenceItem>(job.Layers.Select(layer => layer.Sequence));
            return selectedRows
                .Where(row => sequences.Contains(row.Tag as SequenceItem))
                .ToList();
        }

        private static void SetRowsStatus(IEnumerable<DataGridViewRow> rows, string status, System.Drawing.Color color)
        {
            foreach (var row in rows)
                SetRowStatus(row, status, color);
        }
    }
}
