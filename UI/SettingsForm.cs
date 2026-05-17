using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TrayTranslator.Models;
using TrayTranslator.Services;

namespace TrayTranslator.UI
{
    public class SettingsForm : Form
    {
        private const int ModAlt = 0x0001;
        private const int ModControl = 0x0002;
        private const int ModShift = 0x0004;
        private const int ModWin = 0x0008;

        private readonly TextBox _sourceLanguage = new TextBox();
        private readonly TextBox _targetLanguage = new TextBox();
        private readonly NumericUpDown _maxCharacters = new NumericUpDown();
        private readonly NumericUpDown _uiFontSize = new NumericUpDown();
        private readonly CheckBox _modCtrl = new CheckBox();
        private readonly CheckBox _modShift = new CheckBox();
        private readonly CheckBox _modAlt = new CheckBox();
        private readonly CheckBox _modWin = new CheckBox();
        private readonly ComboBox _hotkeyKey = new ComboBox();

        private readonly CheckBox _googleEnabled = new CheckBox();
        private readonly TextBox _googleKey = new TextBox();
        private readonly CheckBox _deepLEnabled = new CheckBox();
        private readonly TextBox _deepLKey = new TextBox();
        private readonly CheckBox _baiduEnabled = new CheckBox();
        private readonly TextBox _baiduApiKey = new TextBox();
        private readonly TextBox _baiduAppId = new TextBox();
        private readonly CheckBox _deepSeekEnabled = new CheckBox();
        private readonly TextBox _deepSeekKey = new TextBox();
        private readonly TextBox _deepSeekBaseUrl = new TextBox();
        private readonly TextBox _deepSeekModel = new TextBox();

        public AppSettings Settings { get; private set; }

        public SettingsForm(AppSettings settings)
        {
            Settings = settings;
            Text = "TrayTranslator 设置";
            Font = new Font("Microsoft YaHei UI", 9.5F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 820);
            AutoScroll = true;
            BackColor = Color.FromArgb(248, 250, 252);

            BuildUi();
            LoadFromSettings(settings);
        }

        private void BuildUi()
        {
            var title = new Label
            {
                Text = "TrayTranslator 设置",
                Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold),
                Location = new Point(24, 18),
                Size = new Size(300, 34)
            };
            Controls.Add(title);

            int y = 66;
            AddSection("基础", ref y);
            AddTextRow("源语言", _sourceLanguage, "auto", ref y);
            AddTextRow("目标语言", _targetLanguage, "zh", ref y);

            AddLabel("最大字符", 34, y + 3);
            _maxCharacters.Minimum = 100;
            _maxCharacters.Maximum = 6000;
            _maxCharacters.Increment = 100;
            _maxCharacters.Location = new Point(126, y);
            _maxCharacters.Size = new Size(120, 24);
            Controls.Add(_maxCharacters);
            y += 34;

            AddLabel("界面字号", 34, y + 3);
            _uiFontSize.Minimum = 9;
            _uiFontSize.Maximum = 16;
            _uiFontSize.DecimalPlaces = 1;
            _uiFontSize.Increment = 0.5M;
            _uiFontSize.Location = new Point(126, y);
            _uiFontSize.Size = new Size(120, 24);
            Controls.Add(_uiFontSize);
            y += 34;

            AddLabel("快捷键", 34, y + 4);
            ConfigureModifier(_modCtrl, "Ctrl", new Point(126, y));
            ConfigureModifier(_modShift, "Shift", new Point(188, y));
            ConfigureModifier(_modAlt, "Alt", new Point(258, y));
            ConfigureModifier(_modWin, "Win", new Point(316, y));
            Controls.Add(_modCtrl);
            Controls.Add(_modShift);
            Controls.Add(_modAlt);
            Controls.Add(_modWin);
            _hotkeyKey.DropDownStyle = ComboBoxStyle.DropDownList;
            _hotkeyKey.Location = new Point(382, y);
            _hotkeyKey.Size = new Size(100, 24);
            PopulateKeys();
            Controls.Add(_hotkeyKey);
            y += 44;

