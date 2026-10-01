using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using OpenCvSharp.Extensions;

namespace SmartLabel_Vision
{
    public partial class Form1 : Form
    {
        private readonly InspectionEngine _engine;
        private PictureBox _picPreview = null!;
        private Label _lblStatus = null!;
        private Label _lblMetrics = null!;
        private Button _btnRun = null!;
        private DataGridView _gridLogs = null!;
        private string _samplesDir = string.Empty;

        private const string ConnStr =
            @"Server=localhost\SQLEXPRESS;Database=SmartLabelDB;Trusted_Connection=True;TrustServerCertificate=True;";

        public Form1()
        {
            InitializeComponent();
            _engine = new InspectionEngine();
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "SmartLabel-Vision | Industrial QC & Telemetry Station";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. Image Preview Box
            _picPreview = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(580, 420),
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            this.Controls.Add(_picPreview);

            // 2. Control Panel
            var panelControls = new Panel
            {
                Location = new Point(620, 20),
                Size = new Size(440, 420)
            };
            this.Controls.Add(panelControls);

            _btnRun = new Button
            {
                Text = "▶ START BATCH INSPECTION",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(0, 0),
                Size = new Size(440, 45),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnRun.Click += async (s, e) => await RunInspectionBatchAsync();
            panelControls.Controls.Add(_btnRun);

            _lblStatus = new Label
            {
                Text = "STATUS: STANDBY",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(0, 60),
                Size = new Size(440, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGray
            };
            panelControls.Controls.Add(_lblStatus);

            _lblMetrics = new Label
            {
                Text = "Batch Metrics:\n• Total Inspected: 0\n• Passed: 0 | Failed: 0\n• Average Cycle Time: 0 ms",
                Font = new Font("Consolas", 10, FontStyle.Regular),
                Location = new Point(0, 115),
                Size = new Size(440, 90),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };
            panelControls.Controls.Add(_lblMetrics);

            // 3. Historical Telemetry Table (Database View)
            var lblGridHeader = new Label
            {
                Text = "SQL Server Inspection Telemetry Logs (Live Sync):",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(20, 455),
                Size = new Size(400, 20)
            };
            this.Controls.Add(lblGridHeader);

            _gridLogs = new DataGridView
            {
                Location = new Point(20, 480),
                Size = new Size(1040, 160),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White
            };
            this.Controls.Add(_gridLogs);
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // Generate synthetic test batch upon startup
            _samplesDir = TestImageGenerator.GenerateSamples();
            await RefreshDatabaseGridAsync();
        }

        private async Task RunInspectionBatchAsync()
        {
            _btnRun.Enabled = false;
            string[] files = Directory.GetFiles(_samplesDir, "*.png");

            int passCount = 0;
            int failCount = 0;
            double totalTime = 0;

            foreach (var file in files)
            {
                // Run the vision inspection pipeline
                var res = _engine.Inspect(file);

                // Update UI image feed
                _picPreview.Image?.Dispose();
                _picPreview.Image = BitmapConverter.ToBitmap(res.AnnotatedImage);

                // Update Status Banner
                if (res.IsPass)
                {
                    passCount++;
                    _lblStatus.Text = "VERDICT: PASS [OK]";
                    _lblStatus.BackColor = Color.LightGreen;
                }
                else
                {
                    failCount++;
                    _lblStatus.Text = $"VERDICT: FAIL [{res.FailureReason}]";
                    _lblStatus.BackColor = Color.Salmon;
                }

                totalTime += res.CycleTimeMs;
                _lblMetrics.Text = $"Batch Metrics:\n• Total Inspected: {passCount + failCount} / {files.Length}\n" +
                                   $"• Passed: {passCount} | Failed: {failCount}\n" +
                                   $"• Current Latency: {res.CycleTimeMs} ms";

                // Log to SQL Server Express
                try
                {
                    DatabaseLogger.Log(res);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Database write failed: {ex.Message}");
                }

                // Simulate conveyor line cadence (800ms between packages)
                await Task.Delay(800);
            }

            double avgTime = Math.Round(totalTime / files.Length, 1);
            _lblMetrics.Text = $"Batch Finished:\n• Total Inspected: {files.Length}\n" +
                               $"• Passed: {passCount} | Failed: {failCount}\n" +
                               $"• Average Cycle Time: {avgTime} ms";

            await RefreshDatabaseGridAsync();
            _btnRun.Enabled = true;
        }

        private async Task RefreshDatabaseGridAsync()
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                const string query = @"
                    SELECT TOP 15 LogId, Timestamp, BarcodeData, ExpiryDateText, Verdict, FailureReason, CycleTimeMs 
                    FROM InspectionLogs 
                    ORDER BY LogId DESC;";

                using var adapter = new SqlDataAdapter(query, conn);
                var dt = new DataTable();
                await Task.Run(() => adapter.Fill(dt));
                _gridLogs.DataSource = dt;
            }
            catch (Exception)
            {
                _lblStatus.Text = "DB SYNC ERROR";
                _lblStatus.BackColor = Color.OrangeRed;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _engine.Dispose();
            base.OnFormClosing(e);
        }
    }
}