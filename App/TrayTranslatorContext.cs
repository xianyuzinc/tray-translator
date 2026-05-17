using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrayTranslator.Models;
using TrayTranslator.Services;
using TrayTranslator.Translators;
using TrayTranslator.UI;

namespace TrayTranslator.App
{
    public class TrayTranslatorContext : ApplicationContext
    {
        private const int VkLButton = 0x01;

        private readonly SettingsService _settingsService = new SettingsService();
        private readonly SelectionService _selectionService = new SelectionService();
        private readonly TranslationCoordinator _translationCoordinator = new TranslationCoordinator();
        private readonly HotkeyForm _hotkeyForm = new HotkeyForm();
        private readonly NotifyIcon _notifyIcon = new NotifyIcon();
        private readonly ToolStripMenuItem _toggleMenuItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem _statusMenuItem = new ToolStripMenuItem();
        private readonly System.Windows.Forms.Timer _followSelectionTimer = new System.Windows.Forms.Timer { Interval = 160 };
        private readonly object _captureLock = new object();

        private CancellationTokenSource _translationCancellation;
        private PopupForm _popup;
        private AppSettings _settings;
        private string _currentSourceText = "";
        private bool _wasLeftButtonDown;
        private DateTime _lastAutoCaptureUtc = DateTime.MinValue;
        private int _captureGeneration;
        private bool _captureActive;
        private CaptureRequest _pendingCapture;

        public TrayTranslatorContext()
        {
            _settings = _settingsService.Load();

            _hotkeyForm.HotkeyPressed += HotkeyForm_HotkeyPressed;
            _hotkeyForm.CreateControl();
            _followSelectionTimer.Tick += FollowSelectionTimer_Tick;

            BuildTrayIcon();
            ApplyHotkey(showFailure: true);
        }

        private void BuildTrayIcon()
        {
            _statusMenuItem.Enabled = false;

            var settingsItem = new ToolStripMenuItem("设置", null, (sender, args) => ShowSettings());
            _toggleMenuItem.Click += (sender, args) => ToggleEnabled();
            var translateNowItem = new ToolStripMenuItem("立即翻译选中文字", null, (sender, args) => TranslateSelection());
            var exitItem = new ToolStripMenuItem("退出", null, (sender, args) => ExitThread());

            var menu = new ContextMenuStrip();
            menu.Items.Add(_statusMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(translateNowItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(_toggleMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            _notifyIcon.Icon = IconFactory.CreateTrayIcon();
            _notifyIcon.Text = "TrayTranslator";
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (sender, args) => ShowSettings();

            UpdateTrayText();
        }

        private void ToggleEnabled()
        {
            _settings.IsEnabled = !_settings.IsEnabled;
            _settingsService.Save(_settings);
            UpdateTrayText();
        }

        private void ShowSettings()
        {
            using (var form = new SettingsForm(_settingsService.Load()))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _settings = form.Settings;
                    _settingsService.Save(_settings);
                    ApplyHotkey(showFailure: true);
                    UpdateTrayText();
                }
            }
        }

        private void ApplyHotkey(bool showFailure)
        {
            bool ok = _hotkeyForm.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);
            if (!ok && showFailure)
            {
                _notifyIcon.BalloonTipTitle = "TrayTranslator";
                _notifyIcon.BalloonTipText = "快捷键注册失败，可能已被其他程序占用。请在设置中更换快捷键。";
                _notifyIcon.ShowBalloonTip(4000);
            }
        }

        private void UpdateTrayText()
        {
            string hotkey = DescribeHotkey(_settings.HotkeyModifiers, _settings.HotkeyKey);
            _statusMenuItem.Text = (_settings.IsEnabled ? "已启用" : "已暂停") + " · " + hotkey;
            _toggleMenuItem.Text = _settings.IsEnabled ? "暂停" : "启用";
            _notifyIcon.Text = "TrayTranslator · " + hotkey;
        }

        private async void HotkeyForm_HotkeyPressed(object sender, EventArgs e)
        {
            IntPtr sourceWindow = GetForegroundWindow();
            await WaitForHotkeyReleaseAsync();
            TranslateSelection(sourceWindow);
        }

        private void TranslateSelection()
        {
            TranslateSelection(GetForegroundWindow());
        }

        private void TranslateSelection(IntPtr sourceWindow)
        {
            _settings = _settingsService.Load();
            if (!_settings.IsEnabled)
            {
                return;
            }

            EnsurePopup();
            _popup.SetLanguages(_settings.SourceLanguage, _settings.TargetLanguage);
            _popup.SetNotice("正在读取选中文字...");
            _popup.ShowNearCursor();
            _popup.Update();
            StartFollowSelection();

            QueueCapture(sourceWindow, showFailure: true, movePopup: false);
        }

