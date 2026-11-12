using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Lab4Library.Processes;

namespace ProcessManagerApp
{
    public class MainForm : Form
    {
        private readonly ProcessManager manager = new ProcessManager();
        private readonly DataTable table = new DataTable("Processes");
        private readonly Timer autoRefreshTimer = new Timer();
        private bool refreshing;

        private DataGridView grid;
        private TextBox txtFilter;
        private Button btnRefresh;
        private CheckBox chkAutoRefresh;
        private Button btnKill;
        private ComboBox cmbPriority;
        private Button btnSetPriority;
        private Label lblStatus;
        private ContextMenuStrip contextMenu;

        public MainForm()
        {
            CreateTable();
            BuildLayout();

            autoRefreshTimer.Interval = 3000;
            autoRefreshTimer.Tick += async (s, e) => await RefreshProcessesAsync();

            Shown += async (s, e) => await RefreshProcessesAsync();
        }

        private void CreateTable()
        {
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Memory", typeof(double));
            table.Columns.Add("StartTime", typeof(DateTime));
            table.Columns.Add("Priority", typeof(string));
            table.Columns.Add("Threads", typeof(int));
            table.PrimaryKey = new[] { table.Columns["Id"] };
            table.DefaultView.Sort = "Name ASC";
        }

        private void BuildLayout()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Менеджер процесів";
            ClientSize = new Size(960, 640);
            MinimumSize = new Size(820, 520);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5F);

