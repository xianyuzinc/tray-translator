using System;
using System.Drawing;
using System.Windows.Forms;

namespace TrayTranslator.UI
{
    public class ReaderForm : Form
    {
        private readonly RichTextBox _textBox;
        private readonly Label _titleLabel;
        private readonly Button _copyButton;
        private readonly Button _smallerButton;
        private readonly Button _largerButton;
        private readonly Button _closeButton;
        private readonly string _text;
        private float _fontSize;

        private static readonly Color BackgroundColor = Color.FromArgb(248, 250, 252);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color TextColor = Color.FromArgb(24, 31, 42);
        private static readonly Color MutedColor = Color.FromArgb(105, 115, 130);
        private static readonly Color BorderColor = Color.FromArgb(218, 226, 235);
        private static readonly Color AccentColor = Color.FromArgb(0, 122, 255);

        public ReaderForm(string engineName, string text, float uiFontSize)
        {
            _text = text ?? "";
            _fontSize = Math.Max(11F, Math.Min(22F, uiFontSize + 1.5F));

            Text = engineName + " 翻译结果";
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            Font = new Font("Microsoft YaHei UI", Math.Max(10F, uiFontSize));
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(920, 680);
            MinimumSize = new Size(620, 420);
            ShowInTaskbar = false;
            TopMost = true;

            _titleLabel = new Label
            {
                Text = engineName + " 翻译结果",
                AutoSize = false,
                Font = new Font("Microsoft YaHei UI", Math.Max(11F, uiFontSize + 1.5F), FontStyle.Bold),
                ForeColor = TextColor,
                Location = new Point(18, 15),
                Size = new Size(360, 28)
            };

            _copyButton = CreateToolbarButton("复制全文");
            _copyButton.Click += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(_text))
                {
                    Clipboard.SetText(_text, TextDataFormat.UnicodeText);
                }
            };

            _smallerButton = CreateToolbarButton("A-");
            _smallerButton.Click += (sender, args) => ChangeFontSize(-1F);

            _largerButton = CreateToolbarButton("A+");
            _largerButton.Click += (sender, args) => ChangeFontSize(1F);

            _closeButton = CreateToolbarButton("关闭");
            _closeButton.Click += (sender, args) => Close();

            _textBox = new RichTextBox
            {
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = SurfaceColor,
                ForeColor = TextColor,
                Text = _text,
                Location = new Point(18, 58),
                Size = new Size(ClientSize.Width - 36, ClientSize.Height - 76),
                WordWrap = true
            };
            ApplyBodyFont();

            Controls.Add(_titleLabel);
            Controls.Add(_copyButton);
            Controls.Add(_smallerButton);
            Controls.Add(_largerButton);
            Controls.Add(_closeButton);
            Controls.Add(_textBox);
            LayoutControls();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.DrawRectangle(pen, _textBox.Left - 1, _textBox.Top - 1, _textBox.Width + 1, _textBox.Height + 1);
            }
        }

        private void LayoutControls()
        {
            if (_textBox == null)
            {
                return;
            }

            int right = ClientSize.Width - 18;
            _closeButton.SetBounds(right - 64, 14, 64, 30);
            _largerButton.SetBounds(_closeButton.Left - 48, 14, 42, 30);
            _smallerButton.SetBounds(_largerButton.Left - 48, 14, 42, 30);
            _copyButton.SetBounds(_smallerButton.Left - 90, 14, 84, 30);
            _titleLabel.SetBounds(18, 15, Math.Max(120, _copyButton.Left - 28), 28);
            _textBox.SetBounds(18, 58, Math.Max(200, ClientSize.Width - 36), Math.Max(120, ClientSize.Height - 76));
        }

        private static Button CreateToolbarButton(string text)
        {
            var button = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 10F),
                ForeColor = AccentColor,
                BackColor = SurfaceColor,
                TabStop = false
            };
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 249, 255);
            return button;
        }

        private void ChangeFontSize(float delta)
        {
            _fontSize = Math.Max(9F, Math.Min(28F, _fontSize + delta));
            ApplyBodyFont();
        }

        private void ApplyBodyFont()
        {
            _textBox.Font = new Font("Microsoft YaHei UI", _fontSize);
        }
    }
}
