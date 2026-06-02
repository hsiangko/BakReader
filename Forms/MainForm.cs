using BakReader.Models;
using BakReader.Services;
using BakReader.UI;
using Button = BakReader.UI.RoundedButton;

namespace BakReader.Forms
{
    public partial class MainForm : Form
    {
        // ── 狀態 ──
        private int _currentStep = 0;
        private string _bakFilePath = "";
        private BackupInfo? _backupInfo;
        private SqlBackupService? _backupService;
        private List<TableInfo> _allTables = new();
        private ExportOptions _exportOptions = new();
        private CancellationTokenSource? _cts;

        // ── UI 控制項參考 ──
        private Panel[] _stepPanels = null!;
        private Panel pnlStepIndicator = null!;
        private Button btnBack = null!, btnNext = null!, btnFinish = null!;

        // Step 0
        private Label lblBakPath = null!;
        private Panel pnlBackupInfo = null!;
        private Label lblDbName = null!, lblBackupDate = null!;
        private Label lblBackupSize = null!, lblBackupType = null!;

        // Step 1
        private GradientProgressBar pbLoading = null!;
        private Label lblLoadingStatus = null!;
        private RichTextBox rtbLoadingLog = null!;

        // Step 2
        private TextBox txtSearch = null!;
        private DataGridView dgvTables = null!;
        private Label lblTableCount = null!, lblSelectedCount = null!;
        private ComboBox cmbSort = null!;

        // Step 3
        private TextBox txtServer = null!, txtDatabase = null!;
        private TextBox txtUsername = null!, txtPassword = null!, txtPort = null!;
        private CheckBox chkWindowsAuth = null!, chkCreateSchema = null!;
        private CheckBox chkCopyData = null!, chkDropIfExists = null!;
        private Label lblConnStatus = null!;

        // Step 4
        private GradientProgressBar pbExport = null!;
        private Label lblExportStatus = null!;
        private RichTextBox rtbExportLog = null!;

        // Step 5
        private RichTextBox rtbSummary = null!;

        public MainForm()
        {
            InitializeComponent();

            // 讀取並設定視窗 Icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", "icon.png");
                var loadedIcon = CreateIconFromPng(iconPath);
                if (loadedIcon != null)
                {
                    Icon = loadedIcon;
                }
            }
            catch
            {
                // 忽略載入錯誤以防環境不同時中斷
            }

            BuildUI();
            ShowStep(0);
        }

        /// <summary>
        /// 從 PNG 檔案建立一個支援 32-bit 完整透明度（Alpha Channel）的 Icon 物件。
        /// 避開 GDI+ GetHicon() 會遺失 Alpha 通道導致透明邊緣出現黑邊或灰邊的 Bug。
        /// </summary>
        private static Icon? CreateIconFromPng(string filePath)
        {
            if (!System.IO.File.Exists(filePath)) return null;

            try
            {
                byte[] pngBytes = System.IO.File.ReadAllBytes(filePath);
                using var ms = new System.IO.MemoryStream();
                using var bw = new System.IO.BinaryWriter(ms);

                using var img = Image.FromFile(filePath);
                byte w = (byte)(img.Width >= 256 ? 0 : img.Width);
                byte h = (byte)(img.Height >= 256 ? 0 : img.Height);

                // ICO Header (6 bytes)
                bw.Write((short)0);   // Reserved
                bw.Write((short)1);   // Type: Icon (1)
                bw.Write((short)1);   // Count: 1

                // Icon Directory Entry (16 bytes)
                bw.Write(w);
                bw.Write(h);
                bw.Write((byte)0);    // Color count (0 for 256+)
                bw.Write((byte)0);    // Reserved
                bw.Write((short)1);   // Color planes (1)
                bw.Write((short)32);  // Bits per pixel (32-bit ARGB)
                bw.Write(pngBytes.Length); // Image size
                bw.Write(22);         // Offset to image data (6 + 16 = 22)

                // PNG Image Data
                bw.Write(pngBytes);

                ms.Position = 0;
                return new Icon(ms);
            }
            catch
            {
                return null;
            }
        }

