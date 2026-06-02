using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using UltraLightApiTester.Models;
using UltraLightApiTester.Services;

namespace UltraLightApiTester.UI
{
    public class EnvironmentDialog : Form
    {
        private ListBox _envListBox;
        private DataGridView _varGrid;
        private TextBox _envNameTb;
        private Button _btnAddEnv, _btnDeleteEnv, _btnImport, _btnSave, _btnClose, _btnDelRow;
        private List<EnvironmentConfig> _environments;
        private int _selectedEnvIndex = -1;
        private bool _dirty = false;

        public List<EnvironmentConfig> Environments
        {
            get => _environments;
            set
            {
                _environments = value;
                RefreshEnvList();
            }
        }

        public EnvironmentDialog(List<EnvironmentConfig> environments)
        {
            _environments = environments.Select(e => new EnvironmentConfig
            {
                Name = e.Name,
                Variables = new Dictionary<string, string>(e.Variables)
            }).ToList();
            InitializeUI();
            RefreshEnvList();
        }

        private void InitializeUI()
        {
            Text = "环境变量管理";
            Size = new Size(700, 480);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(0xF5, 0xF5, 0xF5);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // left: environment list
            var envLabel = new Label
            {
                Text = "环境列表",
                Location = new Point(12, 12),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x42, 0x42, 0x42)
            };

            _envListBox = new ListBox
            {
                Location = new Point(12, 36),
                Size = new Size(160, 310),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            _envListBox.SelectedIndexChanged += EnvList_SelectedIndexChanged;

            _envNameTb = new TextBox
            {
                Location = new Point(12, 352),
                Size = new Size(160, 24),
                BorderStyle = BorderStyle.FixedSingle,
                Text = ""
            };

            _btnAddEnv = new Button
            {
                Text = "+ 新增",
                Location = new Point(12, 382),
                Size = new Size(55, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x19, 0x76, 0xD2),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8f)
            };
            _btnAddEnv.FlatAppearance.BorderSize = 0;
            _btnAddEnv.Click += BtnAddEnv_Click;

            _btnImport = new Button
            {
                Text = "导入",
                Location = new Point(70, 382),
                Size = new Size(50, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE5, 0xE7, 0xEB),
                ForeColor = Color.FromArgb(0x1F, 0x29, 0x37),
                Font = new Font("Segoe UI", 8f)
            };
            _btnImport.FlatAppearance.BorderSize = 0;
            _btnImport.Click += BtnImportPostman_Click;

            _btnDeleteEnv = new Button
            {
                Text = "- 删除",
                Location = new Point(124, 382),
                Size = new Size(55, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xF4, 0x43, 0x36),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f)
            };
            _btnDeleteEnv.FlatAppearance.BorderSize = 0;
            _btnDeleteEnv.Click += BtnDeleteEnv_Click;

