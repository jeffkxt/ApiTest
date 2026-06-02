using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UltraLightApiTester.Models;
using UltraLightApiTester.Services;

namespace UltraLightApiTester.UI
{
    internal class CustomColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Color.White; } }
        public override Color MenuStripGradientEnd { get { return Color.White; } }
        public override Color StatusStripGradientBegin { get { return Color.FromArgb(0xF9, 0xFA, 0xFB); } }
        public override Color StatusStripGradientEnd { get { return Color.FromArgb(0xF9, 0xFA, 0xFB); } }
    }

    public class MainForm : Form
    {
        private readonly HistoryService _historyService = new HistoryService();
        private readonly FavoriteService _favoriteService = new FavoriteService();
        private readonly EnvironmentService _envService = new EnvironmentService();
        private CancellationTokenSource _sendCts;
        private string _selectedFilePath = "";

        // Colors
        private static readonly Color C_BG = Color.White;
        private static readonly Color C_SIDEBAR = Color.FromArgb(0xF3, 0xF4, 0xF6);
        private static readonly Color C_PRIMARY = Color.FromArgb(0x4F, 0x46, 0xE5);
        private static readonly Color C_PRIMARY_HOVER = Color.FromArgb(0x43, 0x3C, 0xCA);
        private static readonly Color C_TEXT = Color.FromArgb(0x1F, 0x29, 0x37);
        private static readonly Color C_SUBTEXT = Color.FromArgb(0x6B, 0x72, 0x80);
        private static readonly Color C_BORDER = Color.FromArgb(0xE5, 0xE7, 0xEB);
        private static readonly Color C_DISABLED = Color.FromArgb(0xF3, 0xF4, 0xF6);
        private static readonly Color C_SUCCESS = Color.FromArgb(0x10, 0xB9, 0x81);
        private static readonly Color C_DANGER = Color.FromArgb(0xEF, 0x44, 0x44);
        private static readonly Color C_WARNING = Color.FromArgb(0xF5, 0x9E, 0x0B);

        // Top
        private MenuStrip _menuStrip;
        private ToolStrip _toolStrip;
        private ToolStripComboBox _envCombo;
        private ToolStripTextBox _timeoutTextBox;
        private int TimeoutSec
        {
            get => SettingsService.TimeoutSeconds;
            set => SettingsService.TimeoutSeconds = value;
        }
        private ToolStripButton _btnEnvManage;
        private ToolStripButton _btnAuth;

        // Layout
        private SplitContainer _splitContainer;

        // Left
        private TabControl _leftTabs;
        private ListBox _historyListBox;
        private TreeView _favTreeView;
        private ContextMenuStrip _historyCtx, _favCtx, _favGroupCtx;
        private SavedRequest _loadedFav;
        private int _loadedFavIdx = -1;

        // Right — URL bar
        private Panel _rightPanel;
        private ComboBox _methodCombo;
        private TextBox _urlTextBox;
        private Button _btnSend;

        // Right — Headers
        private Label _lblHeaders;
        private TextBox _headersTextBox;

        // Right — Body
        private Label _lblBody;
        private ComboBox _bodyTypeCombo;
        private Button _btnFormatBody;
        private Button _btnBrowseFile;
        private TextBox _bodyTextBox;
        private DataGridView _formGrid;
        private Button _btnAddRow, _btnDelRow;
        private Panel _formPanel;

        // Right — Response
        private Label _lblResponse;
        private Button _btnFormatResp;
        private Button _btnSaveResp;
        private Label _lblRespStatus;
        private TextBox _responseTextBox;

        // Bottom
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusLabel;

        public MainForm()
        {
            InitializeUI();
            LoadData();
            StartPosition = FormStartPosition.CenterScreen;
        }

        // ═══════════════════════════════════════ UI INIT ══

        private void InitializeUI()
        {
            Text = "Api Test v1.0";
            Size = new Size(1100, 720);
            SetAppIcon();
            MinimumSize = new Size(820, 540);
            Font = new Font("Segoe UI", 9f);
            BackColor = C_BG;

            BuildMenu();
            BuildToolbar();
            BuildLeftPanel();
            BuildRightPanel();
            BuildStatusBar();

            // Wrap left + right in a draggable splitter
            _splitContainer = new SplitContainer
            {
                FixedPanel = FixedPanel.None,
                SplitterWidth = 4,
                BackColor = C_BORDER
            };
            _splitContainer.Panel1.Controls.Add(_leftTabs);
            _leftTabs.Dock = DockStyle.Fill;
            _splitContainer.Panel2.Controls.Add(_rightPanel);
            _rightPanel.Dock = DockStyle.Fill;
            Controls.Add(_splitContainer);
        }

        private void BuildMenu()
        {
            _menuStrip = new MenuStrip
            {
                BackColor = Color.White, ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9f),
                Renderer = new ToolStripProfessionalRenderer(new CustomColorTable())
            };
            var importMenu = new ToolStripMenuItem("导入");
            importMenu.DropDownItems.Add("Swagger 文件...", null, (s, e) => OnSwaggerImport());
            importMenu.DropDownItems.Add("Swagger URL...", null, (s, e) => OnSwaggerUrlImport());
            importMenu.DropDownItems.Add("-");
            importMenu.DropDownItems.Add("Postman 环境...", null, (s, e) => OnImportPostmanEnv());
            _menuStrip.Items.Add(importMenu);

            // Settings menu
            var settingsMenu = new ToolStripMenuItem("设置");
            settingsMenu.DropDownItems.Add("修改数据存储目录...", null, (s, e) => OnChangeDataPath());
            settingsMenu.DropDownItems.Add("打开数据目录", null, (s, e) => { try { System.Diagnostics.Process.Start(SettingsService.DataPath); } catch { } });
            _menuStrip.Items.Add(settingsMenu);

            // Show current data path
            _menuStrip.Items.Add(new ToolStripMenuItem("存储: " + SettingsService.DataPath) { Enabled = false, ForeColor = C_SUBTEXT });
            MainMenuStrip = _menuStrip;
            Controls.Add(_menuStrip);
        }

        private void BuildToolbar()
        {
            _toolStrip = new ToolStrip
            {
                BackColor = Color.White, GripStyle = ToolStripGripStyle.Hidden,
                Font = new Font("Segoe UI", 9f),
                Renderer = new ToolStripProfessionalRenderer(new CustomColorTable()),
                Padding = new Padding(6, 4, 6, 4)
            };
            _toolStrip.Items.Add(new ToolStripLabel("环境:") { ForeColor = C_SUBTEXT });
            _envCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Font = new Font("Segoe UI", 9f) };
            _envCombo.SelectedIndexChanged += OnEnvChanged;
            _toolStrip.Items.Add(_envCombo);
            _btnEnvManage = new ToolStripButton("管理", null, (s, e) => OnEnvManage()) { ForeColor = C_PRIMARY };
            _toolStrip.Items.Add(_btnEnvManage);

            _toolStrip.Items.Add(new ToolStripSeparator());

            _toolStrip.Items.Add(new ToolStripLabel("超时(s):") { ForeColor = C_SUBTEXT });
            _timeoutTextBox = new ToolStripTextBox
            {
                Text = SettingsService.TimeoutSeconds.ToString(), Width = 42, Font = new Font("Segoe UI", 9f),
                ToolTipText = "超时秒数，0 = 无限制"
            };
            _timeoutTextBox.TextChanged += (s, e) =>
            {
                if (int.TryParse(_timeoutTextBox.Text, out int v) && v >= 0)
                    SettingsService.TimeoutSeconds = v;
                else if (string.IsNullOrEmpty(_timeoutTextBox.Text))
                    SettingsService.TimeoutSeconds = 30;
            };
            _timeoutTextBox.TextBox.KeyPress += (s, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != '\b')
                    e.Handled = true;
            };
            _toolStrip.Items.Add(_timeoutTextBox);

            _toolStrip.Items.Add(new ToolStripSeparator());

            _btnAuth = new ToolStripButton("Auth", null, (s, e) => OnBasicAuth()) { ForeColor = C_PRIMARY, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            _toolStrip.Items.Add(_btnAuth);

            Controls.Add(_toolStrip);
        }

        private void BuildLeftPanel()
        {
            _leftTabs = new TabControl { Location = new Point(0, 0), Size = new Size(250, 580), Font = new Font("Segoe UI", 9f) };

            var historyTab = new TabPage("历史") { BackColor = C_SIDEBAR };
            _historyListBox = new ListBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = C_SIDEBAR,
                ForeColor = C_TEXT, Font = new Font("Segoe UI", 9f), IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 38
            };
            _historyListBox.DrawItem += DrawHistoryItem;
            _historyListBox.MouseClick += OnHistoryClick;
            _historyCtx = new ContextMenuStrip();
            _historyCtx.Items.Add("加载到编辑器", null, (s, e) => LoadHistoryToEditor());
            _historyCtx.Items.Add("保存到收藏", null, (s, e) => SaveHistoryToFav());
            _historyCtx.Items.Add("-");
            _historyCtx.Items.Add("删除", null, (s, e) => DeleteHistory());
            _historyCtx.Items.Add("清空全部历史", null, (s, e) => ClearAllHistory());
            _historyListBox.ContextMenuStrip = _historyCtx;
            historyTab.Controls.Add(_historyListBox);

            var favTab = new TabPage("收藏") { BackColor = C_SIDEBAR };
            _favTreeView = new TreeView
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = C_SIDEBAR,
                Font = new Font("Segoe UI", 9f), FullRowSelect = true, HideSelection = false
            };
            _favTreeView.NodeMouseClick += OnFavNodeClick;
            _favCtx = new ContextMenuStrip();
            _favCtx.Items.Add("加载到编辑器", null, (s, e) => LoadFavToEditor());
            _favCtx.Items.Add("重命名", null, (s, e) => RenameFav());
            _favCtx.Items.Add("移动到分组...", null, (s, e) => MoveFavToGroup());
            _favCtx.Items.Add("-");
            _favCtx.Items.Add("删除", null, (s, e) => DeleteFav());
            _favGroupCtx = new ContextMenuStrip();
            _favGroupCtx.Items.Add("从当前保存为新收藏", null, (s, e) => SaveCurrentToFav());
            _favGroupCtx.Items.Add("-");
            _favGroupCtx.Items.Add("新建分组", null, (s, e) => AddFavGroup());
            _favGroupCtx.Items.Add("重命名分组", null, (s, e) => RenameFavGroup());
            _favGroupCtx.Items.Add("删除分组", null, (s, e) => DeleteFavGroup());
            _favTreeView.ContextMenuStrip = _favCtx;
            favTab.Controls.Add(_favTreeView);

            _leftTabs.TabPages.Add(historyTab);
            _leftTabs.TabPages.Add(favTab);
        }

        private void BuildRightPanel()
        {
            _rightPanel = new Panel { BackColor = C_BG, Padding = new Padding(8) };

            // ── URL row ──
            _methodCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 26, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = C_BG, ForeColor = C_TEXT, FlatStyle = FlatStyle.Flat,
                Location = new Point(8, 8), Size = new Size(82, 30)
            };
            _methodCombo.Items.AddRange(new[] { "GET", "POST", "PUT", "DELETE", "PATCH" });
            _methodCombo.SelectedIndex = 0;
            _methodCombo.DrawItem += DrawMethodItem;
            _methodCombo.SelectedIndexChanged += OnMethodChanged;

            _urlTextBox = new TextBox
            {
                Location = new Point(95, 8), BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 10f), BackColor = C_BG, ForeColor = C_TEXT,
                Text = "https://jsonplaceholder.typicode.com/users/1"
            };
            _urlTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SendRequest(); };

            _btnSend = new Button
            {
                Text = "发送", FlatStyle = FlatStyle.Flat, BackColor = C_PRIMARY,
                ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand, Location = new Point(500, 6), Size = new Size(80, 30)
            };
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Click += (s, e) => SendRequest();
            _btnSend.MouseEnter += (s, e) => _btnSend.BackColor = C_PRIMARY_HOVER;
            _btnSend.MouseLeave += (s, e) => _btnSend.BackColor = _sendCts != null ? C_DANGER : C_PRIMARY;

            // ── Headers ──
            _lblHeaders = new Label { Text = "Headers", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_SUBTEXT, AutoSize = true };
            _headersTextBox = new TextBox
            {
                Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f), BackColor = C_BG, ForeColor = C_TEXT,
                Text = "User-Agent: UltraApiTester/1.0\r\nAccept: application/json"
            };

            // ── Body section ──
            _lblBody = new Label { Text = "Body", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_SUBTEXT, AutoSize = true };

            _bodyTypeCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f), Size = new Size(110, 20)
            };
            _bodyTypeCombo.Items.AddRange(new[] { "none", "JSON", "form-data", "x-www-form-urlencoded", "binary" });
            _bodyTypeCombo.SelectedIndex = 1; // JSON
            _bodyTypeCombo.SelectedIndexChanged += OnBodyTypeChanged;

            _btnFormatBody = new Button
            {
                Text = "格式化", Size = new Size(55, 22), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB), ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 8f), Cursor = Cursors.Hand
            };
            _btnFormatBody.FlatAppearance.BorderSize = 0;
            _btnFormatBody.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xD1, 0xD5, 0xDB);
            _btnFormatBody.Click += (s, e) => FormatBodyJson();

            _btnBrowseFile = new Button
            {
                Text = "文件", Size = new Size(36, 20), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB), ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 7.5f), Cursor = Cursors.Hand
            };
            _btnBrowseFile.FlatAppearance.BorderSize = 0;
            _btnBrowseFile.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xD1, 0xD5, 0xDB);
            _btnBrowseFile.Click += (s, e) => BrowseFile();

            _bodyTextBox = new TextBox
            {
                Multiline = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f), BackColor = C_BG, ForeColor = C_TEXT
            };

            // Form-data grid
            BuildFormGrid();

            // ── Response ──
            _lblResponse = new Label { Text = "Response", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = C_SUBTEXT, AutoSize = true };

            _btnFormatResp = new Button
            {
                Text = "格式化", Size = new Size(55, 22), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB), ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 8f), Cursor = Cursors.Hand
            };
            _btnFormatResp.FlatAppearance.BorderSize = 0;
            _btnFormatResp.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xD1, 0xD5, 0xDB);
            _btnFormatResp.Click += (s, e) => FormatResponseJson();

            _btnSaveResp = new Button
            {
                Text = "保存", Size = new Size(45, 22), FlatStyle = FlatStyle.Flat,
                BackColor = C_PRIMARY, ForeColor = Color.White,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold), Cursor = Cursors.Hand
            };
            _btnSaveResp.FlatAppearance.BorderSize = 0;
            _btnSaveResp.Click += (s, e) => SaveResponse();

            _lblRespStatus = new Label { AutoSize = true, Font = new Font("Segoe UI", 9f), ForeColor = C_SUBTEXT };
            _responseTextBox = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.5f), BackColor = C_BG, ForeColor = C_TEXT, WordWrap = false
            };

            _rightPanel.Controls.AddRange(new Control[] {
                _methodCombo, _urlTextBox, _btnSend,
                _lblHeaders, _headersTextBox,
                _lblBody, _bodyTypeCombo, _btnFormatBody, _btnBrowseFile,
                _bodyTextBox, _formPanel,
                _lblResponse, _btnFormatResp, _btnSaveResp, _lblRespStatus, _responseTextBox
            });

            _rightPanel.Resize += (s, e) => LayoutRightPanel();

            UpdateBodyUI();
        }

        private void BuildFormGrid()
        {
            _formPanel = new Panel { Visible = false };

            _formGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 26,
                BackgroundColor = C_BG,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xF3, 0xF4, 0xF6),
                    ForeColor = C_SUBTEXT,
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold)
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = C_BG, ForeColor = C_TEXT, Font = new Font("Segoe UI", 9f)
                }
            };

            var chkCol = new DataGridViewCheckBoxColumn { HeaderText = "", Width = 24, FillWeight = 5 };
            var keyCol = new DataGridViewTextBoxColumn { HeaderText = "Key", FillWeight = 35 };
            var typeCol = new DataGridViewComboBoxColumn { HeaderText = "Type", FillWeight = 15 };
            typeCol.Items.AddRange("Text", "File");
            var valCol = new DataGridViewTextBoxColumn { HeaderText = "Value", FillWeight = 40 };
            var btnCol = new DataGridViewButtonColumn { HeaderText = "", Text = "...", Width = 28, FillWeight = 5 };

            _formGrid.Columns.Add(chkCol);
            _formGrid.Columns.Add(keyCol);
            _formGrid.Columns.Add(typeCol);
            _formGrid.Columns.Add(valCol);
            _formGrid.Columns.Add(btnCol);

            _formGrid.CellClick += OnFormGridCellClick;
            _formGrid.CellValueChanged += OnFormGridCellChanged;

            var btnPanel = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = C_BG };

            _btnAddRow = new Button
            {
                Text = "+ 添加", Size = new Size(55, 24), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB), ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 8f), Cursor = Cursors.Hand,
                Location = new Point(0, 3)
            };
            _btnAddRow.FlatAppearance.BorderSize = 0;
            _btnAddRow.Click += (s, e) => AddFormRow();

            _btnDelRow = new Button
            {
                Text = "- 删除", Size = new Size(55, 24), FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xFC, 0xE4, 0xE4), ForeColor = C_DANGER,
                Font = new Font("Segoe UI", 8f), Cursor = Cursors.Hand,
                Location = new Point(58, 3)
            };
            _btnDelRow.FlatAppearance.BorderSize = 0;
            _btnDelRow.Click += (s, e) => DeleteFormRow();

            btnPanel.Controls.Add(_btnAddRow);
            btnPanel.Controls.Add(_btnDelRow);

            _formPanel.Controls.Add(_formGrid);
            _formPanel.Controls.Add(btnPanel);

            // add a default row
            AddFormRow();
        }

        private Button CreateMiniBtn(string text)
        {
            var btn = new Button
            {
                Text = text, Size = new Size(36, 18), FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent, ForeColor = C_SUBTEXT,
                Font = new Font("Segoe UI", 7.5f), Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF3, 0xF4, 0xF6);
            return btn;
        }

        private void BuildStatusBar()
        {
            _statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(0xF9, 0xFA, 0xFB),
                Renderer = new ToolStripProfessionalRenderer(new CustomColorTable())
            };
            _statusLabel = new ToolStripStatusLabel { Text = "就绪", ForeColor = C_SUBTEXT, Font = new Font("Segoe UI", 9f) };
            _statusStrip.Items.Add(_statusLabel);
            Controls.Add(_statusStrip);
        }

        // ═══════════════════════════════════════ BODY TYPE ══

        private void OnMethodChanged(object sender, EventArgs e)
        {
            var method = _methodCombo.SelectedItem?.ToString() ?? "GET";
            bool allowsBody = method != "GET" && method != "DELETE" && method != "HEAD";

            if (!allowsBody)
            {
                _bodyTypeCombo.SelectedIndex = 0; // none
            }
            _bodyTypeCombo.Enabled = allowsBody;
            UpdateBodyUI();
        }

        private void OnBodyTypeChanged(object sender, EventArgs e)
        {
            _selectedFilePath = "";
            UpdateBodyUI();
        }

        private void UpdateBodyUI()
        {
            var btype = _bodyTypeCombo.SelectedItem?.ToString() ?? "JSON";

            bool isNone = btype == "none";
            bool isJson = btype == "JSON";
            bool isForm = btype == "form-data" || btype == "x-www-form-urlencoded";
            bool isBinary = btype == "binary";

            _bodyTextBox.Visible = isJson;
            _btnFormatBody.Visible = isJson;
            _btnBrowseFile.Visible = isBinary;

            _formPanel.Visible = isForm;

            if (isNone)
            {
                _bodyTextBox.Visible = true;
                _bodyTextBox.Text = "";
                _bodyTextBox.Enabled = false;
                _bodyTextBox.BackColor = C_DISABLED;
                _bodyTextBox.ReadOnly = true;
            }
            else
            {
                _bodyTextBox.Enabled = true;
                _bodyTextBox.BackColor = C_BG;
                _bodyTextBox.ReadOnly = false;
            }

            if (isBinary)
            {
                _bodyTextBox.Visible = true;
                _bodyTextBox.ReadOnly = true;
                _bodyTextBox.BackColor = Color.FromArgb(0xEE, 0xF2, 0xFF);
            }

            if (isForm && _formGrid.Rows.Count == 0)
                AddFormRow();

            LayoutRightPanel();
        }

        // ── Form Grid ──

        private void AddFormRow()
        {
            int idx = _formGrid.Rows.Add(true, "", "Text", "");
            _formGrid.Rows[idx].Cells[1].Value = ""; // key
            _formGrid.Rows[idx].Cells[2].Value = "Text"; // type
        }

        private void DeleteFormRow()
        {
            if (_formGrid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in _formGrid.SelectedRows)
                    if (!row.IsNewRow) _formGrid.Rows.Remove(row);
            }
            else if (_formGrid.CurrentRow != null && !_formGrid.CurrentRow.IsNewRow)
            {
                _formGrid.Rows.Remove(_formGrid.CurrentRow);
            }
        }

        private void OnFormGridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            // Browse button column (index 4)
            if (e.ColumnIndex == 4)
            {
                var row = _formGrid.Rows[e.RowIndex];
                if (row.Cells[2].Value?.ToString() == "File")
                {
                    using (var dlg = new OpenFileDialog())
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                            row.Cells[3].Value = dlg.FileName;
                    }
                }
            }
        }

        private void OnFormGridCellChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 2) return;
            var row = _formGrid.Rows[e.RowIndex];
            var type = row.Cells[2].Value?.ToString();
            if (type == "File")
                row.Cells[3].Value = "";
        }

        // ═══════════════════════════════════════ LAYOUT ══

        private void LayoutRightPanel()
        {
            if (_rightPanel == null || _rightPanel.ClientSize.Width < 100) return;
            int w = _rightPanel.ClientSize.Width;
            int h = _rightPanel.ClientSize.Height;
            if (h < 120) return;

            int pad = 8;
            int gap = 4;

            // URL row
            int y = pad;
            _methodCombo.Location = new Point(pad, y);
            _methodCombo.Height = 28;
            _btnSend.Location = new Point(w - _btnSend.Width - pad, y);
            _btnSend.Height = 28;
            _urlTextBox.Location = new Point(pad + _methodCombo.Width + gap, y);
            _urlTextBox.Width = w - _btnSend.Width - _methodCombo.Width - pad * 2 - gap * 2;

            y += 30 + gap;

            int remaining = h - y - pad;
            int lblH = 20;
            int headersH = (remaining - lblH * 3 - gap * 4) * 18 / 100;
            int bodyH = (remaining - lblH * 3 - gap * 4) * 35 / 100;
            int respH = remaining - headersH - bodyH - lblH * 3 - gap * 4;

            // Headers
            _lblHeaders.Location = new Point(pad, y);
            _headersTextBox.Location = new Point(pad, y + lblH);
            _headersTextBox.Size = new Size(w - pad * 2, headersH);
            y += headersH + lblH + gap;

            // Body header row
            _lblBody.Location = new Point(pad, y);
            _bodyTypeCombo.Location = new Point(pad + 38, y);
            _btnFormatBody.Location = new Point(pad + 155, y);
            _btnBrowseFile.Location = new Point(pad + 215, y);

            int bodyContentTop = y + lblH;
            int bodyContentH = bodyH;

            // Body content
            _bodyTextBox.Location = new Point(pad, bodyContentTop);
            _bodyTextBox.Size = new Size(w - pad * 2, bodyContentH);

            _formPanel.Location = new Point(pad, bodyContentTop);
            _formPanel.Size = new Size(w - pad * 2, bodyContentH);

            y += bodyH + lblH + gap;

            // Response
            _lblResponse.Location = new Point(pad, y);
            _btnFormatResp.Location = new Point(pad + 62, y);
            _btnSaveResp.Location = new Point(pad + 122, y);
            _lblRespStatus.Location = new Point(pad + 172, y + 2);
            _lblRespStatus.Size = new Size(w - pad * 2 - 178, lblH);
            _responseTextBox.Location = new Point(pad, y + lblH);
            _responseTextBox.Size = new Size(w - pad * 2, respH);
        }

        // ═══════════════════════════════════════ LEFT DATA ══

        private void RefreshHistory()
        {
            _historyListBox.Items.Clear();
            var items = _historyService.Load();
            for (int i = items.Count - 1; i >= 0; i--)
                _historyListBox.Items.Add(items[i]);
        }

        private void RefreshFavorites()
        {
            _favTreeView.Nodes.Clear();
            var items = _favoriteService.Load();
            var groups = new Dictionary<string, TreeNode>();
            foreach (var item in items)
            {
                var gk = string.IsNullOrEmpty(item.Group) ? "未分组" : item.Group;
                if (!groups.ContainsKey(gk))
                { groups[gk] = new TreeNode("  " + gk) { Tag = null }; _favTreeView.Nodes.Add(groups[gk]); }
                var child = new TreeNode(item.Name) { Tag = item, ForeColor = GetMethodColor(item.Method) };
                child.NodeFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                groups[gk].Nodes.Add(child);
            }
            foreach (TreeNode n in _favTreeView.Nodes) n.Expand();
        }

        private void LoadData() { RefreshHistory(); RefreshFavorites(); RefreshEnvCombo(); }

        private void RefreshEnvCombo()
        {
            _envCombo.Items.Clear();
            foreach (var env in _envService.All) _envCombo.Items.Add(env.Name);
            if (_envService.All.Count > 0) _envCombo.SelectedIndex = _envService.ActiveIndex;
        }

        // ═══════════════════════════════════════ CUSTOM DRAW ══

        private void DrawHistoryItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || _historyListBox.Items[e.Index] == null) return;
            var item = (SavedRequest)_historyListBox.Items[e.Index];
            var g = e.Graphics; var b = e.Bounds;
            var bg = (e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(0xEE, 0xF2, 0xFF) : C_SIDEBAR;
            using (var br = new SolidBrush(bg)) g.FillRectangle(br, b);
            var mc = GetMethodColor(item.Method);
            using (var br = new SolidBrush(mc)) g.FillRectangle(br, b.X, b.Y + 3, 3, b.Height - 6);
            using (var br = new SolidBrush(mc)) g.DrawString(item.Method, new Font("Segoe UI", 8.5f, FontStyle.Bold), br, new Rectangle(b.X + 10, b.Y + 1, 44, b.Height - 2));
            using (var br = new SolidBrush(C_SUBTEXT)) g.DrawString(item.Timestamp.ToString("HH:mm"), new Font("Segoe UI", 7.5f), br, new Rectangle(b.X + 56, b.Y + 1, 36, b.Height - 2));
            var url = item.Url.Length > 58 ? item.Url.Substring(0, 58) + "..." : item.Url;
            using (var br = new SolidBrush(C_TEXT)) g.DrawString(url, new Font("Segoe UI", 8.5f), br, new Rectangle(b.X + 95, b.Y + 1, b.Width - 100, b.Height - 2));
            using (var pen = new Pen(C_BORDER)) g.DrawLine(pen, b.X + 6, b.Bottom - 1, b.Right - 6, b.Bottom - 1);
        }

        private void DrawMethodItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var method = _methodCombo.Items[e.Index].ToString();
            var g = e.Graphics;
            var bg = (e.State & DrawItemState.Selected) != 0 ? Color.FromArgb(0xEE, 0xF2, 0xFF) : C_BG;
            using (var br = new SolidBrush(bg)) g.FillRectangle(br, e.Bounds);
            using (var br = new SolidBrush(GetMethodColor(method)))
                g.DrawString(method, new Font("Segoe UI", 9.5f, FontStyle.Bold), br, new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 12, e.Bounds.Height - 4));
        }

        private static Color GetMethodColor(string m)
        {
            switch (m.ToUpper()) {
                case "GET": return Color.FromArgb(0x10, 0xB9, 0x81);
                case "POST": return Color.FromArgb(0x3B, 0x82, 0xF6);
                case "PUT": return Color.FromArgb(0xF5, 0x9E, 0x0B);
                case "DELETE": return Color.FromArgb(0xEF, 0x44, 0x44);
                case "PATCH": return Color.FromArgb(0x8B, 0x5C, 0xF6);
            }
            return Color.FromArgb(0x6B, 0x72, 0x80);
        }

        // ═══════════════════════════════════════ LEFT EVENTS ══

        private void OnHistoryClick(object sender, MouseEventArgs e)
        {
            int idx = _historyListBox.IndexFromPoint(e.Location);
            if (idx >= 0 && e.Button == MouseButtons.Left)
            {
                _loadedFav = null; _loadedFavIdx = -1; // history items don't receive response write-back
                LoadRequestToEditor((SavedRequest)_historyListBox.Items[idx]);
            }
        }
        private void LoadHistoryToEditor() { if (_historyListBox.SelectedIndex >= 0) { _loadedFav = null; _loadedFavIdx = -1; LoadRequestToEditor((SavedRequest)_historyListBox.Items[_historyListBox.SelectedIndex]); } }
        private void SaveHistoryToFav()
        {
            if (_historyListBox.SelectedIndex < 0) return;
            var item = (SavedRequest)_historyListBox.Items[_historyListBox.SelectedIndex];
            var n = ShowInputDialog("保存到收藏", "名称:", item.Url.Length > 50 ? item.Url.Substring(0, 50) : item.Url);
            if (string.IsNullOrWhiteSpace(n)) return;
            var g = ShowInputDialog("保存到收藏", "分组:", "");
            if (g == null) return;
            item.Name = n; item.Group = g.Trim();
            _favoriteService.Add(item); RefreshFavorites();
        }
        private void DeleteHistory() { int i = _historyListBox.SelectedIndex; if (i >= 0) { var items = _historyService.Load(); _historyService.Delete(items.Count - 1 - i); RefreshHistory(); } }
        private void ClearAllHistory() { if (MessageBox.Show("确定清空全部历史？", "确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK) { _historyService.Clear(); RefreshHistory(); } }

        private void OnFavNodeClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag is SavedRequest sr) { _favTreeView.SelectedNode = e.Node; _favTreeView.ContextMenuStrip = _favCtx; if (e.Button == MouseButtons.Left) SetLoadedFav(sr); }
            else { _favTreeView.SelectedNode = e.Node; _favTreeView.ContextMenuStrip = _favGroupCtx; }
        }
        private void LoadFavToEditor() { if (_favTreeView.SelectedNode?.Tag is SavedRequest sr) SetLoadedFav(sr); }
        private void RenameFav()
        {
            if (_favTreeView.SelectedNode?.Tag is SavedRequest sr) {
                var r = ShowInputDialog("重命名", "新名称:", sr.Name);
                if (!string.IsNullOrWhiteSpace(r)) { var items = _favoriteService.Load(); int idx = FindFavIdx(items, sr); if (idx >= 0) { _favoriteService.Rename(idx, r); RefreshFavorites(); } }
            }
        }
        private void MoveFavToGroup()
        {
            if (!(_favTreeView.SelectedNode?.Tag is SavedRequest sr)) return;
            var r = ShowInputDialog("移动到分组", "分组名:", sr.Group);
            if (r != null) { var items = _favoriteService.Load(); int idx = FindFavIdx(items, sr); if (idx >= 0) { _favoriteService.MoveToGroup(idx, r.Trim()); RefreshFavorites(); } }
        }
        private void DeleteFav()
        {
            if (_favTreeView.SelectedNode?.Tag is SavedRequest sr && MessageBox.Show("确定删除 \"" + sr.Name + "\" 吗？", "确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            { var items = _favoriteService.Load(); int idx = FindFavIdx(items, sr); if (idx >= 0) { _favoriteService.Delete(idx); RefreshFavorites(); } }
        }
        private void AddFavGroup() { var n = ShowInputDialog("新建分组", "分组名称:", ""); if (!string.IsNullOrWhiteSpace(n)) { _favoriteService.Add(new SavedRequest { Name = "新建请求", Group = n.Trim(), Method = "GET", Url = "", Headers = "User-Agent: UltraApiTester/1.0" }); RefreshFavorites(); } }
        private void RenameFavGroup()
        {
            var nd = _favTreeView.SelectedNode; if (nd == null || nd.Tag != null) return;
            var old = nd.Text.Trim(); var nn = ShowInputDialog("重命名分组", "新分组名:", old);
            if (string.IsNullOrWhiteSpace(nn) || nn.Trim() == old) return;
            var items = _favoriteService.Load(); foreach (var i in items) { if (i.Group == old) i.Group = nn.Trim(); }
            _favoriteService.Save(items); RefreshFavorites();
        }
        private void DeleteFavGroup()
        {
            var nd = _favTreeView.SelectedNode; if (nd == null || nd.Tag != null) return;
            if (MessageBox.Show("删除分组 \"" + nd.Text.Trim() + "\" 及其所有条目？", "确认", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            var items = _favoriteService.Load(); items.RemoveAll(i => i.Group == nd.Text.Trim()); _favoriteService.Save(items); RefreshFavorites();
        }
        private int FindFavIdx(List<SavedRequest> items, SavedRequest t) { for (int i = 0; i < items.Count; i++) if (items[i].Name == t.Name && items[i].Url == t.Url && items[i].Method == t.Method) return i; return -1; }

        private void SetLoadedFav(SavedRequest sr)
        {
            _loadedFav = sr;
            _loadedFavIdx = FindFavIdx(_favoriteService.Load(), sr);
            LoadRequestToEditor(sr);
        }

        private void LoadRequestToEditor(SavedRequest sr)
        {
            _methodCombo.SelectedItem = sr.Method;
            _urlTextBox.Text = sr.Url;
            _headersTextBox.Text = sr.Headers;
            _bodyTextBox.Text = sr.Body;
            _bodyTypeCombo.SelectedIndex = string.IsNullOrWhiteSpace(sr.Body) ? 1 : 1;
            _selectedFilePath = "";
            UpdateBodyUI();

            // restore last response
            if (sr.ResponseStatusCode > 0)
            {
                var sc = sr.ResponseStatusCode;
                var color = sc < 400 ? C_SUCCESS : (sc >= 500 ? C_DANGER : C_WARNING);
                _lblRespStatus.Text = string.Format("{0} {1}  |  {2}ms", sc, GetReasonPhrase(sc), sr.ResponseTimeMs);
                _lblRespStatus.ForeColor = color;
                _responseTextBox.Text = sr.ResponseHeaders + "\r\n" + new string('─', 60) + "\r\n\r\n" + sr.ResponseBody;
            }
            else
            {
                _lblRespStatus.Text = "";
                _responseTextBox.Text = "";
            }
        }

        private static string GetReasonPhrase(int code)
        {
            if (code >= 200 && code < 300) return "OK";
            if (code == 301) return "Moved";
            if (code == 302) return "Found";
            if (code == 304) return "Not Modified";
            if (code == 400) return "Bad Request";
            if (code == 401) return "Unauthorized";
            if (code == 403) return "Forbidden";
            if (code == 404) return "Not Found";
            if (code == 405) return "Method Not Allowed";
            if (code == 408) return "Timeout";
            if (code == 409) return "Conflict";
            if (code == 422) return "Unprocessable";
            if (code == 429) return "Too Many Requests";
            if (code == 500) return "Server Error";
            if (code == 502) return "Bad Gateway";
            if (code == 503) return "Unavailable";
            return "";
        }

        // ═══════════════════════════════════════ SEND ══

        private async void SendRequest()
        {
            if (_sendCts != null) { _sendCts.Cancel(); return; }

            var method = _methodCombo.SelectedItem?.ToString() ?? "GET";
            var url = _envService.ReplaceVariables(_urlTextBox.Text.Trim());
            var headers = _envService.ReplaceVariables(_headersTextBox.Text);
            var bodyType = _bodyTypeCombo.SelectedItem?.ToString() ?? "none";

            if (string.IsNullOrWhiteSpace(url)) { MessageBox.Show("请输入 URL。"); return; }

            _sendCts = new CancellationTokenSource();
            CancellationTokenSource timeoutCts = null;
            CancellationTokenSource linkedCts;
            var timeoutSecs = SettingsService.TimeoutSeconds;
            if (timeoutSecs > 0)
            {
                timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSecs));
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_sendCts.Token, timeoutCts.Token);
            }
            else
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_sendCts.Token);
            }
            _btnSend.Text = "取消"; _btnSend.BackColor = C_DANGER;
            _statusLabel.Text = "发送中..."; _responseTextBox.Text = ""; _lblRespStatus.Text = "";

            try
            {
                var request = new HttpRequestMessage(new HttpMethod(method), url);

                // Headers
                if (!string.IsNullOrWhiteSpace(headers))
                {
                    foreach (var line in headers.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var ci = line.IndexOf(':');
                        if (ci > 0)
                            try { request.Headers.TryAddWithoutValidation(line.Substring(0, ci).Trim(), line.Substring(ci + 1).Trim()); } catch { }
                    }
                }

                // Body
                if (method != "GET" && method != "DELETE" && method != "HEAD" && bodyType != "none")
                {
                    switch (bodyType)
                    {
                        case "JSON":
                            var body = _envService.ReplaceVariables(_bodyTextBox.Text);
                            if (!string.IsNullOrWhiteSpace(body))
                            {
                                request.Content = new StringContent(body, Encoding.UTF8);
                                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                            }
                            break;

                        case "binary":
                            if (!string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath))
                            {
                                var bytes = File.ReadAllBytes(_selectedFilePath);
                                request.Content = new ByteArrayContent(bytes);
                                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                            }
                            break;

                        case "form-data":
                            var mp = new MultipartFormDataContent();
                            foreach (DataGridViewRow row in _formGrid.Rows)
                            {
                                if (row.IsNewRow) continue;
                                var enabled = row.Cells[0].Value as bool? ?? true;
                                if (!enabled) continue;
                                var key = (row.Cells[1].Value as string ?? "").Trim();
                                if (string.IsNullOrEmpty(key)) continue;
                                var type = row.Cells[2].Value as string ?? "Text";
                                var val = row.Cells[3].Value as string ?? "";
                                if (type == "File" && File.Exists(val))
                                    mp.Add(new ByteArrayContent(File.ReadAllBytes(val)), key, Path.GetFileName(val));
                                else
                                    mp.Add(new StringContent(val), key);
                            }
                            request.Content = mp;
                            break;

                        case "x-www-form-urlencoded":
                            var kvp = new List<KeyValuePair<string, string>>();
                            foreach (DataGridViewRow row in _formGrid.Rows)
                            {
                                if (row.IsNewRow) continue;
                                var enabled = row.Cells[0].Value as bool? ?? true;
                                if (!enabled) continue;
                                var key = (row.Cells[1].Value as string ?? "").Trim();
                                if (string.IsNullOrEmpty(key)) continue;
                                kvp.Add(new KeyValuePair<string, string>(key, row.Cells[3].Value as string ?? ""));
                            }
                            request.Content = new FormUrlEncodedContent(kvp);
                            break;
                    }
                }

                var sw = Stopwatch.StartNew();
                using (var resp = await HttpSingleton.Instance.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token))
                {
                    sw.Stop();
                    var sc = resp.IsSuccessStatusCode ? C_SUCCESS : ((int)resp.StatusCode >= 500 ? C_DANGER : C_WARNING);
                    _lblRespStatus.Text = string.Format("{0} {1}  |  {2}ms", (int)resp.StatusCode, resp.ReasonPhrase, sw.ElapsedMilliseconds);
                    _lblRespStatus.ForeColor = sc;

                    var sb = new StringBuilder();
                    foreach (var h in resp.Headers) sb.AppendLine(h.Key + ": " + string.Join(", ", h.Value));
                    foreach (var h in resp.Content.Headers) sb.AppendLine(h.Key + ": " + string.Join(", ", h.Value));
                    sb.AppendLine(new string('─', 60));
                    sb.AppendLine();
                    _responseTextBox.Text = sb.ToString();

                    // detect if response is binary (file download)
                    var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
                    bool isText = contentType.Contains("json") || contentType.Contains("xml")
                        || contentType.Contains("html") || contentType.Contains("text")
                        || contentType.Contains("javascript") || contentType.Contains("css")
                        || contentType.Contains("form") || string.IsNullOrEmpty(contentType);

                    if (isText)
                    {
                        using (var stream = await resp.Content.ReadAsStreamAsync())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            var buf = new char[8192]; int n;
                            while ((n = await reader.ReadAsync(buf, 0, buf.Length)) > 0)
                            { linkedCts.Token.ThrowIfCancellationRequested(); _responseTextBox.AppendText(new string(buf, 0, n)); }
                        }
                    }
                    else
                    {
                        // binary file response — read bytes
                        var bytes = await resp.Content.ReadAsByteArrayAsync();
                        var contentDispo = resp.Content.Headers.ContentDisposition;
                        var fileName = "";
                        if (contentDispo != null && !string.IsNullOrEmpty(contentDispo.FileName))
                            fileName = SanitizeFileName(contentDispo.FileName.Trim('"'));

                        // ensure filename has an extension
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "response";
                        if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
                            fileName += GetExtFromContentType(contentType);

                        // prompt user for save location
                        using (var sfd = new SaveFileDialog
                        {
                            Title = "保存响应文件",
                            FileName = fileName,
                            Filter = "所有文件 (*.*)|*.*"
                        })
                        {
                            if (sfd.ShowDialog() == DialogResult.OK)
                            {
                                File.WriteAllBytes(sfd.FileName, bytes);
                                _responseTextBox.AppendText(string.Format("[二进制文件]\r\nContent-Type: {0}\r\n大小: {1}\r\n已保存到: {2}\r\n",
                                    contentType, FormatSize(bytes.Length), sfd.FileName));
                            }
                            else
                            {
                                _responseTextBox.AppendText(string.Format("[二进制文件 - 未保存]\r\nContent-Type: {0}\r\n大小: {1}\r\n",
                                    contentType, FormatSize(bytes.Length)));
                            }
                        }
                    }

                    // auto-format response body if JSON
                    var fullText = _responseTextBox.Text;
                    var headerLen = sb.Length;
                    if (fullText.Length > headerLen && isText)
                    {
                        var body = fullText.Substring(headerLen);
                        var bodyTrimmed = body.TrimStart();
                        if (bodyTrimmed.Length > 0 && bodyTrimmed.Length < 100 * 1024 &&
                            (bodyTrimmed.StartsWith("{") || bodyTrimmed.StartsWith("[")))
                        {
                            var formatted = FormatJson(bodyTrimmed);
                            _responseTextBox.Text = fullText.Substring(0, headerLen) + "\r\n" + formatted;
                        }
                        else if (bodyTrimmed.Length >= 100 * 1024)
                        {
                            _responseTextBox.AppendText("\r\n\r\n[响应较大 (" + FormatSize(bodyTrimmed.Length)
                                + ")，建议点击「保存」按钮下载后查看]");
                        }
                        _lblRespStatus.Text = string.Format("{0} {1}  |  {2}ms  |  {3}",
                            (int)resp.StatusCode, resp.ReasonPhrase, sw.ElapsedMilliseconds, FormatSize(headerLen + bodyTrimmed.Length));
                    }
                    else
                    {
                        _lblRespStatus.Text = string.Format("{0} {1}  |  {2}ms  |  {3}",
                            (int)resp.StatusCode, resp.ReasonPhrase, sw.ElapsedMilliseconds, FormatSize(fullText.Length));
                    }

                    // build response data
                    var respBody = _responseTextBox.Text;
                    var headerPart = sb.ToString();
                    var bodyPart = respBody.Length > headerPart.Length ? respBody.Substring(headerPart.Length).TrimStart('\r', '\n') : respBody;

                    // save to history with full response
                    _historyService.Add(new SavedRequest
                    {
                        Method = method, Url = url, Headers = _headersTextBox.Text, Body = GetBodyText(),
                        ResponseStatusCode = (int)resp.StatusCode, ResponseTimeMs = sw.ElapsedMilliseconds,
                        ResponseHeaders = headerPart.TrimEnd('\r', '\n'), ResponseBody = bodyPart
                    });
                    RefreshHistory();

                    // save response back to loaded favorite (update in-place)
                    try
                    {
                        if (_loadedFav != null && _loadedFavIdx >= 0)
                        {
                            // update the original object (also updates TreeView Tag)
                            _loadedFav.ResponseStatusCode = (int)resp.StatusCode;
                            _loadedFav.ResponseTimeMs = sw.ElapsedMilliseconds;
                            _loadedFav.ResponseHeaders = headerPart.TrimEnd('\r', '\n');
                            _loadedFav.ResponseBody = bodyPart;
                            _loadedFav.Url = _urlTextBox.Text;
                            _loadedFav.Headers = _headersTextBox.Text;
                            _loadedFav.Body = GetBodyText();
                            _loadedFav.Timestamp = DateTime.Now;

                            // persist to file
                            var items = _favoriteService.Load();
                            if (_loadedFavIdx < items.Count)
                            {
                                items[_loadedFavIdx] = _loadedFav;
                                _favoriteService.Save(items);
                            }
                        }
                    }
                    catch { }

                    _statusLabel.Text = "完成";
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts != null && timeoutCts.IsCancellationRequested && !_sendCts.IsCancellationRequested)
                {
                    _lblRespStatus.Text = "请求超时 (" + SettingsService.TimeoutSeconds + "s)";
                    _lblRespStatus.ForeColor = C_WARNING;
                    _statusLabel.Text = "超时";
                }
                else
                {
                    _lblRespStatus.Text = "已取消";
                    _lblRespStatus.ForeColor = C_WARNING;
                    _statusLabel.Text = "已取消";
                }
            }
            catch (Exception ex) { _lblRespStatus.Text = "失败"; _lblRespStatus.ForeColor = C_DANGER; _responseTextBox.Text = ex.Message; _statusLabel.Text = "错误: " + ex.Message; }
            finally
            {
                _btnSend.Text = "发送"; _btnSend.BackColor = C_PRIMARY;
                _sendCts?.Dispose(); _sendCts = null;
                if (timeoutCts != null) timeoutCts.Dispose();
                linkedCts.Dispose();
            }
        }

        private string GetBodyText()
        {
            var bt = _bodyTypeCombo.SelectedItem?.ToString() ?? "none";
            switch (bt)
            {
                case "JSON": return _bodyTextBox.Text;
                case "form-data":
                case "x-www-form-urlencoded":
                    var sb = new StringBuilder();
                    foreach (DataGridViewRow row in _formGrid.Rows)
                    { if (!row.IsNewRow) sb.AppendLine((row.Cells[1].Value as string ?? "") + "=" + (row.Cells[3].Value as string ?? "")); }
                    return sb.ToString();
                case "binary": return _selectedFilePath;
            }
            return "";
        }

        // ═══════════════════════════════════════ FILE / JSON ══

        private void BrowseFile()
        {
            using (var dlg = new OpenFileDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                { _selectedFilePath = dlg.FileName; _bodyTextBox.Text = "[文件: " + dlg.FileName + "]"; _bodyTextBox.BackColor = Color.FromArgb(0xEE, 0xF2, 0xFF); }
            }
        }

        private void SaveResponse()
        {
            if (string.IsNullOrWhiteSpace(_responseTextBox.Text)) { MessageBox.Show("响应为空。"); return; }
            using (var dlg = new SaveFileDialog { Title = "保存响应", Filter = "JSON (*.json)|*.json|文本 (*.txt)|*.txt|所有 (*.*)|*.*", FileName = "response.json" })
            { if (dlg.ShowDialog() == DialogResult.OK) { File.WriteAllText(dlg.FileName, _responseTextBox.Text, Encoding.UTF8); _statusLabel.Text = "已保存: " + Path.GetFileName(dlg.FileName); } }
        }

        private void FormatBodyJson() { var t = _bodyTextBox.Text.Trim(); if (!string.IsNullOrEmpty(t)) _bodyTextBox.Text = FormatJson(t); }
        private void FormatResponseJson()
        {
            var t = _responseTextBox.Text; if (string.IsNullOrWhiteSpace(t)) return;
            int si = 0; for (int i = 0; i < t.Length; i++) if (t[i] == '{' || t[i] == '[') { si = i; break; }
            if (si > 0) _responseTextBox.Text = t.Substring(0, si) + FormatJson(t.Substring(si));
            else _responseTextBox.Text = FormatJson(t);
        }

        private static string FormatJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;
            try
            {
                var sb = new StringBuilder(); int indent = 0; bool inStr = false;
                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inStr) { sb.Append(c); if (c == '"' && i > 0 && json[i - 1] != '\\') inStr = false; continue; }
                    switch (c)
                    {
                        case '"': sb.Append(c); inStr = true; break;
                        case '{': case '[': sb.Append(c); sb.AppendLine(); indent++; sb.Append(' ', indent * 2); break;
                        case '}': case ']': sb.AppendLine(); indent--; if (indent < 0) indent = 0; sb.Append(' ', indent * 2); sb.Append(c); break;
                        case ',': sb.Append(c); sb.AppendLine(); sb.Append(' ', indent * 2); break;
                        case ':': sb.Append(": "); break;
                        case ' ': case '\t': case '\n': case '\r': break;
                        default: sb.Append(c); break;
                    }
                }
                return sb.ToString();
            }
            catch { return json; }
        }

        // ═══════════════════════════════════════ SAVE / IMPORT ══

        private void SaveCurrentToFav()
        {
            var n = ShowInputDialog("保存到收藏", "名称:", "");
            if (string.IsNullOrWhiteSpace(n)) return;
            var g = ShowInputDialog("保存到收藏", "分组:", "");
            if (g == null) return;
            _favoriteService.Add(new SavedRequest { Name = n, Group = g.Trim(), Method = _methodCombo.SelectedItem?.ToString() ?? "GET", Url = _urlTextBox.Text, Headers = _headersTextBox.Text, Body = GetBodyText() });
            RefreshFavorites();
        }

        private async void OnSwaggerImport()
        {
            using (var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json|All (*.*)|*.*", Title = "选择 Swagger JSON 文件" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                _statusLabel.Text = "正在解析...";
                try { var pr = await Task.Run(() => SwaggerParser.ParseFromFile(dlg.FileName)); ShowImportDlg(pr.Endpoints, dlg.FileName, pr.Title); }
                catch (Exception ex) { MessageBox.Show("解析失败: " + ex.Message); }
                _statusLabel.Text = "就绪";
            }
        }

        private async void OnSwaggerUrlImport()
        {
            var url = ShowInputDialog("从 URL 导入", "输入 Swagger JSON URL:", "http://localhost:8080/swagger/v1/swagger.json");
            if (string.IsNullOrWhiteSpace(url)) return;
            _statusLabel.Text = "正在获取...";
            try { var pr = await SwaggerParser.ParseFromUrl(url); ShowImportDlg(pr.Endpoints, url, pr.Title); }
            catch (Exception ex) { MessageBox.Show("获取失败: " + ex.Message); }
            _statusLabel.Text = "就绪";
        }

        private void OnImportPostmanEnv()
        {
            using (var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json|All (*.*)|*.*", Title = "导入 Postman 环境 JSON" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var json = File.ReadAllText(dlg.FileName);
                    var jss = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var root = jss.Deserialize<Dictionary<string, object>>(json);

                    var envName = "Postman Import";
                    if (root.TryGetValue("name", out var nm)) envName = nm as string ?? envName;

                    var vars = new Dictionary<string, string>();
                    if (root.TryGetValue("values", out var vals) && vals is ArrayList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is Dictionary<string, object> entry)
                            {
                                var key = entry.TryGetValue("key", out var k) ? (k as string ?? "") : "";
                                var value = entry.TryGetValue("value", out var v) ? (v as string ?? "") : "";
                                var enabled = entry.TryGetValue("enabled", out var en) ? (en as bool? ?? true) : true;
                                if (!string.IsNullOrEmpty(key) && enabled)
                                    vars[key] = value;
                            }
                        }
                    }

                    if (vars.Count == 0)
                    {
                        MessageBox.Show("未找到有效的环境变量。", "提示");
                        return;
                    }

                    // check for duplicate name
                    int cnt = 1; var baseName = envName;
                    while (_envService.All.Any(e => e.Name == envName))
                        envName = baseName + " (" + (cnt++) + ")";

                    _envService.Add(new EnvironmentConfig { Name = envName, Variables = vars });
                    RefreshEnvCombo();
                    _envCombo.SelectedIndex = _envService.All.Count - 1;
                    _statusLabel.Text = string.Format("已导入 Postman 环境: {0} ({1} 个变量)", envName, vars.Count);
                }
                catch (Exception ex) { MessageBox.Show("解析失败: " + ex.Message); }
            }
        }

        private void ShowImportDlg(List<SwaggerEndpoint> endpoints, string sourceUrl, string title = "")
        {
            using (var dlg = new SwaggerImportDialog(endpoints, sourceUrl, title))
            {
                if (dlg.ShowDialog() != DialogResult.OK || dlg.SelectedRequests.Count == 0) return;
                var existing = _favoriteService.Load();
                var fromSrc = existing.Where(x => x.SourceUrl == sourceUrl).ToList();
                var newItems = dlg.SelectedRequests;
                int mode = dlg.MergeMode;

                if (fromSrc.Count > 0 && mode == 2)
                {
                    var keys = new HashSet<string>(fromSrc.Select(x => x.Method + "|" + x.Url));
                    newItems = newItems.Where(n => !keys.Contains(n.Method + "|" + n.Url)).ToList();
                }
                else if (fromSrc.Count > 0 && mode == 1)
                {
                    var om = new Dictionary<string, SavedRequest>();
                    foreach (var o in fromSrc) om[o.Method + "|" + o.Url] = o;
                    foreach (var ni in newItems)
                    {
                        var k = ni.Method + "|" + ni.Url;
                        if (om.TryGetValue(k, out var old))
                        {
                            ni.Name = old.Name; ni.Group = old.Group;
                            if (!string.IsNullOrWhiteSpace(old.Headers) && old.Headers != "User-Agent: UltraApiTester/1.0\r\nAccept: application/json, text/plain, */*")
                                ni.Headers = old.Headers;
                            if (!string.IsNullOrWhiteSpace(old.Body) && old.Body != ni.Body)
                                ni.Body = old.Body;
                        }
                    }
                }

                var final = existing.ToList();
                if (mode == 0 && fromSrc.Count > 0) final.RemoveAll(x => x.SourceUrl == sourceUrl);
                final.AddRange(newItems);
                _favoriteService.Save(final);
                RefreshFavorites();
                _leftTabs.SelectedIndex = 1;
                _statusLabel.Text = string.Format("导入 {0} 个接口", newItems.Count);
            }
        }

        // ═══════════════════════════════════════ ENV ══

        private void OnChangeDataPath()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择数据存储目录（历史、收藏、环境变量）";
                dlg.SelectedPath = SettingsService.DataPath;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var newPath = dlg.SelectedPath;
                    // copy existing data to new location
                    try
                    {
                        var oldPath = SettingsService.DataPath;
                        if (Directory.Exists(oldPath) && oldPath != newPath)
                        {
                            Directory.CreateDirectory(newPath);
                            foreach (var file in Directory.GetFiles(oldPath, "*.json"))
                            {
                                var dest = Path.Combine(newPath, Path.GetFileName(file));
                                if (!File.Exists(dest))
                                    File.Copy(file, dest);
                            }
                        }
                    }
                    catch { }
                    SettingsService.DataPath = newPath;
                    LoadData();
                    _statusLabel.Text = "数据目录已更新: " + newPath;
                    // update menu display
                    for (int i = 0; i < _menuStrip.Items.Count; i++)
                    {
                        if (_menuStrip.Items[i] is ToolStripMenuItem mi && mi.Text.StartsWith("存储:"))
                            mi.Text = "存储: " + newPath;
                    }
                }
            }
        }

        private void OnEnvChanged(object sender, EventArgs e) { if (_envCombo.SelectedIndex >= 0) { _envService.ActiveIndex = _envCombo.SelectedIndex; _statusLabel.Text = "环境: " + _envService.ActiveName; } }
        private void OnEnvManage() { using (var dlg = new EnvironmentDialog(_envService.All)) { if (dlg.ShowDialog() == DialogResult.OK) { _envService.All.Clear(); _envService.All.AddRange(dlg.Environments); _envService.Save(); RefreshEnvCombo(); } } }

        // ═══════════════════════════════════════ AUTH ══

        private void OnBasicAuth()
        {
            var f = new Form
            {
                Text = "Basic Auth 设置", Size = new Size(320, 190),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false,
                Font = new Font("Segoe UI", 9f), BackColor = Color.White
            };

            f.Controls.Add(new Label { Text = "用户名:", Location = new Point(16, 16), Size = new Size(60, 22), ForeColor = C_TEXT });
            var tbUser = new TextBox { Location = new Point(80, 16), Size = new Size(210, 24), BorderStyle = BorderStyle.FixedSingle };

            f.Controls.Add(new Label { Text = "密码:", Location = new Point(16, 48), Size = new Size(60, 22), ForeColor = C_TEXT });
            var tbPass = new TextBox { Location = new Point(80, 48), Size = new Size(210, 24), BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = true };

            var btnOk = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(130, 90), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat, BackColor = C_PRIMARY, ForeColor = Color.White };
            btnOk.FlatAppearance.BorderSize = 0;
            var btnClr = new Button { Text = "清除", Location = new Point(210, 90), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB), ForeColor = C_TEXT };
            btnClr.FlatAppearance.BorderSize = 0;
            btnClr.Click += (s, ev) => {
                // remove Authorization header
                var lines = _headersTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
                var filtered = lines.Where(l => !l.TrimStart().StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)).ToArray();
                _headersTextBox.Text = string.Join("\r\n", filtered);
                f.Close();
            };

            f.Controls.Add(tbUser); f.Controls.Add(tbPass); f.Controls.Add(btnOk); f.Controls.Add(btnClr);
            f.AcceptButton = btnOk;

            if (f.ShowDialog() == DialogResult.OK)
            {
                var user = tbUser.Text.Trim(); var pass = tbPass.Text;
                if (!string.IsNullOrEmpty(user))
                {
                    var cred = Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + pass));
                    var authHeader = "Authorization: Basic " + cred;
                    // replace existing Authorization or add new
                    var lines = _headersTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.None).ToList();
                    int authIdx = -1;
                    for (int i = 0; i < lines.Count; i++)
                        if (lines[i].TrimStart().StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase)) { authIdx = i; break; }
                    if (authIdx >= 0) lines[authIdx] = authHeader;
                    else lines.Add(authHeader);
                    _headersTextBox.Text = string.Join("\r\n", lines.Where(l => l != "" || lines.IndexOf(l) == lines.Count - 1 || lines[lines.IndexOf(l) + 1] != ""));
                    _statusLabel.Text = "Basic Auth 已设置";
                }
            }
        }

        // ═══════════════════════════════════════ UTIL ══

        private static string ShowInputDialog(string title, string prompt, string def)
        {
            var f = new Form { Text = title, Size = new Size(400, 150), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, Font = new Font("Segoe UI", 9f), BackColor = Color.White };
            f.Controls.Add(new Label { Text = prompt, Location = new Point(12, 12), Size = new Size(360, 20), ForeColor = Color.FromArgb(0x1F, 0x29, 0x37) });
            var tb = new TextBox { Text = def, Location = new Point(12, 38), Size = new Size(360, 24), BorderStyle = BorderStyle.FixedSingle };
            var bok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(210, 72), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat, BackColor = C_PRIMARY, ForeColor = Color.White };
            bok.FlatAppearance.BorderSize = 0;
            var bc = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(295, 72), Size = new Size(75, 26), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB), ForeColor = Color.FromArgb(0x1F, 0x29, 0x37) };
            bc.FlatAppearance.BorderSize = 0;
            f.Controls.Add(tb); f.Controls.Add(bok); f.Controls.Add(bc);
            f.AcceptButton = bok; f.CancelButton = bc;
            return f.ShowDialog() == DialogResult.OK ? tb.Text.Trim() : null;
        }
        private static string FormatSize(long b) { if (b < 1024) return b + "B"; if (b < 1024L * 1024) return (b / 1024.0).ToString("F1") + "KB"; return (b / (1024.0 * 1024.0)).ToString("F1") + "MB"; }

        private static string GetExtFromContentType(string ct)
        {
            if (string.IsNullOrEmpty(ct)) return ".bin";
            // strip charset
            var t = ct.Split(';')[0].Trim().ToLower();
            switch (t)
            {
                case "application/pdf": return ".pdf";
                case "application/zip": case "application/x-zip-compressed": return ".zip";
                case "application/gzip": case "application/x-gzip": return ".gz";
                case "application/msword": return ".doc";
                case "application/vnd.openxmlformats-officedocument.wordprocessingml.document": return ".docx";
                case "application/vnd.ms-excel": return ".xls";
                case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": return ".xlsx";
                case "application/vnd.ms-powerpoint": return ".ppt";
                case "application/vnd.openxmlformats-officedocument.presentationml.presentation": return ".pptx";
                case "application/json": return ".json";
                case "application/xml": case "text/xml": return ".xml";
                case "application/octet-stream": return ".bin";
                case "image/jpeg": return ".jpg";
                case "image/png": return ".png";
                case "image/gif": return ".gif";
                case "image/bmp": return ".bmp";
                case "image/webp": return ".webp";
                case "image/svg+xml": return ".svg";
                case "image/tiff": return ".tiff";
                case "image/x-icon": return ".ico";
                case "audio/mpeg": return ".mp3";
                case "audio/wav": case "audio/x-wav": return ".wav";
                case "audio/ogg": return ".ogg";
                case "video/mp4": return ".mp4";
                case "video/mpeg": return ".mpeg";
                case "video/webm": return ".webm";
                case "text/csv": return ".csv";
                case "text/html": return ".html";
                case "text/plain": return ".txt";
                case "text/javascript": return ".js";
                case "text/css": return ".css";
                default:
                    // try to extract extension from mime subtype
                    var parts = t.Split('/');
                    if (parts.Length == 2 && parts[1].Length > 0 && parts[1].Length <= 10)
                        return "." + parts[1];
                    return ".bin";
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "download";
            // remove invalid chars
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (invalid.Contains(chars[i])) chars[i] = '_';
            var result = new string(chars);
            // truncate to 100 chars max
            if (result.Length > 100) result = result.Substring(0, 100);
            return string.IsNullOrWhiteSpace(result) ? "download" : result;
        }

        private void SetAppIcon()
        {
            try
            {
                using (var bmp = new Bitmap(32, 32))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    // background circle
                    using (var br = new SolidBrush(C_PRIMARY))
                        g.FillEllipse(br, 1, 1, 30, 30);
                    // "API" text
                    using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
                    using (var br = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("API", f, br, new RectangleF(0, 0, 32, 32), sf);
                    }
                    Icon = Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════ FORM LAYOUT ══

        protected override void OnLoad(EventArgs e) { base.OnLoad(e); Shown += OnFirstShown; }
        private void OnFirstShown(object s, EventArgs e) { Shown -= OnFirstShown; LayoutPanels(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutPanels(); }
        private void LayoutPanels()
        {
            if (_splitContainer == null || !IsHandleCreated) return;
            int mb = _menuStrip != null ? _menuStrip.Bottom : 0;
            int tb = (_toolStrip != null && _toolStrip.Visible) ? _toolStrip.Bottom : mb;
            int sh = (_statusStrip != null && _statusStrip.Visible) ? _statusStrip.Height : 0;
            int top = Math.Max(tb, mb);
            int ah = ClientSize.Height - top - sh; if (ah < 100) ah = ClientSize.Height - top;
            _splitContainer.Location = new Point(0, top);
            _splitContainer.Size = new Size(ClientSize.Width, ah);
            // default left panel = 280px
            if (_splitContainer.SplitterDistance < 100) _splitContainer.SplitterDistance = 280;
            LayoutRightPanel();
        }
    }
}
