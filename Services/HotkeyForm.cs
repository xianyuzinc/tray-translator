using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrayTranslator.Services
{
    public sealed class HotkeyForm : Form
    {
        private const int WmHotkey = 0x0312;
        private const int HotkeyId = 9017;
        private bool _registered;

        public event EventHandler HotkeyPressed;

        public HotkeyForm()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = System.Drawing.Size.Empty;
            Opacity = 0;
        }

        public bool Register(int modifiers, int key)
        {
            Unregister();
            _registered = RegisterHotKey(Handle, HotkeyId, (uint)modifiers, (uint)key);
            return _registered;
        }

        public void Unregister()
        {
            if (_registered)
            {
                UnregisterHotKey(Handle, HotkeyId);
                _registered = false;
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                return;
            }

            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            Unregister();
            base.Dispose(disposing);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