            // right: variable grid
            var varLabel = new Label
            {
                Text = "变量列表",
                Location = new Point(190, 12),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x42, 0x42, 0x42)
            };

            _varGrid = new DataGridView
            {
                Location = new Point(190, 36),
                Size = new Size(480, 310),
                BorderStyle = BorderStyle.FixedSingle,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 28,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(0xEE, 0xEE, 0xEE),
                    ForeColor = Color.FromArgb(0x42, 0x42, 0x42),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                }
            };
            _varGrid.Columns.Add("Key", "变量名");
            _varGrid.Columns.Add("Value", "变量值");
            _varGrid.Columns[0].FillWeight = 40;
            _varGrid.Columns[1].FillWeight = 60;
            _varGrid.CellValueChanged += (_, __) => _dirty = true;
            _varGrid.RowsRemoved += (_, __) => _dirty = true;
            _varGrid.UserAddedRow += (_, __) => _dirty = true;

            // buttons
            _btnDelRow = new Button
            {
                Text = "删除行",
                Location = new Point(190, 352),
                Size = new Size(60, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xF4, 0x43, 0x36),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8f)
            };
            _btnDelRow.FlatAppearance.BorderSize = 0;
            _btnDelRow.Click += (_, __) =>
            {
                if (_varGrid.CurrentRow != null && !_varGrid.CurrentRow.IsNewRow)
                {
                    _varGrid.Rows.Remove(_varGrid.CurrentRow);
                    _dirty = true;
                }
            };

            _btnSave = new Button
            {
                Text = "保存",
                Location = new Point(500, 382),
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0x19, 0x76, 0xD2),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;

            _btnClose = new Button
            {
                Text = "关闭",
                Location = new Point(590, 382),
                Size = new Size(80, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0xE0, 0xE0, 0xE0),
                ForeColor = Color.FromArgb(0x42, 0x42, 0x42),
                Font = new Font("Segoe UI", 9f)
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (_, __) => { DialogResult = DialogResult.OK; Close(); };

            Controls.AddRange(new Control[] {
                envLabel, _envListBox, _envNameTb, _btnAddEnv, _btnImport, _btnDeleteEnv,
                varLabel, _varGrid, _btnDelRow, _btnSave, _btnClose
            });
        }

        private void RefreshEnvList()
        {
            _envListBox.Items.Clear();
            foreach (var env in _environments)
                _envListBox.Items.Add(env.Name);
        }

        private void EnvList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_dirty)
            {
                SaveCurrentGridToEnv();
                _dirty = false;
            }

            _selectedEnvIndex = _envListBox.SelectedIndex;
            if (_selectedEnvIndex < 0) return;

            var env = _environments[_selectedEnvIndex];
            _envNameTb.Text = env.Name;

            _varGrid.Rows.Clear();
            foreach (var kv in env.Variables)
                _varGrid.Rows.Add(kv.Key, kv.Value);
        }

        private void SaveCurrentGridToEnv()
        {
            if (_selectedEnvIndex < 0 || _selectedEnvIndex >= _environments.Count) return;

            // update name
            var name = _envNameTb.Text.Trim();
            if (!string.IsNullOrEmpty(name)) _environments[_selectedEnvIndex].Name = name;

            // update variables
            var vars = new Dictionary<string, string>();
            foreach (DataGridViewRow row in _varGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var key = row.Cells[0].Value as string;
                var val = row.Cells[1].Value as string;
                if (!string.IsNullOrWhiteSpace(key))
                    vars[key.Trim()] = val ?? "";
            }
            _environments[_selectedEnvIndex].Variables = vars;
        }

        private void BtnAddEnv_Click(object sender, EventArgs e)
        {
            var name = _envNameTb.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = "新环境";

            // make unique
            int counter = 1;
            var baseName = name;
            while (_environments.Any(x => x.Name == name))
                name = baseName + " (" + (counter++) + ")";

            _environments.Add(new EnvironmentConfig { Name = name, Variables = new Dictionary<string, string>() });
            RefreshEnvList();
            _envListBox.SelectedIndex = _environments.Count - 1;
            _dirty = true;
        }

        private void BtnImportPostman_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog { Filter = "JSON (*.json)|*.json|All (*.*)|*.*", Title = "导入 Postman 环境 JSON" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var json = File.ReadAllText(dlg.FileName);
                    var jss = new JavaScriptSerializer();
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

                    if (vars.Count == 0) { MessageBox.Show("未找到有效变量。"); return; }

                    int cnt = 1; var baseName = envName;
                    while (_environments.Any(x => x.Name == envName))
                        envName = baseName + " (" + (cnt++) + ")";

                    _environments.Add(new EnvironmentConfig { Name = envName, Variables = vars });
                    RefreshEnvList();
                    _envListBox.SelectedIndex = _environments.Count - 1;
                    _dirty = true;
                    MessageBox.Show(string.Format("已导入 {0} ({1} 个变量)", envName, vars.Count), "导入成功");
                }
                catch (Exception ex) { MessageBox.Show("解析失败: " + ex.Message); }
            }
        }

        private void BtnDeleteEnv_Click(object sender, EventArgs e)
        {
            if (_selectedEnvIndex < 0 || _selectedEnvIndex >= _environments.Count)
            {
                MessageBox.Show("请先选择一个环境。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_environments.Count <= 1)
            {
                MessageBox.Show("至少保留一个环境。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var name = _environments[_selectedEnvIndex].Name;
            if (MessageBox.Show("确定删除环境 \"" + name + "\" 吗？", "确认删除",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
            {
                _environments.RemoveAt(_selectedEnvIndex);
                _selectedEnvIndex = -1;
                _varGrid.Rows.Clear();
                RefreshEnvList();
                if (_environments.Count > 0) _envListBox.SelectedIndex = 0;
                _dirty = true;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SaveCurrentGridToEnv();
            _dirty = false;
            MessageBox.Show("已保存。关闭对话框后生效。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
