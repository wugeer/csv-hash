using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CsvHash.WinForms
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox _filePathTextBox;
        private readonly Button _browseButton;
        private readonly ComboBox _delimiterComboBox;
        private readonly TextBox _customDelimiterTextBox;
        private readonly CheckedListBox _fieldsCheckedListBox;
        private readonly Button _selectAllButton;
        private readonly Button _clearButton;
        private readonly Button _encryptButton;
        private readonly Label _statusLabel;

        public MainForm()
        {
            Text = "CSV Hash";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(720, 520);
            Size = new Size(820, 580);
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            var titleLabel = new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
                Text = "CSV 字段加密工具"
            };
            root.Controls.Add(titleLabel, 0, 0);

            var filePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(0, 14, 0, 10)
            };
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(filePanel, 0, 1);

            _filePathTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            filePanel.Controls.Add(_filePathTextBox, 0, 0);

            _browseButton = new Button
            {
                AutoSize = true,
                Margin = new Padding(8, 0, 0, 0),
                Text = "选择 CSV"
            };
            _browseButton.Click += BrowseButton_Click;
            filePanel.Controls.Add(_browseButton, 1, 0);

            var middlePanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            middlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            middlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            middlePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(middlePanel, 0, 2);

            var delimiterPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            middlePanel.Controls.Add(delimiterPanel, 0, 0);

            delimiterPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Margin = new Padding(0, 6, 8, 0),
                Text = "分隔符"
            });

            _delimiterComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120
            };
            _delimiterComboBox.Items.AddRange(new object[] { "逗号 (,)", "分号 (;)", "竖线 (|)", "制表符 (Tab)", "自定义" });
            _delimiterComboBox.SelectedIndex = 0;
            _delimiterComboBox.SelectedIndexChanged += DelimiterChanged;
            delimiterPanel.Controls.Add(_delimiterComboBox);

            _customDelimiterTextBox = new TextBox
            {
                Enabled = false,
                MaxLength = 2,
                Width = 60,
                Margin = new Padding(8, 0, 0, 0)
            };
            _customDelimiterTextBox.Leave += DelimiterChanged;
            delimiterPanel.Controls.Add(_customDelimiterTextBox);

            var fieldsHeaderPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 16, 0, 6)
            };
            middlePanel.Controls.Add(fieldsHeaderPanel, 0, 1);

            fieldsHeaderPanel.Controls.Add(new Label
            {
                AutoSize = true,
                Font = new Font(Font.FontFamily, 10F, FontStyle.Bold),
                Margin = new Padding(0, 6, 12, 0),
                Text = "加密字段"
            });

            _selectAllButton = new Button
            {
                AutoSize = true,
                Enabled = false,
                Text = "全选"
            };
            _selectAllButton.Click += SelectAllButton_Click;
            fieldsHeaderPanel.Controls.Add(_selectAllButton);

            _clearButton = new Button
            {
                AutoSize = true,
                Enabled = false,
                Text = "清空"
            };
            _clearButton.Click += ClearButton_Click;
            fieldsHeaderPanel.Controls.Add(_clearButton);

            _fieldsCheckedListBox = new CheckedListBox
            {
                CheckOnClick = true,
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            _fieldsCheckedListBox.ItemCheck += FieldsCheckedListBox_ItemCheck;
            middlePanel.Controls.Add(_fieldsCheckedListBox, 0, 2);

            var actionPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 14, 0, 0)
            };
            root.Controls.Add(actionPanel, 0, 3);

            _encryptButton = new Button
            {
                AutoSize = true,
                Enabled = false,
                Text = "生成加密 CSV"
            };
            _encryptButton.Click += EncryptButton_Click;
            actionPanel.Controls.Add(_encryptButton);

            _statusLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                ForeColor = Color.DimGray,
                Padding = new Padding(0, 10, 0, 0)
            };
            root.Controls.Add(_statusLabel, 0, 4);
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "CSV 文件 (*.csv;*.txt)|*.csv;*.txt|所有文件 (*.*)|*.*";
                dialog.Title = "选择 CSV 文件";

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _filePathTextBox.Text = dialog.FileName;
                LoadHeaders();
            }
        }

        private void DelimiterChanged(object sender, EventArgs e)
        {
            _customDelimiterTextBox.Enabled = _delimiterComboBox.SelectedIndex == 4;

            if (!string.IsNullOrEmpty(_filePathTextBox.Text))
            {
                LoadHeaders();
            }
        }

        private void SelectAllButton_Click(object sender, EventArgs e)
        {
            for (var i = 0; i < _fieldsCheckedListBox.Items.Count; i++)
            {
                _fieldsCheckedListBox.SetItemChecked(i, true);
            }

            UpdateEncryptButton();
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            for (var i = 0; i < _fieldsCheckedListBox.Items.Count; i++)
            {
                _fieldsCheckedListBox.SetItemChecked(i, false);
            }

            UpdateEncryptButton();
        }

        private void FieldsCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            BeginInvoke(new Action(UpdateEncryptButton));
        }

        private void EncryptButton_Click(object sender, EventArgs e)
        {
            try
            {
                var fields = _fieldsCheckedListBox.CheckedItems.Cast<string>().ToArray();
                var outputPath = CsvProcessor.EncryptFile(_filePathTextBox.Text, fields, GetDelimiter());
                SetStatus("已生成：" + outputPath, false);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private void LoadHeaders()
        {
            try
            {
                var headers = CsvProcessor.ReadHeaders(_filePathTextBox.Text, GetDelimiter());
                _fieldsCheckedListBox.Items.Clear();
                _fieldsCheckedListBox.Items.AddRange(headers.Cast<object>().ToArray());

                _selectAllButton.Enabled = headers.Length > 0;
                _clearButton.Enabled = headers.Length > 0;
                UpdateEncryptButton();
                SetStatus("已读取 " + headers.Length + " 个字段。", false);
            }
            catch (Exception ex)
            {
                _fieldsCheckedListBox.Items.Clear();
                _selectAllButton.Enabled = false;
                _clearButton.Enabled = false;
                UpdateEncryptButton();
                SetStatus(ex.Message, true);
            }
        }

        private string GetDelimiter()
        {
            switch (_delimiterComboBox.SelectedIndex)
            {
                case 1:
                    return ";";
                case 2:
                    return "|";
                case 3:
                    return "\t";
                case 4:
                    return ParseCustomDelimiter(_customDelimiterTextBox.Text);
                default:
                    return ",";
            }
        }

        private static string ParseCustomDelimiter(string value)
        {
            if (value == "\\t")
            {
                return "\t";
            }

            return value;
        }

        private void UpdateEncryptButton()
        {
            _encryptButton.Enabled = !string.IsNullOrEmpty(_filePathTextBox.Text) && _fieldsCheckedListBox.CheckedItems.Count > 0;
        }

        private void SetStatus(string message, bool isError)
        {
            _statusLabel.Text = message;
            _statusLabel.ForeColor = isError ? Color.Firebrick : Color.DimGray;
        }
    }
}
