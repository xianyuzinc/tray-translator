using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace TrayTranslator.Services
{
    public static class IconFactory
    {
        public static Icon CreateTrayIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    graphics.Clear(Color.Transparent);

                    RectangleF body = new RectangleF(3, 3, 26, 26);
                    using (GraphicsPath path = RoundedRect(body, 7))
                    using (var background = new LinearGradientBrush(
                        body,
                        Color.FromArgb(255, 255, 255),
                        Color.FromArgb(225, 242, 255),
                        LinearGradientMode.ForwardDiagonal))
                    {
                        graphics.FillPath(background, path);
                        using (var rim = new Pen(Color.FromArgb(178, 209, 244), 1.2f))
                        {
                            graphics.DrawPath(rim, path);
                        }
                    }

                    using (var glow = new SolidBrush(Color.FromArgb(36, 0, 122, 255)))
                    {
                        graphics.FillEllipse(glow, 8, 7, 16, 16);
                    }

                    using (var font = new Font("Segoe UI Semibold", 15, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.FromArgb(0, 98, 210)))
                    {
                        SizeF size = graphics.MeasureString("A", font);
                        graphics.DrawString("A", font, brush, (32 - size.Width) / 2 - 1.5f, 6.0f);
                    }

                    using (var font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.FromArgb(0, 122, 255)))
                    {
                        graphics.DrawString("译", font, brush, 17.0f, 17.0f);
                    }
                }

                System.IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon icon = Icon.FromHandle(handle))
                    {
                        return (Icon)icon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            float diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(System.IntPtr hIcon);
    }
}