            AddSection("Google Cloud Translation", ref y);
            ConfigureEngineCheck(_googleEnabled, "启用 Google", y);
            y += 28;
            AddPasswordRow("API Key", _googleKey, ref y);

            AddSection("DeepL", ref y);
            ConfigureEngineCheck(_deepLEnabled, "启用 DeepL", y);
            y += 28;
            AddPasswordRow("API Key", _deepLKey, ref y);

            AddSection("百度翻译", ref y);
            ConfigureEngineCheck(_baiduEnabled, "启用百度", y);
            y += 28;
            AddPasswordRow("API Key", _baiduApiKey, ref y);
            AddTextRow("AppID", _baiduAppId, "", ref y);

            AddSection("DeepSeek AI 翻译", ref y);
            ConfigureEngineCheck(_deepSeekEnabled, "启用 AI", y);
            y += 28;
            AddPasswordRow("API Key", _deepSeekKey, ref y);
            AddTextRow("Base URL", _deepSeekBaseUrl, "https://api.deepseek.com", ref y);
            AddTextRow("模型", _deepSeekModel, "deepseek-v4-flash", ref y);

            var save = new Button
            {
                Text = "保存",
                DialogResult = DialogResult.None,
                Location = new Point(458, 770),
                Size = new Size(82, 30)
            };
            save.Click += Save_Click;

            var cancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(552, 770),
                Size = new Size(82, 30)
            };

            Controls.Add(save);
            Controls.Add(cancel);
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void LoadFromSettings(AppSettings settings)
        {
            _sourceLanguage.Text = settings.SourceLanguage;
            _targetLanguage.Text = settings.TargetLanguage;
            _maxCharacters.Value = Math.Max(_maxCharacters.Minimum, Math.Min(_maxCharacters.Maximum, settings.MaxCharacters));
            _uiFontSize.Value = Math.Max(_uiFontSize.Minimum, Math.Min(_uiFontSize.Maximum, (decimal)settings.UiFontSize));

            _modCtrl.Checked = (settings.HotkeyModifiers & ModControl) != 0;
            _modShift.Checked = (settings.HotkeyModifiers & ModShift) != 0;
            _modAlt.Checked = (settings.HotkeyModifiers & ModAlt) != 0;
            _modWin.Checked = (settings.HotkeyModifiers & ModWin) != 0;
            SelectKey(settings.HotkeyKey);

            _googleEnabled.Checked = settings.GoogleEnabled;
            _googleKey.Text = SecretProtector.Unprotect(settings.GoogleApiKeyProtected);

            _deepLEnabled.Checked = settings.DeepLEnabled;
            _deepLKey.Text = SecretProtector.Unprotect(settings.DeepLApiKeyProtected);

            _baiduEnabled.Checked = settings.BaiduEnabled;
            _baiduApiKey.Text = SecretProtector.Unprotect(settings.BaiduApiKeyProtected);
            _baiduAppId.Text = settings.BaiduAppId;

            _deepSeekEnabled.Checked = settings.DeepSeekEnabled;
            _deepSeekKey.Text = SecretProtector.Unprotect(settings.DeepSeekApiKeyProtected);
            _deepSeekBaseUrl.Text = settings.DeepSeekBaseUrl;
            _deepSeekModel.Text = settings.DeepSeekModel;
        }

