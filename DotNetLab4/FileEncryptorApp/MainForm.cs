using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Lab4Library.Common;
using Lab4Library.Encryption;

namespace FileEncryptorApp
{
    public class MainForm : Form
    {
        private readonly FileCryptor cryptor = new FileCryptor();
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private CancellationTokenSource cancellation;
        private DateTime startTime;
        private CryptoOperation currentOperation;

        private TextBox txtFile;
        private Button btnBrowse;
        private Label lblFileInfo;
        private TextBox txtKey;
        private CheckBox chkShowKey;
        private Button btnEncrypt;
        private Button btnDecrypt;
        private Button btnCancel;
        private ProgressBar progressBar;
        private Label lblPercent;
        private Label lblElapsed;
        private Label lblStatus;

        public MainForm()
        {
            BuildLayout();

            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            timer.Interval = 100;
            timer.Tick += (s, e) => UpdateElapsed();

            SetBusy(false);
        }

        private void BuildLayout()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Шифрування файлів";
            ClientSize = new Size(640, 360);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5F);
            AllowDrop = true;
            DragEnter += Form_DragEnter;
            DragDrop += Form_DragDrop;

            var groupFile = new GroupBox { Text = "Файл", Location = new Point(12, 10), Size = new Size(616, 92) };
            Controls.Add(groupFile);

            txtFile = new TextBox { Location = new Point(12, 26), Size = new Size(480, 26) };
            txtFile.TextChanged += (s, e) => UpdateFileInfo();
            groupFile.Controls.Add(txtFile);

            btnBrowse = new Button { Text = "Огляд…", Location = new Point(500, 24), Size = new Size(104, 30) };
            btnBrowse.Click += BtnBrowse_Click;
            groupFile.Controls.Add(btnBrowse);

            lblFileInfo = new Label
            {
                Location = new Point(12, 60),
                Size = new Size(592, 22),
                ForeColor = Color.DimGray,
                Text = "Оберіть файл або перетягніть його у вікно"
            };
            groupFile.Controls.Add(lblFileInfo);

            var groupKey = new GroupBox { Text = "Ключ шифрування", Location = new Point(12, 110), Size = new Size(616, 64) };
            Controls.Add(groupKey);

            txtKey = new TextBox { Location = new Point(12, 26), Size = new Size(440, 26), UseSystemPasswordChar = true };
            groupKey.Controls.Add(txtKey);

            chkShowKey = new CheckBox { Text = "Показати ключ", Location = new Point(468, 27), Size = new Size(140, 24) };
            chkShowKey.CheckedChanged += (s, e) => txtKey.UseSystemPasswordChar = !chkShowKey.Checked;
            groupKey.Controls.Add(chkShowKey);

