using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TrayTranslator.Models;

namespace TrayTranslator.UI
{
    public class PopupForm : Form
    {
        private const int WmNclButtonDown = 0x00A1;
        private const int HtCaption = 0x0002;

        private readonly Label _titleLabel;
        private readonly ComboBox _sourceLanguage;
        private readonly ComboBox _targetLanguage;
        private readonly Label _sourceLabel;
        private readonly Label _noticeLabel;
        private readonly ResultCard _deepLCard;
        private readonly ResultCard _googleCard;
        private readonly ResultCard _baiduCard;
        private readonly ResultCard _aiCard;

        private static readonly Color BackgroundColor = Color.FromArgb(248, 250, 252);
        private static readonly Color HeaderColor = Color.FromArgb(253, 254, 255);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color BorderColor = Color.FromArgb(218, 226, 235);
        private static readonly Color SoftBorderColor = Color.FromArgb(232, 237, 244);
        private static readonly Color TextColor = Color.FromArgb(24, 31, 42);
        private static readonly Color MutedColor = Color.FromArgb(105, 115, 130);
        private static readonly Color AccentColor = Color.FromArgb(0, 122, 255);
        private readonly float _uiFontSize;
        private bool _updatingLanguages;

        public event EventHandler LanguageChanged;

        public PopupForm(float uiFontSize)
        {
            _uiFontSize = Math.Max(9F, Math.Min(16F, uiFontSize <= 0 ? 10.5F : uiFontSize));
            Font = new Font("Microsoft YaHei UI", _uiFontSize);
            BackColor = BackgroundColor;
            ForeColor = TextColor;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = new Size(660, 680);
            Padding = new Padding(18);

            _titleLabel = new Label
            {
                Text = "划词翻译",
                AutoSize = false,
                Font = new Font("Microsoft YaHei UI", _uiFontSize + 1.8F, FontStyle.Bold),
                ForeColor = TextColor,
                BackColor = HeaderColor,
                Location = new Point(24, 18),
                Size = new Size(190, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label fromLabel = new Label
            {
                Text = "从",
                AutoSize = false,
                ForeColor = MutedColor,
                BackColor = HeaderColor,
                Location = new Point(252, 20),
                Size = new Size(24, 24),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _sourceLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(278, 18),
                Size = new Size(108, 26),
                TabStop = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", Math.Max(9F, _uiFontSize - 0.5F))
            };

            Label toLabel = new Label
            {
                Text = "到",
                AutoSize = false,
                ForeColor = MutedColor,
                BackColor = HeaderColor,
                Location = new Point(394, 20),
                Size = new Size(24, 24),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _targetLanguage = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(420, 18),
                Size = new Size(128, 26),
                TabStop = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", Math.Max(9F, _uiFontSize - 0.5F))
            };

            PopulateLanguages();
            _sourceLanguage.SelectedIndexChanged += LanguageCombo_SelectedIndexChanged;
            _targetLanguage.SelectedIndexChanged += LanguageCombo_SelectedIndexChanged;

            Button closeButton = new Button
            {
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", _uiFontSize + 2.2F, FontStyle.Regular),
                ForeColor = Color.FromArgb(118, 128, 142),
                BackColor = HeaderColor,
                Location = new Point(610, 15),
                Size = new Size(32, 32),
                TabStop = false
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            closeButton.Click += (sender, args) => Close();

            _sourceLabel = new Label
            {
                AutoSize = false,
                ForeColor = Color.FromArgb(67, 77, 92),
                BackColor = SurfaceColor,
                Location = new Point(24, 72),
                Size = new Size(612, 56),
                Padding = new Padding(12, 9, 12, 9),
                Text = "选择文本后按快捷键开始"
            };

            _noticeLabel = new Label
            {
                AutoSize = false,
                ForeColor = Color.FromArgb(180, 92, 0),
                BackColor = BackgroundColor,
                Location = new Point(26, 136),
                Size = new Size(608, 20)
            };

            _deepLCard = new ResultCard("DeepL", "高质量", new Point(24, 164), new Size(612, 108), Color.FromArgb(0, 169, 224), _uiFontSize);
            _googleCard = new ResultCard("Google", "可选", new Point(24, 282), new Size(612, 104), Color.FromArgb(66, 133, 244), _uiFontSize);
            _baiduCard = new ResultCard("百度", "API Key", new Point(24, 396), new Size(612, 104), Color.FromArgb(41, 98, 255), _uiFontSize);
            _aiCard = new ResultCard("AI", "DeepSeek", new Point(24, 510), new Size(612, 132), Color.FromArgb(125, 92, 255), _uiFontSize);

            Controls.Add(_titleLabel);
            Controls.Add(fromLabel);
            Controls.Add(_sourceLanguage);
            Controls.Add(toLabel);
            Controls.Add(_targetLanguage);
            Controls.Add(closeButton);
            Controls.Add(_sourceLabel);
            Controls.Add(_noticeLabel);
            Controls.Add(_deepLCard);
            Controls.Add(_googleCard);
            Controls.Add(_baiduCard);
            Controls.Add(_aiCard);

            MakeDraggable(this);
            MakeDraggable(_titleLabel);
            MakeDraggable(fromLabel);
            MakeDraggable(toLabel);
            MakeDraggable(_sourceLabel);
            MakeDraggable(_noticeLabel);
        }

        public string SourceLanguageCode => GetSelectedCode(_sourceLanguage, "auto");
        public string TargetLanguageCode => GetSelectedCode(_targetLanguage, "zh");

        public void SetLanguages(string source, string target)
        {
            _updatingLanguages = true;
            SelectLanguage(_sourceLanguage, string.IsNullOrWhiteSpace(source) ? "auto" : source);
            SelectLanguage(_targetLanguage, string.IsNullOrWhiteSpace(target) ? "zh" : target);
            _updatingLanguages = false;
        }

        public void ShowNearCursor()
        {
            Point cursor = Cursor.Position;
            Screen screen = Screen.FromPoint(cursor);
            int x = Math.Min(cursor.X + 16, screen.WorkingArea.Right - Width - 8);
            int y = Math.Min(cursor.Y + 16, screen.WorkingArea.Bottom - Height - 8);
            x = Math.Max(screen.WorkingArea.Left + 8, x);
            y = Math.Max(screen.WorkingArea.Top + 8, y);
            Location = new Point(x, y);
            Show();
            BringToFront();
            Activate();
        }

        public void SetSourceText(string text, bool truncated)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, bool>(SetSourceText), text, truncated);
                return;
            }

            _sourceLabel.Text = Compact(text, 280);
            _noticeLabel.Text = truncated ? "原文较长，已按设置截断后翻译。" : "";
        }