        private void QueueCapture(IntPtr sourceWindow, bool showFailure, bool movePopup)
        {
            var request = new CaptureRequest
            {
                Generation = Interlocked.Increment(ref _captureGeneration),
                SourceWindow = sourceWindow,
                MaxCharacters = _settings.MaxCharacters,
                ShowFailure = showFailure,
                MovePopup = movePopup
            };

            bool shouldStart = false;
            lock (_captureLock)
            {
                if (_captureActive)
                {
                    _pendingCapture = request;
                }
                else
                {
                    _captureActive = true;
                    shouldStart = true;
                }
            }

            if (shouldStart)
            {
                RunCaptureAsync(request);
            }
        }

        private async void RunCaptureAsync(CaptureRequest request)
        {
            SelectionResult selection;
            try
            {
                selection = await CaptureOnStaThreadAsync(request.MaxCharacters, request.SourceWindow);
            }
            catch (Exception ex)
            {
                selection = SelectionResult.Fail("读取选中文字失败：" + ex.Message);
            }

            if (request.Generation == _captureGeneration)
            {
                ApplySelectionResult(request, selection);
            }

            CaptureRequest next = null;
            lock (_captureLock)
            {
                if (_pendingCapture != null)
                {
                    next = _pendingCapture;
                    _pendingCapture = null;
                }
                else
                {
                    _captureActive = false;
                }
            }

            if (next != null)
            {
                RunCaptureAsync(next);
            }
        }

        private Task<SelectionResult> CaptureOnStaThreadAsync(int maxCharacters, IntPtr sourceWindow)
        {
            var completion = new TaskCompletionSource<SelectionResult>();
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(_selectionService.CaptureSelectedText(maxCharacters, sourceWindow));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });

            thread.IsBackground = true;
            thread.Name = "TrayTranslator Selection Capture";
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }

        private void ApplySelectionResult(CaptureRequest request, SelectionResult selection)
        {
            EnsurePopup();

            if (!selection.Success)
            {
                if (request.ShowFailure)
                {
                    _popup.SetNotice(selection.Error);
                    if (!_popup.Visible || request.MovePopup)
                    {
                        _popup.ShowNearCursor();
                    }
                }

                return;
            }

            if (!request.ShowFailure && string.Equals(selection.Text, _currentSourceText, StringComparison.Ordinal))
            {
                return;
            }

            _translationCancellation?.Cancel();
            _translationCancellation = new CancellationTokenSource();

            _currentSourceText = selection.Text;
            _popup.SetSourceText(selection.Text, selection.Truncated);
            if (!_popup.Visible || request.MovePopup)
            {
                _popup.ShowNearCursor();
            }

            RunAllTranslators(selection.Text, _translationCancellation.Token);
            StartFollowSelection();
        }

        private void RunAllTranslators(string text, CancellationToken cancellationToken)
        {
            if (_popup == null || _popup.IsDisposed)
            {
                return;
            }

            foreach (ITranslator translator in _translationCoordinator.CreateTranslators(_settings))
            {
                if (!translator.IsEnabled)
                {
                    _popup.SetSkipped(translator.Name, "已在设置中停用。");
                    continue;
                }

                if (!translator.IsConfigured)
                {
                    _popup.SetSkipped(translator.Name, "未配置 API 密钥。请在托盘菜单中打开设置。");
                    continue;
                }

                _popup.SetLoading(translator.Name);
                RunTranslatorAsync(translator, text, cancellationToken);
            }
        }

        private async void RunTranslatorAsync(ITranslator translator, string text, CancellationToken cancellationToken)
        {
            PopupForm popup = _popup;
            TranslationResult result;
            try
            {
                result = await translator.TranslateAsync(text, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result = new TranslationResult
                {
                    EngineName = translator.Name,
                    Success = false,
                    Text = "",
                    Error = translator.Name + " 错误：" + ex.Message,
                    Elapsed = TimeSpan.Zero
                };
            }

            if (!cancellationToken.IsCancellationRequested && popup != null && !popup.IsDisposed)
            {
                popup.ApplyResult(result);
            }
        }

        private void EnsurePopup()
        {
            if (_popup != null && !_popup.IsDisposed)
            {
                return;
            }

            _popup = new PopupForm(_settings.UiFontSize);
            _popup.SetLanguages(_settings.SourceLanguage, _settings.TargetLanguage);
            _popup.LanguageChanged += Popup_LanguageChanged;
            _popup.FormClosed += Popup_FormClosed;
        }

        private void Popup_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (ReferenceEquals(sender, _popup))
            {
                _followSelectionTimer.Stop();
            }
        }

        private void Popup_LanguageChanged(object sender, EventArgs e)
        {
            if (_popup == null || string.IsNullOrWhiteSpace(_currentSourceText))
            {
                return;
            }

            _translationCancellation?.Cancel();
            _translationCancellation = new CancellationTokenSource();

            _settings.SourceLanguage = _popup.SourceLanguageCode;
            _settings.TargetLanguage = _popup.TargetLanguageCode;
            _settingsService.Save(_settings);

            RunAllTranslators(_currentSourceText, _translationCancellation.Token);
        }

        private async void FollowSelectionTimer_Tick(object sender, EventArgs e)
        {
            if (_popup == null || _popup.IsDisposed || !_popup.Visible)
            {
                return;
            }

            short buttonState = GetAsyncKeyState(VkLButton);
            bool leftButtonDown = (buttonState & 0x8000) != 0;
            bool clickedSinceLastTick = (buttonState & 0x0001) != 0;
            bool released = (_wasLeftButtonDown && !leftButtonDown) || (clickedSinceLastTick && !leftButtonDown);
            _wasLeftButtonDown = leftButtonDown;

            if (!released || DateTime.UtcNow - _lastAutoCaptureUtc < TimeSpan.FromMilliseconds(420))
            {
                return;
            }

            await Task.Delay(70);
            IntPtr sourceWindow = GetForegroundWindow();
            if (sourceWindow == IntPtr.Zero || IsPopupWindow(sourceWindow))
            {
                return;
            }

            _settings = _settingsService.Load();
            if (!_settings.IsEnabled)
            {
                return;
            }

            _lastAutoCaptureUtc = DateTime.UtcNow;
            QueueCapture(sourceWindow, showFailure: false, movePopup: false);
        }

        private void StartFollowSelection()
        {
            _wasLeftButtonDown = IsKeyDown(VkLButton);
            if (!_followSelectionTimer.Enabled)
            {
                _followSelectionTimer.Start();
            }
        }

        private bool IsPopupWindow(IntPtr foreground)
        {
            if (_popup == null || _popup.IsDisposed || foreground == IntPtr.Zero)
            {
                return false;
            }

            return foreground == _popup.Handle || IsChild(_popup.Handle, foreground);
        }

        protected override void ExitThreadCore()
        {
            _translationCancellation?.Cancel();
            _followSelectionTimer.Stop();
            _followSelectionTimer.Dispose();
            _hotkeyForm.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            if (_popup != null && !_popup.IsDisposed)
            {
                _popup.Close();
                _popup.Dispose();
            }

            base.ExitThreadCore();
        }

        private static string DescribeHotkey(int modifiers, int key)
        {
            string text = "";
            if ((modifiers & 0x0002) != 0) text += "Ctrl+";
            if ((modifiers & 0x0004) != 0) text += "Shift+";
            if ((modifiers & 0x0001) != 0) text += "Alt+";
            if ((modifiers & 0x0008) != 0) text += "Win+";
            text += ((Keys)key).ToString();
            return text;
        }

        private async Task WaitForHotkeyReleaseAsync()
        {
            for (int i = 0; i < 18; i++)
            {
                if (!IsModifierDown(_settings.HotkeyModifiers) && !IsKeyDown(_settings.HotkeyKey))
                {
                    break;
                }

                await Task.Delay(25);
            }

            await Task.Delay(35);
        }

        private static bool IsModifierDown(int modifiers)
        {
            if ((modifiers & 0x0002) != 0 && (IsKeyDown((int)Keys.ControlKey) || IsKeyDown((int)Keys.LControlKey) || IsKeyDown((int)Keys.RControlKey))) return true;
            if ((modifiers & 0x0004) != 0 && (IsKeyDown((int)Keys.ShiftKey) || IsKeyDown((int)Keys.LShiftKey) || IsKeyDown((int)Keys.RShiftKey))) return true;
            if ((modifiers & 0x0001) != 0 && (IsKeyDown((int)Keys.Menu) || IsKeyDown((int)Keys.LMenu) || IsKeyDown((int)Keys.RMenu))) return true;
            if ((modifiers & 0x0008) != 0 && (IsKeyDown((int)Keys.LWin) || IsKeyDown((int)Keys.RWin))) return true;
            return false;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private class CaptureRequest
        {
            public int Generation { get; set; }
            public IntPtr SourceWindow { get; set; }
            public int MaxCharacters { get; set; }
            public bool ShowFailure { get; set; }
            public bool MovePopup { get; set; }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);
    }
}
