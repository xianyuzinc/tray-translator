using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using TrayTranslator.Models;

namespace TrayTranslator.Services
{
    public class SelectionService
    {
        private const int FastWaitAttempts = 14;
        private const int FastWaitDelayMs = 10;
        private const int FallbackWaitAttempts = 8;
        private const int FallbackWaitDelayMs = 24;
        private const int WmCopy = 0x0301;

        public SelectionResult CaptureSelectedText(int maxCharacters)
        {
            return CaptureSelectedText(maxCharacters, IntPtr.Zero);
        }

        public SelectionResult CaptureSelectedText(int maxCharacters, IntPtr sourceWindow)
        {
            IDataObject originalClipboard = null;
            try
            {
                RestoreSourceFocus(sourceWindow);

                string captured = TryGetSelectedTextFromWord(sourceWindow, maxCharacters);
                if (!string.IsNullOrWhiteSpace(captured))
                {
                    return BuildSuccess(captured, maxCharacters);
                }

                originalClipboard = TryGetDataObject();

                captured = TryCaptureByClipboard(sourceWindow, SendCtrlC, FastWaitAttempts, FastWaitDelayMs);
                if (string.IsNullOrWhiteSpace(captured))
                {
                    captured = TryCaptureByClipboard(sourceWindow, SendLegacyCtrlC, FallbackWaitAttempts, FallbackWaitDelayMs);
                }

                if (string.IsNullOrWhiteSpace(captured))
                {
                    captured = TryCaptureByClipboard(sourceWindow, () => TryPostCopy(sourceWindow), FallbackWaitAttempts, FallbackWaitDelayMs);
                }

                if (string.IsNullOrWhiteSpace(captured))
                {
                    captured = TryCaptureByClipboard(sourceWindow, TrySendKeysCopy, FallbackWaitAttempts, FallbackWaitDelayMs);
                }

                if (string.IsNullOrWhiteSpace(captured) && !IsKnownSlowDocumentApp(sourceWindow))
                {
                    captured = TryGetSelectedTextFromAutomation(maxCharacters, sourceWindow);
                }

                if (string.IsNullOrWhiteSpace(captured))
                {
                    return SelectionResult.Fail("未检测到选中文字。请先选中文本，再按快捷键。若目标程序以管理员权限运行，也请用管理员权限启动 TrayTranslator。");
                }

                return BuildSuccess(captured, maxCharacters);
            }
            catch (Exception ex)
            {
                return SelectionResult.Fail("读取选中文字失败：" + ex.Message);
            }
            finally
            {
                RestoreClipboard(originalClipboard);
            }
        }

        private static SelectionResult BuildSuccess(string text, int maxCharacters)
        {
            text = TextPreprocessor.CleanForTranslation(text);
            bool truncated = false;
            if (text.Length > maxCharacters)
            {
                text = text.Substring(0, maxCharacters);
                truncated = true;
            }

            return new SelectionResult
            {
                Success = true,
                Text = text,
                Truncated = truncated,
                Error = ""
            };
        }

