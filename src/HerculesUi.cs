using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace HerculesP32
{
    /// <summary>Paleta, textos y dibujos compartidos por la ventana y la bandeja.</summary>
    public static class HerculesUi
    {
        public static readonly Color Background = Color.FromArgb(17, 20, 27);
        public static readonly Color Surface = Color.FromArgb(27, 31, 41);
        public static readonly Color SurfaceHover = Color.FromArgb(35, 40, 53);
        public static readonly Color SurfaceSelected = Color.FromArgb(46, 52, 69);
        public static readonly Color Border = Color.FromArgb(45, 51, 66);
        public static readonly Color TextPrimary = Color.FromArgb(236, 240, 247);
        public static readonly Color TextMuted = Color.FromArgb(152, 162, 182);
        public static readonly Color TextFaint = Color.FromArgb(108, 117, 137);
        public static readonly Color Green = Color.FromArgb(46, 204, 135);
        public static readonly Color Amber = Color.FromArgb(240, 180, 60);
        public static readonly Color OffGray = Color.FromArgb(118, 126, 145);
        public static readonly Color Focus = Color.FromArgb(120, 170, 255);

        public static readonly Color PadPurple = Color.FromArgb(176, 96, 255);
        public static readonly Color PadBlue = Color.FromArgb(56, 140, 255);
        public static readonly Color PadRed = Color.FromArgb(255, 72, 88);

        public static Color PadColor(string color)
        {
            switch (color)
            {
                case "blue": return PadBlue;
                case "red": return PadRed;
                default: return PadPurple;
            }
        }

        public static string ColorName(string color)
        {
            switch (color)
            {
                case "purple": return "Púrpura neón";
                case "blue": return "Azul eléctrico";
                case "red": return "Rojo fuego";
                default: return "DJ multicolor";
            }
        }

        public static Color ModeColor(AppMode mode, string color)
        {
            if (mode == AppMode.Normal) return Green;
            if (mode == AppMode.Off) return OffGray;
            return PadColor(color);
        }

        public static string ModeTitle(AppMode mode)
        {
            if (mode == AppMode.Normal) return "Luces encendidas";
            if (mode == AppMode.Off) return "Luces apagadas";
            return "Modo lámpara";
        }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, Color color, RectangleF r, float radius)
        {
            using (var path = RoundedRect(r, radius))
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }

        public static void DrawRounded(Graphics g, Color color, float width, RectangleF r, float radius)
        {
            using (var path = RoundedRect(r, radius))
            using (var pen = new Pen(color, width))
            {
                g.DrawPath(pen, path);
            }
        }

        /// <summary>Círculo con el color del modo lámpara; "dj" se dibuja con los tres colores.</summary>
        public static void DrawSwatch(Graphics g, RectangleF r, string color)
        {
            if (color == "dj")
            {
                Color[] slices = new Color[] { PadRed, PadBlue, PadPurple };
                for (int i = 0; i < 3; i++)
                {
                    using (var brush = new SolidBrush(slices[i]))
                    {
                        g.FillPie(brush, r.X, r.Y, r.Width, r.Height, -90 + i * 120, 120);
                    }
                }
            }
            else
            {
                using (var brush = new SolidBrush(PadColor(color)))
                {
                    g.FillEllipse(brush, r);
                }
            }
        }

        public static Bitmap CreateDotBitmap(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float inset = size * 0.22f;
                using (var brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, inset, inset, size - 2 * inset, size - 2 * inset);
                }
            }
            return bmp;
        }

        public static Bitmap CreateSwatchBitmap(int size, string color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float inset = size * 0.12f;
                DrawSwatch(g, new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset), color);
            }
            return bmp;
        }
    }
}
