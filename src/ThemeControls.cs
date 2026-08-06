using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ArtBatchEncoder
{
    internal static class ArtTheme
    {
        public static readonly Color Window = Color.FromArgb(42, 37, 34);
        public static readonly Color Header = Color.FromArgb(72, 63, 58);
        public static readonly Color Sidebar = Color.FromArgb(38, 35, 32);
        public static readonly Color Card = Color.FromArgb(78, 69, 65);
        public static readonly Color CardAlt = Color.FromArgb(70, 63, 59);
        public static readonly Color Input = Color.FromArgb(100, 91, 86);
        public static readonly Color Button = Color.FromArgb(103, 94, 89);
        public static readonly Color ButtonHover = Color.FromArgb(120, 108, 101);
        public static readonly Color Border = Color.FromArgb(137, 111, 91);
        public static readonly Color Accent = Color.FromArgb(226, 101, 0);
        public static readonly Color AccentHover = Color.FromArgb(242, 118, 13);
        public static readonly Color Text = Color.FromArgb(236, 229, 224);
        public static readonly Color MutedText = Color.FromArgb(190, 179, 172);
        public static readonly Color DisabledText = Color.FromArgb(130, 122, 117);
        public static readonly Color Success = Color.FromArgb(118, 200, 145);
        public static readonly Color Warning = Color.FromArgb(240, 176, 82);
        public static readonly Color Error = Color.FromArgb(242, 112, 105);
        public static readonly Color Info = Color.FromArgb(127, 178, 231);

        public static Button CreateButton(string text)
        {
            var button = new Button();
            button.Text = text;
            button.Height = 30;
            button.AutoSize = true;
            button.Padding = new Padding(10, 0, 10, 0);
            button.Margin = new Padding(4, 2, 4, 2);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ButtonHover;
            button.FlatAppearance.MouseDownBackColor = Accent;
            button.BackColor = Button;
            button.ForeColor = Text;
            button.Cursor = Cursors.Hand;
            return button;
        }

        public static Button CreatePrimaryButton(string text)
        {
            var button = CreateButton(text);
            button.BackColor = Accent;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 76, 0);
            button.Font = new Font("Consolas", 9.0f, FontStyle.Bold, GraphicsUnit.Point);
            button.Padding = new Padding(16, 0, 16, 0);
            button.Height = 32;
            return button;
        }

        public static TextBox CreateTextBox()
        {
            var textBox = new TextBox();
            textBox.BackColor = Input;
            textBox.ForeColor = Text;
            textBox.BorderStyle = BorderStyle.FixedSingle;
            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 3, 8, 3);
            return textBox;
        }

        public static Label CreateLabel(string text)
        {
            var label = new Label();
            label.Text = text;
            label.ForeColor = Text;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.AutoSize = true;
            return label;
        }

        public static Label CreateMutedLabel(string text)
        {
            var label = CreateLabel(text);
            label.ForeColor = MutedText;
            return label;
        }

        public static ComboBox CreateComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = Input;
            comboBox.ForeColor = Text;
            return comboBox;
        }

        public static CheckBox CreateCheckBox(string text)
        {
            var checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.AutoSize = true;
            checkBox.ForeColor = Text;
            checkBox.BackColor = Color.Transparent;
            checkBox.Margin = new Padding(2, 4, 14, 4);
            return checkBox;
        }

        public static RadioButton CreateRadioButton(string text)
        {
            var radioButton = new RadioButton();
            radioButton.Text = text;
            radioButton.AutoSize = true;
            radioButton.ForeColor = Text;
            radioButton.BackColor = Color.Transparent;
            radioButton.Margin = new Padding(2, 4, 18, 4);
            return radioButton;
        }
    }

    internal sealed class BorderPanel : Panel
    {
        public BorderPanel()
        {
            BackColor = ArtTheme.Card;
            DoubleBuffered = true;
            Padding = new Padding(1);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            using (var pen = new Pen(ArtTheme.Border))
            {
                eventArgs.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    internal sealed class FlatProgressBar : Control
    {
        private int _minimum;
        private int _maximum;
        private int _value;

        public int Minimum
        {
            get { return _minimum; }
            set
            {
                _minimum = value;
                if (_maximum < _minimum)
                    _maximum = _minimum;
                Value = _value;
            }
        }

        public int Maximum
        {
            get { return _maximum; }
            set
            {
                _maximum = Math.Max(value, _minimum);
                Value = _value;
            }
        }

        public int Value
        {
            get { return _value; }
            set
            {
                _value = Math.Max(_minimum, Math.Min(_maximum, value));
                Invalidate();
            }
        }

        public FlatProgressBar()
        {
            _minimum = 0;
            _maximum = 1000;
            _value = 0;
            Height = 15;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.None;
            using (var backgroundBrush = new SolidBrush(ArtTheme.Input))
                eventArgs.Graphics.FillRectangle(backgroundBrush, ClientRectangle);

            var range = Math.Max(1, _maximum - _minimum);
            var fraction = (_value - _minimum) / (double)range;
            var fillWidth = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, fraction)) * Width);
            if (fillWidth > 0)
            {
                using (var fillBrush = new SolidBrush(ArtTheme.Accent))
                    eventArgs.Graphics.FillRectangle(fillBrush, 0, 0, fillWidth, Height);
            }

            using (var pen = new Pen(ArtTheme.Border))
                eventArgs.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }
}