        private static string TryCaptureByClipboard(IntPtr sourceWindow, Action copyAction, int waitAttempts, int waitDelayMs)
        {
            RestoreSourceFocus(sourceWindow);
            uint before = GetClipboardSequenceNumber();
            copyAction();

            for (int i = 0; i < waitAttempts; i++)
            {
                Thread.Sleep(waitDelayMs);
                uint current = GetClipboardSequenceNumber();
                if (before == 0 || current == 0 || current != before)
                {
                    string candidate = TryGetText();
                    if (!string.IsNullOrEmpty(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return "";
        }

        private static string TryGetSelectedTextFromWord(IntPtr sourceWindow, int maxCharacters)
        {
            if (!IsProcessName(sourceWindow, "WINWORD"))
            {
                return "";
            }

            object app = null;
            object selection = null;
            try
            {
                app = Marshal.GetActiveObject("Word.Application");
                selection = app.GetType().InvokeMember("Selection", BindingFlags.GetProperty, null, app, null);
                if (selection == null)
                {
                    return "";
                }

                object type = selection.GetType().InvokeMember("Type", BindingFlags.GetProperty, null, selection, null);
                if (Convert.ToInt32(type) == 1)
                {
                    return "";
                }

                object value = selection.GetType().InvokeMember("Text", BindingFlags.GetProperty, null, selection, null);
                string text = Convert.ToString(value);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return "";
                }

                return TextPreprocessor.CleanForTranslation(text.Length > maxCharacters ? text.Substring(0, maxCharacters) : text);
            }
            catch
            {
                return "";
            }
            finally
            {
                ReleaseComObject(selection);
                ReleaseComObject(app);
            }
        }

        private static string TryGetSelectedTextFromAutomation(int maxCharacters, IntPtr sourceWindow)
        {
            try
            {
                AutomationElement element = null;
                if (sourceWindow != IntPtr.Zero && IsWindow(sourceWindow))
                {
                    try
                    {
                        element = AutomationElement.FromHandle(sourceWindow);
                    }
                    catch
                    {
                        element = null;
                    }
                }

                string text = TryReadElementAndChildren(element, maxCharacters);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return TextPreprocessor.CleanForTranslation(text);
                }

                element = AutomationElement.FocusedElement;
                for (int depth = 0; element != null && depth < 5; depth++)
                {
                    text = TryReadTextPatternSelection(element, maxCharacters);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return TextPreprocessor.CleanForTranslation(text);
                    }

                    element = TreeWalker.ControlViewWalker.GetParent(element);
                }
            }
            catch
            {
            }

            return "";
        }

        private static string TryReadElementAndChildren(AutomationElement root, int maxCharacters)
        {
            if (root == null)
            {
                return "";
            }

            string text = TryReadTextPatternSelection(root, maxCharacters);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            try
            {
                Condition condition = new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true);
                AutomationElementCollection descendants = root.FindAll(TreeScope.Descendants, condition);
                int limit = Math.Min(descendants.Count, 16);
                for (int i = 0; i < limit; i++)
                {
                    text = TryReadTextPatternSelection(descendants[i], maxCharacters);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
            catch
            {
            }

            return "";
        }

        private static string TryReadTextPatternSelection(AutomationElement element, int maxCharacters)
        {
            try
            {
                object pattern;
                if (!element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                {
                    return "";
                }

                TextPattern textPattern = pattern as TextPattern;
                if (textPattern == null)
                {
                    return "";
                }

                TextPatternRange[] ranges = textPattern.GetSelection();
                if (ranges == null || ranges.Length == 0)
                {
                    return "";
                }

                var builder = new System.Text.StringBuilder();
                foreach (TextPatternRange range in ranges)
                {
                    string text = range.GetText(maxCharacters);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (builder.Length > 0)
                        {
                            builder.AppendLine();
                        }
                        builder.Append(text.Trim());
                    }
                }

                return builder.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static IDataObject TryGetDataObject()
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    return Clipboard.GetDataObject();
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }

            return null;
        }

        private static string TryGetText()
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText))
                    {
                        return Clipboard.GetText(TextDataFormat.UnicodeText);
                    }

