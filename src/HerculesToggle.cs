using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace HerculesP32
{
    /// <summary>Control segmentado Programa / Apagado / Lámpara.</summary>
    public class ModeSelector : Control
    {
        private static readonly AppMode[] Modes = new AppMode[] { AppMode.Normal, AppMode.Off, AppMode.Demo };
        private static readonly string[] Labels = new string[] { "Programa", "Apagado", "Lámpara" };

        public event Action<AppMode> ModeChosen;

        private AppMode selected = AppMode.Normal;
        private string lampColor = "dj";
        private int hover = -1;
        private int focusIndex;
        private float indicator;            // posición animada del resaltado, en segmentos
        private readonly System.Windows.Forms.Timer anim;
        private readonly float scale;

        public ModeSelector(float scale)
        {
            this.scale = scale;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 10f);
            AccessibleName = "Modo de luces";
            AccessibleRole = AccessibleRole.PageTabList;

            anim = new System.Windows.Forms.Timer();
            anim.Interval = 15;
            anim.Tick += (s, e) =>
            {
                float target = IndexOf(selected);
                indicator += (target - indicator) * 0.35f;
                if (Math.Abs(target - indicator) < 0.01f)
                {
                    indicator = target;
                    anim.Stop();
                }
                Invalidate();
            };
        }

        private static int IndexOf(AppMode mode)
        {
            return Array.IndexOf(Modes, mode);
        }

        public void SetState(AppMode mode, string color, bool animate)
        {
            bool changed = mode != selected;
            selected = mode;
            lampColor = color;
            focusIndex = IndexOf(mode);
            AccessibleDescription = Labels[focusIndex];
            if (changed && animate) anim.Start();
            else if (!anim.Enabled) indicator = IndexOf(mode);
            Invalidate();
        }

        private RectangleF SegmentRect(float index)
        {
            float pad = 4 * scale;
            float w = (Width - 2 * pad) / Modes.Length;
            return new RectangleF(pad + index * w, pad, w, Height - 2 * pad);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < Modes.Length; i++)
            {
                if (SegmentRect(i).Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : HerculesUi.Background);

            var bounds = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            HerculesUi.FillRounded(g, HerculesUi.Surface, bounds, 12 * scale);
            HerculesUi.DrawRounded(g, HerculesUi.Border, 1, bounds, 12 * scale);

            if (hover >= 0 && hover != IndexOf(selected))
            {
                HerculesUi.FillRounded(g, HerculesUi.SurfaceHover, SegmentRect(hover), 9 * scale);
            }

            RectangleF ind = SegmentRect(indicator);
            HerculesUi.FillRounded(g, HerculesUi.SurfaceSelected, ind, 9 * scale);

            for (int i = 0; i < Modes.Length; i++)
            {
                RectangleF r = SegmentRect(i);
                bool isSelected = Modes[i] == selected;
                Color dotColor = HerculesUi.ModeColor(Modes[i], lampColor);
                if (!isSelected) dotColor = Color.FromArgb(150, dotColor);

                using (Font f = new Font(Font, isSelected ? FontStyle.Bold : FontStyle.Regular))
                {
                    Size textSize = TextRenderer.MeasureText(Labels[i], f);
                    float dot = 9 * scale;
                    float gap = 7 * scale;
                    float total = dot + gap + textSize.Width;
                    float x = r.X + (r.Width - total) / 2;
                    float cy = r.Y + r.Height / 2;

                    if (Modes[i] == AppMode.Demo && lampColor == "dj")
                        HerculesUi.DrawSwatch(g, new RectangleF(x, cy - dot / 2, dot, dot), "dj");
                    else
                        using (var b = new SolidBrush(dotColor)) g.FillEllipse(b, x, cy - dot / 2, dot, dot);

                    var textRect = new Rectangle((int)(x + dot + gap), (int)r.Y, textSize.Width + 2, (int)r.Height);
                    TextRenderer.DrawText(g, Labels[i], f, textRect,
                        isSelected ? HerculesUi.TextPrimary : HerculesUi.TextMuted,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                }
            }

            if (Focused && ShowFocusCues)
            {
                RectangleF fr = SegmentRect(focusIndex);
                fr.Inflate(-1.5f, -1.5f);
                HerculesUi.DrawRounded(g, HerculesUi.Focus, 1.5f * scale, fr, 8 * scale);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hover)
            {
                hover = h;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();
            int h = HitTest(e.Location);
            if (h >= 0 && e.Button == MouseButtons.Left) Choose(h);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left && focusIndex > 0) { focusIndex--; Invalidate(); }
            else if (e.KeyCode == Keys.Right && focusIndex < Modes.Length - 1) { focusIndex++; Invalidate(); }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Choose(focusIndex); e.Handled = true; }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private void Choose(int index)
        {
            focusIndex = index;
            if (ModeChosen != null) ModeChosen(Modes[index]);
        }
    }

    /// <summary>Selector de color del modo lámpara.</summary>
    public class SwatchPicker : Control
    {
        public event Action<string> ColorChosen;

        private string selectedColor = "dj";
        private bool lampActive;
        private int hover = -1;
        private int focusIndex;
        private readonly float scale;
        private readonly ToolTip tip = new ToolTip();

        public SwatchPicker(float scale)
        {
            this.scale = scale;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleName = "Color del modo lámpara";
            AccessibleRole = AccessibleRole.RadioButton;
        }

        public void SetState(string color, bool active)
        {
            selectedColor = color;
            lampActive = active;
            focusIndex = Math.Max(0, Array.IndexOf(HerculesShared.DemoColors, color));
            AccessibleDescription = HerculesUi.ColorName(color);
            Invalidate();
        }

        private RectangleF SwatchRect(int i)
        {
            float size = 30 * scale;
            float gap = 14 * scale;
            float ring = 4 * scale;
            return new RectangleF(ring + i * (size + gap), (Height - size) / 2, size, size);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < HerculesShared.DemoColors.Length; i++)
            {
                RectangleF r = SwatchRect(i);
                r.Inflate(5 * scale, 5 * scale);
                if (r.Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : HerculesUi.Background);

            for (int i = 0; i < HerculesShared.DemoColors.Length; i++)
            {
                string color = HerculesShared.DemoColors[i];
                RectangleF r = SwatchRect(i);
                bool isSelected = color == selectedColor;

                RectangleF ring = r;
                ring.Inflate(3.5f * scale, 3.5f * scale);
                if (isSelected)
                {
                    Color ringColor = lampActive ? HerculesUi.TextPrimary : HerculesUi.TextFaint;
                    using (var pen = new Pen(ringColor, 2 * scale)) g.DrawEllipse(pen, ring);
                }
                else if (i == hover)
                {
                    using (var pen = new Pen(HerculesUi.Border, 2 * scale)) g.DrawEllipse(pen, ring);
                }

                HerculesUi.DrawSwatch(g, r, color);

                if (Focused && ShowFocusCues && i == focusIndex)
                {
                    RectangleF fr = r;
                    fr.Inflate(6.5f * scale, 6.5f * scale);
                    using (var pen = new Pen(HerculesUi.Focus, 1.5f * scale)) g.DrawEllipse(pen, fr);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hover)
            {
                hover = h;
                tip.SetToolTip(this, h >= 0 ? HerculesUi.ColorName(HerculesShared.DemoColors[h]) : null);
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();
            int h = HitTest(e.Location);
            if (h >= 0 && e.Button == MouseButtons.Left) Choose(h);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int last = HerculesShared.DemoColors.Length - 1;
            if (e.KeyCode == Keys.Left && focusIndex > 0) { focusIndex--; Invalidate(); }
            else if (e.KeyCode == Keys.Right && focusIndex < last) { focusIndex++; Invalidate(); }
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Choose(focusIndex); e.Handled = true; }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private void Choose(int index)
        {
            focusIndex = index;
            if (ColorChosen != null) ColorChosen(HerculesShared.DemoColors[index]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Interruptor visual. No toca el MIDI: le manda órdenes explícitas a HerculesTray
    /// (y la inicia si no está corriendo), y muestra el estado que la bandeja guarda.
    /// </summary>
    public class SwitchForm : Form
    {
        public const string WindowTitle = "Luces Hercules P32";

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private AppMode mode = AppMode.Normal;
        private string demoColor = "dj";
        private bool deviceConnected;
        private bool trayRunning;
        private DateTime ignoreFileUntil = DateTime.MinValue;

        private readonly float s;
        private readonly ModeSelector selector;
        private readonly SwatchPicker picker;
        private readonly System.Windows.Forms.Timer refreshTimer;

        private readonly Font fontEyebrow = new Font("Segoe UI", 8f, FontStyle.Bold);
        private readonly Font fontChip = new Font("Segoe UI", 8.5f);
        private readonly Font fontTitle = new Font("Segoe UI Semibold", 17f);
        private readonly Font fontBody = new Font("Segoe UI", 9.5f);
        private readonly Font fontLabel = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        private readonly Font fontFooter = new Font("Segoe UI", 8.5f);

        // Medidas en píxeles a 96 ppp; se multiplican por la escala del monitor
        private const int W = 420, PAD = 24;
        private const int Y_EYEBROW = 22, Y_TITLE = 50, Y_SUBTITLE = 92, Y_SELECTOR = 140, H_SELECTOR = 52;
        private const int Y_LAMP_LABEL = 214, Y_PICKER = 236, H_PICKER = 44, Y_SEPARATOR = 298, Y_FOOTER = 310, H = 356;

        public SwitchForm()
        {
            using (Graphics g = CreateGraphics()) s = g.DpiX / 96f;

            AutoScaleMode = AutoScaleMode.None;
            Text = WindowTitle;
            ClientSize = new Size(S(W), S(H));
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowInTaskbar = true;
            BackColor = HerculesUi.Background;
            ForeColor = HerculesUi.TextPrimary;
            DoubleBuffered = true;
            KeyPreview = true;

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            selector = new ModeSelector(s);
            selector.Bounds = new Rectangle(S(PAD), S(Y_SELECTOR), S(W - 2 * PAD), S(H_SELECTOR));
            selector.ModeChosen += m => RequestMode(m, demoColor);
            Controls.Add(selector);

            picker = new SwatchPicker(s);
            picker.Bounds = new Rectangle(S(PAD) - S(4), S(Y_PICKER), S(W - 2 * PAD), S(H_PICKER));
            picker.ColorChosen += c => RequestMode(AppMode.Demo, c);
            Controls.Add(picker);

            LoadState();
            PollEnvironment();
            ApplyStateToControls(false);

            // Mantiene la ventana al día si el modo se cambia desde la bandeja o se desconecta la controladora
            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = 1000;
            refreshTimer.Tick += (sender, e) => RefreshAll();
            refreshTimer.Start();
            Activated += (sender, e) => RefreshAll();
        }

        private int S(int px)
        {
            return (int)Math.Round(px * s);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Barra de título oscura (Windows 10 20H1+ / Windows 11)
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0)
                    DwmSetWindowAttribute(Handle, 19, ref on, 4);
            }
            catch { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            selector.Focus();
        }

        private void LoadState()
        {
            HerculesShared.LoadState(out mode, out demoColor);
        }

        private bool PollEnvironment()
        {
            bool connected = false;
            try { connected = HerculesMidi.IsConnected(); }
            catch { }
            bool tray = HerculesShared.IsTrayRunning();
            bool changed = connected != deviceConnected || tray != trayRunning;
            deviceConnected = connected;
            trayRunning = tray;
            return changed;
        }

        private void RefreshAll()
        {
            bool changed = PollEnvironment();

            // Tras un clic, da tiempo a que la bandeja guarde el estado nuevo
            if (DateTime.Now >= ignoreFileUntil)
            {
                AppMode newMode;
                string newColor;
                HerculesShared.LoadState(out newMode, out newColor);
                if (newMode != mode || newColor != demoColor)
                {
                    mode = newMode;
                    demoColor = newColor;
                    ApplyStateToControls(true);
                    changed = true;
                }
            }

            if (changed) Invalidate();
        }

        private void ApplyStateToControls(bool animate)
        {
            selector.SetState(mode, demoColor, animate);
            picker.SetState(demoColor, mode == AppMode.Demo);
        }

        private void RequestMode(AppMode target, string color)
        {
            if (target == mode && (target != AppMode.Demo || color == demoColor)) return;

            bool sent = HerculesShared.SignalTray(HerculesShared.CommandEventName(target, color));
            if (!sent && !StartTray(HerculesShared.CommandArg(target, color))) return;

            mode = target;
            if (target == AppMode.Demo) demoColor = color;
            ignoreFileUntil = DateTime.Now.AddMilliseconds(1500);
            trayRunning = true;
            ApplyStateToControls(true);
            Invalidate();
        }

        private bool StartTray(string arg)
        {
            string trayExe = Path.Combine(HerculesShared.AppDir, "HerculesTray.exe");
            if (!File.Exists(trayExe))
            {
                MessageBox.Show(this, "No se encontró HerculesTray.exe en:\n" + HerculesShared.AppDir,
                    WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(trayExe, arg)
                {
                    WorkingDirectory = HerculesShared.AppDir,
                    UseShellExecute = false
                });
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo iniciar HerculesTray.exe:\n" + ex.Message,
                    WindowTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
            else if (e.KeyCode == Keys.D1 || e.KeyCode == Keys.NumPad1) RequestMode(AppMode.Normal, demoColor);
            else if (e.KeyCode == Keys.D2 || e.KeyCode == Keys.NumPad2) RequestMode(AppMode.Off, demoColor);
            else if (e.KeyCode == Keys.D3 || e.KeyCode == Keys.NumPad3) RequestMode(AppMode.Demo, demoColor);
        }

        private string Subtitle()
        {
            if (!deviceConnected) return "No se detecta la controladora. Conectala por USB; el modo elegido se aplica al reconectar.";
            if (mode == AppMode.Off) return "La controladora queda a oscuras hasta que vuelvas a encenderla.";
            if (mode == AppMode.Demo) return "Colores fijos decorativos: " + HerculesUi.ColorName(demoColor).ToLower() + ".";
            return "Las luces las controla tu programa de DJ.";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int contentW = S(W - 2 * PAD);
            var left = TextFormatFlags.Left | TextFormatFlags.NoPadding;

            // Encabezado: nombre del equipo + estado de conexión
            TextRenderer.DrawText(g, "HERCULES DJCONTROL P32", fontEyebrow,
                new Rectangle(S(PAD), S(Y_EYEBROW), contentW, S(18)), HerculesUi.TextFaint, left | TextFormatFlags.VerticalCenter);

            string chipText = deviceConnected ? "Conectada" : "Sin controladora";
            Color chipDot = deviceConnected ? HerculesUi.Green : HerculesUi.Amber;
            Size chipSize = TextRenderer.MeasureText(chipText, fontChip);
            int chipH = S(24), chipW = chipSize.Width + S(30);
            var chip = new Rectangle(S(W - PAD) - chipW, S(Y_EYEBROW) - S(3), chipW, chipH);
            HerculesUi.FillRounded(g, HerculesUi.Surface, chip, chipH / 2f);
            using (var b = new SolidBrush(chipDot)) g.FillEllipse(b, chip.X + S(11), chip.Y + (chipH - S(7)) / 2f, S(7), S(7));
            TextRenderer.DrawText(g, chipText, fontChip,
                new Rectangle(chip.X + S(23), chip.Y, chipSize.Width + S(4), chipH), HerculesUi.TextMuted, left | TextFormatFlags.VerticalCenter);

            // Estado actual
            Color modeColor = HerculesUi.ModeColor(mode, demoColor);
            float dot = S(12);
            float dotY = S(Y_TITLE) + (S(36) - dot) / 2f;
            var dotRect = new RectangleF(S(PAD), dotY, dot, dot);
            if (mode != AppMode.Off)
            {
                // Halo suave alrededor del punto cuando hay luces encendidas
                RectangleF halo = dotRect;
                halo.Inflate(S(4), S(4));
                using (var b = new SolidBrush(Color.FromArgb(50, modeColor))) g.FillEllipse(b, halo);
            }
            if (mode == AppMode.Demo && demoColor == "dj") HerculesUi.DrawSwatch(g, dotRect, "dj");
            else using (var b = new SolidBrush(modeColor)) g.FillEllipse(b, dotRect);

            TextRenderer.DrawText(g, HerculesUi.ModeTitle(mode), fontTitle,
                new Rectangle(S(PAD + 24), S(Y_TITLE), contentW - S(24), S(36)), HerculesUi.TextPrimary, left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, Subtitle(), fontBody,
                new Rectangle(S(PAD), S(Y_SUBTITLE), contentW, S(40)), HerculesUi.TextMuted, left | TextFormatFlags.WordBreak);

            // Color del modo lámpara
            string lampLabel = "COLOR DEL MODO LÁMPARA";
            TextRenderer.DrawText(g, lampLabel, fontLabel,
                new Rectangle(S(PAD), S(Y_LAMP_LABEL), contentW, S(18)), HerculesUi.TextFaint, left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, HerculesUi.ColorName(demoColor), fontChip,
                new Rectangle(S(PAD), S(Y_LAMP_LABEL), contentW, S(18)), HerculesUi.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);

            // Pie: ayuda contextual
            using (var pen = new Pen(HerculesUi.Border, 1)) g.DrawLine(pen, S(PAD), S(Y_SEPARATOR), S(W - PAD), S(Y_SEPARATOR));
            string footer = trayRunning
                ? "Atajos: 1 Programa · 2 Apagado · 3 Lámpara · Esc cierra.\nPara anclarla: clic derecho en su ícono de la barra de tareas."
                : "La bandeja no está abierta: se inicia sola al elegir un modo.\nAtajos: 1 Programa · 2 Apagado · 3 Lámpara · Esc cierra.";
            TextRenderer.DrawText(g, footer, fontFooter,
                new Rectangle(S(PAD), S(Y_FOOTER), contentW, S(H - Y_FOOTER - 8)), HerculesUi.TextFaint, left | TextFormatFlags.WordBreak);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer.Dispose();
                fontEyebrow.Dispose();
                fontChip.Dispose();
                fontTitle.Dispose();
                fontBody.Dispose();
                fontLabel.Dispose();
                fontFooter.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public class HerculesToggle
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private const int SW_RESTORE = 9;

        [STAThread]
        public static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, HerculesShared.ToggleMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Ya hay una ventana abierta: la trae al frente en vez de abrir otra
                    IntPtr hwnd = FindWindow(null, SwitchForm.WindowTitle);
                    if (hwnd != IntPtr.Zero)
                    {
                        ShowWindow(hwnd, SW_RESTORE);
                        SetForegroundWindow(hwnd);
                    }
                    return;
                }

                try { SetProcessDPIAware(); }
                catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SwitchForm());
            }
        }
    }
}
