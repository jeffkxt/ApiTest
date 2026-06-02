using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using UltraLightApiTester.Models;
using UltraLightApiTester.Services;

namespace UltraLightApiTester.UI
{
    public class SwaggerImportDialog : Form
    {
        private CheckedListBox _listBox;
        private CheckBox _selectAllCb;
        private TextBox _groupNameTb;
        private Button _btnImport;
        private Button _btnCancel;
        private Label _infoLabel;
        private ComboBox _mergeCombo;
        private List<SwaggerEndpoint> _endpoints;
        private string _sourceUrl;

        public List<SavedRequest> SelectedRequests { get; private set; } = new List<SavedRequest>();
        public string GroupName => _groupNameTb.Text.Trim();
        public int MergeMode => _mergeCombo.SelectedIndex; // 0=覆盖全部, 1=智能合并, 2=跳过已有

        public SwaggerImportDialog(List<SwaggerEndpoint> endpoints, string sourceUrl = "", string title = "")
        {
            _endpoints = endpoints;
            _sourceUrl = sourceUrl;
            SelectedRequests = new List<SavedRequest>();
            InitializeUI();
            if (!string.IsNullOrWhiteSpace(title))
                _groupNameTb.Text = title;
            PopulateList();
        }

        private void InitializeUI()
        {
            Text = "从 Swagger 导入";
            Size = new Size(650, 530);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _infoLabel = new Label
            {
                Text = "选择要导入的接口:",
                Location = new Point(12, 12),
                Size = new Size(610, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x1F, 0x29, 0x37)
            };

            _selectAllCb = new CheckBox
            {
                Text = "全选",
                Location = new Point(12, 36),
                Size = new Size(60, 22),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _selectAllCb.CheckedChanged += SelectAll_Changed;

            _listBox = new CheckedListBox
            {
                Location = new Point(12, 62),
                Size = new Size(610, 320),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(0x1F, 0x29, 0x37),
                Font = new Font("Segoe UI", 9f),
                CheckOnClick = true
            };

            // group name
            var groupLabel = new Label
            {
                Text = "导入到分组:",
                Location = new Point(12, 390),
                Size = new Size(80, 24),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(0x6B, 0x72, 0x80)
            };

            _groupNameTb = new TextBox
            {
                Text = "",
                Location = new Point(96, 390),
                Size = new Size(150, 24),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f)
            };

            // merge mode
            var mergeLabel = new Label
            {
                Text = "重复处理:",
                Location = new Point(260, 390),
                Size = new Size(70, 24),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(0x6B, 0x72, 0x80)
            };

            _mergeCombo = new ComboBox
            {
                Location = new Point(335, 390),
                Size = new Size(160, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };
            _mergeCombo.Items.AddRange(new[] { "覆盖全部", "智能合并（保留修改）", "跳过已有" });
            _mergeCombo.SelectedIndex = 1; // default: smart merge

            _btnImport = new Button
            {
                Text = "导入选中",
                Location = new Point(450, 450),
                Size = new Size(85, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x4F, 0x46, 0xE5),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _btnImport.FlatAppearance.BorderSize = 0;
            _btnImport.Click += BtnImport_Click;

            _btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(540, 450),
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB),
                ForeColor = Color.FromArgb(0x1F, 0x29, 0x37),
                Font = new Font("Segoe UI", 9f)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (_, __) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] {
                _infoLabel, _selectAllCb, _listBox,
                groupLabel, _groupNameTb, mergeLabel, _mergeCombo,
                _btnImport, _btnCancel
            });
        }

        private void PopulateList()
        {
            _listBox.Items.Clear();
            foreach (var ep in _endpoints)
            {
                var label = ep.Method.ToUpper().PadRight(7) + ep.Path;
                if (!string.IsNullOrEmpty(ep.Summary)) label += "  — " + ep.Summary;
                _listBox.Items.Add(label, true);
            }
            _infoLabel.Text = string.Format("选择要导入的接口 (共 {0} 个):", _endpoints.Count);
        }

        private void SelectAll_Changed(object sender, EventArgs e)
        {
            for (int i = 0; i < _listBox.Items.Count; i++)
                _listBox.SetItemChecked(i, _selectAllCb.Checked);
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            SelectedRequests.Clear();
            for (int i = 0; i < _listBox.Items.Count; i++)
            {
                if (_listBox.GetItemChecked(i))
                {
                    var sr = SwaggerParser.ConvertToSavedRequest(_endpoints[i], _sourceUrl);
                    sr.Group = GroupName;
                    SelectedRequests.Add(sr);
                }
            }
            if (SelectedRequests.Count == 0)
            {
                MessageBox.Show("请至少选择一个接口。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