                    if (Clipboard.ContainsText(TextDataFormat.Text))
                    {
                        return Clipboard.GetText(TextDataFormat.Text);
                    }
                }
                catch
                {
                    Thread.Sleep(20);
                }
            }

            return "";
        }

        private static void RestoreClipboard(IDataObject original)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (original == null)
                    {
                        Clipboard.Clear();
                    }
                    else
                    {
                        Clipboard.SetDataObject(original, true);
                    }
                    return;
                }
                catch
                {
                    Thread.Sleep(25);
                }
            }
        }

        private static void SendCtrlC()
        {
            var inputs = new System.Collections.Generic.List<INPUT>();
            AddReleaseKeys(inputs);
            inputs.Add(INPUT.KeyDown(Keys.ControlKey));
            inputs.Add(INPUT.KeyDown(Keys.C));
            inputs.Add(INPUT.KeyUp(Keys.C));
            inputs.Add(INPUT.KeyUp(Keys.ControlKey));

            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        private static void AddReleaseKeys(System.Collections.Generic.List<INPUT> inputs)
        {
            inputs.Add(INPUT.KeyUp(Keys.ShiftKey));
            inputs.Add(INPUT.KeyUp(Keys.LShiftKey));
            inputs.Add(INPUT.KeyUp(Keys.RShiftKey));
            inputs.Add(INPUT.KeyUp(Keys.Menu));
            inputs.Add(INPUT.KeyUp(Keys.LMenu));
            inputs.Add(INPUT.KeyUp(Keys.RMenu));
            inputs.Add(INPUT.KeyUp(Keys.LWin));
            inputs.Add(INPUT.KeyUp(Keys.RWin));
            inputs.Add(INPUT.KeyUp(Keys.ControlKey));
            inputs.Add(INPUT.KeyUp(Keys.LControlKey));
            inputs.Add(INPUT.KeyUp(Keys.RControlKey));

            for (int key = (int)Keys.A; key <= (int)Keys.Z; key++)
            {
                inputs.Add(INPUT.KeyUp((Keys)key));
            }
        }

        private static void SendLegacyCtrlC()
        {
            keybd_event((byte)Keys.ControlKey, 0, 0, UIntPtr.Zero);
            keybd_event((byte)Keys.C, 0, 0, UIntPtr.Zero);
            Thread.Sleep(20);
            keybd_event((byte)Keys.C, 0, 0x0002, UIntPtr.Zero);
            keybd_event((byte)Keys.ControlKey, 0, 0x0002, UIntPtr.Zero);
        }

        private static void TryPostCopy(IntPtr sourceWindow)
        {
            try
            {
                IntPtr target = GetFocusedHandle(sourceWindow);
                if (target == IntPtr.Zero)
                {
                    target = sourceWindow;
                }

                if (target != IntPtr.Zero)
                {
                    PostMessage(target, WmCopy, IntPtr.Zero, IntPtr.Zero);
                }

                Thread.Sleep(18);
                SendCtrlC();
            }
            catch
            {
                SendCtrlC();
            }
        }

        private static void TrySendKeysCopy()
        {
            try
            {
                SendKeys.SendWait("^c");
            }
            catch
            {
                SendCtrlC();
            }
        }

        private static void RestoreSourceFocus(IntPtr sourceWindow)
        {
            if (sourceWindow == IntPtr.Zero || !IsWindow(sourceWindow))
            {
                return;
            }

            try
            {
                IntPtr foreground = GetForegroundWindow();
                uint currentThread = GetCurrentThreadId();
                uint targetThread = GetWindowThreadProcessId(sourceWindow, IntPtr.Zero);
                uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, IntPtr.Zero);

                if (targetThread != 0)
                {
                    AttachThreadInput(currentThread, targetThread, true);
                }

                if (foregroundThread != 0 && foregroundThread != targetThread)
                {
                    AttachThreadInput(currentThread, foregroundThread, true);
                }

                SetForegroundWindow(sourceWindow);
                IntPtr focus = GetFocusedHandle(sourceWindow);
                if (focus != IntPtr.Zero)
                {
                    SetFocus(focus);
                }

                Thread.Sleep(35);

                if (foregroundThread != 0 && foregroundThread != targetThread)
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }

                if (targetThread != 0)
                {
                    AttachThreadInput(currentThread, targetThread, false);
                }
            }
            catch
            {
            }
        }

        private static IntPtr GetFocusedHandle(IntPtr window)
        {
            try
            {
                uint thread = GetWindowThreadProcessId(window, IntPtr.Zero);
                GUITHREADINFO info = new GUITHREADINFO();
                info.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
                if (thread != 0 && GetGUIThreadInfo(thread, ref info))
                {
                    if (info.hwndFocus != IntPtr.Zero)
                    {
                        return info.hwndFocus;
                    }

                    if (info.hwndCaret != IntPtr.Zero)
                    {
                        return info.hwndCaret;
                    }
                }
            }
            catch
            {
            }

            return IntPtr.Zero;
        }

        private static bool IsKnownSlowDocumentApp(IntPtr sourceWindow)
        {
            string name = GetProcessName(sourceWindow);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return name.Equals("AcroRd32", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Acrobat", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("SumatraPDF", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("FoxitPDFReader", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("FoxitReader", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("WINWORD", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("wps", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("et", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("wpp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProcessName(IntPtr sourceWindow, string processName)
        {
            return string.Equals(GetProcessName(sourceWindow), processName, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetProcessName(IntPtr sourceWindow)
        {
            try
            {
                if (sourceWindow == IntPtr.Zero)
                {
                    return "";
                }

                IntPtr processId;
                GetWindowThreadProcessId(sourceWindow, out processId);
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

        private static void ReleaseComObject(object value)
        {
            try
            {
                if (value != null && Marshal.IsComObject(value))
                {
                    Marshal.ReleaseComObject(value);
                }
            }
            catch
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;

            public static INPUT KeyDown(Keys key)
            {
                return new INPUT
                {
                    type = 1,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (ushort)key,
                            dwFlags = 0
                        }
                    }
                };
            }

            public static INPUT KeyUp(Keys key)
            {
                return new INPUT
                {
                    type = 1,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (ushort)key,
                            dwFlags = 0x0002
                        }
                    }
                };
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte virtualKey, byte scanCode, int flags, UIntPtr extraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out IntPtr processId);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }
}
