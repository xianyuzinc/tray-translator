using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
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
        private const int AutoCaptureDragPixels = 8;
        private static readonly TimeSpan AutoCaptureMinHold = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan DefaultAutoCaptureCooldown = TimeSpan.FromMilliseconds(520);
        private static readonly TimeSpan OfficeAutoCaptureCooldown = TimeSpan.FromMilliseconds(1800);

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
        private bool _leftButtonMovedEnough;
        private Point _leftButtonDownPoint = Point.Empty;
        private DateTime _leftButtonDownUtc = DateTime.MinValue;
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
            AppSettings loadedSettings = _settingsService.Load();
            if (_settingsService.LastLoadFailed)
            {
                MessageBox.Show(
                    "设置文件读取失败，为避免覆盖你的 API 配置，本次不会打开设置保存。\n\n" +
                    _settingsService.SettingsPath + "\n\n" +
                    _settingsService.LastLoadError,
                    "TrayTranslator 设置读取失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            using (var form = new SettingsForm(loadedSettings))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _settings = form.Settings;
                    _settingsService.Save(_settings);
                    ApplyHotkey(showFailure: true);
                    UpdateTrayText();
                    ApplyPopupTranslatorVisibility();
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
            ApplyPopupTranslatorVisibility();
            _popup.SetNotice("正在读取选中文字...");
            _popup.ShowNearCursor();
            _popup.Update();
            StartFollowSelection();

            QueueCapture(sourceWindow, showFailure: true, movePopup: false, automatic: false);
        }

        private void QueueCapture(IntPtr sourceWindow, bool showFailure, bool movePopup, bool automatic)
        {
            var request = new CaptureRequest
            {
                Generation = Interlocked.Increment(ref _captureGeneration),
                SourceWindow = sourceWindow,
                MaxCharacters = _settings.MaxCharacters,
                ShowFailure = showFailure,
                MovePopup = movePopup,
                Automatic = automatic
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
                selection = await CaptureOnStaThreadAsync(request.MaxCharacters, request.SourceWindow, request.Automatic);
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

        private Task<SelectionResult> CaptureOnStaThreadAsync(int maxCharacters, IntPtr sourceWindow, bool automatic)
        {
            var completion = new TaskCompletionSource<SelectionResult>();
            var thread = new Thread(() =>
            {
                try
                {
                    completion.SetResult(_selectionService.CaptureSelectedText(maxCharacters, sourceWindow, automatic));
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

            ApplyPopupTranslatorVisibility();
            IReadOnlyList<ITranslator> translators = _translationCoordinator.CreateTranslators(_settings);
            if (translators.Count == 0)
            {
                _popup.SetNoticeLine("未启用任何翻译源。请在托盘菜单的设置中至少启用一个。");
                return;
            }

            _popup.SetNoticeLine("");
            foreach (ITranslator translator in translators)
            {
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
            _popup.SetEnabledTranslators(_settings);
            _popup.LanguageChanged += Popup_LanguageChanged;
            _popup.SourceTranslateRequested += Popup_SourceTranslateRequested;
            _popup.FormClosed += Popup_FormClosed;
        }

        private void ApplyPopupTranslatorVisibility()
        {
            if (_popup == null || _popup.IsDisposed)
            {
                return;
            }

            _popup.SetEnabledTranslators(_settings);
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

            bool truncated;
            string sourceText = NormalizeManualSourceText(_popup.SourceEditorText, out truncated);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                return;
            }

            _translationCancellation?.Cancel();
            _translationCancellation = new CancellationTokenSource();

            _settings.SourceLanguage = _popup.SourceLanguageCode;
            _settings.TargetLanguage = _popup.TargetLanguageCode;
            _settingsService.Save(_settings);

            _currentSourceText = sourceText;
            _popup.SetSourceText(sourceText, truncated);
            RunAllTranslators(sourceText, _translationCancellation.Token);
        }

        private void Popup_SourceTranslateRequested(object sender, EventArgs e)
        {
            if (_popup == null || _popup.IsDisposed)
            {
                return;
            }

            bool truncated;
            string sourceText = NormalizeManualSourceText(_popup.SourceEditorText, out truncated);
            if (string.IsNullOrWhiteSpace(sourceText))
            {
                _popup.SetNotice("请输入要翻译的文本。");
                return;
            }

            _translationCancellation?.Cancel();
            _translationCancellation = new CancellationTokenSource();

            _settings.SourceLanguage = _popup.SourceLanguageCode;
            _settings.TargetLanguage = _popup.TargetLanguageCode;
            _currentSourceText = sourceText;
            _popup.SetSourceText(sourceText, truncated);
            RunAllTranslators(sourceText, _translationCancellation.Token);
            StartFollowSelection();
        }

        private string NormalizeManualSourceText(string text, out bool truncated)
        {
            truncated = false;
            string sourceText = TextPreprocessor.CleanForTranslation(text);
            int maxCharacters = Math.Max(1, _settings.MaxCharacters);
            if (sourceText.Length > maxCharacters)
            {
                sourceText = sourceText.Substring(0, maxCharacters);
                truncated = true;
            }

            return sourceText;
        }

        private async void FollowSelectionTimer_Tick(object sender, EventArgs e)
        {
            if (_popup == null || _popup.IsDisposed || !_popup.Visible)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            bool leftButtonDown = (GetAsyncKeyState(VkLButton) & 0x8000) != 0;

            if (!TryConsumeSelectionDrag(leftButtonDown, now))
            {
                return;
            }

            await Task.Delay(70);
            IntPtr sourceWindow = GetForegroundWindow();
            IntPtr cursorWindow = WindowFromPoint(new NativePoint(Cursor.Position.X, Cursor.Position.Y));
            if (sourceWindow == IntPtr.Zero ||
                IsPopupWindow(sourceWindow) ||
                IsPopupWindow(cursorWindow) ||
                IsSystemShellWindow(sourceWindow) ||
                IsSystemShellWindow(cursorWindow))
            {
                return;
            }

            if (now - _lastAutoCaptureUtc < GetAutoCaptureCooldown(sourceWindow) || IsCaptureActive())
            {
                return;
            }

            _settings = _settingsService.Load();
            if (!_settings.IsEnabled)
            {
                return;
            }

            _lastAutoCaptureUtc = now;
            QueueCapture(sourceWindow, showFailure: false, movePopup: false, automatic: true);
        }

        private void StartFollowSelection()
        {
            _wasLeftButtonDown = IsKeyDown(VkLButton);
            _leftButtonDownPoint = Cursor.Position;
            _leftButtonDownUtc = DateTime.UtcNow;
            _leftButtonMovedEnough = false;
            if (!_followSelectionTimer.Enabled)
            {
                _followSelectionTimer.Start();
            }
        }

        private bool TryConsumeSelectionDrag(bool leftButtonDown, DateTime now)
        {
            Point cursor = Cursor.Position;
            bool pressed = leftButtonDown && !_wasLeftButtonDown;
            bool released = _wasLeftButtonDown && !leftButtonDown;

            if (pressed)
            {
                _leftButtonDownPoint = cursor;
                _leftButtonDownUtc = now;
                _leftButtonMovedEnough = false;
            }

            if (leftButtonDown)
            {
                if (HasMovedEnough(cursor))
                {
                    _leftButtonMovedEnough = true;
                }

                _wasLeftButtonDown = true;
                return false;
            }

            _wasLeftButtonDown = false;
            if (!released)
            {
                return false;
            }

            bool heldLongEnough = now - _leftButtonDownUtc >= AutoCaptureMinHold;
            bool movedEnough = _leftButtonMovedEnough || HasMovedEnough(cursor);
            _leftButtonMovedEnough = false;
            return heldLongEnough && movedEnough;
        }

        private bool HasMovedEnough(Point cursor)
        {
            int dx = cursor.X - _leftButtonDownPoint.X;
            int dy = cursor.Y - _leftButtonDownPoint.Y;
            return dx * dx + dy * dy >= AutoCaptureDragPixels * AutoCaptureDragPixels;
        }

        private bool IsCaptureActive()
        {
            lock (_captureLock)
            {
                return _captureActive;
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

        private static bool IsSystemShellWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return false;
            }

            if (IsSystemShellClass(GetWindowClassName(window)))
            {
                return true;
            }

            IntPtr root = GetAncestor(window, 2);
            if (root != IntPtr.Zero && IsSystemShellClass(GetWindowClassName(root)))
            {
                return true;
            }

            IntPtr parent = GetParent(window);
            for (int i = 0; parent != IntPtr.Zero && i < 8; i++)
            {
                if (IsSystemShellClass(GetWindowClassName(parent)))
                {
                    return true;
                }

                parent = GetParent(parent);
            }

            return false;
        }

        private static bool IsSystemShellClass(string className)
        {
            if (string.IsNullOrEmpty(className))
            {
                return false;
            }

            return className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("NotifyIconOverflowWindow", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TrayNotifyWnd", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetWindowClassName(IntPtr window)
        {
            var builder = new StringBuilder(128);
            return GetClassName(window, builder, builder.Capacity) == 0 ? "" : builder.ToString();
        }

        private static TimeSpan GetAutoCaptureCooldown(IntPtr sourceWindow)
        {
            return IsOfficeProcessName(GetProcessName(sourceWindow))
                ? OfficeAutoCaptureCooldown
                : DefaultAutoCaptureCooldown;
        }

        private static bool IsOfficeProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                return false;
            }

            return processName.Equals("WINWORD", StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("wps", StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("et", StringComparison.OrdinalIgnoreCase) ||
                   processName.Equals("wpp", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetProcessName(IntPtr window)
        {
            try
            {
                if (window == IntPtr.Zero)
                {
                    return "";
                }

                IntPtr processId;
                GetWindowThreadProcessId(window, out processId);
                if (processId == IntPtr.Zero)
                {
                    return "";
                }

                using (Process process = Process.GetProcessById(processId.ToInt32()))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "";
            }
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
            public bool Automatic { get; set; }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativePoint point);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out IntPtr processId);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;

            public NativePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
