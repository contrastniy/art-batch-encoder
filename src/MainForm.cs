using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ArtBatchEncoder
{
    internal sealed partial class MainForm : Form
    {
        private const string ApplicationName = "ART Batch Encoder";
        private const string ApplicationVersion = "1.0";

        private const int IncludeColumnIndex = 0;
        private const int TakeColumnIndex = 1;
        private const int NameColumnIndex = 2;
        private const int FpsColumnIndex = 3;
        private const int FramesColumnIndex = 4;
        private const int RangeColumnIndex = 5;
        private const int FolderColumnIndex = 6;
        private const int OutputColumnIndex = 7;
        private const int StatusColumnIndex = 8;

        private readonly List<CodecProfile> _codecs;
        private readonly List<ExrCompressionProfile> _exrCompressions;
        private readonly AppSettings _settings;

        private Panel _contentHost;
        private Panel _encodePage;
        private Panel _settingsPage;
        private Panel _logPage;
        private Button _encodeNavButton;
        private Button _settingsNavButton;
        private Button _logNavButton;
        private Label _headerStatusLabel;

        private RadioButton _singleTakeRadio;
        private RadioButton _recordingFolderRadio;
        private TextBox _sourcePathTextBox;
        private Button _browseSourceButton;
        private Button _scanButton;
        private Label _sourceSummaryLabel;
        private DataGridView _sequenceGrid;
        private Button _selectAllButton;
        private Button _selectNoneButton;
        private Button _reloadButton;
        private Button _removeSelectedButton;
        private Button _openFolderButton;

        private ComboBox _outputModeComboBox;
        private ComboBox _codecComboBox;
        private Label _codecDescriptionLabel;
        private CheckBox _overrideFrameRateCheckBox;
        private NumericUpDown _frameRateControl;
        private Panel _gpuOptionsPanel;
        private Panel _exrOptionsPanel;
        private CheckBox _gpuCheckBox;
        private ComboBox _gpuBackendComboBox;
        private Label _gpuStatusLabel;
        private ComboBox _exrCompressionComboBox;
        private Label _exrCompressionDescriptionLabel;
        private Label _exrCompressionLevelLabel;
        private NumericUpDown _exrCompressionLevelControl;
        private Label _outputNoteLabel;
        private CheckBox _overwriteCheckBox;
        private CheckBox _deleteFramesCheckBox;

        private TextBox _ffmpegPathTextBox;
        private Button _browseFfmpegButton;
        private Button _testFfmpegButton;
        private Label _ffmpegStatusLabel;
        private TextBox _oiiotoolPathTextBox;
        private Button _browseOiiotoolButton;
        private Button _testOiiotoolButton;
        private Label _oiiotoolStatusLabel;
        private CheckBox _rememberLastFolderCheckBox;

        private FlatProgressBar _progressBar;
        private Label _progressLabel;
        private Button _encodeButton;
        private Button _encodeTopButton;
        private Button _cancelTopButton;
        private Button _cancelButton;
        private RichTextBox _logTextBox;

        private BatchLoadResult _loadedBatch;
        private Process _currentProcess;
        private volatile bool _cancelRequested;
        private bool _encoding;
        private FfmpegCapabilities _cachedCapabilities;
        private string _cachedCapabilitiesPath;

        public MainForm()
        {
            _codecs = CodecCatalog.CreateAll();
            _exrCompressions = ExrCompressionCatalog.CreateAll();
            _settings = SettingsStore.Load();

            Text = ApplicationName + " v" + ApplicationVersion;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1040, 700);
            Size = new Size(1260, 830);
            BackColor = ArtTheme.Window;
            ForeColor = ArtTheme.Text;
            Font = new Font("Consolas", 9.0f, FontStyle.Regular, GraphicsUnit.Point);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            BuildInterface();
            WireEvents();
            ShowPage(_encodePage, _encodeNavButton);
        }
    }
}