            var groupLaunch = new GroupBox
            {
                Text = "Запуск програм",
                Location = new Point(12, 8),
                Size = new Size(936, 64),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(groupLaunch);

            int x = 12;
            foreach (ProgramDefinition program in ProcessManager.DefaultPrograms)
            {
                ProgramDefinition current = program;
                var button = new Button { Text = program.DisplayName, Location = new Point(x, 24), Size = new Size(140, 30) };
                button.Click += async (s, e) => await LaunchAsync(current);
                groupLaunch.Controls.Add(button);
                x += 148;
            }

            var btnOther = new Button { Text = "Інша програма…", Location = new Point(x, 24), Size = new Size(150, 30) };
            btnOther.Click += BtnOther_Click;
            groupLaunch.Controls.Add(btnOther);

            var lblFilter = new Label { Text = "Пошук:", Location = new Point(12, 86), Size = new Size(60, 24), TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(lblFilter);

            txtFilter = new TextBox { Location = new Point(74, 86), Size = new Size(260, 26) };
            txtFilter.TextChanged += (s, e) => ApplyFilter();
            Controls.Add(txtFilter);

            btnRefresh = new Button { Text = "Оновити", Location = new Point(346, 84), Size = new Size(110, 30) };
            btnRefresh.Click += async (s, e) => await RefreshProcessesAsync();
            Controls.Add(btnRefresh);

            chkAutoRefresh = new CheckBox { Text = "Автооновлення (3 с)", Location = new Point(470, 88), Size = new Size(180, 24) };
            chkAutoRefresh.CheckedChanged += (s, e) => autoRefreshTimer.Enabled = chkAutoRefresh.Checked;
            Controls.Add(chkAutoRefresh);

            grid = new DataGridView
            {
                Location = new Point(12, 122),
                Size = new Size(936, 446),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(CreateColumn("Id", "PID", 70, DataGridViewContentAlignment.MiddleRight, null));
            grid.Columns.Add(CreateColumn("Name", "Назва процесу", 200, DataGridViewContentAlignment.MiddleLeft, null));
            grid.Columns.Add(CreateColumn("Memory", "Пам'ять, МБ", 100, DataGridViewContentAlignment.MiddleRight, "N1"));
            grid.Columns.Add(CreateColumn("StartTime", "Час запуску", 150, DataGridViewContentAlignment.MiddleCenter, "dd.MM.yyyy HH:mm:ss"));
            grid.Columns.Add(CreateColumn("Priority", "Пріоритет", 130, DataGridViewContentAlignment.MiddleLeft, null));
            grid.Columns.Add(CreateColumn("Threads", "Потоків", 80, DataGridViewContentAlignment.MiddleRight, null));
            grid.DataSource = table.DefaultView;
            grid.SelectionChanged += (s, e) => UpdateSelection();
            grid.CellMouseDown += Grid_CellMouseDown;
            Controls.Add(grid);

            btnKill = new Button
            {
                Text = "Завершити процес",
                Location = new Point(12, 580),
                Size = new Size(170, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = Color.FromArgb(198, 40, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnKill.Click += async (s, e) => await KillSelectedAsync();
            Controls.Add(btnKill);

            var lblPriority = new Label
            {
                Text = "Пріоритет:",
                Location = new Point(200, 585),
                Size = new Size(80, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(lblPriority);

            cmbPriority = new ComboBox
            {
                Location = new Point(282, 585),
                Size = new Size(180, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            foreach (ProcessPriorityClass priority in ProcessManager.Priorities)
                cmbPriority.Items.Add(new PriorityItem(priority));
            Controls.Add(cmbPriority);

            btnSetPriority = new Button
            {
                Text = "Змінити пріоритет",
                Location = new Point(470, 580),
                Size = new Size(170, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnSetPriority.Click += async (s, e) =>
            {
                var item = cmbPriority.SelectedItem as PriorityItem;
                if (item != null) await SetSelectedPriorityAsync(item.Priority);
            };
            Controls.Add(btnSetPriority);

            lblStatus = new Label
            {
                Location = new Point(652, 580),
                Size = new Size(296, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.DimGray
            };
            Controls.Add(lblStatus);

            BuildContextMenu();

            ResumeLayout(false);
            PerformLayout();
        }

        private static DataGridViewTextBoxColumn CreateColumn(string property, string header, int weight,
                                                              DataGridViewContentAlignment alignment, string format)
        {
            var column = new DataGridViewTextBoxColumn
            {
                DataPropertyName = property,
                Name = property,
                HeaderText = header,
                FillWeight = weight,
                SortMode = DataGridViewColumnSortMode.Automatic
            };
            column.DefaultCellStyle.Alignment = alignment;
            if (format != null)
                column.DefaultCellStyle.Format = format;
            return column;
        }

        private void BuildContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            var killItem = new ToolStripMenuItem("Завершити процес");
            killItem.Click += async (s, e) => await KillSelectedAsync();
            contextMenu.Items.Add(killItem);

            var priorityMenu = new ToolStripMenuItem("Пріоритет");
            foreach (ProcessPriorityClass priority in ProcessManager.Priorities)
            {
                ProcessPriorityClass current = priority;
                var item = new ToolStripMenuItem(ProcessManager.GetPriorityName(priority));
                item.Click += async (s, e) => await SetSelectedPriorityAsync(current);
                priorityMenu.DropDownItems.Add(item);
            }
            contextMenu.Items.Add(priorityMenu);

            contextMenu.Items.Add(new ToolStripSeparator());
            var refreshItem = new ToolStripMenuItem("Оновити список");
            refreshItem.Click += async (s, e) => await RefreshProcessesAsync();
            contextMenu.Items.Add(refreshItem);

            grid.ContextMenuStrip = contextMenu;
        }

        private void Grid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
        }

        private async Task RefreshProcessesAsync()
        {
            if (refreshing)
                return;
            refreshing = true;
            btnRefresh.Enabled = false;

            try
            {
                List<ProcessInfo> processes = await Task.Run(() => manager.GetProcesses());
                if (IsDisposed)
                    return;
                FillTable(processes);
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Не вдалося отримати список процесів: " + ex.Message;
            }
            finally
            {
                refreshing = false;
                if (!IsDisposed)
                    btnRefresh.Enabled = true;
            }
        }

        private void FillTable(List<ProcessInfo> processes)
        {
            int? selectedId = SelectedProcessId();
            int firstRow = grid.FirstDisplayedScrollingRowIndex;

            table.BeginLoadData();
            table.Clear();
            foreach (ProcessInfo info in processes)
            {
                table.Rows.Add(
                    info.Id,
                    info.Name,
                    Math.Round(info.MemoryMegabytes, 1),
                    info.StartTime.HasValue ? (object)info.StartTime.Value : DBNull.Value,
                    info.PriorityName,
                    info.ThreadCount);
            }
            table.EndLoadData();

            if (firstRow >= 0 && firstRow < grid.Rows.Count)
                grid.FirstDisplayedScrollingRowIndex = firstRow;
            SelectProcess(selectedId);
            UpdateSelection();
        }

        private void ApplyFilter()
        {
            string text = txtFilter.Text.Trim().Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
            table.DefaultView.RowFilter = text.Length == 0 ? string.Empty : "Name LIKE '%" + text + "%'";
            UpdateSelection();
        }

        private void SelectProcess(int? id)
        {
            grid.ClearSelection();
            if (!id.HasValue)
                return;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if ((int)row.Cells["Id"].Value == id.Value)
                {
                    row.Selected = true;
                    grid.CurrentCell = row.Cells["Name"];
                    return;
                }
            }
        }

        private int? SelectedProcessId()
        {
            if (grid.SelectedRows.Count == 0)
                return null;
            object value = grid.SelectedRows[0].Cells["Id"].Value;
            return value is int ? (int?)value : null;
        }

        private string SelectedProcessName()
        {
            return grid.SelectedRows.Count == 0 ? string.Empty : Convert.ToString(grid.SelectedRows[0].Cells["Name"].Value);
        }

        private void UpdateSelection()
        {
            int? id = SelectedProcessId();
            btnKill.Enabled = id.HasValue;
            btnSetPriority.Enabled = id.HasValue;
            cmbPriority.Enabled = id.HasValue;

            string count = "Процесів: " + table.DefaultView.Count + (table.DefaultView.Count != table.Rows.Count ? " з " + table.Rows.Count : string.Empty);
            if (!id.HasValue)
            {
                lblStatus.Text = count;
                return;
            }

            lblStatus.Text = count + "   |   Обрано: " + SelectedProcessName() + " (PID " + id.Value + ")";
            string priorityName = Convert.ToString(grid.SelectedRows[0].Cells["Priority"].Value);
            foreach (PriorityItem item in cmbPriority.Items)
            {
                if (item.ToString() == priorityName)
                {
                    cmbPriority.SelectedItem = item;
                    return;
                }
            }
            cmbPriority.SelectedIndex = -1;
        }

        private async Task LaunchAsync(ProgramDefinition program)
        {
            try
            {
                manager.Start(program);
                await Task.Delay(700);
                await RefreshProcessesAsync();
            }
            catch (ProcessManagerException ex)
            {
                ShowError(ex.Message);
            }
        }

        private async void BtnOther_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Оберіть програму для запуску";
                dialog.Filter = "Програми (*.exe)|*.exe|Усі файли (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                string name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
                await LaunchAsync(new ProgramDefinition(name, dialog.FileName));
            }
        }

        private async Task KillSelectedAsync()
        {
            int? id = SelectedProcessId();
            if (!id.HasValue)
                return;

            string name = SelectedProcessName();
            DialogResult answer = MessageBox.Show(this,
                "Завершити процес \"" + name + "\" (PID " + id.Value + ")?\nНезбережені дані в цій програмі буде втрачено.",
                "Завершення процесу", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
                return;

            try
            {
                manager.Kill(id.Value);
                lblStatus.Text = "Процес \"" + name + "\" завершено.";
            }
            catch (ProcessManagerException ex)
            {
                ShowError(ex.Message);
            }
            await RefreshProcessesAsync();
        }

        private async Task SetSelectedPriorityAsync(ProcessPriorityClass priority)
        {
            int? id = SelectedProcessId();
            if (!id.HasValue)
                return;

            if (priority == ProcessPriorityClass.RealTime)
            {
                DialogResult answer = MessageBox.Show(this,
                    "Пріоритет реального часу може зробити систему нестабільною і потребує прав адміністратора. Продовжити?",
                    "Зміна пріоритету", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes)
                    return;
            }

            string name = SelectedProcessName();
            try
            {
                manager.SetPriority(id.Value, priority);
                ProcessInfo updated = manager.GetProcess(id.Value);
                lblStatus.Text = "Пріоритет процесу \"" + name + "\": " + updated.PriorityName;
            }
            catch (ProcessManagerException ex)
            {
                ShowError(ex.Message);
            }
            await RefreshProcessesAsync();
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Менеджер процесів", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            autoRefreshTimer.Stop();
            autoRefreshTimer.Dispose();
            base.OnFormClosed(e);
        }

        private sealed class PriorityItem
        {
            public PriorityItem(ProcessPriorityClass priority)
            {
                Priority = priority;
            }

            public ProcessPriorityClass Priority { get; private set; }

            public override string ToString()
            {
                return ProcessManager.GetPriorityName(Priority);
            }
        }
    }
}