        public void SetNotice(string notice)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetNotice), notice);
                return;
            }

            _sourceLabel.Text = notice;
            _noticeLabel.Text = "";
            _deepLCard.SetSkipped("未开始");
            _googleCard.SetSkipped("未开始");
            _baiduCard.SetSkipped("未开始");
            _aiCard.SetSkipped("未开始");
        }

        public void SetLoading(string engineName)
        {
            ResultCard card = FindCard(engineName);
            if (card == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetLoading), engineName);
                return;
            }

            card.SetLoading();
        }

        public void ApplyResult(TranslationResult result)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<TranslationResult>(ApplyResult), result);
                return;
            }

            ResultCard card = FindCard(result.EngineName);
            if (card == null)
            {
                return;
            }

            if (result.Success)
            {
                card.SetResult(result.Text, result.Elapsed);
            }
            else
            {
                card.SetError(result.Error);
            }
        }

        public void SetSkipped(string engineName, string reason)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, string>(SetSkipped), engineName, reason);
                return;
            }

            ResultCard card = FindCard(engineName);
            card?.SetSkipped(reason);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var header = new SolidBrush(HeaderColor))
            {
                e.Graphics.FillRectangle(header, 0, 0, Width, 60);
            }

            using (var pen = new Pen(SoftBorderColor))
            {
                e.Graphics.DrawLine(pen, 0, 60, Width, 60);
                e.Graphics.DrawRectangle(pen, _sourceLabel.Left, _sourceLabel.Top, _sourceLabel.Width - 1, _sourceLabel.Height - 1);
            }

            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 16, 16));
        }

        private void MakeDraggable(Control control)
        {
            control.MouseDown += BeginDrag;
        }

        private void BeginDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNclButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
        }

        private ResultCard FindCard(string engineName)
        {
            if (engineName == "DeepL")
            {
                return _deepLCard;
            }

            if (engineName == "Google")
            {
                return _googleCard;
            }

            if (engineName == "百度")
            {
                return _baiduCard;
            }

            if (engineName == "AI")
            {
                return _aiCard;
            }

            return null;
        }

        private void LanguageCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_updatingLanguages)
            {
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void PopulateLanguages()
        {
            LanguageOption[] sourceOptions =
            {
                new LanguageOption("混合/自动", "auto"),
                new LanguageOption("简体中文", "zh"),
                new LanguageOption("英语", "en"),
                new LanguageOption("日语", "ja"),
                new LanguageOption("韩语", "ko"),
                new LanguageOption("法语", "fr"),
                new LanguageOption("德语", "de"),
                new LanguageOption("西班牙语", "es")
            };

            LanguageOption[] targetOptions =
            {
                new LanguageOption("简体中文", "zh"),
                new LanguageOption("英语", "en"),
                new LanguageOption("日语", "ja"),
                new LanguageOption("韩语", "ko"),
                new LanguageOption("法语", "fr"),
                new LanguageOption("德语", "de"),
                new LanguageOption("西班牙语", "es")
            };

            _sourceLanguage.Items.AddRange(sourceOptions);
            _targetLanguage.Items.AddRange(targetOptions);
            _sourceLanguage.SelectedIndex = 0;
            _targetLanguage.SelectedIndex = 0;
        }

        private static string GetSelectedCode(ComboBox comboBox, string fallback)
        {
            var option = comboBox.SelectedItem as LanguageOption;
            return option == null ? fallback : option.Code;
        }

        private static void SelectLanguage(ComboBox comboBox, string code)
        {
            foreach (object item in comboBox.Items)
            {
                var option = item as LanguageOption;
                if (option != null && string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = option;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private class LanguageOption
        {
            public string Label { get; }
            public string Code { get; }

            public LanguageOption(string label, string code)
            {
                Label = label;
                Code = code;
            }

            public override string ToString()
            {
                return Label;
            }
        }

        private static string Compact(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length <= max)
            {
                return text;
            }

            return text.Substring(0, max) + "...";
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private class ResultCard : Panel
        {
            private readonly Label _title;
            private readonly Label _badge;
            private readonly Label _status;
            private readonly RichTextBox _body;
            private readonly Button _copy;
            private readonly Color _accent;
            private readonly Font _cjkFont;
            private readonly Font _latinFont;
            private readonly Font _titleFont;
            private readonly Font _badgeFont;
            private string _currentText = "";

            public ResultCard(string title, string badge, Point location, Size size, Color accent, float uiFontSize)
            {
                Location = location;
                Size = size;
                BackColor = SurfaceColor;
                Padding = new Padding(12);
                _accent = accent;
                _cjkFont = new Font("Microsoft YaHei UI", uiFontSize);
                _latinFont = new Font("Times New Roman", uiFontSize + 0.4F);
                _titleFont = new Font("Microsoft YaHei UI", uiFontSize, FontStyle.Bold);
                _badgeFont = new Font("Microsoft YaHei UI", Math.Max(8F, uiFontSize - 1.5F));
                Font = _cjkFont;

                _title = new Label
                {
                    Text = title,
                    AutoSize = false,
                    Font = _titleFont,
                    ForeColor = TextColor,
                    Location = new Point(14, 10),
                    Size = new Size(92, 22)
                };

                _badge = new Label
                {
                    Text = badge,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = _badgeFont,
                    ForeColor = accent,
                    BackColor = Color.FromArgb(242, 247, 255),
                    Location = new Point(104, 10),
                    Size = new Size(92, 22)
                };

                _status = new Label
                {
                    Text = "等待",
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = _cjkFont,
                    ForeColor = MutedColor,
                    Location = new Point(360, 10),
                    Size = new Size(size.Width - 444, 22)
                };

                _copy = new Button
                {
                    Text = "复制",
                    Enabled = false,
                    FlatStyle = FlatStyle.Flat,
                    Font = _cjkFont,
                    ForeColor = accent,
                    BackColor = SurfaceColor,
                    Location = new Point(size.Width - 74, 7),
                    Size = new Size(56, 28),
                    TabStop = false
                };
                _copy.FlatAppearance.BorderColor = Color.FromArgb(205, 222, 246);
                _copy.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 249, 255);
                _copy.Click += (sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(_currentText))
                    {
                        Clipboard.SetText(_currentText, TextDataFormat.UnicodeText);
                    }
                };

                _body = new RichTextBox
                {
                    BorderStyle = BorderStyle.None,
                    ReadOnly = true,
                    ScrollBars = RichTextBoxScrollBars.Vertical,
                    BackColor = SurfaceColor,
                    ForeColor = TextColor,
                    DetectUrls = false,
                    TabStop = false,
                    Font = _cjkFont,
                    Location = new Point(14, 40),
                    Size = new Size(size.Width - 28, size.Height - 50)
                };

                Controls.Add(_title);
                Controls.Add(_badge);
                Controls.Add(_status);
                Controls.Add(_copy);
                Controls.Add(_body);
            }

            public void SetLoading()
            {
                _currentText = "";
                _status.Text = "翻译中";
                _status.ForeColor = MutedColor;
                SetBodyText("正在请求...", false);
                _copy.Enabled = false;
            }

            public void SetResult(string text, TimeSpan elapsed)
            {
                _currentText = text;
                _status.Text = elapsed.TotalSeconds.ToString("0.0") + "s";
                _status.ForeColor = Color.FromArgb(22, 163, 74);
                SetBodyText(text, true);
                _copy.Enabled = true;
            }

            public void SetError(string error)
            {
                _currentText = "";
                _status.Text = "失败";
                _status.ForeColor = Color.FromArgb(220, 38, 38);
                SetBodyText(error, false);
                _copy.Enabled = false;
            }

            public void SetSkipped(string reason)
            {
                _currentText = "";
                _status.Text = "跳过";
                _status.ForeColor = MutedColor;
                SetBodyText(reason, false);
                _copy.Enabled = false;
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 8, 8));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                using (var accent = new SolidBrush(Color.FromArgb(40, _accent)))
                {
                    e.Graphics.FillRectangle(accent, 0, 0, 4, Height);
                }

                using (var accent = new SolidBrush(_accent))
                {
                    e.Graphics.FillRectangle(accent, 0, 0, 4, 34);
                }

                using (var pen = new Pen(SoftBorderColor))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            private void SetBodyText(string text, bool mixedFonts)
            {
                _body.SuspendLayout();
                _body.Text = text ?? "";
                _body.SelectAll();
                _body.SelectionFont = _cjkFont;
                _body.SelectionColor = TextColor;

                if (mixedFonts && _body.TextLength > 0)
                {
                    ApplyLatinRuns(_body.Text);
                }

                _body.Select(0, 0);
                _body.ResumeLayout();
            }

            private void ApplyLatinRuns(string text)
            {
                int start = -1;
                for (int i = 0; i <= text.Length; i++)
                {
                    bool isLatin = i < text.Length && IsLatinRunChar(text[i]);
                    if (isLatin && start < 0)
                    {
                        start = i;
                    }

                    if ((!isLatin || i == text.Length) && start >= 0)
                    {
                        _body.Select(start, i - start);
                        _body.SelectionFont = _latinFont;
                        start = -1;
                    }
                }
            }

            private static bool IsLatinRunChar(char ch)
            {
                if (ch > 127)
                {
                    return false;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    return true;
                }

                return " .,:;!?()[]{}<>+-=*/_%'\"`~@#$&|\\\r\n".IndexOf(ch) >= 0;
            }
        }
    }
}
