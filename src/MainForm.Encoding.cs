using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        // Encoding entry point and transactional source-frame cleanup.
        private async void EncodeClicked(object sender, EventArgs eventArgs)
        {
            if (_encoding)
                return;

            _sequenceGrid.EndEdit();
            var selectedRows = GetSelectedRows();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show(this, "Select at least one contiguous sequence to encode.", "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!ValidateSelectedSequences(selectedRows) || !ConfirmScreenshotDeletion(selectedRows))
                return;

            SaveSettings();
            ShowPage(_encodePage, _encodeNavButton);

            if (IsOpenExrMode())
            {
                await PrepareAndEncodeOpenExrAsync(selectedRows);
                return;
            }

            await PrepareAndEncodeVideoAsync(selectedRows);
        }

        private bool ValidateSelectedSequences(IEnumerable<DataGridViewRow> selectedRows)
        {
            foreach (var row in selectedRows)
            {
                var sequence = row.Tag as SequenceItem;
                if (sequence == null)
                    continue;

                if (!sequence.IsContiguous)
                {
                    MessageBox.Show(
                        this,
                        "The selected sequence '" + sequence.Name + "' contains missing frame numbers.",
                        "Frame gap detected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }

            return true;
        }

        private bool ConfirmScreenshotDeletion(IEnumerable<DataGridViewRow> selectedRows)
        {
            if (!_deleteFramesCheckBox.Checked)
                return true;

            var frameCount = selectedRows
                .Select(row => row.Tag as SequenceItem)
                .Where(item => item != null)
                .Sum(item => item.FrameFiles.Count);
            var answer = MessageBox.Show(
                this,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "After every selected encode succeeds, {0} source screenshot(s) will be permanently deleted.\r\n\r\n" +
                    "Any failure, cancellation, or skipped existing output cancels all deletion. Continue?",
                    frameCount),
                "Confirm screenshot deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            return answer == DialogResult.Yes;
        }

        private async Task PrepareAndEncodeVideoAsync(List<DataGridViewRow> selectedRows)
        {
            var ffmpegPath = ResolveFfmpegPath(_ffmpegPathTextBox.Text.Trim());
            if (ffmpegPath == null)
            {
                MessageBox.Show(
                    this,
                    "ffmpeg.exe was not found. Place it in the ffmpeg folder beside the application, beside the executable itself, inside tools, on PATH, or select it in Settings.",
                    "FFmpeg is required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ShowPage(_settingsPage, _settingsNavButton);
                return;
            }
            _ffmpegPathTextBox.Text = ffmpegPath;

            var codec = GetSelectedCodec();
            var useGpu = _gpuCheckBox.Checked && codec.SupportsGpu;
            var resolvedBackend = string.Empty;
            FfmpegCapabilities capabilities;

            try
            {
                Cursor = Cursors.WaitCursor;
                capabilities = GetFfmpegCapabilities(ffmpegPath, false);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    this,
                    "Could not inspect the selected FFmpeg build.\r\n\r\n" + exception.Message,
                    "FFmpeg validation failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            if (useGpu)
            {
                var requestedBackend = Convert.ToString(_gpuBackendComboBox.SelectedItem, CultureInfo.InvariantCulture) ?? GpuBackends.Auto;
                resolvedBackend = FfmpegProbe.ResolveGpuBackend(codec, requestedBackend, capabilities);
                if (string.IsNullOrWhiteSpace(resolvedBackend))
                {
                    var autoBackend = string.Equals(requestedBackend, GpuBackends.Auto, StringComparison.OrdinalIgnoreCase);
                    var requestedEncoder = autoBackend
                        ? "a uniquely matched hardware encoder"
                        : codec.GetEncoderName(true, requestedBackend);
                    var guidance = autoBackend
                        ? "Auto could not match the installed Windows GPU to one compatible FFmpeg backend. Select NVIDIA NVENC, AMD AMF, or Intel QSV manually."
                        : "The selected FFmpeg build does not expose " + requestedEncoder + " for " + codec.Name + ".";
                    MessageBox.Show(
                        this,
                        guidance + "\r\n\r\nDisable GPU encoding, choose another backend, or install an FFmpeg build with the required hardware encoder.",
                        "GPU encoder unavailable",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    ShowPage(_encodePage, _encodeNavButton);
                    return;
                }
            }
            else if (!capabilities.HasEncoder(codec.CpuEncoder))
            {
                MessageBox.Show(
                    this,
                    "The selected FFmpeg build does not include the required encoder: " + codec.CpuEncoder + ".\r\n\r\n" +
                    "Choose another codec or use a fuller FFmpeg build.",
                    "Codec unavailable",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            await EncodeBatchAsync(selectedRows, ffmpegPath, codec, useGpu, resolvedBackend);
        }

        private async Task EncodeBatchAsync(
            List<DataGridViewRow> selectedRows,
            string ffmpegPath,
            CodecProfile codec,
            bool useGpu,
            string gpuBackend)
        {
            SetEncodingState(true);
            _cancelRequested = false;
            _progressBar.Value = 0;

            var completed = 0;
            var failed = 0;
            var skipped = 0;
            var processed = 0;

            AppendLog(string.Format(
                CultureInfo.InvariantCulture,
                "Starting batch: {0} sequence(s), codec {1}, encoder {2}.",
                selectedRows.Count,
                codec.Name,
                codec.GetEncoderName(useGpu, gpuBackend)));

            foreach (var row in selectedRows)
            {
                if (_cancelRequested)
                    break;

                var sequence = row.Tag as SequenceItem;
                if (sequence == null)
                    continue;

                var frameRate = _overrideFrameRateCheckBox.Checked
                    ? (double)_frameRateControl.Value
                    : sequence.FrameRate;
                if (frameRate <= 0.0)
                    frameRate = 60.0;

                var outputPath = sequence.GetOutputPath(codec.Extension);
                row.Cells[OutputColumnIndex].Value = Path.GetFileName(outputPath);
                row.Cells[OutputColumnIndex].ToolTipText = outputPath;

                if (File.Exists(outputPath) && !_overwriteCheckBox.Checked)
                {
                    skipped++;
                    processed++;
                    SetRowStatus(row, "Skipped", ArtTheme.Warning);
                    AppendLog("Skipped existing output: " + outputPath);
                    SetOverallProgress(processed, selectedRows.Count, 0.0, "Skipped " + sequence.Name);
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(sequence.FolderPath);
                    SetRowStatus(row, "Encoding", ArtTheme.Info);
                    AppendLog(string.Format(
                        CultureInfo.InvariantCulture,
                        "Encoding {0}/{1} at {2:0.###} fps -> {3}",
                        sequence.TakeName,
                        sequence.Name,
                        frameRate,
                        outputPath));

                    var exitCode = await RunFfmpegAsync(
                        ffmpegPath,
                        sequence,
                        outputPath,
                        codec,
                        useGpu,
                        gpuBackend,
                        frameRate,
                        _overwriteCheckBox.Checked,
                        processed,
                        selectedRows.Count);

                    if (_cancelRequested)
                    {
                        SetRowStatus(row, "Cancelled", ArtTheme.Warning);
                        break;
                    }

                    if (exitCode != 0 || !File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                    {
                        failed++;
                        processed++;
                        SetRowStatus(row, "Failed", ArtTheme.Error);
                        AppendLog(string.Format(
                            CultureInfo.InvariantCulture,
                            "FFmpeg failed for {0}/{1} with exit code {2}.",
                            sequence.TakeName,
                            sequence.Name,
                            exitCode));
                        SetOverallProgress(processed, selectedRows.Count, 0.0, "Failed " + sequence.Name);
                        continue;
                    }

                    completed++;
                    processed++;
                    SetRowStatus(row, "Done", ArtTheme.Success);
                    SetOverallProgress(processed, selectedRows.Count, 0.0, sequence.Name + " complete");
                    AppendLog("Completed: " + outputPath);
                }
                catch (Exception exception)
                {
                    failed++;
                    processed++;
                    SetRowStatus(row, "Failed", ArtTheme.Error);
                    AppendLog("Encoding failed for " + sequence.Name + ": " + exception.Message);
                    SetOverallProgress(processed, selectedRows.Count, 0.0, "Failed " + sequence.Name);
                }
            }

            var allSucceeded = !_cancelRequested && failed == 0 && skipped == 0 && completed == selectedRows.Count;
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
                    AppendLog("Videos were encoded, but screenshot deletion stopped: " + exception.Message);
                    MessageBox.Show(
                        this,
                        "All videos were encoded successfully, but not all screenshots could be deleted.\r\n\r\n" + exception.Message,
                        "Deletion incomplete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }

            if (_cancelRequested)
            {
                _progressLabel.Text = "Cancelled";
                _headerStatusLabel.Text = "CANCELLED";
                AppendLog("Batch cancelled. Source screenshots were not deleted.");
            }
            else if (allSucceeded)
            {
                _progressBar.Value = _progressBar.Maximum;
                _progressLabel.Text = "Batch complete";
                _headerStatusLabel.Text = "DONE #" + completed.ToString("0000", CultureInfo.InvariantCulture);
                AppendLog("Batch completed successfully.");
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
                    "Batch finished: {0} done, {1} failed, {2} skipped. Source screenshots were not deleted.",
                    completed,
                    failed,
                    skipped));

                if (failed > 0)
                {
                    MessageBox.Show(
                        this,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Batch finished with {0} successful, {1} failed, and {2} skipped sequence(s).\r\n\r\nSource screenshots were not deleted.",
                            completed,
                            failed,
                            skipped),
                        "Encoding error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            SetEncodingState(false);
            RefreshOutputColumn();
        }

        private async Task<int> RunFfmpegAsync(
            string ffmpegPath,
            SequenceItem sequence,
            string outputPath,
            CodecProfile codec,
            bool useGpu,
            string gpuBackend,
            double frameRate,
            bool overwrite,
            int processedSequences,
            int totalSequences)
        {
            var arguments = BuildFfmpegArguments(
                sequence,
                outputPath,
                codec,
                useGpu,
                gpuBackend,
                frameRate,
                overwrite);
            AppendLog("ffmpeg " + arguments);

            var startInfo = new ProcessStartInfo();
            startInfo.FileName = ffmpegPath;
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

                // Keep the active process reachable so either Cancel button can stop it.
                _currentProcess = process;

                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs)
                {
                    if (string.IsNullOrWhiteSpace(eventArgs.Data))
                        return;

                    if (eventArgs.Data.StartsWith("frame=", StringComparison.OrdinalIgnoreCase))
                    {
                        int frame;
                        if (int.TryParse(eventArgs.Data.Substring(6).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
                        {
                            var fraction = sequence.FrameCount > 0
                                ? Math.Min(1.0, Math.Max(0.0, frame / (double)sequence.FrameCount))
                                : 0.0;
                            try
                            {
                                BeginInvoke(new Action(delegate
                                {
                                    SetOverallProgress(
                                        processedSequences,
                                        totalSequences,
                                        fraction,
                                        string.Format(
                                            CultureInfo.InvariantCulture,
                                            "{0}/{1}: {2}/{3}",
                                            sequence.TakeName,
                                            sequence.Name,
                                            Math.Min(frame, sequence.FrameCount),
                                            sequence.FrameCount));
                                }));
                            }
                            catch
                            {
                            }
                        }
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

                if (!process.Start())
                    throw new InvalidOperationException("Could not start FFmpeg.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(delegate { process.WaitForExit(); });
                process.WaitForExit();
                _currentProcess = null;
                return process.ExitCode;
            }
        }

        private static string BuildFfmpegArguments(
            SequenceItem sequence,
            string outputPath,
            CodecProfile codec,
            bool useGpu,
            string gpuBackend,
            double frameRate,
            bool overwrite)
        {
            var videoArguments = codec.GetVideoArguments(useGpu, gpuBackend);
            if (string.IsNullOrWhiteSpace(videoArguments))
                throw new InvalidOperationException("No FFmpeg arguments are defined for the selected codec/backend combination.");

            var fps = frameRate.ToString("0.###", CultureInfo.InvariantCulture);
            var builder = new StringBuilder();
            builder.Append("-hide_banner ");
            builder.Append(overwrite ? "-y " : "-n ");
            builder.Append("-loglevel info ");
            builder.Append("-framerate ").Append(fps).Append(' ');
            builder.Append("-start_number ").Append(sequence.StartFrame.ToString(CultureInfo.InvariantCulture)).Append(' ');
            builder.Append("-i ").Append(QuoteArgument(sequence.InputPatternPath)).Append(' ');
            builder.Append("-frames:v ").Append(sequence.FrameCount.ToString(CultureInfo.InvariantCulture)).Append(' ');
            builder.Append("-an ");
            builder.Append(videoArguments).Append(' ');
            builder.Append("-progress pipe:1 -nostats ");
            builder.Append(QuoteArgument(outputPath));
            return builder.ToString();
        }

        private static string QuoteArgument(string value)
        {
            if (value == null)
                return "\"\"";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        // Called only after the whole selected batch has been verified as successful.
        private int DeleteSourceFrames(IEnumerable<DataGridViewRow> rows)
        {
            var deleted = 0;
            foreach (var row in rows)
            {
                var sequence = row.Tag as SequenceItem;
                if (sequence == null)
                    continue;

                foreach (var frameFile in sequence.FrameFiles)
                {
                    if (!File.Exists(frameFile))
                        continue;
                    File.Delete(frameFile);
                    deleted++;
                }
            }
            return deleted;
        }

        private void CancelClicked(object sender, EventArgs eventArgs)
        {
            if (!_encoding)
                return;

            _cancelRequested = true;
            _cancelButton.Enabled = false;
            _cancelTopButton.Enabled = false;
            _progressLabel.Text = "Cancelling...";
            _headerStatusLabel.Text = "CANCELLING";
            AppendLog("Cancellation requested.");
            TryKillCurrentProcess();
        }

        private void SetEncodingState(bool encoding)
        {
            _encoding = encoding;

            _singleTakeRadio.Enabled = !encoding;
            _recordingFolderRadio.Enabled = !encoding;
            _sourcePathTextBox.Enabled = !encoding;
            _browseSourceButton.Enabled = !encoding;
            _scanButton.Enabled = !encoding;
            _selectAllButton.Enabled = !encoding;
            _selectNoneButton.Enabled = !encoding;
            _removeSelectedButton.Enabled = !encoding;
            _reloadButton.Enabled = !encoding;
            _openFolderButton.Enabled = !encoding;
            _sequenceGrid.Enabled = !encoding;

            var openExr = IsOpenExrMode();
            _outputModeComboBox.Enabled = !encoding;
            _codecComboBox.Enabled = !encoding && !openExr;
            _overrideFrameRateCheckBox.Enabled = !encoding && !openExr;
            _frameRateControl.Enabled = !encoding && !openExr && _overrideFrameRateCheckBox.Checked;
            _gpuOptionsPanel.Enabled = !encoding && !openExr;
            _gpuCheckBox.Enabled = !encoding && !openExr && GetSelectedCodec().SupportsGpu;
            _gpuBackendComboBox.Enabled = !encoding && !openExr && _gpuCheckBox.Checked && GetSelectedCodec().SupportsGpu;
            _exrOptionsPanel.Enabled = !encoding && openExr;
            _exrCompressionComboBox.Enabled = !encoding && openExr;
            _exrCompressionLevelControl.Enabled = !encoding && openExr && GetSelectedExrCompression().UsesLevel;
            _overwriteCheckBox.Enabled = !encoding;
            _deleteFramesCheckBox.Enabled = !encoding;

            _ffmpegPathTextBox.Enabled = !encoding;
            _browseFfmpegButton.Enabled = !encoding;
            _testFfmpegButton.Enabled = !encoding;
            _oiiotoolPathTextBox.Enabled = !encoding;
            _browseOiiotoolButton.Enabled = !encoding;
            _testOiiotoolButton.Enabled = !encoding;
            _rememberLastFolderCheckBox.Enabled = !encoding;

            _encodeButton.Enabled = !encoding;
            _encodeTopButton.Enabled = !encoding;
            _cancelButton.Enabled = encoding;
            _cancelTopButton.Enabled = encoding;
        }

        private static void SetRowStatus(DataGridViewRow row, string status, Color color)
        {
            row.Cells[StatusColumnIndex].Value = status;
            row.Cells[StatusColumnIndex].Style.ForeColor = color;
        }

        private void SetOverallProgress(int processedSequences, int totalSequences, double currentFraction, string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, int, double, string>(SetOverallProgress), processedSequences, totalSequences, currentFraction, text);
                return;
            }

            if (totalSequences <= 0)
                totalSequences = 1;
            var fraction = (processedSequences + currentFraction) / totalSequences;
            var value = (int)Math.Round(fraction * _progressBar.Maximum);
            value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, value));
            _progressBar.Value = value;
            _progressLabel.Text = text;
        }

    }
}
