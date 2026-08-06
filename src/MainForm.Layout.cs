using System;
using System.Drawing;
using System.Windows.Forms;

namespace ArtBatchEncoder
{
    internal sealed partial class MainForm
    {
        // UI composition is kept separate from application behavior.
        private void BuildInterface()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            root.BackColor = ArtTheme.Window;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);

            var body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.ColumnCount = 2;
            body.RowCount = 1;
            body.Margin = new Padding(0);
            body.Padding = new Padding(0);
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(body, 0, 1);

            body.Controls.Add(BuildSidebar(), 0, 0);

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = ArtTheme.Window;
            _contentHost.Padding = new Padding(14);
            body.Controls.Add(_contentHost, 1, 0);

            _encodePage = BuildEncodePage();
            _settingsPage = BuildSettingsPage();
            _logPage = BuildLogPage();

            _contentHost.Controls.Add(_logPage);
            _contentHost.Controls.Add(_settingsPage);
            _contentHost.Controls.Add(_encodePage);
        }

        private Control BuildHeader()
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = ArtTheme.Header;
            panel.Margin = new Padding(0);

            var title = new Label();
            title.Text = "ART BATCH ENCODER";
            title.Font = new Font("Consolas", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = ArtTheme.Text;
            title.AutoSize = true;
            title.Location = new Point(16, 9);
            panel.Controls.Add(title);

            var subtitle = new Label();
            subtitle.Text = "ADVANCED RECORDING TOOLS  /  VIDEO AND MULTILAYER OPENEXR";
            subtitle.Font = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.ForeColor = ArtTheme.MutedText;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(16, 32);
            panel.Controls.Add(subtitle);

            _headerStatusLabel = new Label();
            _headerStatusLabel.Text = "READY #0000";
            _headerStatusLabel.Font = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _headerStatusLabel.ForeColor = ArtTheme.MutedText;
            _headerStatusLabel.TextAlign = ContentAlignment.MiddleRight;
            _headerStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _headerStatusLabel.Size = new Size(220, 22);
            _headerStatusLabel.Location = new Point(1000, 18);
            panel.Controls.Add(_headerStatusLabel);
            panel.Resize += delegate
            {
                _headerStatusLabel.Left = Math.Max(0, panel.ClientSize.Width - _headerStatusLabel.Width - 18);
            };

            return panel;
        }

        private Control BuildSidebar()
        {
            var sidebar = new Panel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.BackColor = ArtTheme.Sidebar;
            sidebar.Padding = new Padding(10, 12, 10, 10);

            var navigation = new FlowLayoutPanel();
            navigation.Dock = DockStyle.Top;
            navigation.FlowDirection = FlowDirection.TopDown;
            navigation.WrapContents = false;
            navigation.AutoSize = true;
            navigation.Margin = new Padding(0);
            navigation.Padding = new Padding(0);
            navigation.BackColor = Color.Transparent;
            sidebar.Controls.Add(navigation);

            _encodeNavButton = CreateNavigationButton("Encode");
            _settingsNavButton = CreateNavigationButton("Settings");
            _logNavButton = CreateNavigationButton("Console");

            navigation.Controls.Add(_encodeNavButton);
            navigation.Controls.Add(_settingsNavButton);
            navigation.Controls.Add(_logNavButton);

            var footer = new Label();
            footer.Text = "ART Batch Encoder\r\nv" + ApplicationVersion + "\r\n\r\nDouble-click a row\r\nto open its folder.";
            footer.ForeColor = ArtTheme.DisabledText;
            footer.Font = new Font("Consolas", 8.0f, FontStyle.Regular, GraphicsUnit.Point);
            footer.TextAlign = ContentAlignment.BottomLeft;
            footer.Dock = DockStyle.Bottom;
            footer.Height = 92;
            sidebar.Controls.Add(footer);

            return sidebar;
        }

        private Button CreateNavigationButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.Width = 152;
            button.Height = 42;
            button.Margin = new Padding(0, 0, 0, 4);
            button.Padding = new Padding(8, 0, 0, 0);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ArtTheme.CardAlt;
            button.FlatAppearance.MouseDownBackColor = ArtTheme.Accent;
            button.BackColor = ArtTheme.Sidebar;
            button.ForeColor = ArtTheme.Text;
            button.Cursor = Cursors.Hand;
            return button;
        }

        private Panel BuildEncodePage()
        {
            var page = CreatePage();
            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 5;
            layout.Margin = new Padding(0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 326));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            page.Controls.Add(layout);

            Panel sourceContent;
            var sourceCard = CreateCard("Source", out sourceContent);
            sourceCard.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(sourceCard, 0, 0);

            var sourceLayout = new TableLayoutPanel();
            sourceLayout.Dock = DockStyle.Fill;
            sourceLayout.ColumnCount = 4;
            sourceLayout.RowCount = 3;
            sourceLayout.Margin = new Padding(0);
            sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            sourceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            sourceLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sourceContent.Controls.Add(sourceLayout);

            sourceLayout.Controls.Add(CreateFieldLabel("Mode"), 0, 0);
            var modePanel = new FlowLayoutPanel();
            modePanel.Dock = DockStyle.Fill;
            modePanel.FlowDirection = FlowDirection.LeftToRight;
            modePanel.WrapContents = false;
            modePanel.Margin = new Padding(0);
            modePanel.BackColor = Color.Transparent;
            _singleTakeRadio = ArtTheme.CreateRadioButton("Single ART JSON");
            _recordingFolderRadio = ArtTheme.CreateRadioButton("All takes in ART recording folder");
            _singleTakeRadio.Checked = true;
            modePanel.Controls.Add(_singleTakeRadio);
            modePanel.Controls.Add(_recordingFolderRadio);
            sourceLayout.Controls.Add(modePanel, 1, 0);
            sourceLayout.SetColumnSpan(modePanel, 3);

            sourceLayout.Controls.Add(CreateFieldLabel("Path"), 0, 1);
            _sourcePathTextBox = ArtTheme.CreateTextBox();
            sourceLayout.Controls.Add(_sourcePathTextBox, 1, 1);
            _browseSourceButton = ArtTheme.CreateButton("Browse...");
            _browseSourceButton.Dock = DockStyle.Fill;
            _browseSourceButton.Margin = new Padding(3);
            sourceLayout.Controls.Add(_browseSourceButton, 2, 1);
            _scanButton = ArtTheme.CreatePrimaryButton("Scan");
            _scanButton.Dock = DockStyle.Fill;
            _scanButton.Margin = new Padding(3);
            sourceLayout.Controls.Add(_scanButton, 3, 1);

            _sourceSummaryLabel = ArtTheme.CreateMutedLabel("Select an ART JSON file or an ART recording root folder.");
            _sourceSummaryLabel.Dock = DockStyle.Fill;
            _sourceSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            _sourceSummaryLabel.AutoEllipsis = true;
            sourceLayout.Controls.Add(_sourceSummaryLabel, 1, 2);
            sourceLayout.SetColumnSpan(_sourceSummaryLabel, 3);

            var outputParameters = BuildOutputParametersCard();
            outputParameters.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(outputParameters, 0, 1);

            var toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.Padding = new Padding(0, 4, 0, 4);
            toolbar.Margin = new Padding(0);
            toolbar.BackColor = ArtTheme.Window;
            layout.Controls.Add(toolbar, 0, 2);

            _selectAllButton = ArtTheme.CreateButton("Select all");
            _selectNoneButton = ArtTheme.CreateButton("Select none");
            _removeSelectedButton = ArtTheme.CreateButton("Remove selected");
            _reloadButton = ArtTheme.CreateButton("Reload");
            _openFolderButton = ArtTheme.CreateButton("Open folder");
            toolbar.Controls.Add(_selectAllButton);
            toolbar.Controls.Add(_selectNoneButton);
            toolbar.Controls.Add(_removeSelectedButton);
            toolbar.Controls.Add(_reloadButton);
            toolbar.Controls.Add(_openFolderButton);

            _sequenceGrid = BuildSequenceGrid();
            layout.Controls.Add(_sequenceGrid, 0, 3);

            Panel progressContent;
            var progressCard = CreateCard("Batch status", out progressContent);
            progressCard.Margin = new Padding(0, 10, 0, 0);
            layout.Controls.Add(progressCard, 0, 4);

            var progressLayout = new TableLayoutPanel();
            progressLayout.Dock = DockStyle.Fill;
            progressLayout.ColumnCount = 2;
            progressLayout.RowCount = 2;
            progressLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            progressLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            progressLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            progressLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            progressContent.Controls.Add(progressLayout);

            _progressBar = new FlatProgressBar();
            _progressBar.Dock = DockStyle.Fill;
            _progressBar.Margin = new Padding(0, 5, 10, 5);
            progressLayout.Controls.Add(_progressBar, 0, 0);

            _progressLabel = ArtTheme.CreateMutedLabel("Ready");
            _progressLabel.Dock = DockStyle.Fill;
            _progressLabel.TextAlign = ContentAlignment.MiddleRight;
            progressLayout.Controls.Add(_progressLabel, 1, 0);

            var actionPanel = new FlowLayoutPanel();
            actionPanel.Dock = DockStyle.Fill;
            actionPanel.FlowDirection = FlowDirection.RightToLeft;
            actionPanel.WrapContents = false;
            actionPanel.Margin = new Padding(0);
            actionPanel.Padding = new Padding(0, 3, 0, 0);
            actionPanel.BackColor = Color.Transparent;
            progressLayout.Controls.Add(actionPanel, 0, 1);
            progressLayout.SetColumnSpan(actionPanel, 2);

            _encodeButton = ArtTheme.CreatePrimaryButton("Encode selected");
            _cancelButton = ArtTheme.CreateButton("Cancel");
            _cancelButton.Enabled = false;
            actionPanel.Controls.Add(_encodeButton);
            actionPanel.Controls.Add(_cancelButton);

            return page;
        }

        private Control BuildOutputParametersCard()
        {
            Panel content;
            var card = CreateCard("Output parameters", out content);

            var columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Fill;
            columns.ColumnCount = 3;
            columns.RowCount = 1;
            columns.Margin = new Padding(0);
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
            content.Controls.Add(columns);

            var codecLayout = new TableLayoutPanel();
            codecLayout.Dock = DockStyle.Fill;
            codecLayout.ColumnCount = 2;
            codecLayout.RowCount = 6;
            codecLayout.Margin = new Padding(0, 0, 12, 0);
            codecLayout.Padding = new Padding(0);
            codecLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            codecLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            codecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            codecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            codecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            codecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            codecLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            codecLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            columns.Controls.Add(codecLayout, 0, 0);

            var codecTitle = CreateOutputSectionTitle("Output and frame rate");
            codecLayout.Controls.Add(codecTitle, 0, 0);
            codecLayout.SetColumnSpan(codecTitle, 2);

            codecLayout.Controls.Add(CreateFieldLabel("Output type"), 0, 1);
            _outputModeComboBox = ArtTheme.CreateComboBox();
            _outputModeComboBox.Dock = DockStyle.Fill;
            _outputModeComboBox.Margin = new Padding(0, 3, 0, 3);
            _outputModeComboBox.Items.AddRange(OutputModes.All);
            _outputModeComboBox.SelectedIndex = 0;
            codecLayout.Controls.Add(_outputModeComboBox, 1, 1);

            codecLayout.Controls.Add(CreateFieldLabel("FFmpeg codec"), 0, 2);
            _codecComboBox = ArtTheme.CreateComboBox();
            _codecComboBox.Dock = DockStyle.Fill;
            _codecComboBox.Margin = new Padding(0, 3, 0, 3);
            foreach (var codec in _codecs)
                _codecComboBox.Items.Add(codec);
            codecLayout.Controls.Add(_codecComboBox, 1, 2);

            _codecDescriptionLabel = ArtTheme.CreateMutedLabel(string.Empty);
            _codecDescriptionLabel.Dock = DockStyle.Fill;
            _codecDescriptionLabel.AutoSize = false;
            _codecDescriptionLabel.TextAlign = ContentAlignment.TopLeft;
            _codecDescriptionLabel.Margin = new Padding(0, 4, 4, 2);
            codecLayout.Controls.Add(_codecDescriptionLabel, 0, 3);
            codecLayout.SetColumnSpan(_codecDescriptionLabel, 2);

            _overrideFrameRateCheckBox = ArtTheme.CreateCheckBox("Override FPS");
            _overrideFrameRateCheckBox.AutoSize = false;
            _overrideFrameRateCheckBox.Dock = DockStyle.Fill;
            _overrideFrameRateCheckBox.Margin = new Padding(2, 9, 6, 2);
            codecLayout.Controls.Add(_overrideFrameRateCheckBox, 0, 4);

            _frameRateControl = new NumericUpDown();
            _frameRateControl.Minimum = 1;
            _frameRateControl.Maximum = 1000;
            _frameRateControl.DecimalPlaces = 3;
            _frameRateControl.Increment = 1;
            _frameRateControl.Value = 60;
            _frameRateControl.BackColor = ArtTheme.Input;
            _frameRateControl.ForeColor = ArtTheme.Text;
            _frameRateControl.BorderStyle = BorderStyle.FixedSingle;
            _frameRateControl.Dock = DockStyle.Left;
            _frameRateControl.Width = 140;
            _frameRateControl.Margin = new Padding(0, 7, 0, 5);
            _frameRateControl.Enabled = false;
            codecLayout.Controls.Add(_frameRateControl, 1, 4);

            _outputNoteLabel = ArtTheme.CreateMutedLabel(
                "Each video is written beside its source frames using the sequence name and codec container extension.");
            _outputNoteLabel.Dock = DockStyle.Fill;
            _outputNoteLabel.AutoSize = false;
            _outputNoteLabel.TextAlign = ContentAlignment.TopLeft;
            _outputNoteLabel.Margin = new Padding(0, 4, 4, 0);
            codecLayout.Controls.Add(_outputNoteLabel, 0, 5);
            codecLayout.SetColumnSpan(_outputNoteLabel, 2);

            var modeOptionsHost = new Panel();
            modeOptionsHost.Dock = DockStyle.Fill;
            modeOptionsHost.Margin = new Padding(0, 0, 12, 0);
            modeOptionsHost.BackColor = Color.Transparent;
            columns.Controls.Add(modeOptionsHost, 1, 0);

            _gpuOptionsPanel = BuildGpuOptionsPanel();
            _exrOptionsPanel = BuildExrOptionsPanel();
            modeOptionsHost.Controls.Add(_exrOptionsPanel);
            modeOptionsHost.Controls.Add(_gpuOptionsPanel);

            var safetyLayout = new TableLayoutPanel();
            safetyLayout.Dock = DockStyle.Fill;
            safetyLayout.ColumnCount = 1;
            safetyLayout.RowCount = 5;
            safetyLayout.Margin = new Padding(0);
            safetyLayout.Padding = new Padding(0);
            safetyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            safetyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            safetyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            safetyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            safetyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            columns.Controls.Add(safetyLayout, 2, 0);

            safetyLayout.Controls.Add(CreateOutputSectionTitle("Output safety"), 0, 0);

            _overwriteCheckBox = ArtTheme.CreateCheckBox("Overwrite existing output files");
            _overwriteCheckBox.Checked = true;
            _overwriteCheckBox.Dock = DockStyle.Fill;
            safetyLayout.Controls.Add(_overwriteCheckBox, 0, 1);

            _deleteFramesCheckBox = ArtTheme.CreateCheckBox(
                "Delete screenshots after the entire selected batch succeeds");
            _deleteFramesCheckBox.Checked = false;
            _deleteFramesCheckBox.Dock = DockStyle.Fill;
            _deleteFramesCheckBox.AutoSize = false;
            _deleteFramesCheckBox.TextAlign = ContentAlignment.MiddleLeft;
            safetyLayout.Controls.Add(_deleteFramesCheckBox, 0, 2);

            var deletionNote = ArtTheme.CreateMutedLabel(
                "Any failure, cancellation, skipped output, or frame gap keeps every screenshot.");
            deletionNote.Dock = DockStyle.Fill;
            deletionNote.AutoSize = false;
            deletionNote.TextAlign = ContentAlignment.TopLeft;
            deletionNote.Margin = new Padding(2, 6, 2, 2);
            safetyLayout.Controls.Add(deletionNote, 0, 3);

            var topActionPanel = new FlowLayoutPanel();
            topActionPanel.Dock = DockStyle.Fill;
            topActionPanel.FlowDirection = FlowDirection.RightToLeft;
            topActionPanel.WrapContents = false;
            topActionPanel.Margin = new Padding(0);
            topActionPanel.Padding = new Padding(0, 3, 0, 0);
            topActionPanel.BackColor = Color.Transparent;
            safetyLayout.Controls.Add(topActionPanel, 0, 4);

            _encodeTopButton = ArtTheme.CreatePrimaryButton("Encode selected");
            _cancelTopButton = ArtTheme.CreateButton("Cancel");
            _cancelTopButton.Enabled = false;
            topActionPanel.Controls.Add(_encodeTopButton);
            topActionPanel.Controls.Add(_cancelTopButton);

            return card;
        }

        private Panel BuildGpuOptionsPanel()
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 4;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(0);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(layout);

            var title = CreateOutputSectionTitle("GPU encoding");
            layout.Controls.Add(title, 0, 0);
            layout.SetColumnSpan(title, 2);

            _gpuCheckBox = ArtTheme.CreateCheckBox("Use GPU encoder");
            _gpuCheckBox.Checked = false;
            _gpuCheckBox.Dock = DockStyle.Fill;
            layout.Controls.Add(_gpuCheckBox, 0, 1);
            layout.SetColumnSpan(_gpuCheckBox, 2);

            layout.Controls.Add(CreateFieldLabel("GPU backend"), 0, 2);
            _gpuBackendComboBox = ArtTheme.CreateComboBox();
            _gpuBackendComboBox.Items.AddRange(GpuBackends.All);
            _gpuBackendComboBox.SelectedIndex = 0;
            _gpuBackendComboBox.Dock = DockStyle.Fill;
            _gpuBackendComboBox.Margin = new Padding(0, 3, 0, 3);
            _gpuBackendComboBox.Enabled = false;
            layout.Controls.Add(_gpuBackendComboBox, 1, 2);

            _gpuStatusLabel = ArtTheme.CreateMutedLabel("GPU encoding is off by default.");
            _gpuStatusLabel.Dock = DockStyle.Fill;
            _gpuStatusLabel.AutoSize = false;
            _gpuStatusLabel.TextAlign = ContentAlignment.TopLeft;
            _gpuStatusLabel.Margin = new Padding(0, 6, 4, 0);
            layout.Controls.Add(_gpuStatusLabel, 0, 3);
            layout.SetColumnSpan(_gpuStatusLabel, 2);

            return panel;
        }

        private Panel BuildExrOptionsPanel()
        {
            var panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Transparent;
            panel.Visible = false;

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 5;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(0);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(layout);

            var title = CreateOutputSectionTitle("OpenEXR multilayer");
            layout.Controls.Add(title, 0, 0);
            layout.SetColumnSpan(title, 2);

            layout.Controls.Add(CreateFieldLabel("Compression"), 0, 1);
            _exrCompressionComboBox = ArtTheme.CreateComboBox();
            _exrCompressionComboBox.Dock = DockStyle.Fill;
            _exrCompressionComboBox.Margin = new Padding(0, 3, 0, 3);
            foreach (var compression in _exrCompressions)
                _exrCompressionComboBox.Items.Add(compression);
            _exrCompressionComboBox.SelectedIndex = 0;
            layout.Controls.Add(_exrCompressionComboBox, 1, 1);

            _exrCompressionLevelLabel = CreateFieldLabel("Level");
            layout.Controls.Add(_exrCompressionLevelLabel, 0, 2);

            _exrCompressionLevelControl = new NumericUpDown();
            _exrCompressionLevelControl.Minimum = 1;
            _exrCompressionLevelControl.Maximum = 9;
            _exrCompressionLevelControl.Value = 4;
            _exrCompressionLevelControl.DecimalPlaces = 0;
            _exrCompressionLevelControl.Increment = 1;
            _exrCompressionLevelControl.BackColor = ArtTheme.Input;
            _exrCompressionLevelControl.ForeColor = ArtTheme.Text;
            _exrCompressionLevelControl.BorderStyle = BorderStyle.FixedSingle;
            _exrCompressionLevelControl.Dock = DockStyle.Left;
            _exrCompressionLevelControl.Width = 140;
            _exrCompressionLevelControl.Margin = new Padding(0, 7, 0, 5);
            layout.Controls.Add(_exrCompressionLevelControl, 1, 2);

            _exrCompressionDescriptionLabel = ArtTheme.CreateMutedLabel(string.Empty);
            _exrCompressionDescriptionLabel.Dock = DockStyle.Fill;
            _exrCompressionDescriptionLabel.AutoSize = false;
            _exrCompressionDescriptionLabel.TextAlign = ContentAlignment.TopLeft;
            _exrCompressionDescriptionLabel.Margin = new Padding(0, 5, 4, 0);
            layout.Controls.Add(_exrCompressionDescriptionLabel, 0, 3);
            layout.SetColumnSpan(_exrCompressionDescriptionLabel, 2);

            var note = ArtTheme.CreateMutedLabel(
                "Selected sequences become named channel layers in one EXR sequence per take.");
            note.Dock = DockStyle.Fill;
            note.AutoSize = false;
            note.TextAlign = ContentAlignment.TopLeft;
            note.Margin = new Padding(0, 4, 4, 0);
            layout.Controls.Add(note, 0, 4);
            layout.SetColumnSpan(note, 2);

            return panel;
        }

        private static Label CreateOutputSectionTitle(string text)
        {
            var label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = ArtTheme.Text;
            label.Font = new Font("Consolas", 8.7f, FontStyle.Bold, GraphicsUnit.Point);
            return label;
        }

        private Panel BuildSettingsPage()
        {
            var page = CreatePage();
            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Top;
            layout.AutoSize = true;
            layout.ColumnCount = 1;
            layout.RowCount = 4;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            page.Controls.Add(layout);

            Panel ffmpegContent;
            var ffmpegCard = CreateCard("FFmpeg", out ffmpegContent);
            ffmpegCard.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(ffmpegCard, 0, 0);

            var ffmpegLayout = BuildExecutableSettingsLayout();
            ffmpegContent.Controls.Add(ffmpegLayout);

            ffmpegLayout.Controls.Add(CreateFieldLabel("Path"), 0, 0);
            _ffmpegPathTextBox = ArtTheme.CreateTextBox();
            ffmpegLayout.Controls.Add(_ffmpegPathTextBox, 1, 0);
            _browseFfmpegButton = ArtTheme.CreateButton("Browse...");
            _browseFfmpegButton.Dock = DockStyle.Fill;
            _browseFfmpegButton.Margin = new Padding(3);
            ffmpegLayout.Controls.Add(_browseFfmpegButton, 2, 0);
            _testFfmpegButton = ArtTheme.CreateButton("Test");
            _testFfmpegButton.Dock = DockStyle.Fill;
            _testFfmpegButton.Margin = new Padding(3);
            ffmpegLayout.Controls.Add(_testFfmpegButton, 3, 0);

            _ffmpegStatusLabel = ArtTheme.CreateMutedLabel("Not tested");
            _ffmpegStatusLabel.Dock = DockStyle.Fill;
            _ffmpegStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            ffmpegLayout.Controls.Add(_ffmpegStatusLabel, 1, 1);
            ffmpegLayout.SetColumnSpan(_ffmpegStatusLabel, 3);

            var ffmpegNote = ArtTheme.CreateMutedLabel(
                "Search order: <exe>\\ffmpeg\\ffmpeg.exe, saved/manual path, beside the executable, tools folder, then Windows PATH.");
            ffmpegNote.Dock = DockStyle.Fill;
            ffmpegNote.AutoSize = false;
            ffmpegNote.TextAlign = ContentAlignment.MiddleLeft;
            ffmpegLayout.Controls.Add(ffmpegNote, 0, 2);
            ffmpegLayout.SetColumnSpan(ffmpegNote, 4);

            Panel oiioContent;
            var oiioCard = CreateCard("OpenImageIO / multilayer EXR", out oiioContent);
            oiioCard.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(oiioCard, 0, 1);

            var oiioLayout = BuildExecutableSettingsLayout();
            oiioContent.Controls.Add(oiioLayout);

            oiioLayout.Controls.Add(CreateFieldLabel("Path"), 0, 0);
            _oiiotoolPathTextBox = ArtTheme.CreateTextBox();
            oiioLayout.Controls.Add(_oiiotoolPathTextBox, 1, 0);
            _browseOiiotoolButton = ArtTheme.CreateButton("Browse...");
            _browseOiiotoolButton.Dock = DockStyle.Fill;
            _browseOiiotoolButton.Margin = new Padding(3);
            oiioLayout.Controls.Add(_browseOiiotoolButton, 2, 0);
            _testOiiotoolButton = ArtTheme.CreateButton("Test");
            _testOiiotoolButton.Dock = DockStyle.Fill;
            _testOiiotoolButton.Margin = new Padding(3);
            oiioLayout.Controls.Add(_testOiiotoolButton, 3, 0);

            _oiiotoolStatusLabel = ArtTheme.CreateMutedLabel("Not tested");
            _oiiotoolStatusLabel.Dock = DockStyle.Fill;
            _oiiotoolStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            oiioLayout.Controls.Add(_oiiotoolStatusLabel, 1, 1);
            oiioLayout.SetColumnSpan(_oiiotoolStatusLabel, 3);

            var oiioNote = ArtTheme.CreateMutedLabel(
                "Place the OpenImageIO Windows runtime in <exe>\\openimageio, including oiiotool.exe and its DLL files. It is only required for multilayer EXR output.");
            oiioNote.Dock = DockStyle.Fill;
            oiioNote.AutoSize = false;
            oiioNote.TextAlign = ContentAlignment.MiddleLeft;
            oiioLayout.Controls.Add(oiioNote, 0, 2);
            oiioLayout.SetColumnSpan(oiioNote, 4);

            Panel memoryContent;
            var memoryCard = CreateCard("Startup", out memoryContent);
            memoryCard.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(memoryCard, 0, 2);

            var memoryLayout = new TableLayoutPanel();
            memoryLayout.Dock = DockStyle.Fill;
            memoryLayout.ColumnCount = 1;
            memoryLayout.RowCount = 2;
            memoryLayout.Margin = new Padding(0);
            memoryLayout.Padding = new Padding(0);
            memoryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            memoryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            memoryContent.Controls.Add(memoryLayout);

            _rememberLastFolderCheckBox = ArtTheme.CreateCheckBox("Remember last folder and reopen it on startup");
            _rememberLastFolderCheckBox.Checked = true;
            _rememberLastFolderCheckBox.Dock = DockStyle.Fill;
            _rememberLastFolderCheckBox.Margin = new Padding(2, 2, 2, 4);
            memoryLayout.Controls.Add(_rememberLastFolderCheckBox, 0, 0);

            var memoryNote = ArtTheme.CreateMutedLabel(
                "All application and output settings are stored in artbe_settings.ini beside ARTBatchEncoder.exe. " +
                "When folder memory is disabled, the last source paths are cleared from the INI file.");
            memoryNote.Dock = DockStyle.Fill;
            memoryNote.AutoSize = false;
            memoryNote.TextAlign = ContentAlignment.TopLeft;
            memoryNote.Margin = new Padding(2, 7, 2, 2);
            memoryLayout.Controls.Add(memoryNote, 0, 1);

            Panel aboutContent;
            var aboutCard = CreateCard("About", out aboutContent);
            layout.Controls.Add(aboutCard, 0, 3);

            var about = ArtTheme.CreateMutedLabel(
                "ART Batch Encoder v" + ApplicationVersion + "\r\n\r\n" +
                "Reads ART JSON manifests with any filename, detects numbered TGA/PNG/BMP/JPEG/TIFF/EXR/DPX sequences, " +
                "encodes video through FFmpeg, and can combine selected passes into named layers in an OpenEXR sequence. " +
                "Batch-folder mode recursively detects JSON manifests below the selected ART recording root.");
            about.Dock = DockStyle.Fill;
            about.AutoSize = false;
            about.TextAlign = ContentAlignment.TopLeft;
            aboutContent.Controls.Add(about);

            return page;
        }

        private static TableLayoutPanel BuildExecutableSettingsLayout()
        {
            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 4;
            layout.RowCount = 3;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return layout;
        }

        private Panel BuildLogPage()
        {
            var page = CreatePage();
            Panel logContent;
            var logCard = CreateCard("Console", out logContent);
            logCard.Dock = DockStyle.Fill;
            page.Controls.Add(logCard);

            var logLayout = new TableLayoutPanel();
            logLayout.Dock = DockStyle.Fill;
            logLayout.ColumnCount = 1;
            logLayout.RowCount = 2;
            logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            logContent.Controls.Add(logLayout);

            var logToolbar = new FlowLayoutPanel();
            logToolbar.Dock = DockStyle.Fill;
            logToolbar.FlowDirection = FlowDirection.LeftToRight;
            logToolbar.WrapContents = false;
            logToolbar.BackColor = Color.Transparent;
            var clearButton = ArtTheme.CreateButton("Clear console");
            clearButton.Click += delegate { _logTextBox.Clear(); };
            logToolbar.Controls.Add(clearButton);
            logLayout.Controls.Add(logToolbar, 0, 0);

            _logTextBox = new RichTextBox();
            _logTextBox.Dock = DockStyle.Fill;
            _logTextBox.BorderStyle = BorderStyle.FixedSingle;
            _logTextBox.BackColor = Color.FromArgb(31, 29, 27);
            _logTextBox.ForeColor = ArtTheme.Text;
            _logTextBox.Font = new Font("Consolas", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _logTextBox.ReadOnly = true;
            _logTextBox.WordWrap = false;
            _logTextBox.DetectUrls = false;
            logLayout.Controls.Add(_logTextBox, 0, 1);

            return page;
        }

        private static Panel CreatePage()
        {
            var page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = ArtTheme.Window;
            page.Visible = false;
            page.AutoScroll = true;
            return page;
        }

        private static BorderPanel CreateCard(string title, out Panel content)
        {
            var card = new BorderPanel();
            card.Dock = DockStyle.Fill;

            var layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.Margin = new Padding(0);
            layout.Padding = new Padding(0);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.Controls.Add(layout);

            var titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Padding = new Padding(10, 0, 0, 0);
            titleLabel.ForeColor = ArtTheme.Text;
            titleLabel.BackColor = ArtTheme.CardAlt;
            titleLabel.Font = new Font("Consolas", 9.0f, FontStyle.Bold, GraphicsUnit.Point);
            layout.Controls.Add(titleLabel, 0, 0);

            content = new Panel();
            content.Dock = DockStyle.Fill;
            content.BackColor = ArtTheme.Card;
            content.Padding = new Padding(10, 7, 10, 7);
            layout.Controls.Add(content, 0, 1);

            return card;
        }

        private static Label CreateFieldLabel(string text)
        {
            var label = ArtTheme.CreateMutedLabel(text);
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0);
            return label;
        }

        private DataGridView BuildSequenceGrid()
        {
            var grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = ArtTheme.Card;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = Color.FromArgb(91, 80, 74);
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.AutoGenerateColumns = false;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersDefaultCellStyle.BackColor = ArtTheme.CardAlt;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ArtTheme.Text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ArtTheme.CardAlt;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Consolas", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            grid.ColumnHeadersHeight = 31;
            grid.RowTemplate.Height = 27;
            grid.DefaultCellStyle.BackColor = ArtTheme.Card;
            grid.DefaultCellStyle.ForeColor = ArtTheme.Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(116, 78, 51);
            grid.DefaultCellStyle.SelectionForeColor = ArtTheme.Text;
            grid.DefaultCellStyle.Font = new Font("Consolas", 8.2f, FontStyle.Regular, GraphicsUnit.Point);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(73, 65, 61);
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "Use",
                Width = 44,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Take",
                Width = 128,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Sequence",
                Width = 128,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "FPS",
                Width = 62,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Frames",
                Width = 70,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Range",
                Width = 92,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Sequence folder",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                FillWeight = 58,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Output",
                Width = 170,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Status",
                Width = 104,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            return grid;
        }

    }
}