        private void Save_Click(object sender, EventArgs e)
        {
            int modifiers = 0;
            if (_modCtrl.Checked) modifiers |= ModControl;
            if (_modShift.Checked) modifiers |= ModShift;
            if (_modAlt.Checked) modifiers |= ModAlt;
            if (_modWin.Checked) modifiers |= ModWin;

            if (modifiers == 0 || _hotkeyKey.SelectedItem == null)
            {
                MessageBox.Show(this, "请至少选择一个修饰键和一个主键。", "快捷键无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string target = _targetLanguage.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show(this, "目标语言不能为空。", "设置无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Settings = new AppSettings
            {
                IsEnabled = Settings.IsEnabled,
                SourceLanguage = string.IsNullOrWhiteSpace(_sourceLanguage.Text) ? "auto" : _sourceLanguage.Text.Trim(),
                TargetLanguage = target,
                MaxCharacters = (int)_maxCharacters.Value,
                UiFontSize = (float)_uiFontSize.Value,
                HotkeyModifiers = modifiers,
                HotkeyKey = ((KeyOption)_hotkeyKey.SelectedItem).KeyCode,

                GoogleEnabled = _googleEnabled.Checked,
                GoogleApiKeyProtected = SecretProtector.Protect(_googleKey.Text.Trim()),

                DeepLEnabled = _deepLEnabled.Checked,
                DeepLApiKeyProtected = SecretProtector.Protect(_deepLKey.Text.Trim()),

                BaiduEnabled = _baiduEnabled.Checked,
                BaiduApiKeyProtected = SecretProtector.Protect(_baiduApiKey.Text.Trim()),
                BaiduAppId = _baiduAppId.Text.Trim(),
                BaiduSecretKeyProtected = Settings.BaiduSecretKeyProtected,

                DeepSeekEnabled = _deepSeekEnabled.Checked,
                DeepSeekApiKeyProtected = SecretProtector.Protect(_deepSeekKey.Text.Trim()),
                DeepSeekBaseUrl = string.IsNullOrWhiteSpace(_deepSeekBaseUrl.Text) ? "https://api.deepseek.com" : _deepSeekBaseUrl.Text.Trim().TrimEnd('/'),
                DeepSeekModel = string.IsNullOrWhiteSpace(_deepSeekModel.Text) ? "deepseek-v4-flash" : _deepSeekModel.Text.Trim()
            };

            DialogResult = DialogResult.OK;
            Close();
        }

        private void AddSection(string text, ref int y)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 64, 175),
                Location = new Point(24, y),
                Size = new Size(580, 24)
            };
            Controls.Add(label);
            y += 30;
        }

        private void AddTextRow(string label, TextBox box, string placeholder, ref int y)
        {
            AddLabel(label, 34, y + 3);
            ConfigureTextBox(box, placeholder, y);
            Controls.Add(box);
            y += 34;
        }

        private void AddPasswordRow(string label, TextBox box, ref int y)
        {
            AddLabel(label, 34, y + 3);
            ConfigureTextBox(box, "", y);
            box.UseSystemPasswordChar = true;
            Controls.Add(box);
            y += 34;
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(x, y),
                Size = new Size(86, 22)
            });
        }

        private static void ConfigureTextBox(TextBox box, string placeholder, int y)
        {
            box.Location = new Point(126, y);
            box.Size = new Size(488, 24);
            box.Text = placeholder;
        }

        private static void ConfigureEngineCheck(CheckBox checkBox, string text, int y)
        {
            checkBox.Text = text;
            checkBox.Location = new Point(126, y);
            checkBox.Size = new Size(180, 22);
        }

        private static void ConfigureModifier(CheckBox checkBox, string text, Point location)
        {
            checkBox.Text = text;
            checkBox.Location = location;
            checkBox.Size = new Size(64, 22);
        }

        private void PopulateKeys()
        {
            var options = new List<KeyOption>();
            for (int key = (int)Keys.A; key <= (int)Keys.Z; key++)
            {
                options.Add(new KeyOption((Keys)key));
            }

            for (int key = (int)Keys.F1; key <= (int)Keys.F12; key++)
            {
                options.Add(new KeyOption((Keys)key));
            }

            _hotkeyKey.Items.AddRange(options.ToArray());
        }

        private void SelectKey(int keyCode)
        {
            foreach (object item in _hotkeyKey.Items)
            {
                var option = item as KeyOption;
                if (option != null && option.KeyCode == keyCode)
                {
                    _hotkeyKey.SelectedItem = option;
                    return;
                }
            }

            _hotkeyKey.SelectedIndex = 19;
        }

        private class KeyOption
        {
            public int KeyCode { get; }
            private readonly string _name;

            public KeyOption(Keys key)
            {
                KeyCode = (int)key;
                _name = key.ToString();
            }

            public override string ToString()
            {
                return _name;
            }
        }
    }
}
