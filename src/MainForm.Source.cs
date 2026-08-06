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
        // Source discovery, settings restoration, and control-state synchronization.
        private void WireEvents()
        {
            _encodeNavButton.Click += delegate { ShowPage(_encodePage, _encodeNavButton); };
            _settingsNavButton.Click += delegate { ShowPage(_settingsPage, _settingsNavButton); };
            _logNavButton.Click += delegate { ShowPage(_logPage, _logNavButton); };

            _singleTakeRadio.CheckedChanged += SourceModeChanged;
            _recordingFolderRadio.CheckedChanged += SourceModeChanged;
            _browseSourceButton.Click += BrowseSourceClicked;
            _scanButton.Click += delegate { LoadCurrentSource(); };
            _reloadButton.Click += delegate { LoadCurrentSource(); };
            _selectAllButton.Click += delegate { SetAllRowsChecked(true); };
            _selectNoneButton.Click += delegate { SetAllRowsChecked(false); };
            _removeSelectedButton.Click += RemoveSelectedSequencesClicked;
            _openFolderButton.Click += OpenSelectedFolderClicked;
            _sequenceGrid.CellDoubleClick += SequenceGridCellDoubleClick;
            _sequenceGrid.KeyDown += SequenceGridKeyDown;
            _sequenceGrid.CurrentCellDirtyStateChanged += delegate
            {
                if (_sequenceGrid.IsCurrentCellDirty)
                    _sequenceGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            _outputModeComboBox.SelectedIndexChanged += delegate { UpdateOutputModeUi(); };
            _codecComboBox.SelectedIndexChanged += CodecSelectionChanged;
            _exrCompressionComboBox.SelectedIndexChanged += delegate { UpdateExrCompressionUi(true); };
            _overrideFrameRateCheckBox.CheckedChanged += delegate
            {
                _frameRateControl.Enabled = !IsOpenExrMode() && _overrideFrameRateCheckBox.Checked && !_encoding;
            };
            _gpuCheckBox.CheckedChanged += delegate { UpdateGpuControls(); };
            _gpuBackendComboBox.SelectedIndexChanged += delegate { UpdateGpuControls(); };

            _browseFfmpegButton.Click += BrowseFfmpegClicked;
            _testFfmpegButton.Click += TestFfmpegClicked;
            _ffmpegPathTextBox.TextChanged += delegate
            {
                _cachedCapabilities = null;
                _cachedCapabilitiesPath = null;
                _ffmpegStatusLabel.Text = "Not tested";
                UpdateGpuControls();
            };

            _browseOiiotoolButton.Click += BrowseOiiotoolClicked;
            _testOiiotoolButton.Click += TestOiiotoolClicked;
            _oiiotoolPathTextBox.TextChanged += delegate { _oiiotoolStatusLabel.Text = "Not tested"; };

            _encodeButton.Click += EncodeClicked;
            _encodeTopButton.Click += EncodeClicked;
            _cancelButton.Click += CancelClicked;
            _cancelTopButton.Click += CancelClicked;

            Shown += MainFormShown;
            FormClosing += MainFormClosing;
        }

        private void ShowPage(Panel page, Button selectedButton)
        {
            _encodePage.Visible = false;
            _settingsPage.Visible = false;
            _logPage.Visible = false;

            _encodeNavButton.BackColor = ArtTheme.Sidebar;
            _settingsNavButton.BackColor = ArtTheme.Sidebar;
            _logNavButton.BackColor = ArtTheme.Sidebar;

            page.Visible = true;
            page.BringToFront();
            selectedButton.BackColor = ArtTheme.Accent;
        }

        private void MainFormShown(object sender, EventArgs eventArgs)
        {
            RestoreSettings();
            AppendLog(ApplicationName + " v" + ApplicationVersion + " ready.");
            AppendLog("Select one ART JSON file or scan an ART recording folder for JSON manifests recursively.");
            AppendLog("Video output uses FFmpeg. Multilayer OpenEXR output uses OpenImageIO oiiotool.");

            if (_rememberLastFolderCheckBox.Checked && SourcePathExists())
                BeginInvoke(new Action(LoadCurrentSource));
        }

        private void RestoreSettings()
        {
            _rememberLastFolderCheckBox.Checked = _settings.RememberLastFolder;
            _overwriteCheckBox.Checked = _settings.OverwriteOutputs;
            _deleteFramesCheckBox.Checked = _settings.DeleteFrames;

            var savedFfmpeg = ResolveFfmpegPath(_settings.LastFfmpegPath);
            _ffmpegPathTextBox.Text = savedFfmpeg ?? string.Empty;

            var savedOiiotool = ResolveOiiotoolPath(_settings.LastOiiotoolPath);
            _oiiotoolPathTextBox.Text = savedOiiotool ?? string.Empty;

            var codecIndex = _codecs.FindIndex(codec => string.Equals(codec.Id, _settings.LastCodecId, StringComparison.OrdinalIgnoreCase));
            _codecComboBox.SelectedIndex = codecIndex >= 0 ? codecIndex : 3;

            _overrideFrameRateCheckBox.Checked = _settings.OverrideFrameRate;
            _frameRateControl.Value = ClampDecimal(
                (decimal)(_settings.OverrideFrameRateValue > 0.0 ? _settings.OverrideFrameRateValue : 60.0),
                _frameRateControl.Minimum,
                _frameRateControl.Maximum);
            _frameRateControl.Enabled = _overrideFrameRateCheckBox.Checked;

            var gpuBackendIndex = _gpuBackendComboBox.Items.IndexOf(
                string.IsNullOrWhiteSpace(_settings.GpuBackend) ? GpuBackends.Auto : _settings.GpuBackend);
            _gpuBackendComboBox.SelectedIndex = gpuBackendIndex >= 0 ? gpuBackendIndex : 0;
            _gpuCheckBox.Checked = _settings.UseGpu && GetSelectedCodec().SupportsGpu;

            var compression = ExrCompressionCatalog.FindById(_exrCompressions, _settings.ExrCompressionId) ?? _exrCompressions[0];
            _exrCompressionComboBox.SelectedItem = compression;
            UpdateExrCompressionUi(true);
            _exrCompressionLevelControl.Value = ClampDecimal(
                (decimal)_settings.ExrCompressionLevel,
                _exrCompressionLevelControl.Minimum,
                _exrCompressionLevelControl.Maximum);

            var outputModeIndex = _outputModeComboBox.Items.IndexOf(_settings.LastOutputMode);
            _outputModeComboBox.SelectedIndex = outputModeIndex >= 0 ? outputModeIndex : 0;

            if (_settings.RememberLastFolder &&
                string.Equals(_settings.LastSourceMode, "folder", StringComparison.OrdinalIgnoreCase))
            {
                _recordingFolderRadio.Checked = true;
                _sourcePathTextBox.Text = _settings.LastRecordingFolder ?? string.Empty;
            }
            else
            {
                _singleTakeRadio.Checked = true;
                _sourcePathTextBox.Text = _settings.RememberLastFolder ? (_settings.LastTakeJson ?? string.Empty) : string.Empty;
            }

            UpdateSourceModeUi();
            UpdateCodecUi();
            UpdateOutputModeUi();
            UpdateExrCompressionUi(false);
            UpdateGpuControls();
        }

        private void SourceModeChanged(object sender, EventArgs eventArgs)
        {
            if (!_singleTakeRadio.Checked && !_recordingFolderRadio.Checked)
                return;
            UpdateSourceModeUi();
        }

        private void UpdateSourceModeUi()
        {
            if (_recordingFolderRadio.Checked)
            {
                _browseSourceButton.Text = "Browse folder...";
                _scanButton.Text = "Scan all";
            }
            else
            {
                _browseSourceButton.Text = "Browse JSON...";
                _scanButton.Text = "Load take";
            }
        }

        private void BrowseSourceClicked(object sender, EventArgs eventArgs)
        {
            if (_encoding)
                return;

            if (_recordingFolderRadio.Checked)
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select the ART recording root folder. JSON manifests below it will be detected automatically.";
                    dialog.ShowNewFolderButton = false;
                    var initialFolder = GetInitialSourceFolder();
                    if (Directory.Exists(initialFolder))
                        dialog.SelectedPath = initialFolder;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    _sourcePathTextBox.Text = dialog.SelectedPath;
                }
            }
            else
            {
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Filter = "ART JSON files (*.json)|*.json|All files (*.*)|*.*";
                    dialog.Title = "Select ART JSON";
                    dialog.CheckFileExists = true;
                    var initialFolder = GetInitialSourceFolder();
                    if (Directory.Exists(initialFolder))
                        dialog.InitialDirectory = initialFolder;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    _sourcePathTextBox.Text = dialog.FileName;
                }
            }

            LoadCurrentSource();
        }

        private string GetInitialSourceFolder()
        {
            var sourcePath = _sourcePathTextBox.Text.Trim();
            if (Directory.Exists(sourcePath))
                return sourcePath;
            if (File.Exists(sourcePath))
                return Path.GetDirectoryName(sourcePath);

            if (_recordingFolderRadio.Checked && Directory.Exists(_settings.LastRecordingFolder))
                return _settings.LastRecordingFolder;
            if (!_recordingFolderRadio.Checked && File.Exists(_settings.LastTakeJson))
                return Path.GetDirectoryName(_settings.LastTakeJson);

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private bool SourcePathExists()
        {
            var sourcePath = _sourcePathTextBox.Text.Trim();
            return _recordingFolderRadio.Checked ? Directory.Exists(sourcePath) : File.Exists(sourcePath);
        }

        private void LoadCurrentSource()
        {
            if (_encoding)
                return;

            var sourcePath = _sourcePathTextBox.Text.Trim();
            try
            {
                Cursor = Cursors.WaitCursor;
                _progressLabel.Text = "Scanning...";
                _headerStatusLabel.Text = "SCANNING";

                _loadedBatch = _recordingFolderRadio.Checked
                    ? BatchManifestReader.LoadRecordingFolder(sourcePath)
                    : BatchManifestReader.LoadSingleTake(sourcePath);

                PopulateSequenceGrid(_loadedBatch.Sequences);

                if (!_overrideFrameRateCheckBox.Checked && _loadedBatch.Takes.Count == 1 && _loadedBatch.Takes[0].FrameRate > 0.0)
                {
                    _frameRateControl.Value = ClampDecimal(
                        (decimal)_loadedBatch.Takes[0].FrameRate,
                        _frameRateControl.Minimum,
                        _frameRateControl.Maximum);
                }

                _sourceSummaryLabel.Text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} take(s) / {1} sequence(s) / {2} warning(s)",
                    _loadedBatch.Takes.Count,
                    _loadedBatch.Sequences.Count,
                    _loadedBatch.Warnings.Count);

                _progressLabel.Text = _loadedBatch.Sequences.Count == 0
                    ? "No sequences found"
                    : _loadedBatch.Sequences.Count.ToString(CultureInfo.InvariantCulture) + " sequence(s) ready";
                _headerStatusLabel.Text = "READY #" + _loadedBatch.Sequences.Count.ToString("0000", CultureInfo.InvariantCulture);

                AppendLog(string.Format(
                    CultureInfo.InvariantCulture,
                    "Loaded source: {0} take(s), {1} sequence(s).",
                    _loadedBatch.Takes.Count,
                    _loadedBatch.Sequences.Count));

                foreach (var warning in _loadedBatch.Warnings)
                    AppendLog("Warning: " + warning);

                SaveSettings();
            }
            catch (Exception exception)
            {
                _loadedBatch = null;
                _sequenceGrid.Rows.Clear();
                _sourceSummaryLabel.Text = "Load failed: " + exception.Message;
                _progressLabel.Text = "Load failed";
                _headerStatusLabel.Text = "ERROR";
                AppendLog("Load failed: " + exception.Message);
                MessageBox.Show(this, exception.Message, "Could not load ART source", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void PopulateSequenceGrid(IEnumerable<SequenceItem> sequences)
        {
            _sequenceGrid.Rows.Clear();
            foreach (var item in sequences)
            {
                var outputPath = GetOutputPathForDisplay(item);
                var outputExists = OutputExists(item);
                var status = item.IsContiguous ? (outputExists ? "Exists" : "Ready") : "Frame gap";
                var index = _sequenceGrid.Rows.Add(
                    item.IsContiguous,
                    item.TakeName,
                    item.Name,
                    item.FrameRate.ToString("0.###", CultureInfo.InvariantCulture),
                    item.FrameCount.ToString(CultureInfo.InvariantCulture),
                    item.FrameRangeText,
                    item.FolderPath,
                    GetOutputCellText(item),
                    status);

                var row = _sequenceGrid.Rows[index];
                row.Tag = item;
                row.Cells[FolderColumnIndex].ToolTipText = item.FolderPath;
                row.Cells[OutputColumnIndex].ToolTipText = outputPath;
                row.Cells[TakeColumnIndex].ToolTipText = item.TakeJsonPath;

                if (!item.IsContiguous)
                {
                    row.Cells[IncludeColumnIndex].Value = false;
                    row.Cells[IncludeColumnIndex].ToolTipText = "Missing frame numbers were detected. Repair the sequence before encoding.";
                    SetRowStatus(row, "Frame gap", ArtTheme.Warning);
                }
                else if (outputExists)
                {
                    SetRowStatus(row, "Exists", ArtTheme.Warning);
                }
                else
                {
                    SetRowStatus(row, "Ready", ArtTheme.MutedText);
                }
            }
        }

        private void CodecSelectionChanged(object sender, EventArgs eventArgs)
        {
            UpdateCodecUi();
        }

        private void UpdateCodecUi()
        {
            var codec = GetSelectedCodec();
            if (codec == null)
                return;

            _codecDescriptionLabel.Text = codec.Description +
                "  Output: " + codec.Extension.ToUpperInvariant() +
                (codec.PreservesAlpha ? "  /  alpha preserved" : "  /  no alpha");

            if (!codec.SupportsGpu)
                _gpuCheckBox.Checked = false;
            _gpuCheckBox.Enabled = !IsOpenExrMode() && codec.SupportsGpu && !_encoding;

            RefreshOutputColumn(true);
            UpdateGpuControls();
        }

        private void UpdateGpuControls()
        {
            if (IsOpenExrMode())
            {
                _gpuCheckBox.Enabled = false;
                _gpuBackendComboBox.Enabled = false;
                _gpuStatusLabel.Text = "GPU video encoding does not apply to multilayer OpenEXR output.";
                return;
            }

            var codec = GetSelectedCodec();
            var supportsGpu = codec != null && codec.SupportsGpu;
            _gpuCheckBox.Enabled = supportsGpu && !_encoding;
            _gpuBackendComboBox.Enabled = supportsGpu && _gpuCheckBox.Checked && !_encoding;

            if (!supportsGpu)
            {
                _gpuStatusLabel.Text = "The selected intermediate/lossless codec uses CPU encoding.";
                return;
            }

            if (!_gpuCheckBox.Checked)
            {
                _gpuStatusLabel.Text = "GPU encoding is off. FFmpeg will use " + codec.CpuEncoder + ".";
                return;
            }

            var backend = Convert.ToString(_gpuBackendComboBox.SelectedItem, CultureInfo.InvariantCulture) ?? GpuBackends.Auto;
            var detection = _cachedCapabilities == null
                ? "Run Settings > FFmpeg > Test to inspect available hardware encoders."
                : "Detected FFmpeg GPU families: " + FfmpegProbe.SummarizeGpuEncoders(_cachedCapabilities) + ".";
            _gpuStatusLabel.Text = "Requested backend: " + backend + ". " + detection;
        }

        private void RefreshOutputColumn()
        {
            RefreshOutputColumn(false);
        }

        private void RefreshOutputColumn(bool resetStatus)
        {
            if (_sequenceGrid == null)
                return;

            foreach (DataGridViewRow row in _sequenceGrid.Rows)
            {
                var sequence = row.Tag as SequenceItem;
                if (sequence == null)
                    continue;

                var outputPath = GetOutputPathForDisplay(sequence);
                row.Cells[OutputColumnIndex].Value = GetOutputCellText(sequence);
                row.Cells[OutputColumnIndex].ToolTipText = outputPath;

                var currentStatus = Convert.ToString(row.Cells[StatusColumnIndex].Value, CultureInfo.InvariantCulture);
                if (!_encoding && sequence.IsContiguous &&
                    (resetStatus ||
                     string.Equals(currentStatus, "Ready", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(currentStatus, "Exists", StringComparison.OrdinalIgnoreCase)))
                {
                    var exists = OutputExists(sequence);
                    SetRowStatus(row, exists ? "Exists" : "Ready",
                        exists ? ArtTheme.Warning : ArtTheme.MutedText);
                }
            }
        }

        private string GetSelectedOutputMode()
        {
            return Convert.ToString(_outputModeComboBox.SelectedItem, CultureInfo.InvariantCulture) ?? OutputModes.Video;
        }

        private bool IsOpenExrMode()
        {
            return string.Equals(GetSelectedOutputMode(), OutputModes.OpenExrMultilayer, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateOutputModeUi()
        {
            if (_codecComboBox == null || _gpuOptionsPanel == null || _exrOptionsPanel == null)
                return;

            var openExr = IsOpenExrMode();
            _codecComboBox.Enabled = !openExr && !_encoding;
            _overrideFrameRateCheckBox.Enabled = !openExr && !_encoding;
            _frameRateControl.Enabled = !openExr && !_encoding && _overrideFrameRateCheckBox.Checked;
            _gpuOptionsPanel.Visible = !openExr;
            _exrOptionsPanel.Visible = openExr;
            _exrOptionsPanel.BringToFront();
            _gpuOptionsPanel.Enabled = !openExr && !_encoding;
            _exrOptionsPanel.Enabled = openExr && !_encoding;

            if (openExr)
            {
                _codecDescriptionLabel.Text = "Selected sequences are combined as named channel layers. " +
                    "All selected layers in a take must use the same frame range.";
                _outputNoteLabel.Text =
                    "One multilayer sequence per take is written to %take_path%\\EXR\\%take%_%0Nd.exr.";
            }
            else
            {
                var codec = GetSelectedCodec();
                _codecDescriptionLabel.Text = codec.Description +
                    "  Output: " + codec.Extension.ToUpperInvariant() +
                    (codec.PreservesAlpha ? "  /  alpha preserved" : "  /  no alpha");
                _outputNoteLabel.Text =
                    "Each video is written beside its source frames using the sequence name and codec container extension.";
            }

            UpdateGpuControls();
            UpdateExrCompressionUi(false);
            RefreshOutputColumn(true);
        }

        private ExrCompressionProfile GetSelectedExrCompression()
        {
            var profile = _exrCompressionComboBox == null ? null : _exrCompressionComboBox.SelectedItem as ExrCompressionProfile;
            return profile ?? _exrCompressions[0];
        }

        private void UpdateExrCompressionUi(bool resetLevel)
        {
            if (_exrCompressionComboBox == null || _exrCompressionLevelControl == null)
                return;

            var profile = GetSelectedExrCompression();
            _exrCompressionDescriptionLabel.Text = profile.Description;
            _exrCompressionLevelLabel.Text = profile.UsesLevel ? "Level" : "Level (n/a)";
            _exrCompressionLevelControl.Enabled = profile.UsesLevel && IsOpenExrMode() && !_encoding;

            if (!profile.UsesLevel)
                return;

            _exrCompressionLevelControl.Minimum = profile.MinimumLevel;
            _exrCompressionLevelControl.Maximum = profile.MaximumLevel;
            _exrCompressionLevelControl.DecimalPlaces = profile.DecimalPlaces;
            _exrCompressionLevelControl.Increment = profile.Increment;
            if (resetLevel)
                _exrCompressionLevelControl.Value = profile.DefaultLevel;
            else
                _exrCompressionLevelControl.Value = ClampDecimal(
                    _exrCompressionLevelControl.Value,
                    profile.MinimumLevel,
                    profile.MaximumLevel);
        }

        private string GetOutputPathForDisplay(SequenceItem sequence)
        {
            if (IsOpenExrMode())
                return OpenExrJobBuilder.GetAbsoluteOutputPattern(sequence);

            return sequence.GetOutputPath(GetSelectedCodec().Extension);
        }

        private string GetOutputCellText(SequenceItem sequence)
        {
            if (IsOpenExrMode())
                return OpenExrJobBuilder.GetDisplayOutputPattern(sequence);

            return Path.GetFileName(sequence.GetOutputPath(GetSelectedCodec().Extension));
        }

        private bool OutputExists(SequenceItem sequence)
        {
            if (IsOpenExrMode())
                return OpenExrJobBuilder.ExpectedOutputExists(sequence);

            return File.Exists(sequence.GetOutputPath(GetSelectedCodec().Extension));
        }

        private CodecProfile GetSelectedCodec()
        {
            var codec = _codecComboBox == null ? null : _codecComboBox.SelectedItem as CodecProfile;
            return codec ?? _codecs.First(codecItem => codecItem.Id == "prores_422_hq");
        }

        private void BrowseFfmpegClicked(object sender, EventArgs eventArgs)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "FFmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executable files (*.exe)|*.exe";
                dialog.Title = "Select ffmpeg.exe";
                dialog.CheckFileExists = true;
                var currentPath = _ffmpegPathTextBox.Text.Trim();
                if (File.Exists(currentPath))
                    dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _ffmpegPathTextBox.Text = dialog.FileName;
            }
        }

        private void TestFfmpegClicked(object sender, EventArgs eventArgs)
        {
            if (_encoding)
                return;

            var ffmpegPath = ResolveFfmpegPath(_ffmpegPathTextBox.Text.Trim());
            if (ffmpegPath == null)
            {
                MessageBox.Show(this, "ffmpeg.exe was not found.", "FFmpeg required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                _ffmpegPathTextBox.Text = ffmpegPath;
                var capabilities = GetFfmpegCapabilities(ffmpegPath, true);
                var version = string.IsNullOrWhiteSpace(capabilities.VersionLine) ? "FFmpeg detected" : capabilities.VersionLine;
                var gpuSummary = FfmpegProbe.SummarizeGpuEncoders(capabilities);
                var gpuControllers = GpuHardwareDetector.SummarizeControllers();
                _ffmpegStatusLabel.Text = version + "  /  FFmpeg GPU: " + gpuSummary;
                AppendLog("FFmpeg test: " + version);
                AppendLog("Windows video controllers: " + gpuControllers + ".");
                AppendLog("Available FFmpeg GPU encoder families: " + gpuSummary + ".");
                UpdateGpuControls();
                MessageBox.Show(this, version + "\r\n\r\nWindows GPU: " + gpuControllers +
                    "\r\nFFmpeg GPU encoder families: " + gpuSummary,
                    "FFmpeg test successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                _ffmpegStatusLabel.Text = "Test failed: " + exception.Message;
                AppendLog("FFmpeg test failed: " + exception.Message);
                MessageBox.Show(this, exception.Message, "FFmpeg test failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void BrowseOiiotoolClicked(object sender, EventArgs eventArgs)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "OpenImageIO executable (oiiotool.exe)|oiiotool.exe|Executable files (*.exe)|*.exe";
                dialog.Title = "Select oiiotool.exe";
                dialog.CheckFileExists = true;
                var currentPath = _oiiotoolPathTextBox.Text.Trim();
                if (File.Exists(currentPath))
                    dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _oiiotoolPathTextBox.Text = dialog.FileName;
            }
        }

        private void TestOiiotoolClicked(object sender, EventArgs eventArgs)
        {
            if (_encoding)
                return;

            var path = ResolveOiiotoolPath(_oiiotoolPathTextBox.Text.Trim());
            if (path == null)
            {
                MessageBox.Show(this, "oiiotool.exe was not found.", "OpenImageIO required", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                _oiiotoolPathTextBox.Text = path;
                var version = OpenImageIoProbe.Probe(path);
                _oiiotoolStatusLabel.Text = version;
                AppendLog("OpenImageIO test: " + version);
                MessageBox.Show(this, version, "OpenImageIO test successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                _oiiotoolStatusLabel.Text = "Test failed: " + exception.Message;
                AppendLog("OpenImageIO test failed: " + exception.Message);
                MessageBox.Show(this, exception.Message, "OpenImageIO test failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private FfmpegCapabilities GetFfmpegCapabilities(string ffmpegPath, bool force)
        {
            var fullPath = Path.GetFullPath(ffmpegPath);
            if (!force && _cachedCapabilities != null &&
                string.Equals(_cachedCapabilitiesPath, fullPath, StringComparison.OrdinalIgnoreCase))
                return _cachedCapabilities;

            _cachedCapabilities = FfmpegProbe.Probe(fullPath);
            _cachedCapabilitiesPath = fullPath;
            return _cachedCapabilities;
        }

    }
}
