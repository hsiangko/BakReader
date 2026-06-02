namespace BakReader.UI
{
    /// <summary>
    /// 深色主題色彩定義
    /// </summary>
    public static class DarkTheme
    {
        // ── 主要背景色 ──
        public static readonly Color Background       = Color.FromArgb(15, 17, 26);
        public static readonly Color Surface          = Color.FromArgb(24, 28, 44);
        public static readonly Color SurfaceLight     = Color.FromArgb(34, 39, 60);
        public static readonly Color Card             = Color.FromArgb(28, 32, 50);
        public static readonly Color CardHover        = Color.FromArgb(38, 43, 65);

        // ── 文字色 ──
        public static readonly Color TextPrimary      = Color.FromArgb(255, 230, 80);   // 明亮黃
        public static readonly Color TextSecondary    = Color.FromArgb(220, 190, 80);   // 淡黃
        public static readonly Color TextMuted        = Color.FromArgb(160, 130, 60);   // 暗黃灰

        // ── 強調色（藍紫漸層系列）──
        public static readonly Color Accent           = Color.FromArgb(99, 102, 241);   // Indigo
        public static readonly Color AccentHover      = Color.FromArgb(129, 132, 255);
        public static readonly Color AccentLight      = Color.FromArgb(99, 102, 241, 30);
        public static readonly Color Success          = Color.FromArgb(52, 211, 153);   // Emerald
        public static readonly Color Warning          = Color.FromArgb(251, 191, 36);   // Amber
        public static readonly Color Danger           = Color.FromArgb(239, 68, 68);    // Red
        public static readonly Color Info             = Color.FromArgb(56, 189, 248);   // Sky

        // ── 邊框色 ──
        public static readonly Color Border           = Color.FromArgb(50, 56, 80);
        public static readonly Color BorderLight      = Color.FromArgb(65, 72, 100);

        // ── 進度條 ──
        public static readonly Color ProgressBg       = Color.FromArgb(24, 28, 44);
        public static readonly Color ProgressFill     = Color.FromArgb(99, 102, 241);

        // ── 字型 ──
        public static readonly Font FontTitle         = new("Segoe UI", 26f, FontStyle.Bold);
        public static readonly Font FontSubtitle      = new("Segoe UI", 15f, FontStyle.Regular);
        public static readonly Font FontBody          = new("Segoe UI", 13f, FontStyle.Regular);
        public static readonly Font FontSmall         = new("Segoe UI", 12f, FontStyle.Regular);
        public static readonly Font FontBold          = new("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font FontMono          = new("Consolas", 12f, FontStyle.Regular);
        public static readonly Font FontStep          = new("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font FontButtonLarge   = new("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font FontHeader        = new("Segoe UI", 18f, FontStyle.Bold);

        /// <summary>
        /// 套用深色主題到 TextBox
        /// </summary>
        public static void Apply(TextBox tb)
        {
            tb.BackColor = SurfaceLight;
            tb.ForeColor = TextPrimary;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = FontBody;
        }

        /// <summary>
        /// 套用深色主題到 ComboBox
        /// </summary>
        public static void Apply(ComboBox cb)
        {
            cb.BackColor = SurfaceLight;
            cb.ForeColor = TextPrimary;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Font = FontBody;
        }

        /// <summary>
        /// 套用主要按鈕樣式
        /// </summary>
        public static void ApplyPrimary(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Accent;
            btn.ForeColor = Color.White;
            btn.Font = FontButtonLarge;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = AccentHover;
            btn.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// 套用次要按鈕樣式
        /// </summary>
        public static void ApplySecondary(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = SurfaceLight;
            btn.ForeColor = TextPrimary;
            btn.Font = FontBody;
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.MouseOverBackColor = CardHover;
            btn.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// 套用成功按鈕樣式
        /// </summary>
        public static void ApplySuccess(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Color.FromArgb(16, 185, 129);
            btn.ForeColor = Color.White;
            btn.Font = FontButtonLarge;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Success;
            btn.Cursor = Cursors.Hand;
        }

        /// <summary>
        /// 套用標籤樣式
        /// </summary>
        public static void ApplyLabel(Label lbl, bool isSecondary = false)
        {
            lbl.ForeColor = isSecondary ? TextSecondary : TextPrimary;
            lbl.BackColor = Color.Transparent;
            lbl.Font = FontBody;
        }
    }

    /// <summary>
    /// 自訂進度條（支援漸層色）
    /// </summary>
    public class GradientProgressBar : ProgressBar
    {
        public GradientProgressBar()
        {
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(DarkTheme.ProgressBg);

            if (Maximum <= 0) return;

            float pct = (float)Value / Maximum;
            int fillW = (int)(ClientSize.Width * pct);

            if (fillW > 0)
            {
                using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, fillW, ClientSize.Height),
                    Color.FromArgb(99, 102, 241),
                    Color.FromArgb(168, 85, 247),
                    System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                g.FillRectangle(brush, 0, 0, fillW, ClientSize.Height);
            }

            // 顯示百分比文字
            var text = $"{(int)(pct * 100)}%";
            using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
            var textSize = g.MeasureString(text, font);
            var textX = (ClientSize.Width - textSize.Width) / 2;
            var textY = (ClientSize.Height - textSize.Height) / 2;
            g.DrawString(text, font, Brushes.White, textX, textY);
        }
    }

    /// <summary>
    /// 自訂圓角 Panel
    /// </summary>
    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 12;
        public Color BorderColor { get; set; } = DarkTheme.Border;
        public bool ShowBorder { get; set; } = true;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new Rectangle(1, 1, Width - 2, Height - 2);
            using var path = GetRoundedPath(rect, CornerRadius);
            using var brush = new SolidBrush(BackColor);
            g.FillPath(brush, path);

            if (ShowBorder)
            {
                using var pen = new Pen(BorderColor, 1f);
                g.DrawPath(pen, path);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
            path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
            path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// 自訂圓角按鈕
    /// </summary>
    public class RoundedButton : Button
    {
        private bool _isHovered;
        private bool _isPressed;

        public int CornerRadius { get; set; } = 8;

        public RoundedButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 決定背景色
            Color bg = BackColor;
            if (Enabled)
            {
                if (_isPressed)
                {
                    bg = FlatAppearance.MouseDownBackColor != Color.Empty 
                        ? FlatAppearance.MouseDownBackColor 
                        : ControlPaint.Dark(BackColor, 0.1f);
                }
                else if (_isHovered)
                {
                    bg = FlatAppearance.MouseOverBackColor != Color.Empty 
                        ? FlatAppearance.MouseOverBackColor 
                        : ControlPaint.Light(BackColor, 0.15f);
                }
            }
            else
            {
                bg = Color.FromArgb(24, 28, 44); // 停用時的背景色
            }

            // 清除背景以避免圓角邊緣殘留
            g.Clear(Parent?.BackColor ?? DarkTheme.Background);

            // 繪製圓角背景
            var rect = new Rectangle(0, 0, Width, Height);
            var rectInner = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = GetRoundedPath(rectInner, CornerRadius);

            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            // 繪製邊框
            if (FlatAppearance.BorderSize > 0 && FlatAppearance.BorderColor != Color.Empty)
            {
                using var pen = new Pen(Enabled ? FlatAppearance.BorderColor : DarkTheme.Border, FlatAppearance.BorderSize);
                g.DrawPath(pen, path);
            }

            // 繪製文字
            Color fg = Enabled ? ForeColor : Color.FromArgb(100, 110, 130);
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, Text, Font, rect, fg, flags);
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int r2 = radius * 2;
            if (r2 > rect.Width) r2 = rect.Width;
            if (r2 > rect.Height) r2 = rect.Height;

            path.AddArc(rect.X, rect.Y, r2, r2, 180, 90);
            path.AddArc(rect.Right - r2, rect.Y, r2, r2, 270, 90);
            path.AddArc(rect.Right - r2, rect.Bottom - r2, r2, r2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r2, r2, r2, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