            btnEncrypt = new Button
            {
                Text = "Зашифрувати",
                Location = new Point(12, 186),
                Size = new Size(200, 38),
                BackColor = Color.FromArgb(46, 125, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnEncrypt.Click += (s, e) => StartOperation(CryptoOperation.Encrypt);
            Controls.Add(btnEncrypt);

            btnDecrypt = new Button
            {
                Text = "Розшифрувати",
                Location = new Point(220, 186),
                Size = new Size(200, 38),
                BackColor = Color.FromArgb(21, 101, 192),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnDecrypt.Click += (s, e) => StartOperation(CryptoOperation.Decrypt);
            Controls.Add(btnDecrypt);

            btnCancel = new Button { Text = "Скасувати", Location = new Point(428, 186), Size = new Size(200, 38) };
            btnCancel.Click += BtnCancel_Click;
            Controls.Add(btnCancel);

            progressBar = new ProgressBar { Location = new Point(12, 240), Size = new Size(540, 26), Minimum = 0, Maximum = 100 };
            Controls.Add(progressBar);

            lblPercent = new Label
            {
                Location = new Point(560, 240),
                Size = new Size(68, 26),
                Text = "0 %",
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            Controls.Add(lblPercent);

            lblElapsed = new Label
            {
                Location = new Point(12, 274),
                Size = new Size(616, 24),
                Text = "Минуло часу: 00:00:00.0",
                Font = new Font("Consolas", 10F)
            };
            Controls.Add(lblElapsed);

            lblStatus = new Label
            {
                Location = new Point(12, 304),
                Size = new Size(616, 44),
                ForeColor = Color.DimGray,
                Text = "Готово до роботи."
            };
            Controls.Add(lblStatus);

            ResumeLayout(false);
            PerformLayout();
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Оберіть файл для шифрування або розшифрування";
                dialog.Filter = "Усі файли (*.*)|*.*|Зашифровані файли (*.enc)|*.enc";
                if (File.Exists(txtFile.Text))
                    dialog.InitialDirectory = Path.GetDirectoryName(txtFile.Text);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    txtFile.Text = dialog.FileName;
            }
        }

        private void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (!worker.IsBusy && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void Form_DragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0 && File.Exists(files[0]))
                txtFile.Text = files[0];
        }

        private void UpdateFileInfo()
        {
            string path = txtFile.Text.Trim();
            if (!File.Exists(path))
            {
                lblFileInfo.Text = path.Length == 0 ? "Оберіть файл або перетягніть його у вікно" : "Файл не знайдено";
                return;
            }

            try
            {
                var info = new FileInfo(path);
                lblFileInfo.Text = "Розмір: " + SizeFormatter.FormatWithBytes(info.Length) +
                                   (FileCryptor.IsEncryptedFile(path) ? "   •   файл зашифровано цією програмою" : string.Empty);
            }
            catch (Exception ex)
            {
                lblFileInfo.Text = ex.Message;
            }
        }

        private void StartOperation(CryptoOperation operation)
        {
            if (worker.IsBusy)
                return;

            string inputPath = txtFile.Text.Trim();
            if (!File.Exists(inputPath))
            {
                ShowWarning("Оберіть файл, який існує.");
                return;
            }
            if (txtKey.Text.Length == 0)
            {
                ShowWarning("Введіть ключ шифрування.");
                txtKey.Focus();
                return;
            }
            if (operation == CryptoOperation.Decrypt && !FileCryptor.IsEncryptedFile(inputPath))
            {
                ShowWarning("Обраний файл не зашифровано цією програмою, тому розшифрувати його неможливо.");
                return;
            }

            string outputPath = AskOutputPath(inputPath, operation);
            if (outputPath == null)
                return;

            currentOperation = operation;
            cancellation = new CancellationTokenSource();
            progressBar.Value = 0;
            lblPercent.Text = "0 %";
            lblStatus.ForeColor = Color.DimGray;
            lblStatus.Text = (operation == CryptoOperation.Encrypt ? "Шифрування" : "Розшифрування") +
                             " файлу " + Path.GetFileName(inputPath) + "…";

            startTime = DateTime.Now;
            UpdateElapsed();
            timer.Start();
            SetBusy(true);

            worker.RunWorkerAsync(new CryptoJob(operation, inputPath, outputPath, txtKey.Text, cancellation.Token));
        }

        private string AskOutputPath(string inputPath, CryptoOperation operation)
        {
            string suggested = FileCryptor.SuggestOutputPath(inputPath, operation);
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = operation == CryptoOperation.Encrypt ? "Зберегти зашифрований файл" : "Зберегти розшифрований файл";
                dialog.Filter = operation == CryptoOperation.Encrypt
                    ? "Зашифровані файли (*.enc)|*.enc|Усі файли (*.*)|*.*"
                    : "Усі файли (*.*)|*.*";
                dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(suggested));
                dialog.FileName = Path.GetFileName(suggested);
                dialog.OverwritePrompt = true;
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (!worker.IsBusy)
                return;
            btnCancel.Enabled = false;
            lblStatus.Text = "Скасування…";
            worker.CancelAsync();
            cancellation.Cancel();
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var job = (CryptoJob)e.Argument;
            var progress = new CallbackProgress<int>(percent => worker.ReportProgress(percent));

            try
            {
                e.Result = job.Operation == CryptoOperation.Encrypt
                    ? cryptor.Encrypt(job.InputPath, job.OutputPath, job.Key, progress, job.CancellationToken)
                    : cryptor.Decrypt(job.InputPath, job.OutputPath, job.Key, progress, job.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int percent = Math.Max(0, Math.Min(100, e.ProgressPercentage));
            progressBar.Value = percent;
            lblPercent.Text = percent + " %";
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            timer.Stop();
            if (IsDisposed || Disposing)
                return;
            UpdateElapsed();
            SetBusy(false);
            cancellation.Dispose();
            cancellation = null;

            string operationName = currentOperation == CryptoOperation.Encrypt ? "Шифрування" : "Розшифрування";

            if (e.Error != null)
            {
                progressBar.Value = 0;
                lblPercent.Text = "0 %";
                lblStatus.ForeColor = Color.Firebrick;
                lblStatus.Text = operationName + " не виконано: " + DescribeError(e.Error);
                MessageBox.Show(this, DescribeError(e.Error), operationName + ": помилка",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (e.Cancelled)
            {
                progressBar.Value = 0;
                lblPercent.Text = "0 %";
                lblStatus.ForeColor = Color.DarkOrange;
                lblStatus.Text = operationName + " скасовано користувачем. Файл результату не створено.";
                return;
            }

            var result = (CryptoOperationResult)e.Result;
            progressBar.Value = 100;
            lblPercent.Text = "100 %";
            lblStatus.ForeColor = Color.ForestGreen;
            lblStatus.Text = operationName + " завершено: " + result.OutputPath;
            UpdateFileInfo();

            string message =
                operationName + " успішно завершено.\n\n" +
                "Файл: " + result.OutputFileName + "\n" +
                "Папка: " + Path.GetDirectoryName(result.OutputPath) + "\n" +
                "Розмір: " + SizeFormatter.FormatWithBytes(result.OutputSize) + "\n" +
                "Затрачений час: " + FormatTime(result.Elapsed);
            MessageBox.Show(this, message, operationName + " завершено", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string DescribeError(Exception error)
        {
            if (error is CryptoFileException || error is ArgumentException)
                return error.Message;
            if (error is FileNotFoundException)
                return "Файл не знайдено. Можливо, його було переміщено або видалено.";
            if (error is DirectoryNotFoundException)
                return "Папку не знайдено.";
            if (error is UnauthorizedAccessException)
                return "Немає доступу до файлу. Перевірте права або оберіть іншу папку.";
            if (error is PathTooLongException)
                return "Шлях до файлу задовгий.";
            if (error is IOException)
                return "Помилка читання або запису файлу: " + error.Message +
                       "\nМожливо, файл відкрито в іншій програмі або на диску недостатньо місця.";
            return error.Message;
        }

        private void UpdateElapsed()
        {
            TimeSpan elapsed = DateTime.Now - startTime;
            lblElapsed.Text = "Минуло часу: " + FormatTime(elapsed);
        }

        private static string FormatTime(TimeSpan time)
        {
            return string.Format("{0:00}:{1:00}:{2:00}.{3}", (int)time.TotalHours, time.Minutes, time.Seconds, time.Milliseconds / 100);
        }

        private void SetBusy(bool busy)
        {
            txtFile.Enabled = !busy;
            btnBrowse.Enabled = !busy;
            txtKey.Enabled = !busy;
            chkShowKey.Enabled = !busy;
            btnEncrypt.Enabled = !busy;
            btnDecrypt.Enabled = !busy;
            btnCancel.Enabled = busy;
            UseWaitCursor = false;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(this, message, "Шифрування файлів", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (worker.IsBusy)
            {
                DialogResult answer = MessageBox.Show(this,
                    "Операцію ще не завершено. Скасувати її та закрити програму?",
                    "Шифрування файлів", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                worker.CancelAsync();
                if (cancellation != null) cancellation.Cancel();
            }
            base.OnFormClosing(e);
        }

        private sealed class CryptoJob
        {
            public CryptoJob(CryptoOperation operation, string inputPath, string outputPath, string key,
                             CancellationToken cancellationToken)
            {
                Operation = operation;
                InputPath = inputPath;
                OutputPath = outputPath;
                Key = key;
                CancellationToken = cancellationToken;
            }

            public CryptoOperation Operation { get; private set; }
            public string InputPath { get; private set; }
            public string OutputPath { get; private set; }
            public string Key { get; private set; }
            public CancellationToken CancellationToken { get; private set; }
        }
    }
}
