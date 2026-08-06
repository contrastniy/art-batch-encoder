using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace ArtBatchEncoder
{
    internal sealed partial class MainForm
    {
        // Shutdown, persistence, logging, and executable discovery helpers.
        private void AppendLog(string text)
        {
            if (_logTextBox == null)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(AppendLog), text);
                }
                catch
                {
                }
                return;
            }

            _logTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + text + Environment.NewLine);
            _logTextBox.SelectionStart = _logTextBox.TextLength;
            _logTextBox.ScrollToCaret();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs eventArgs)
        {
            if (_encoding)
            {
                var answer = MessageBox.Show(
                    this,
                    "Encoding is still running. Cancel the active encoder process and close?",
                    "Encoding in progress",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes)
                {
                    eventArgs.Cancel = true;
                    return;
                }

                _cancelRequested = true;
                TryKillCurrentProcess();
            }

            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                _settings.RememberLastFolder = _rememberLastFolderCheckBox.Checked;
                _settings.LastSourceMode = _recordingFolderRadio.Checked ? "folder" : "take";

                if (_settings.RememberLastFolder)
                {
                    if (_recordingFolderRadio.Checked)
                        _settings.LastRecordingFolder = _sourcePathTextBox.Text.Trim();
                    else
                        _settings.LastTakeJson = _sourcePathTextBox.Text.Trim();
                }
                else
                {
                    _settings.LastTakeJson = string.Empty;
                    _settings.LastRecordingFolder = string.Empty;
                }

                _settings.LastFfmpegPath = _ffmpegPathTextBox.Text.Trim();
                _settings.LastOiiotoolPath = _oiiotoolPathTextBox.Text.Trim();
                _settings.LastOutputMode = GetSelectedOutputMode();
                _settings.LastCodecId = GetSelectedCodec().Id;
                _settings.UseGpu = _gpuCheckBox.Checked;
                _settings.GpuBackend = Convert.ToString(_gpuBackendComboBox.SelectedItem, CultureInfo.InvariantCulture) ?? GpuBackends.Auto;
                _settings.OverrideFrameRate = _overrideFrameRateCheckBox.Checked;
                _settings.OverrideFrameRateValue = (double)_frameRateControl.Value;
                _settings.OverwriteOutputs = _overwriteCheckBox.Checked;
                _settings.DeleteFrames = _deleteFramesCheckBox.Checked;
                _settings.ExrCompressionId = GetSelectedExrCompression().Id;
                _settings.ExrCompressionLevel = (double)_exrCompressionLevelControl.Value;

                SettingsStore.Save(_settings);
            }
            catch (Exception exception)
            {
                AppendLog("Could not save settings: " + exception.Message);
            }
        }

        private void TryKillCurrentProcess()
        {
            var process = _currentProcess;
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch
            {
            }
        }

        private static decimal ClampDecimal(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }

        private static string FindPreferredLocalFfmpeg()
        {
            var preferred = Path.Combine(Application.StartupPath, "ffmpeg", "ffmpeg.exe");
            return File.Exists(preferred) ? preferred : null;
        }

        private static string FindFfmpegExecutable()
        {
            var local = Path.Combine(Application.StartupPath, "ffmpeg.exe");
            if (File.Exists(local))
                return local;

            var tools = Path.Combine(Application.StartupPath, "tools", "ffmpeg.exe");
            if (File.Exists(tools))
                return tools;

            var parentFolder = Directory.GetParent(Application.StartupPath);
            if (parentFolder != null)
            {
                var parentTools = Path.Combine(parentFolder.FullName, "tools", "ffmpeg.exe");
                if (File.Exists(parentTools))
                    return parentTools;
            }

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var part in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;
                try
                {
                    var candidate = Path.Combine(part.Trim().Trim('"'), "ffmpeg.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return null;
        }

        private static string ResolveFfmpegPath(string requestedPath)
        {
            // A portable FFmpeg copy beside the executable always has first priority.
            var preferredLocal = FindPreferredLocalFfmpeg();
            if (!string.IsNullOrWhiteSpace(preferredLocal))
                return preferredLocal;

            if (!string.IsNullOrWhiteSpace(requestedPath))
            {
                try
                {
                    if (File.Exists(requestedPath))
                        return Path.GetFullPath(requestedPath);
                }
                catch
                {
                }
            }

            return FindFfmpegExecutable();
        }

        private static string FindPreferredLocalOiiotool()
        {
            var direct = Path.Combine(Application.StartupPath, "openimageio", "oiiotool.exe");
            if (File.Exists(direct))
                return direct;

            var bin = Path.Combine(Application.StartupPath, "openimageio", "bin", "oiiotool.exe");
            return File.Exists(bin) ? bin : null;
        }

        private static string FindOiiotoolExecutable()
        {
            var local = Path.Combine(Application.StartupPath, "oiiotool.exe");
            if (File.Exists(local))
                return local;

            var tools = Path.Combine(Application.StartupPath, "tools", "oiiotool.exe");
            if (File.Exists(tools))
                return tools;

            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var part in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;
                try
                {
                    var candidate = Path.Combine(part.Trim().Trim('"'), "oiiotool.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                }
            }

            return null;
        }

        private static string ResolveOiiotoolPath(string requestedPath)
        {
            var preferredLocal = FindPreferredLocalOiiotool();
            if (!string.IsNullOrWhiteSpace(preferredLocal))
                return preferredLocal;

            if (!string.IsNullOrWhiteSpace(requestedPath))
            {
                try
                {
                    if (File.Exists(requestedPath))
                        return Path.GetFullPath(requestedPath);
                }
                catch
                {
                }
            }

            return FindOiiotoolExecutable();
        }

    }
}
