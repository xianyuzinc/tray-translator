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
        private const int WmNcHitTest = 0x0084;
        private const int HtCaption = 0x0002;
        private const int HtRight = 0x000B;
        private const int HtBottom = 0x000F;
        private const int HtBottomRight = 0x0011;
        private const int ResizeBorder = 8;

        private readonly Label _titleLabel;
        private readonly Label _fromLabel;
        private readonly Label _toLabel;
        private readonly ComboBox _sourceLanguage;
        private readonly ComboBox _targetLanguage;
        private readonly Button _closeButton;
        private readonly TextBox _sourceTextBox;
        private readonly Button _translateButton;
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
        public event EventHandler SourceTranslateRequested;

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
            MinimumSize = new Size(620, 520);
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

            _fromLabel = new Label
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

            _toLabel = new Label
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

            _closeButton = new Button
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
            _closeButton.FlatAppearance.BorderSize = 0;
            _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 245, 249);
            _closeButton.Click += (sender, args) => Close();

            _sourceTextBox = new TextBox
            {
                ForeColor = Color.FromArgb(67, 77, 92),
                BackColor = SurfaceColor,
                Location = new Point(24, 72),
                Size = new Size(532, 56),
                Multiline = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true,
                AcceptsTab = false,
                Font = new Font("Microsoft YaHei UI", _uiFontSize + 0.2F),
                Text = "选择文本后按快捷键开始"
            };
            _sourceTextBox.KeyDown += SourceTextBox_KeyDown;

            _translateButton = new Button
            {
                Text = "重译",
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", Math.Max(9F, _uiFontSize - 0.2F)),
                ForeColor = AccentColor,
                BackColor = SurfaceColor,
                Location = new Point(566, 72),
                Size = new Size(70, 56),
                TabStop = false
            };
            _translateButton.FlatAppearance.BorderColor = Color.FromArgb(205, 222, 246);
            _translateButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 249, 255);
            _translateButton.Click += (sender, args) => RequestSourceTranslate();

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
            _deepLCard.ExpandRequested += ResultCard_ExpandRequested;
            _googleCard.ExpandRequested += ResultCard_ExpandRequested;
            _baiduCard.ExpandRequested += ResultCard_ExpandRequested;
            _aiCard.ExpandRequested += ResultCard_ExpandRequested;

            Controls.Add(_titleLabel);
            Controls.Add(_fromLabel);
            Controls.Add(_sourceLanguage);
            Controls.Add(_toLabel);
            Controls.Add(_targetLanguage);
            Controls.Add(_closeButton);
            Controls.Add(_sourceTextBox);
            Controls.Add(_translateButton);
            Controls.Add(_noticeLabel);
            Controls.Add(_deepLCard);
            Controls.Add(_googleCard);
            Controls.Add(_baiduCard);
            Controls.Add(_aiCard);

            MakeDraggable(this);
            MakeDraggable(_titleLabel);
            MakeDraggable(_fromLabel);
            MakeDraggable(_toLabel);
            MakeDraggable(_noticeLabel);
            LayoutControls();
        }

        public string SourceLanguageCode => GetSelectedCode(_sourceLanguage, "auto");
        public string TargetLanguageCode => GetSelectedCode(_targetLanguage, "zh");
        public string SourceEditorText => (_sourceTextBox.Text ?? "").Trim();

        public void SetEnabledTranslators(AppSettings settings)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<AppSettings>(SetEnabledTranslators), settings);
                return;
            }

            SetCardVisible(_deepLCard, settings.DeepLEnabled);
            SetCardVisible(_googleCard, settings.GoogleEnabled);
            SetCardVisible(_baiduCard, settings.BaiduEnabled);
            SetCardVisible(_aiCard, settings.DeepSeekEnabled);
            LayoutControls();
            Invalidate();
        }

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

            _sourceTextBox.Text = text ?? "";
            _noticeLabel.Text = truncated ? "原文较长，已按设置截断后翻译。" : "";
        }

        public void SetNotice(string notice)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetNotice), notice);
                return;
            }

            _sourceTextBox.Text = notice;
            _noticeLabel.Text = "";
            _deepLCard.SetSkipped("未开始");
            _googleCard.SetSkipped("未开始");
            _baiduCard.SetSkipped("未开始");
            _aiCard.SetSkipped("未开始");
        }

        public void SetNoticeLine(string notice)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetNoticeLine), notice);
                return;
            }

            _noticeLabel.Text = notice ?? "";
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

            Rectangle sourceSurface = new Rectangle(
                _sourceTextBox.Left - 12,
                _sourceTextBox.Top - 10,
                _sourceTextBox.Width + 13,
                _sourceTextBox.Height + 19);
            using (var surface = new SolidBrush(SurfaceColor))
            {
                e.Graphics.FillRectangle(surface, sourceSurface);
            }

            using (var pen = new Pen(SoftBorderColor))
            {
                e.Graphics.DrawLine(pen, 0, 60, Width, 60);
                e.Graphics.DrawRectangle(pen, sourceSurface);
            }

            using (var pen = new Pen(BorderColor))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }

            ControlPaint.DrawSizeGrip(
                e.Graphics,
                Color.FromArgb(180, 190, 204),
                new Rectangle(Width - 19, Height - 19, 16, 16));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutControls();
            UpdateRoundedRegion();
            Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest)
            {
                base.WndProc(ref m);
                Point cursor = PointToClient(Cursor.Position);
                bool right = cursor.X >= Width - ResizeBorder;
                bool bottom = cursor.Y >= Height - ResizeBorder;

                if (right && bottom)
                {
                    m.Result = (IntPtr)HtBottomRight;
                    return;
                }

                if (right)
                {
                    m.Result = (IntPtr)HtRight;
                    return;
                }

                if (bottom)
                {
                    m.Result = (IntPtr)HtBottom;
                    return;
                }

                return;
            }

            base.WndProc(ref m);
        }

        private void UpdateRoundedRegion()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 16, 16));
        }

        private void LayoutControls()
        {
            if (_titleLabel == null || _sourceTextBox == null || _deepLCard == null || _aiCard == null)
            {
                return;
            }

            int margin = 24;
            int width = Math.Max(200, ClientSize.Width - margin * 2);

            _closeButton.SetBounds(ClientSize.Width - 50, 15, 32, 32);
            int languageRight = Math.Min(ClientSize.Width - 112, 548);
            _targetLanguage.SetBounds(languageRight - 128, 18, 128, 26);
            _toLabel.SetBounds(_targetLanguage.Left - 26, 20, 24, 24);
            _sourceLanguage.SetBounds(_toLabel.Left - 116, 18, 108, 26);
            _fromLabel.SetBounds(_sourceLanguage.Left - 26, 20, 24, 24);
            _titleLabel.SetBounds(margin, 18, Math.Max(140, _fromLabel.Left - margin - 18), 28);

            int translateButtonWidth = 70;
            _translateButton.SetBounds(margin + width - translateButtonWidth, 72, translateButtonWidth, 56);
            _sourceTextBox.SetBounds(margin + 12, 82, Math.Max(120, width - translateButtonWidth - 28), 38);
            _noticeLabel.SetBounds(margin + 2, 136, Math.Max(120, width - 4), 20);

            int top = 164;
            int gap = 10;
            int bottomMargin = 28;
            ResultCard[] cards = { _deepLCard, _googleCard, _baiduCard, _aiCard };
            int visibleCount = CountVisibleCards(cards);
            if (visibleCount == 0)
            {
                return;
            }

            int totalGap = gap * Math.Max(0, visibleCount - 1);
            int available = Math.Max(visibleCount * 96, ClientSize.Height - top - bottomMargin - totalGap);
            int baseCardHeight = Math.Max(96, available / visibleCount);
            int y = top;
            int remaining = available;
            int index = 0;

            foreach (ResultCard card in cards)
            {
                if (!card.Visible)
                {
                    continue;
                }

                int height = index == visibleCount - 1 ? Math.Max(96, remaining) : baseCardHeight;
                card.SetBounds(margin, y, width, height);
                y = card.Bottom + gap;
                remaining -= height;
                index++;
            }
        }

        private void ResultCard_ExpandRequested(object sender, ResultExpandEventArgs e)
        {
            var reader = new ReaderForm(e.EngineName, e.Text, _uiFontSize);
            reader.Show(this);
        }

        private void SourceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                RequestSourceTranslate();
            }
        }

        private void RequestSourceTranslate()
        {
            SourceTranslateRequested?.Invoke(this, EventArgs.Empty);
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

        private static void SetCardVisible(ResultCard card, bool visible)
        {
            if (!visible)
            {
                card.SetSkipped("未开始");
            }

            card.Visible = visible;
        }

        private static int CountVisibleCards(ResultCard[] cards)
        {
            int count = 0;
            foreach (ResultCard card in cards)
            {
                if (card.Visible)
                {
                    count++;
                }
            }

            return count;
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

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private class ResultExpandEventArgs : EventArgs
        {
            public string EngineName { get; private set; }
            public string Text { get; private set; }

            public ResultExpandEventArgs(string engineName, string text)
            {
                EngineName = engineName;
                Text = text;
            }
        }

        private class ResultCard : Panel
        {
            private readonly Label _title;
            private readonly Label _badge;
            private readonly Label _status;
            private readonly RichTextBox _body;
            private readonly Button _expand;
            private readonly Button _copy;
            private readonly Color _accent;
            private readonly Font _cjkFont;
            private readonly Font _latinFont;
            private readonly Font _titleFont;
            private readonly Font _badgeFont;
            private string _currentText = "";
            public event EventHandler<ResultExpandEventArgs> ExpandRequested;

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

                _expand = new Button
                {
                    Text = "展开",
                    Enabled = false,
                    FlatStyle = FlatStyle.Flat,
                    Font = _cjkFont,
                    ForeColor = accent,
                    BackColor = SurfaceColor,
                    Location = new Point(size.Width - 142, 7),
                    Size = new Size(62, 28),
                    TabStop = false
                };
                _expand.FlatAppearance.BorderColor = Color.FromArgb(205, 222, 246);
                _expand.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 249, 255);
                _expand.Click += (sender, args) =>
                {
                    if (!string.IsNullOrWhiteSpace(_currentText))
                    {
                        ExpandRequested?.Invoke(this, new ResultExpandEventArgs(_title.Text, _currentText));
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
                Controls.Add(_expand);
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
                _expand.Enabled = false;
            }

            public void SetResult(string text, TimeSpan elapsed)
            {
                _currentText = text;
                _status.Text = elapsed.TotalSeconds.ToString("0.0") + "s";
                _status.ForeColor = Color.FromArgb(22, 163, 74);
                SetBodyText(text, true);
                _copy.Enabled = true;
                _expand.Enabled = true;
            }

            public void SetError(string error)
            {
                _currentText = "";
                _status.Text = "失败";
                _status.ForeColor = Color.FromArgb(220, 38, 38);
                SetBodyText(error, false);
                _copy.Enabled = false;
                _expand.Enabled = false;
            }

            public void SetSkipped(string reason)
            {
                _currentText = "";
                _status.Text = "跳过";
                _status.ForeColor = MutedColor;
                SetBodyText(reason, false);
                _copy.Enabled = false;
                _expand.Enabled = false;
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (_copy == null || _expand == null || _status == null || _body == null)
                {
                    return;
                }

                int buttonTop = 7;
                UpdateRoundedRegion();
                _copy.SetBounds(Width - 74, buttonTop, 56, 28);
                _expand.SetBounds(Width - 142, buttonTop, 62, 28);
                _title.SetBounds(14, 10, 92, 22);
                _badge.SetBounds(104, 10, 92, 22);
                _status.SetBounds(205, 10, Math.Max(40, Width - 360), 22);
                _body.SetBounds(14, 40, Math.Max(100, Width - 28), Math.Max(40, Height - 50));
                Invalidate();
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                UpdateRoundedRegion();
            }

            private void UpdateRoundedRegion()
            {
                if (!IsHandleCreated)
                {
                    return;
                }

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