        // ════════════════════════════════════════
        // 主佈局（使用 TableLayoutPanel 避免 z-order 問題）
        // ════════════════════════════════════════
        private void BuildUI()
        {
            SuspendLayout();

            Text = "SQL Server 資料表匯出工具";
            Size = new Size(1060, 740);
            MinimumSize = new Size(920, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = DarkTheme.Background;
            ForeColor = DarkTheme.TextPrimary;
            Font = DarkTheme.FontBody;

            // ── 主 TableLayoutPanel：4 列（Header / StepBar / Content / NavBar）──
            var tbl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = DarkTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));   // Header
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));   // Step bar
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Content
            tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // Nav

            tbl.Controls.Add(BuildHeaderPanel(), 0, 0);
            tbl.Controls.Add(BuildStepBarPanel(), 0, 1);
            tbl.Controls.Add(BuildContentPanel(), 0, 2);
            tbl.Controls.Add(BuildNavPanel(), 0, 3);

            Controls.Add(tbl);
            ResumeLayout(false);
        }

        // ────────────────────────────────────────
        // Header
        // ────────────────────────────────────────
        private Panel BuildHeaderPanel()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Surface
            };

            var border = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = DarkTheme.Border
            };

            var lblTitle = new Label
            {
                Text = "🗄  SQL Server 資料表匯出工具",
                Font = DarkTheme.FontHeader,
                ForeColor = DarkTheme.TextPrimary,
                BackColor = DarkTheme.Surface,
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 440,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(22, 0, 0, 0)
            };

            var lblVer = new Label
            {
                Text = "Free SQL Recover v1.0 20260602",
                Font = DarkTheme.FontSmall,
                ForeColor = DarkTheme.TextMuted,
                BackColor = DarkTheme.Surface,
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 220,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 18, 0)
            };

            pnl.Controls.Add(border);
            pnl.Controls.Add(lblTitle);
            pnl.Controls.Add(lblVer);
            return pnl;
        }

        // ────────────────────────────────────────
        // Step Bar（自訂繪製）
        // ────────────────────────────────────────
        private Panel BuildStepBarPanel()
        {
            pnlStepIndicator = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Card
            };
            pnlStepIndicator.Paint += PnlStepIndicator_Paint;
            return pnlStepIndicator;
        }

        // ────────────────────────────────────────
        // Content（所有步驟面板）
        // ────────────────────────────────────────
        private Panel BuildContentPanel()
        {
            var pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Background,
                Padding = new Padding(28, 18, 28, 10)
            };

            _stepPanels = new Panel[6];
            for (int i = 0; i < 6; i++)
            {
                _stepPanels[i] = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = DarkTheme.Background,
                    Visible = false
                };
                pnlContent.Controls.Add(_stepPanels[i]);
            }

            BuildStep0(_stepPanels[0]);
            BuildStep1(_stepPanels[1]);
            BuildStep2(_stepPanels[2]);
            BuildStep3(_stepPanels[3]);
            BuildStep4(_stepPanels[4]);
            BuildStep5(_stepPanels[5]);

            return pnlContent;
        }

        // ────────────────────────────────────────
        // Nav Bar
        // ────────────────────────────────────────
        private Panel BuildNavPanel()
        {
            var pnlNav = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Surface
            };

            var topBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = DarkTheme.Border
            };

            var pnlBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                BackColor = DarkTheme.Surface,
                Padding = new Padding(0, 12, 18, 0),
                WrapContents = false
            };

            btnBack = new Button
            {
                Text = "← 上一步",
                Width = 112,
                Height = 38,
                Margin = new Padding(0, 0, 8, 0)
            };
            DarkTheme.ApplySecondary(btnBack);
            btnBack.Click += BtnBack_Click;

            btnNext = new Button
            {
                Text = "下一步  →",
                Width = 130,
                Height = 38,
                Margin = new Padding(0, 0, 8, 0)
            };
            DarkTheme.ApplyPrimary(btnNext);
            btnNext.Click += BtnNext_Click;

            btnFinish = new Button
            {
                Text = "關閉",
                Width = 100,
                Height = 38,
                Visible = false
            };
            DarkTheme.ApplySecondary(btnFinish);
            btnFinish.Click += (_, _) => Application.Exit();

            pnlBtns.Controls.Add(btnBack);
            pnlBtns.Controls.Add(btnNext);
            pnlBtns.Controls.Add(btnFinish);

            pnlNav.Controls.Add(topBorder);
            pnlNav.Controls.Add(pnlBtns);
            return pnlNav;
        }

        // ════════════════════════════════════════
        // Step 0：選擇備份檔
        // ════════════════════════════════════════
        private void BuildStep0(Panel p)
        {
            // ── 說明提示（底部）──
            var pnlTip = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = Color.FromArgb(20, 99, 102, 241)
            };
            pnlTip.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(70, 99, 102, 241), 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlTip.Width - 1, pnlTip.Height - 1);
            };
            const string tipUrl  = "https://aka.ms/sqlserver-download";
            const string tipText = "ℹ️  本工具透過 SQL Server LocalDB 引擎讀取 .bak 內容。\n" +
                                   "    若尚未安裝，請至 " + tipUrl + " 下載 SQL Server Express（內含 LocalDB）。";
            var lblTip = new LinkLabel
            {
                Text       = tipText,
                ForeColor  = DarkTheme.Info,
                Font       = DarkTheme.FontSmall,
                BackColor  = Color.FromArgb(20, 99, 102, 241),
                AutoSize   = false,
                Dock       = DockStyle.Fill,
                TextAlign  = ContentAlignment.MiddleLeft,
                Padding    = new Padding(14, 0, 14, 0),
                LinkColor        = Color.FromArgb(120, 200, 255),
                ActiveLinkColor  = Color.White,
                VisitedLinkColor = Color.FromArgb(120, 200, 255),
                DisabledLinkColor = DarkTheme.TextMuted
            };
            // 只讓 URL 部分變成連結
            int urlStart = tipText.IndexOf(tipUrl, StringComparison.Ordinal);
            lblTip.Links.Add(urlStart, tipUrl.Length, tipUrl);
            lblTip.LinkClicked += (_, e) =>
            {
                if (e.Link?.LinkData is string url)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            };
            pnlTip.Controls.Add(lblTip);

            var sp3 = MakeSpacer(14);

            // ── 備份資訊卡（初始隱藏）──
            pnlBackupInfo = new Panel
            {
                Dock = DockStyle.Top,
                Height = 128,
                BackColor = DarkTheme.Card,
                Visible = false
            };
            pnlBackupInfo.Paint += (s, e) =>
            {
                using var pen = new Pen(DarkTheme.Accent, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlBackupInfo.Width - 1, pnlBackupInfo.Height - 1);
            };

            var infoTitleLbl = new Label
            {
                Text = "📋  備份檔案資訊",
                ForeColor = DarkTheme.Accent,
                Font = DarkTheme.FontBold,
                BackColor = DarkTheme.Card,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 32,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };

            var infoGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = DarkTheme.Card,
                Padding = new Padding(12, 4, 12, 8)
            };
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14));
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));

            lblDbName   = MakeInfoValue();
            lblBackupDate = MakeInfoValue();
            lblBackupSize = MakeInfoValue();
            lblBackupType = MakeInfoValue();

            infoGrid.Controls.Add(MakeInfoKey("資料庫名稱"), 0, 0);
            infoGrid.Controls.Add(lblDbName,      1, 0);
            infoGrid.Controls.Add(MakeInfoKey("備份時間"),   2, 0);
            infoGrid.Controls.Add(lblBackupDate,  3, 0);
            infoGrid.Controls.Add(MakeInfoKey("備份大小"),   0, 1);
            infoGrid.Controls.Add(lblBackupSize,  1, 1);
            infoGrid.Controls.Add(MakeInfoKey("備份類型"),   2, 1);
            infoGrid.Controls.Add(lblBackupType,  3, 1);

            // 內部佈局：infoGrid (Fill) 先加，infoTitleLbl (Top) 後加 → 標題出現在上方
            pnlBackupInfo.Controls.Add(infoGrid);
            pnlBackupInfo.Controls.Add(infoTitleLbl);

            var sp2 = MakeSpacer(16);

            // ── 選檔區 ──
            var pnlFilePick = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                BackColor = DarkTheme.Card
            };
            pnlFilePick.Paint += (s, e) =>
            {
                using var pen = new Pen(DarkTheme.Border, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlFilePick.Width - 1, pnlFilePick.Height - 1);
            };

            var btnBrowse = new Button
            {
                Text = "📂  瀏覽",
                Width = 116,
                Height = 40,
                Dock = DockStyle.Right,
                Margin = new Padding(0, 16, 16, 0)
            };
            DarkTheme.ApplyPrimary(btnBrowse);
            btnBrowse.Click += BtnBrowse_Click;

            lblBakPath = new Label
            {
                Text = "尚未選擇檔案...",
                ForeColor = DarkTheme.TextMuted,
                Font = DarkTheme.FontBody,
                BackColor = DarkTheme.Card,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(16, 0, 0, 0)
            };

            pnlFilePick.Controls.Add(lblBakPath);
            pnlFilePick.Controls.Add(btnBrowse);

            var sp1 = MakeSpacer(12);

            // ── 副標 ──
            var lblSub = new Label
            {
                Text = "請選擇從 SQL Server Management Studio 匯出的 .bak 備份檔案。",
                ForeColor = Color.White,
                Font = DarkTheme.FontBody,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 26
            };

            // ── 主標 ──
            var lblTitle = new Label
            {
                Text = "步驟一：選擇 SQL Server 備份檔案 (.bak)",
                Font = DarkTheme.FontHeader,
                ForeColor = Color.White,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 44
            };

            // ══ 加入順序：Controls.Add() 會插到 index 0（最高優先）
            //    所以「最後加」的控制項排在最上面 (DockStyle.Top)
            //    → 底部元素先加，標題最後加
            p.Controls.Add(pnlTip);         // 視覺最底 → 最先加
            p.Controls.Add(sp3);
            p.Controls.Add(pnlBackupInfo);
            p.Controls.Add(sp2);
            p.Controls.Add(pnlFilePick);
            p.Controls.Add(sp1);
            p.Controls.Add(lblSub);
            p.Controls.Add(lblTitle);       // 視覺最頂 → 最後加 ✓
        }

        // ════════════════════════════════════════
        // Step 1：讀取中
        // ════════════════════════════════════════
        private void BuildStep1(Panel p)
        {
            // Fill 的 RichTextBox 最先加
            rtbLoadingLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Surface,
                ForeColor = Color.FromArgb(130, 160, 200),
                Font = DarkTheme.FontMono,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            p.Controls.Add(rtbLoadingLog);

            // 以下由下到上加入
            var lblLogHdr = new Label
            {
                Text = "執行記錄：",
                ForeColor = DarkTheme.TextMuted,
                Font = DarkTheme.FontSmall,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 24
            };
            p.Controls.Add(lblLogHdr);

            p.Controls.Add(MakeSpacer(14));

            lblLoadingStatus = new Label
            {
                Text = "準備中...",
                ForeColor = DarkTheme.Info,
                Font = DarkTheme.FontBody,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 28
            };
            p.Controls.Add(lblLoadingStatus);

            p.Controls.Add(MakeSpacer(10));

            pbLoading = new GradientProgressBar
            {
                Dock = DockStyle.Top,
                Height = 24,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            p.Controls.Add(pbLoading);

            p.Controls.Add(MakeSpacer(28));

            var lblTitle = new Label
            {
                Text = "步驟二：讀取備份檔案內容",
                Font = DarkTheme.FontHeader,
                ForeColor = Color.White,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 44
            };
            p.Controls.Add(lblTitle);
        }

        // ════════════════════════════════════════
        // Step 2：Table 清單
        // ════════════════════════════════════════
        private void BuildStep2(Panel p)
        {
            // 統計列（Bottom）最先加
            var pnlStats = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                BackColor = DarkTheme.Card
            };
            lblTableCount = new Label
            {
                Text = "共 0 個資料表",
                ForeColor = DarkTheme.TextSecondary,
                Font = DarkTheme.FontBody,
                BackColor = DarkTheme.Card,
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 220,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };
            lblSelectedCount = new Label
            {
                Text = "已勾選：0",
                ForeColor = DarkTheme.Success,
                Font = DarkTheme.FontBold,
                BackColor = DarkTheme.Card,
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 200,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 14, 0)
            };
            pnlStats.Controls.Add(lblTableCount);
            pnlStats.Controls.Add(lblSelectedCount);
            p.Controls.Add(pnlStats);

            // DataGridView（Fill）
            dgvTables = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = DarkTheme.Surface,
                GridColor = DarkTheme.Border,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 34 },
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = DarkTheme.Surface,
                    ForeColor = DarkTheme.TextPrimary,
                    SelectionBackColor = Color.FromArgb(55, 99, 102, 241),
                    SelectionForeColor = DarkTheme.TextPrimary,
                    Font = DarkTheme.FontBody,
                    Padding = new Padding(6, 0, 6, 0)
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = DarkTheme.Card,
                    ForeColor = DarkTheme.TextSecondary,
                    SelectionBackColor = DarkTheme.Card,
                    SelectionForeColor = DarkTheme.TextSecondary,
                    Font = DarkTheme.FontBold,
                    Padding = new Padding(6, 0, 6, 0)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(210, 28, 32, 50)
                },
                EnableHeadersVisualStyles = false
            };

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "✓", Width = 46,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Name = "colCheck", FillWeight = 1
            };
            var colSchema = new DataGridViewTextBoxColumn
            {
                HeaderText = "Schema", Width = 110,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Name = "colSchema", ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { ForeColor = DarkTheme.Accent }
            };
            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText = "資料表名稱", Name = "colName",
                ReadOnly = true, FillWeight = 70
            };
            var colRows = new DataGridViewTextBoxColumn
            {
                HeaderText = "筆數", Width = 110,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Name = "colRows", ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    ForeColor = DarkTheme.Info,
                    Font = DarkTheme.FontMono
                }
            };
            dgvTables.Columns.AddRange(colCheck, colSchema, colName, colRows);
            dgvTables.CellValueChanged += DgvTables_CellValueChanged;
            dgvTables.CurrentCellDirtyStateChanged += DgvTables_CurrentCellDirtyStateChanged;
            p.Controls.Add(dgvTables);

            // Toolbar（Top）最後加 → 排最上面
            // 使用 TableLayoutPanel 確保控制項垂直對齊
            var pnlToolbar = new TableLayoutPanel
            {
                Dock        = DockStyle.Top,
                Height      = 64,
                ColumnCount = 5,
                RowCount    = 1,
                BackColor   = DarkTheme.Background,
                Padding     = new Padding(0, 10, 8, 0),
                Margin      = Padding.Empty
            };
            pnlToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290)); // 搜尋框
            pnlToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));  // 排序標籤
            pnlToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); // 下拉選單
            pnlToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // 空白填充
            pnlToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); // 全選/全不選
            pnlToolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 搜尋框
            txtSearch = new TextBox
            {
                PlaceholderText = "🔍  搜尋資料表名稱...",
                Dock   = DockStyle.Fill,
                Height = 36,
                Margin = new Padding(0, 0, 10, 0)
            };
            DarkTheme.Apply(txtSearch);
            txtSearch.TextChanged += TxtSearch_TextChanged;
            pnlToolbar.Controls.Add(txtSearch, 0, 0);

            // 排序標籤
            var lblSort = new Label
            {
                Text      = "排序：",
                ForeColor = DarkTheme.TextSecondary,
                Font      = DarkTheme.FontBody,
                BackColor = DarkTheme.Background,
                AutoSize  = false,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlToolbar.Controls.Add(lblSort, 1, 0);

            // 排序下拉
            cmbSort = new ComboBox
            {
                Dock          = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin        = new Padding(0, 0, 0, 0)
            };
            DarkTheme.Apply(cmbSort);
            cmbSort.Items.AddRange(new[] { "名稱 A→Z", "名稱 Z→A", "筆數（高→低）", "筆數（低→高）" });
            cmbSort.SelectedIndex = 0;
            cmbSort.SelectedIndexChanged += (_, _) => RefreshTableGrid();
            pnlToolbar.Controls.Add(cmbSort, 2, 0);

            // 空白（col 3 留空）

            // 按鈕群組
            var pnlBtns = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor     = DarkTheme.Background,
                WrapContents  = false,
                Padding       = new Padding(0, 0, 0, 0)
            };
            var btnNone = new Button { Text = "全不選", Width = 90, Height = 42, Margin = new Padding(0, 0, 8, 0) };
            DarkTheme.ApplySecondary(btnNone);
            btnNone.Click += (_, _) => { _allTables.ForEach(t => t.IsSelected = false); RefreshTableGrid(); };

            var btnAll = new Button { Text = "全選", Width = 78, Height = 42 };
            DarkTheme.ApplySecondary(btnAll);
            btnAll.Click += (_, _) => { _allTables.ForEach(t => t.IsSelected = true); RefreshTableGrid(); };

            pnlBtns.Controls.Add(btnNone);
            pnlBtns.Controls.Add(btnAll);
            pnlToolbar.Controls.Add(pnlBtns, 4, 0);

            // 標題（更頂部）
            var lblTitle = new Label
            {
                Text = "步驟三：選擇要匯出的資料表",
                Font = DarkTheme.FontHeader,
                ForeColor = Color.White,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 44
            };

            // 最後加 = 排最上方
            p.Controls.Add(pnlToolbar);
            p.Controls.Add(lblTitle);
        }

        // ════════════════════════════════════════
        // Step 3：匯出設定
        // ════════════════════════════════════════
        private void BuildStep3(Panel p)
        {
            // 選中摘要（底部）
            var pnlSummary = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(18, 52, 211, 153)
            };
            pnlSummary.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(70, 52, 211, 153), 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, pnlSummary.Width - 1, pnlSummary.Height - 1);
            };
            var lblSummary = new Label
            {
                Text = "✅  已選擇 0 個資料表待匯出",
                ForeColor = DarkTheme.Success,
                Font = DarkTheme.FontBody,
                BackColor = Color.FromArgb(18, 52, 211, 153),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                Name = "lblSummary"
            };
            pnlSummary.Controls.Add(lblSummary);

            var sp2 = MakeSpacer(14);

            // 表單主體
            var tblForm = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 6,
                Height = 360,
                BackColor = DarkTheme.Background,
                Padding = Padding.Empty
            };
            tblForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tblForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 5; i++)
                tblForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            tblForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 90)); // row 5: 加高讓錯誤訊息完整顯示

            // Row 0
            tblForm.Controls.Add(MakeFormGroup("🖥  SQL Server 主機位址", out txtServer, "例：192.168.1.100 或 server\\instance"), 0, 0);
            tblForm.Controls.Add(MakeFormGroup("連接埠", out txtPort, "1433"), 1, 0);
            txtPort.Text = "1433";

            // Row 1
            tblForm.Controls.Add(MakeFormGroup("📦  目標資料庫名稱（不存在自動建立）", out txtDatabase, "NewDatabaseName"), 0, 1);
            var chkPnl = BuildCheckboxPanel();
            tblForm.Controls.Add(chkPnl, 1, 1);
            tblForm.SetRowSpan(chkPnl, 3);   // 跨 row 1~3，高度夠顯示三個勾選項目

            // Row 2: Windows Auth
            chkWindowsAuth = new CheckBox
            {
                Text = "使用 Windows 驗證",
                ForeColor = DarkTheme.TextPrimary,
                Font = DarkTheme.FontBody,
                BackColor = DarkTheme.Background,
                AutoSize = true,
                Margin = new Padding(4, 18, 0, 0)
            };
            chkWindowsAuth.CheckedChanged += ChkWindowsAuth_CheckedChanged;
            tblForm.Controls.Add(chkWindowsAuth, 0, 2);

            // Row 3
            tblForm.Controls.Add(MakeFormGroup("👤  帳號", out txtUsername, "sa"), 0, 3);

            // Row 4
            tblForm.Controls.Add(MakeFormGroup("🔒  密碼", out txtPassword, ""), 0, 4);
            txtPassword.PasswordChar = '●';
            txtPassword.UseSystemPasswordChar = false;

            // Row 5: 測試連線按鈕 + 錯誤訊息（跨兩欄，垂直排列）
            var pnlTest = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Background,
                Padding = new Padding(4, 6, 0, 2)
            };
            var btnTest = new Button
            {
                Text = "🔗  測試連線",
                Width = 130,
                Height = 36,
                Dock = DockStyle.Top
            };
            DarkTheme.ApplySecondary(btnTest);
            btnTest.Click += BtnTestConn_Click;

            lblConnStatus = new Label
            {
                Text = "",
                ForeColor = DarkTheme.TextSecondary,
                Font = DarkTheme.FontSmall,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(2, 6, 0, 0)
            };
            pnlTest.Controls.Add(lblConnStatus); // Fill 先加
            pnlTest.Controls.Add(btnTest);        // Top 後加 → 排在上方
            tblForm.Controls.Add(pnlTest, 0, 5);
            tblForm.SetColumnSpan(pnlTest, 2);    // 跨兩欄，錯誤訊息有完整寬度

            var sp1 = MakeSpacer(12);

            var lblTitle = new Label
            {
                Text = "步驟四：設定目標 SQL Server",
                Font = DarkTheme.FontHeader,
                ForeColor = Color.White,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 44
            };

            // 由下到上
            p.Controls.Add(pnlSummary);
            p.Controls.Add(sp2);
            p.Controls.Add(tblForm);
            p.Controls.Add(sp1);
            p.Controls.Add(lblTitle);
        }

        private Panel BuildCheckboxPanel()
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Background,
                Padding = new Padding(12, 8, 0, 0)
            };
            var fl = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Background,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            chkCreateSchema = new CheckBox { Text = "建立資料表 Schema", Checked = true, ForeColor = DarkTheme.TextPrimary, Font = DarkTheme.FontBody, BackColor = DarkTheme.Background, AutoSize = true };
            chkCopyData     = new CheckBox { Text = "複製資料", Checked = true, ForeColor = DarkTheme.TextPrimary, Font = DarkTheme.FontBody, BackColor = DarkTheme.Background, AutoSize = true };
            chkDropIfExists = new CheckBox { Text = "若 Table 已存在先刪除（⚠ 慎用）", Checked = false, ForeColor = DarkTheme.Warning, Font = DarkTheme.FontBody, BackColor = DarkTheme.Background, AutoSize = true };

            fl.Controls.Add(chkCreateSchema);
            fl.Controls.Add(chkCopyData);
            fl.Controls.Add(chkDropIfExists);
            pnl.Controls.Add(fl);
            return pnl;
        }

        // ════════════════════════════════════════
        // Step 4：匯出進度
        // ════════════════════════════════════════
        private void BuildStep4(Panel p)
        {
            rtbExportLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Surface,
                ForeColor = Color.FromArgb(130, 160, 200),
                Font = DarkTheme.FontMono,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            p.Controls.Add(rtbExportLog);

            var lblLogHdr = new Label
            {
                Text = "匯出記錄：",
                ForeColor = DarkTheme.TextMuted,
                Font = DarkTheme.FontSmall,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 24
            };
            p.Controls.Add(lblLogHdr);

            p.Controls.Add(MakeSpacer(8));

            var btnCancel = new Button
            {
                Text = "取消匯出",
                Width = 110,
                Height = 34,
                Dock = DockStyle.Top
            };
            DarkTheme.ApplySecondary(btnCancel);
            btnCancel.Click += (_, _) => _cts?.Cancel();
            p.Controls.Add(btnCancel);

            p.Controls.Add(MakeSpacer(8));

            lblExportStatus = new Label
            {
                Text = "準備匯出...",
                ForeColor = DarkTheme.Info,
                Font = DarkTheme.FontBody,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 28
            };
            p.Controls.Add(lblExportStatus);

            p.Controls.Add(MakeSpacer(10));

            pbExport = new GradientProgressBar
            {
                Dock = DockStyle.Top,
                Height = 26,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            p.Controls.Add(pbExport);

            p.Controls.Add(MakeSpacer(24));

            var lblTitle = new Label
            {
                Text = "步驟五：執行匯出",
                Font = DarkTheme.FontHeader,
                ForeColor = Color.White,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 44
            };
            p.Controls.Add(lblTitle);
        }

        // ════════════════════════════════════════
        // Step 5：完成
        // ════════════════════════════════════════
        private void BuildStep5(Panel p)
        {
            rtbSummary = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Card,
                ForeColor = DarkTheme.TextPrimary,
                Font = DarkTheme.FontBody,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Padding = new Padding(16)
            };
            p.Controls.Add(rtbSummary);

            var lblSub = new Label
            {
                Text = "所有選取的資料表已成功匯出到目標資料庫。",
                ForeColor = Color.White,
                Font = DarkTheme.FontBody,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleCenter
            };
            p.Controls.Add(lblSub);

            var lblTitle = new Label
            {
                Text = "✅  匯出完成！",
                Font = new Font("Segoe UI", 24f, FontStyle.Bold),
                ForeColor = DarkTheme.Success,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 66,
                TextAlign = ContentAlignment.MiddleCenter
            };
            p.Controls.Add(lblTitle);
        }

        // ════════════════════════════════════════
        // 步驟切換 & 步驟列繪製
        // ════════════════════════════════════════
        private void ShowStep(int step)
        {
            _currentStep = step;
            for (int i = 0; i < _stepPanels.Length; i++)
                _stepPanels[i].Visible = (i == step);

            btnBack.Visible   = step > 0 && step != 1 && step != 4 && step != 5;
            btnNext.Visible   = step != 1 && step != 4 && step != 5;
            btnFinish.Visible = step == 5;

            if (step == 3)
            {
                btnNext.Text = "開始匯出  ▶";
                DarkTheme.ApplySuccess(btnNext);
            }
            else
            {
                btnNext.Text = "下一步  →";
                DarkTheme.ApplyPrimary(btnNext);
            }

            pnlStepIndicator.Invalidate();
        }

        private void PnlStepIndicator_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            string[] labels = { "1 選檔案", "2 讀取中", "3 選資料表", "4 設定目標", "5 執行匯出", "6 完成" };
            int count = labels.Length;
            float w = (float)pnlStepIndicator.Width / count;
            int h = pnlStepIndicator.Height;

            for (int i = 0; i < count; i++)
            {
                var rect = new RectangleF(i * w, 0, w, h);
                bool isCurrent = (i == _currentStep);
                bool isDone    = (i < _currentStep);

                if (isCurrent)
                    g.FillRectangle(new SolidBrush(Color.FromArgb(28, 99, 102, 241)), rect);

                using var linePen = new Pen(
                    isCurrent ? DarkTheme.Accent : (isDone ? DarkTheme.Success : DarkTheme.Border), 3);
                g.DrawLine(linePen, rect.X + 6, rect.Bottom - 2, rect.Right - 6, rect.Bottom - 2);

                var textColor = isCurrent ? DarkTheme.Accent
                              : isDone    ? DarkTheme.Success
                                          : DarkTheme.TextMuted;
                using var font = isCurrent
                    ? new Font("Segoe UI", 20f, FontStyle.Bold)
                    : new Font("Segoe UI", 20f, FontStyle.Regular);

                string label = (isDone ? "✓ " : "") + labels[i];
                var sf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(label, font, new SolidBrush(textColor), rect, sf);
            }
        }

        // ════════════════════════════════════════
        // 導覽事件
        // ════════════════════════════════════════
        private async void BtnNext_Click(object? sender, EventArgs e)
        {
            switch (_currentStep)
            {
                case 0: await HandleStep0NextAsync(); break;
                case 2: HandleStep2Next(); break;
                case 3: await HandleStep3NextAsync(); break;
            }
        }

        private void BtnBack_Click(object? sender, EventArgs e)
        {
            switch (_currentStep)
            {
                case 2: ShowStep(0); break;
                case 3: ShowStep(2); break;
            }
        }

        // ── Step 0 ──
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "選擇 SQL Server 備份檔案",
                Filter = "SQL Server 備份 (*.bak)|*.bak|所有檔案 (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _bakFilePath      = dlg.FileName;
            lblBakPath.Text   = _bakFilePath;
            lblBakPath.ForeColor = DarkTheme.TextPrimary;
            _ = TryLoadBackupInfoAsync(_bakFilePath);
        }

        private async Task TryLoadBackupInfoAsync(string path)
        {
            try
            {
                pnlBackupInfo.Visible = false;
                var svc = new SqlBackupService(LocalDbService.LocalDbConnectionString);
                _backupInfo = await svc.ReadBackupInfoAsync(path);

                lblDbName.Text    = _backupInfo.DatabaseName;
                lblBackupDate.Text = _backupInfo.BackupFinishDate == default
                    ? "—" : _backupInfo.BackupFinishDate.ToString("yyyy/MM/dd HH:mm:ss");
                lblBackupSize.Text = _backupInfo.BackupSizeDisplay;
                lblBackupType.Text = _backupInfo.BackupTypeDisplay;
                pnlBackupInfo.Visible = true;
            }
            catch { /* LocalDB 未安裝時預期失敗，之後步驟再處理 */ }
        }

        private async Task HandleStep0NextAsync()
        {
            if (string.IsNullOrEmpty(_bakFilePath) || !File.Exists(_bakFilePath))
            {
                MessageBox.Show("請先選擇 .bak 備份檔案。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ShowStep(1);
            await StartRestoreAsync();
        }

        private async Task StartRestoreAsync()
        {
            rtbLoadingLog.Clear();
            pbLoading.Value   = 10;
            btnNext.Enabled   = false;
            btnBack.Enabled   = false;

            var progress = new Progress<string>(msg =>
            {
                lblLoadingStatus.Text = msg;
                AppendColorLog(rtbLoadingLog, $"[{DateTime.Now:HH:mm:ss}] {msg}", DarkTheme.TextSecondary);
                pbLoading.Value = Math.Min(pbLoading.Value + 12, 88);
            });

            try
            {
                AppendColorLog(rtbLoadingLog, "檢查 SQL Server LocalDB 環境...", DarkTheme.TextMuted);

                if (!LocalDbService.IsLocalDbInstalled())
                {
                    ShowStep(0);
                    MessageBox.Show(
                        "未偵測到 SQL Server LocalDB！\n\n" +
                        "請先安裝 SQL Server Express（包含 LocalDB）：\n" +
                        "https://aka.ms/sqlserver-download\n\n" +
                        "安裝完成後重新啟動本程式。",
                        "需要安裝 LocalDB",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                AppendColorLog(rtbLoadingLog, "LocalDB 已安裝 ✓", DarkTheme.Success);
                AppendColorLog(rtbLoadingLog, "啟動 LocalDB 實例...", DarkTheme.TextMuted);
                await LocalDbService.StartInstanceAsync();
                AppendColorLog(rtbLoadingLog, "LocalDB 已啟動 ✓", DarkTheme.Success);

                _backupService?.Dispose();
                _backupService = new SqlBackupService(LocalDbService.LocalDbConnectionString);
                pbLoading.Value = 30;

                await _backupService.RestoreToTempAsync(_bakFilePath, progress);
                pbLoading.Value = 76;

                AppendColorLog(rtbLoadingLog, "讀取資料表清單...", DarkTheme.TextMuted);
                _allTables = await _backupService.GetTableListAsync();
                AppendColorLog(rtbLoadingLog, $"找到 {_allTables.Count} 個資料表 ✓", DarkTheme.Success);

                pbLoading.Value     = 100;
                lblLoadingStatus.Text = $"完成！共讀取到 {_allTables.Count} 個資料表。";
                lblLoadingStatus.ForeColor = DarkTheme.Success;

                await Task.Delay(700);
                RefreshTableGrid();
                ShowStep(2);
            }
            catch (Exception ex)
            {
                AppendColorLog(rtbLoadingLog, $"錯誤：{ex.Message}", DarkTheme.Danger);
                lblLoadingStatus.Text = "發生錯誤，請重試。";
                lblLoadingStatus.ForeColor = DarkTheme.Danger;
                MessageBox.Show($"讀取備份失敗：\n\n{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowStep(0);
            }
            finally
            {
                btnNext.Enabled = true;
                btnBack.Enabled = true;
            }
        }

        // ── Step 2 ──
        private void HandleStep2Next()
        {
            var selected = _allTables.Where(t => t.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("請至少勾選一個資料表。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _exportOptions.SelectedTables = selected;

            // 更新 Step3 摘要標籤
            var lbl = _stepPanels[3].Controls.Find("lblSummary", true).FirstOrDefault() as Label;
            if (lbl != null)
                lbl.Text = $"✅  已選擇 {selected.Count} 個資料表待匯出";

            ShowStep(3);
        }

        private void TxtSearch_TextChanged(object? sender, EventArgs e) => RefreshTableGrid();

        private void RefreshTableGrid()
        {
            var kw = txtSearch?.Text?.Trim().ToLower() ?? "";
            var list = string.IsNullOrEmpty(kw)
                ? new List<TableInfo>(_allTables)
                : _allTables.Where(t =>
                    t.TableName.ToLower().Contains(kw) ||
                    t.Schema.ToLower().Contains(kw)).ToList();

            list = (cmbSort?.SelectedIndex ?? 0) switch
            {
                0 => list.OrderBy(t => t.Schema).ThenBy(t => t.TableName).ToList(),
                1 => list.OrderByDescending(t => t.TableName).ToList(),
                2 => list.OrderByDescending(t => t.RowCount).ToList(),
                3 => list.OrderBy(t => t.RowCount).ToList(),
                _ => list
            };

            dgvTables?.SuspendLayout();
            dgvTables?.Rows.Clear();
            foreach (var t in list)
            {
                int idx = dgvTables!.Rows.Add(t.IsSelected, t.Schema, t.TableName, t.RowCountDisplay);
                dgvTables.Rows[idx].Tag = t;
            }
            dgvTables?.ResumeLayout();

            int sel = _allTables.Count(t => t.IsSelected);
            if (lblTableCount != null)
                lblTableCount.Text = list.Count == _allTables.Count
                    ? $"共 {_allTables.Count} 個資料表"
                    : $"顯示 {list.Count} / {_allTables.Count}";
            if (lblSelectedCount != null)
                lblSelectedCount.Text = $"已勾選：{sel}";
        }

        private void DgvTables_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
        {
            if (dgvTables.IsCurrentCellDirty)
                dgvTables.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }

        private void DgvTables_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 0 || e.RowIndex < 0 || e.RowIndex >= dgvTables.Rows.Count) return;
            if (dgvTables.Rows[e.RowIndex].Tag is TableInfo t)
            {
                t.IsSelected = Convert.ToBoolean(dgvTables.Rows[e.RowIndex].Cells[0].Value);
                int sel = _allTables.Count(x => x.IsSelected);
                if (lblSelectedCount != null) lblSelectedCount.Text = $"已勾選：{sel}";
            }
        }

        // ── Step 3 ──
        private async Task HandleStep3NextAsync()
        {
            if (string.IsNullOrWhiteSpace(txtServer.Text))  { Warn("請輸入目標 SQL Server 主機位址。"); return; }
            if (string.IsNullOrWhiteSpace(txtDatabase.Text)) { Warn("請輸入目標資料庫名稱。"); return; }
            if (!chkWindowsAuth.Checked && string.IsNullOrWhiteSpace(txtUsername.Text)) { Warn("請輸入帳號。"); return; }

            CollectExportOptions();
            ShowStep(4);
            await StartExportAsync();
        }

        private void CollectExportOptions()
        {
            _exportOptions.TargetServer   = txtServer.Text.Trim();
            _exportOptions.TargetDatabase = txtDatabase.Text.Trim();
            _exportOptions.TargetUsername = txtUsername.Text.Trim();
            _exportOptions.TargetPassword = txtPassword.Text;
            _exportOptions.UseWindowsAuth = chkWindowsAuth.Checked;
            _exportOptions.CreateSchema   = chkCreateSchema.Checked;
            _exportOptions.CopyData       = chkCopyData.Checked;
            _exportOptions.DropIfExists   = chkDropIfExists.Checked;
            if (int.TryParse(txtPort.Text, out int port)) _exportOptions.Port = port;
        }

        private async void BtnTestConn_Click(object? sender, EventArgs e)
        {
            CollectExportOptions();
            var btn = (Button)sender!;
            btn.Enabled = false;
            lblConnStatus.Text = "連線測試中...";
            lblConnStatus.ForeColor = DarkTheme.TextSecondary;

            var (ok, msg) = await SqlExportService.TestTargetConnectionAsync(_exportOptions);
            lblConnStatus.Text      = ok ? $"✓ {msg}" : $"✗ {msg}";
            lblConnStatus.ForeColor = ok ? DarkTheme.Success : DarkTheme.Danger;
            btn.Enabled = true;
        }

        private void ChkWindowsAuth_CheckedChanged(object? sender, EventArgs e)
        {
            bool win = chkWindowsAuth.Checked;
            txtUsername.Enabled  = !win;
            txtPassword.Enabled  = !win;
            txtUsername.BackColor = win ? DarkTheme.Card : DarkTheme.SurfaceLight;
            txtPassword.BackColor = win ? DarkTheme.Card : DarkTheme.SurfaceLight;
        }

        // ── Step 4 ──
        private async Task StartExportAsync()
        {
            rtbExportLog.Clear();
            pbExport.Value = 0;
            _cts = new CancellationTokenSource();

            try
            {
                var sourceConn = _backupService!.GetTempDbConnectionString();
                var svc        = new SqlExportService();
                int total      = _exportOptions.SelectedTables.Count;

                var progress = new Progress<SqlExportService.ExportProgress>(p =>
                {
                    lblExportStatus.Text = $"({p.Current}/{p.Total})  {p.TableName}  →  {p.Status}";
                    pbExport.Value       = (int)((double)p.Current / p.Total * 100);

                    var c = p.Status.StartsWith("✓") ? DarkTheme.Success
                          : p.Status.StartsWith("✗") ? DarkTheme.Danger
                          : DarkTheme.TextSecondary;
                    AppendColorLog(rtbExportLog,
                        $"[{DateTime.Now:HH:mm:ss}] [{p.Current:D3}/{p.Total:D3}]  {p.TableName}  {p.Status}", c);
                });

                AppendColorLog(rtbExportLog, $"目標：{_exportOptions.TargetServer} / {_exportOptions.TargetDatabase}", DarkTheme.Info);
                AppendColorLog(rtbExportLog, $"共 {total} 個資料表", DarkTheme.TextSecondary);
                AppendColorLog(rtbExportLog, new string('─', 60), DarkTheme.Border);

                await svc.ExportAsync(sourceConn, _exportOptions, progress, _cts.Token);

                pbExport.Value        = 100;
                lblExportStatus.Text  = "✅  匯出完成！";
                lblExportStatus.ForeColor = DarkTheme.Success;
                AppendColorLog(rtbExportLog, $"✅ 全部完成！共匯出 {total} 個資料表", DarkTheme.Success);

                BuildSummary();
                await Task.Delay(900);
                ShowStep(5);
            }
            catch (OperationCanceledException)
            {
                AppendColorLog(rtbExportLog, "⚠️ 已取消", DarkTheme.Warning);
                lblExportStatus.Text      = "已取消";
                lblExportStatus.ForeColor = DarkTheme.Warning;
            }
            catch (Exception ex)
            {
                AppendColorLog(rtbExportLog, $"❌ {ex.Message}", DarkTheme.Danger);
                lblExportStatus.Text      = "匯出失敗";
                lblExportStatus.ForeColor = DarkTheme.Danger;
                MessageBox.Show($"匯出錯誤：\n\n{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BuildSummary()
        {
            rtbSummary.Clear();
            rtbSummary.AppendText("匯出摘要\n\n");
            rtbSummary.AppendText($"備份來源：{_bakFilePath}\n");
            rtbSummary.AppendText($"目標伺服器：{_exportOptions.TargetServer}\n");
            rtbSummary.AppendText($"目標資料庫：{_exportOptions.TargetDatabase}\n");
            rtbSummary.AppendText($"匯出時間：{DateTime.Now:yyyy/MM/dd HH:mm:ss}\n\n");
            rtbSummary.AppendText($"成功匯出 {_exportOptions.SelectedTables.Count} 個資料表：\n");
            foreach (var t in _exportOptions.SelectedTables)
                rtbSummary.AppendText($"  ✓  {t.DisplayName}  ({t.RowCountDisplay} 筆)\n");
        }

        // ════════════════════════════════════════
        // 輔助 UI 方法
        // ════════════════════════════════════════
        private static Panel MakeSpacer(int height) =>
            new Panel { Dock = DockStyle.Top, Height = height, BackColor = DarkTheme.Background };

        private static Label MakeInfoKey(string text) => new Label
        {
            Text = text,
            ForeColor = DarkTheme.TextSecondary,
            Font = DarkTheme.FontSmall,
            BackColor = DarkTheme.Card,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static Label MakeInfoValue() => new Label
        {
            Text = "—",
            ForeColor = DarkTheme.TextPrimary,
            Font = DarkTheme.FontBold,
            BackColor = DarkTheme.Card,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private static Panel MakeFormGroup(string labelText, out TextBox tb, string placeholder = "")
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DarkTheme.Background,
                Padding = new Padding(4, 2, 14, 2)
            };
            var lbl = new Label
            {
                Text = labelText,
                ForeColor = DarkTheme.TextSecondary,
                Font = DarkTheme.FontSmall,
                BackColor = DarkTheme.Background,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 20
            };
            tb = new TextBox
            {
                PlaceholderText = placeholder,
                Dock = DockStyle.Top,
                Height = 30
            };
            DarkTheme.Apply(tb);
            // 後加 label = 排在上面（z-order 0 = 頂部 DockStyle.Top）
            pnl.Controls.Add(tb);
            pnl.Controls.Add(lbl);
            return pnl;
        }

        private static void AppendColorLog(RichTextBox rtb, string text, Color color)
        {
            rtb.SelectionStart  = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor  = color;
            rtb.AppendText(text + "\n");
            rtb.ScrollToCaret();
        }

        private static void Warn(string msg) =>
            MessageBox.Show(msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _backupService?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
