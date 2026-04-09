using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using RaceFlow.Core.Models;
using RaceFlow.Core.Runtime;
using RaceFlow.Core.Services;
using RaceFlow.Core.State;
using RaceFlow.RuntimeHost.Models;
using RaceFlow.RuntimeHost.Services;

namespace RaceFlow.Admin
{
    public partial class MainForm : Form
    {
        private Panel _topBar;
        private Panel _mainArea;
        private Panel _rightPanel;
        private Panel _bottomPanel;

        private Label _lblSocketUrl;
        private TextBox _txtSocketUrl;
        private Label _lblSessionCode;
        private TextBox _txtSessionCode;
        private Button _btnConnect;
        private Button _btnDisconnect;
        private Button _btnLoadFlow;
        private Label _lblConnectionStatusValue;

        private DataGridView _gridRacers;

        private Label _lblSelectedRacerValue;
        private Label _lblSelectedSessionValue;
        private Label _lblSelectedMapValue;
        private Label _lblSelectedPositionValue;
        private Label _lblLastConfirmedValue;
        private Label _lblTargetNodeValue;
        private Label _lblPendingSplitValue;
        private Label _lblBranchStateValue;
        private Label _lblStatusTextValue;

        private ComboBox _cmbTheme;
        private NumericUpDown _numTriggerScale;
        private Button _btnApplyTriggerScale;
        private NumericUpDown _numPlaybackDelayMs;
        private Button _btnApplyPlaybackDelay;
        private Button _btnSaveFlow;

        private Button _btnOpenOutput;
        private Button _btnOpenOutputTuner;

        private TextBox _txtLog;

        private BeetleRankSocketClient? _socketClient;
        private CancellationTokenSource? _socketCts;
        private Task? _socketTask;

        private readonly Dictionary<string, DataGridViewRow> _racerRows = new();
        private readonly Dictionary<string, BeetleRankUserSnapshot> _latestUserSnapshots = new();
        private readonly List<ThemeListItem> _availableThemes = new();
        private readonly List<RaceOutputFrame> _outputFrameHistory = new();

        private readonly FlowMapLoader _flowLoader = new();
        private readonly FlowMapGraphBuilder _graphBuilder = new();
        private readonly FlowMapValidator _graphValidator = new();
        private readonly RacerProgressResolver _resolver = new();
        private readonly BeetleRankTelemetryMapper _telemetryMapper = new();

        private FlowMapDocument? _activeFlow;
        private RuntimeGraph? _activeGraph;
        private RaceRuntimeCoordinator? _runtimeCoordinator;
        private string? _activeFlowPath;

        private RaceOutputWindow? _outputWindow;
        private SectionLayoutTunerWindow? _sectionLayoutTunerWindow;
        private RaceOutputFrame? _latestOutputFrame;
        private OverlayWebServer? _overlayWebServer;

        private bool _isPopulatingThemes;

        public MainForm()
        {
            BuildShell();

            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            LoadAvailableThemes();
            StartOverlayWebServer();
            ApplyWindowSettings();
            WireEvents();
            SetConnectionState(false, "Disconnected");
            AppendLog("RaceFlow.Admin started.");
        }

        private void BuildShell()
        {
            SuspendLayout();

            Controls.Clear();
            BackColor = Color.FromArgb(28, 32, 38);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 74,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(36, 41, 48)
            };
            Controls.Add(_topBar);

            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 180,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(32, 36, 42)
            };
            Controls.Add(_bottomPanel);

            _rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 320,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(34, 39, 46)
            };
            Controls.Add(_rightPanel);

            _mainArea = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 40, 12, 12),
                BackColor = Color.FromArgb(28, 32, 38)
            };
            Controls.Add(_mainArea);

            BuildTopBar();
            BuildRacerGrid();
            BuildDetailsPanel();
            BuildLogPanel();

            ResumeLayout();
        }

        private void ApplyWindowSettings()
        {
            Text = "RaceFlow Admin";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1280, 760);
            Size = new Size(1500, 900);
        }

        private void BuildTopBar()
        {
            _lblSocketUrl = MakeLabel("WebSocket URL", 0, 8, 120);
            _topBar.Controls.Add(_lblSocketUrl);

            _txtSocketUrl = new TextBox
            {
                Left = 0,
                Top = 30,
                Width = 430,
                Text = "wss://www.beetlerank.com:3002"
            };
            _topBar.Controls.Add(_txtSocketUrl);

            _lblSessionCode = MakeLabel("Session / Event Code", 450, 8, 150);
            _topBar.Controls.Add(_lblSessionCode);

            _txtSessionCode = new TextBox
            {
                Left = 450,
                Top = 30,
                Width = 180,
                Text = string.Empty
            };
            _topBar.Controls.Add(_txtSessionCode);

            _btnConnect = new Button
            {
                Left = 650,
                Top = 28,
                Width = 110,
                Height = 30,
                Text = "Connect",
                UseVisualStyleBackColor = true
            };
            _topBar.Controls.Add(_btnConnect);

            _btnDisconnect = new Button
            {
                Left = 768,
                Top = 28,
                Width = 110,
                Height = 30,
                Text = "Disconnect",
                Enabled = false,
                UseVisualStyleBackColor = true
            };
            _topBar.Controls.Add(_btnDisconnect);

            _btnLoadFlow = new Button
            {
                Left = 886,
                Top = 28,
                Width = 110,
                Height = 30,
                Text = "Load Flow",
                UseVisualStyleBackColor = true
            };
            _topBar.Controls.Add(_btnLoadFlow);

            var lblStatus = MakeLabel("Status", 1018, 8, 60);
            _topBar.Controls.Add(lblStatus);

            _lblConnectionStatusValue = new Label
            {
                Left = 1018,
                Top = 30,
                Width = 220,
                Height = 24,
                ForeColor = Color.FromArgb(220, 120, 120),
                Text = "Disconnected",
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
                AutoEllipsis = true
            };
            _topBar.Controls.Add(_lblConnectionStatusValue);
        }

        private void BuildRacerGrid()
        {
            var header = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Live Racers",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold)
            };

            _gridRacers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.FromArgb(24, 27, 32),
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(55, 60, 68)
            };

            _gridRacers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(46, 52, 60);
            _gridRacers.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _gridRacers.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(46, 52, 60);
            _gridRacers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

            _gridRacers.DefaultCellStyle.BackColor = Color.FromArgb(30, 34, 40);
            _gridRacers.DefaultCellStyle.ForeColor = Color.Gainsboro;
            _gridRacers.DefaultCellStyle.SelectionBackColor = Color.FromArgb(64, 92, 140);
            _gridRacers.DefaultCellStyle.SelectionForeColor = Color.White;

            _gridRacers.Columns.Add("RacerName", "Racer");
            _gridRacers.Columns.Add("SessionCode", "Session");
            _gridRacers.Columns.Add("MapId", "Map");
            _gridRacers.Columns.Add("PosX", "X");
            _gridRacers.Columns.Add("PosY", "Y");
            _gridRacers.Columns.Add("PosZ", "Z");
            _gridRacers.Columns.Add("LastConfirmed", "Last Confirmed");
            _gridRacers.Columns.Add("TargetNode", "Target");
            _gridRacers.Columns.Add("PendingSplit", "Pending Split");
            _gridRacers.Columns.Add("BranchState", "Branch");
            _gridRacers.Columns.Add("StatusText", "Status");

            _mainArea.Controls.Add(_gridRacers);
            _mainArea.Controls.Add(header);
        }

        private void BuildDetailsPanel()
        {
            var title = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Selected Racer",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold)
            };
            _rightPanel.Controls.Add(title);

            var tuningPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 340,
                Padding = new Padding(0, 8, 0, 0)
            };
            _rightPanel.Controls.Add(tuningPanel);

            var lblTuning = new Label
            {
                Left = 0,
                Top = 0,
                Width = 280,
                Height = 20,
                Text = "Flow Tuning",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold)
            };
            tuningPanel.Controls.Add(lblTuning);

            var lblTheme = new Label
            {
                Left = 0,
                Top = 28,
                Width = 280,
                Height = 20,
                Text = "Theme",
                ForeColor = Color.Silver
            };
            tuningPanel.Controls.Add(lblTheme);

            _cmbTheme = new ComboBox
            {
                Left = 0,
                Top = 52,
                Width = 280,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            tuningPanel.Controls.Add(_cmbTheme);

            _btnOpenOutput = new Button
            {
                Left = 0,
                Top = 88,
                Width = 135,
                Height = 28,
                Text = "Open Output",
                UseVisualStyleBackColor = true
            };
            tuningPanel.Controls.Add(_btnOpenOutput);

            _btnOpenOutputTuner = new Button
            {
                Left = 145,
                Top = 88,
                Width = 135,
                Height = 28,
                Text = "Open Tuning",
                UseVisualStyleBackColor = true
            };
            tuningPanel.Controls.Add(_btnOpenOutputTuner);

            var lblScale = new Label
            {
                Left = 0,
                Top = 128,
                Width = 280,
                Height = 20,
                Text = "Global Trigger Scale",
                ForeColor = Color.Silver
            };
            tuningPanel.Controls.Add(lblScale);

            _numTriggerScale = new NumericUpDown
            {
                Left = 0,
                Top = 153,
                Width = 120,
                DecimalPlaces = 2,
                Minimum = 0.10m,
                Maximum = 10.00m,
                Increment = 0.10m,
                Value = 1.00m
            };
            tuningPanel.Controls.Add(_numTriggerScale);

            _btnApplyTriggerScale = new Button
            {
                Left = 135,
                Top = 151,
                Width = 145,
                Height = 28,
                Text = "Apply Scale",
                UseVisualStyleBackColor = true
            };
            tuningPanel.Controls.Add(_btnApplyTriggerScale);

            var lblDelay = new Label
            {
                Left = 0,
                Top = 192,
                Width = 280,
                Height = 20,
                Text = "Playback Delay (ms)",
                ForeColor = Color.Silver
            };
            tuningPanel.Controls.Add(lblDelay);

            _numPlaybackDelayMs = new NumericUpDown
            {
                Left = 0,
                Top = 217,
                Width = 120,
                DecimalPlaces = 0,
                Minimum = 0,
                Maximum = 30000,
                Increment = 50,
                Value = 0
            };
            tuningPanel.Controls.Add(_numPlaybackDelayMs);

            _btnApplyPlaybackDelay = new Button
            {
                Left = 135,
                Top = 215,
                Width = 145,
                Height = 28,
                Text = "Apply Delay",
                UseVisualStyleBackColor = true
            };
            tuningPanel.Controls.Add(_btnApplyPlaybackDelay);

            _btnSaveFlow = new Button
            {
                Left = 0,
                Top = 260,
                Width = 280,
                Height = 28,
                Text = "Save Flow",
                UseVisualStyleBackColor = true
            };
            tuningPanel.Controls.Add(_btnSaveFlow);

            var content = new Panel
            {
                Dock = DockStyle.Fill
            };
            _rightPanel.Controls.Add(content);
            content.BringToFront();

            int y = 12;
            AddDetailRow(content, "Name", ref y, out _lblSelectedRacerValue);
            AddDetailRow(content, "Session", ref y, out _lblSelectedSessionValue);
            AddDetailRow(content, "Map", ref y, out _lblSelectedMapValue);
            AddDetailRow(content, "Position", ref y, out _lblSelectedPositionValue);
            AddDetailRow(content, "Last Confirmed", ref y, out _lblLastConfirmedValue);
            AddDetailRow(content, "Target Node", ref y, out _lblTargetNodeValue);
            AddDetailRow(content, "Pending Split", ref y, out _lblPendingSplitValue);
            AddDetailRow(content, "Branch State", ref y, out _lblBranchStateValue);
            AddDetailRow(content, "Status", ref y, out _lblStatusTextValue, 72);
        }

        private void BuildLogPanel()
        {
            var lblLog = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Text = "Status / Log",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold)
            };
            _bottomPanel.Controls.Add(lblLog);

            _txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(20, 23, 28),
                ForeColor = Color.Gainsboro,
                Font = new Font("Consolas", 9.25F, FontStyle.Regular)
            };
            _bottomPanel.Controls.Add(_txtLog);
            _txtLog.BringToFront();
        }

        private void WireEvents()
        {
            _btnConnect.Click += BtnConnect_Click;
            _btnDisconnect.Click += BtnDisconnect_Click;
            _btnLoadFlow.Click += BtnLoadFlow_Click;
            _btnApplyTriggerScale.Click += BtnApplyTriggerScale_Click;
            _btnApplyPlaybackDelay.Click += BtnApplyPlaybackDelay_Click;
            _btnSaveFlow.Click += BtnSaveFlow_Click;
            _btnOpenOutput.Click += BtnOpenOutput_Click;
            _btnOpenOutputTuner.Click += BtnOpenOutputTuner_Click;
            _cmbTheme.SelectedIndexChanged += CmbTheme_SelectedIndexChanged;
            _gridRacers.SelectionChanged += GridRacers_SelectionChanged;
        }

        private void StartOverlayWebServer()
        {
            try
            {
                _overlayWebServer = new OverlayWebServer(5057);
                _overlayWebServer.Start();
                _overlayWebServer.UpdateScene(null, null, null);
                AppendLog($"OBS overlay server started: {_overlayWebServer.OverlayUrl}");
            }
            catch (Exception ex)
            {
                AppendLog($"OBS overlay server failed to start: {ex.Message}");
            }
        }

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            string urlText = _txtSocketUrl.Text.Trim();

            if (string.IsNullOrWhiteSpace(urlText))
            {
                AppendLog("Connect cancelled: WebSocket URL is empty.");
                return;
            }

            if (!Uri.TryCreate(urlText, UriKind.Absolute, out Uri? socketUri))
            {
                AppendLog("Connect cancelled: WebSocket URL is invalid.");
                return;
            }

            if (!TryParseSessionFilter(_txtSessionCode.Text.Trim(), out int? sessionFilter))
            {
                AppendLog("Connect cancelled: Session / Event Code must be blank or numeric.");
                return;
            }

            try
            {
                await DisconnectSocketAsync(clearGrid: true);

                _socketClient = new BeetleRankSocketClient();
                _socketCts = new CancellationTokenSource();

                if (_activeGraph != null)
                {
                    _runtimeCoordinator = new RaceRuntimeCoordinator(_activeGraph, _resolver);
                    AppendLog("Runtime coordinator initialized.");
                }
                else
                {
                    _runtimeCoordinator = null;
                    AppendLog("No flow loaded. Running in viewer mode until flow is loaded.");
                }

                SetConnectionState(true, "Connecting...");
                AppendLog($"Connecting to {socketUri} ...");

                _socketTask = Task.Run(async () =>
                {
                    try
                    {
                        await _socketClient.RunAsync(
                            socketUri,
                            message => HandleSocketMessageAsync(message, sessionFilter),
                            _socketCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            AppendLog($"Socket error: {ex.Message}");
                            SetConnectionState(false, "Disconnected");
                        }));
                    }
                });

                SetConnectionState(true, "Connected");
                AppendLog("WebSocket connected.");
            }
            catch (Exception ex)
            {
                SetConnectionState(false, "Disconnected");
                AppendLog($"Connection failed: {ex.Message}");
            }
        }

        private async void BtnDisconnect_Click(object? sender, EventArgs e)
        {
            await DisconnectSocketAsync(clearGrid: false);
            SetConnectionState(false, "Disconnected");
            AppendLog("WebSocket disconnected.");
        }

        private void BtnLoadFlow_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Flow Map JSON (*.json)|*.json",
                Title = "Load Flow Map"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                LoadAndBuildFlow(dialog.FileName, resetGrid: true);
                AppendLog($"Flow file loaded from: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                AppendLog($"Flow load failed: {ex.Message}");
            }
        }

        private void BtnApplyTriggerScale_Click(object? sender, EventArgs e)
        {
            if (_activeFlow == null)
            {
                AppendLog("Apply scale cancelled: no flow is loaded.");
                return;
            }

            _activeFlow.Admin ??= new FlowMapAdminSettings();
            _activeFlow.Admin.GlobalTriggerScale = (double)_numTriggerScale.Value;

            try
            {
                RebuildRuntimeFromActiveFlow(resetGrid: true);
                AppendLog($"Applied global trigger scale: {_activeFlow.Admin.GlobalTriggerScale:0.00}");
                AppendLog("Runtime state reset. Incoming snapshots will repopulate racers with new trigger sizes.");
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to apply trigger scale: {ex.Message}");
            }
        }

        private void BtnApplyPlaybackDelay_Click(object? sender, EventArgs e)
        {
            if (_activeFlow == null)
            {
                AppendLog("Apply delay cancelled: no flow is loaded.");
                return;
            }

            _activeFlow.Admin ??= new FlowMapAdminSettings();
            _activeFlow.Admin.PlaybackDelayMs = (int)_numPlaybackDelayMs.Value;

            AppendLog($"Applied playback delay: {_activeFlow.Admin.PlaybackDelayMs} ms");
            RefreshRaceOutputWindow();
        }

        private void BtnSaveFlow_Click(object? sender, EventArgs e)
        {
            if (_activeFlow == null)
            {
                AppendLog("Save cancelled: no flow is loaded.");
                return;
            }

            using var dialog = new SaveFileDialog
            {
                Filter = "Flow Map JSON (*.json)|*.json",
                Title = "Save Flow Map",
                FileName = string.IsNullOrWhiteSpace(_activeFlowPath)
                    ? "flowmap_admin.json"
                    : Path.GetFileName(_activeFlowPath)
            };

            if (!string.IsNullOrWhiteSpace(_activeFlowPath))
            {
                try
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(_activeFlowPath);
                }
                catch
                {
                }
            }

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                SaveFlowToFile(dialog.FileName);
                _activeFlowPath = dialog.FileName;
                AppendLog($"Flow saved: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                AppendLog($"Flow save failed: {ex.Message}");
            }
        }

        private void BtnOpenOutput_Click(object? sender, EventArgs e)
        {
            if (_outputWindow == null || _outputWindow.IsDisposed)
            {
                _outputWindow = new RaceOutputWindow();
                _outputWindow.FormClosed += (_, __) => _outputWindow = null;
            }

            _outputWindow.Show();
            _outputWindow.BringToFront();

            RefreshRaceOutputWindow();
        }

        private void BtnOpenOutputTuner_Click(object? sender, EventArgs e)
        {
            if (_sectionLayoutTunerWindow == null || _sectionLayoutTunerWindow.IsDisposed)
            {
                _sectionLayoutTunerWindow = new SectionLayoutTunerWindow(() =>
                {
                    _outputWindow?.RefreshScene();
                    PushOverlaySceneToBrowserSource();
                });

                _sectionLayoutTunerWindow.FormClosed += (_, __) => _sectionLayoutTunerWindow = null;
            }

            _sectionLayoutTunerWindow.BindGraph(_activeGraph);
            _sectionLayoutTunerWindow.Show();
            _sectionLayoutTunerWindow.BringToFront();
        }

        private void CmbTheme_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isPopulatingThemes)
                return;

            if (_cmbTheme.SelectedItem is not ThemeListItem selectedTheme)
                return;

            if (_activeFlow == null)
                return;

            _activeFlow.Admin ??= new FlowMapAdminSettings();
            _activeFlow.Admin.ThemeFile = selectedTheme.FileName;

            AppendLog($"Theme selected: {selectedTheme.DisplayName} ({selectedTheme.FileName})");
            RefreshRaceOutputWindow();
        }

        private void LoadAvailableThemes()
        {
            _availableThemes.Clear();

            string themesDirectory = GetThemesDirectoryPath();

            if (!Directory.Exists(themesDirectory))
            {
                try
                {
                    Directory.CreateDirectory(themesDirectory);
                }
                catch
                {
                }
            }

            if (Directory.Exists(themesDirectory))
            {
                foreach (string themeFilePath in Directory.GetFiles(themesDirectory, "*.json", SearchOption.TopDirectoryOnly))
                {
                    if (TryLoadThemeListItem(themeFilePath, out ThemeListItem? themeItem))
                    {
                        _availableThemes.Add(themeItem);
                    }
                }
            }

            _availableThemes.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

            RebuildThemeDropdownItems();

            AppendLog($"Themes found: {_availableThemes.Count}");
        }

        private void RebuildThemeDropdownItems()
        {
            if (_cmbTheme == null)
                return;

            _isPopulatingThemes = true;

            try
            {
                _cmbTheme.BeginUpdate();
                _cmbTheme.Items.Clear();

                foreach (ThemeListItem theme in _availableThemes)
                {
                    _cmbTheme.Items.Add(theme);
                }

                _cmbTheme.SelectedItem = null;

                if (_cmbTheme.Items.Count > 0)
                    _cmbTheme.SelectedIndex = 0;
            }
            finally
            {
                _cmbTheme.EndUpdate();
                _isPopulatingThemes = false;
            }
        }

        private void ApplySelectedThemeFromActiveFlow()
        {
            if (_cmbTheme == null)
                return;

            _isPopulatingThemes = true;

            try
            {
                _cmbTheme.SelectedItem = null;

                string desiredThemeFile = _activeFlow?.Admin?.ThemeFile ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(desiredThemeFile))
                {
                    ThemeListItem? match = _availableThemes.FirstOrDefault(t =>
                        string.Equals(t.FileName, desiredThemeFile, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        _cmbTheme.SelectedItem = match;

                        if (_activeFlow != null)
                        {
                            _activeFlow.Admin ??= new FlowMapAdminSettings();
                            _activeFlow.Admin.ThemeFile = match.FileName;
                        }

                        return;
                    }
                }

                if (_cmbTheme.Items.Count > 0)
                {
                    _cmbTheme.SelectedIndex = 0;

                    if (_cmbTheme.SelectedItem is ThemeListItem selectedTheme && _activeFlow != null)
                    {
                        _activeFlow.Admin ??= new FlowMapAdminSettings();
                        _activeFlow.Admin.ThemeFile = selectedTheme.FileName;
                    }
                }
            }
            finally
            {
                _isPopulatingThemes = false;
            }
        }

        private static string GetThemesDirectoryPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");
        }

        private static bool TryLoadThemeListItem(string themeFilePath, out ThemeListItem? themeItem)
        {
            themeItem = null;

            try
            {
                string json = File.ReadAllText(themeFilePath);
                using JsonDocument document = JsonDocument.Parse(json);

                string displayName = Path.GetFileNameWithoutExtension(themeFilePath);

                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("themeName", out JsonElement themeNameElement) &&
                    themeNameElement.ValueKind == JsonValueKind.String)
                {
                    string? parsedName = themeNameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(parsedName))
                        displayName = parsedName;
                }

                themeItem = new ThemeListItem(
                    displayName,
                    Path.GetFileName(themeFilePath),
                    themeFilePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private RaceOutputFrame BuildOutputFrameFromRuntime()
        {
            var frame = new RaceOutputFrame
            {
                CapturedUtc = DateTime.UtcNow
            };

            if (_runtimeCoordinator == null)
                return frame;

            foreach (KeyValuePair<string, RacerRuntimeState> pair in _runtimeCoordinator.Racers.OrderBy(p => p.Value.RacerName))
            {
                RacerRuntimeState racer = pair.Value;

                string colorHex = "#FFFFFF";
                bool isActive = true;

                if (_latestUserSnapshots.TryGetValue(pair.Key, out BeetleRankUserSnapshot? userSnapshot))
                {
                    colorHex = GetSnapshotColorHex(userSnapshot);
                    isActive = userSnapshot.Active;
                }

                frame.Racers.Add(new RaceOutputRacerVisual
                {
                    RacerKey = pair.Key,
                    RacerName = racer.RacerName,
                    ColorHex = colorHex,
                    IsActive = isActive,
                    LastConfirmedNodeId = racer.LastConfirmedNode?.Id,
                    TargetNodeId = racer.CurrentTargetNode?.Id,
                    EdgeProgress = racer.EdgeProgress,
                    HasFinished = racer.HasFinished,
                    StatusText = racer.StatusText
                });
            }

            return frame;
        }

        private void RefreshRaceOutputWindow()
        {
            RaceOutputFrame liveFrame = BuildOutputFrameFromRuntime();
            AddFrameToHistory(liveFrame);

            int delayMs = _activeFlow?.Admin?.PlaybackDelayMs ?? 0;
            _latestOutputFrame = GetDelayedFrame(delayMs) ?? liveFrame;

            PushOverlaySceneToBrowserSource();

            if (_outputWindow == null || _outputWindow.IsDisposed)
                return;

            _outputWindow.UpdateScene(_activeGraph, _latestOutputFrame, delayMs, _activeFlow?.Admin?.ThemeFile);
        }

        private void AddFrameToHistory(RaceOutputFrame frame)
        {
            _outputFrameHistory.Add(frame);

            DateTime minUtc = DateTime.UtcNow.AddSeconds(-30);
            _outputFrameHistory.RemoveAll(f => f.CapturedUtc < minUtc);
        }

        private RaceOutputFrame? GetDelayedFrame(int delayMs)
        {
            if (_outputFrameHistory.Count == 0)
                return null;

            if (delayMs <= 0)
                return _outputFrameHistory[^1];

            DateTime targetUtc = DateTime.UtcNow.AddMilliseconds(-delayMs);

            for (int i = _outputFrameHistory.Count - 1; i >= 0; i--)
            {
                if (_outputFrameHistory[i].CapturedUtc <= targetUtc)
                    return _outputFrameHistory[i];
            }

            return _outputFrameHistory[0];
        }

        private void PushOverlaySceneToBrowserSource()
        {
            string? themeFile = _activeFlow?.Admin?.ThemeFile;
            _overlayWebServer?.UpdateScene(_activeGraph, _latestOutputFrame, themeFile);
        }

        private void LoadAndBuildFlow(string filePath, bool resetGrid)
        {
            _activeFlow = _flowLoader.LoadFromFile(filePath);
            _activeFlow.Admin ??= new FlowMapAdminSettings();

            _activeFlowPath = filePath;

            RebuildRuntimeFromActiveFlow(resetGrid);

            _numTriggerScale.Value = ClampDecimal((decimal)_activeFlow.Admin.GlobalTriggerScale, _numTriggerScale.Minimum, _numTriggerScale.Maximum);
            _numPlaybackDelayMs.Value = ClampDecimal(_activeFlow.Admin.PlaybackDelayMs, _numPlaybackDelayMs.Minimum, _numPlaybackDelayMs.Maximum);

            ApplySelectedThemeFromActiveFlow();

            AppendLog($"Flow loaded: {_activeGraph?.LayoutName}");
            AppendLog($"Sections: {_activeGraph?.Sections.Count ?? 0}");
            AppendLog($"Segments: {_activeGraph?.Segments.Count ?? 0}");
            AppendLog($"Nodes: {_activeGraph?.Nodes.Count ?? 0}");
            AppendLog($"Edges: {_activeGraph?.Edges.Count ?? 0}");
            AppendLog($"Global Trigger Scale: {_activeFlow.Admin.GlobalTriggerScale:0.00}");
            AppendLog($"Playback Delay: {_activeFlow.Admin.PlaybackDelayMs} ms");

            if (!string.IsNullOrWhiteSpace(_activeFlow.Admin.ThemeFile))
                AppendLog($"Theme file: {_activeFlow.Admin.ThemeFile}");

            _sectionLayoutTunerWindow?.BindGraph(_activeGraph);

            if (_activeGraph != null)
            {
                List<string> issues = _graphValidator.Validate(_activeGraph);

                if (issues.Count == 0)
                {
                    AppendLog("Validation: OK");
                }
                else
                {
                    AppendLog($"Validation issues: {issues.Count}");
                    foreach (string issue in issues)
                    {
                        AppendLog($" - {issue}");
                    }
                }
            }

            RefreshRaceOutputWindow();
        }

        private void RebuildRuntimeFromActiveFlow(bool resetGrid)
        {
            if (_activeFlow == null)
                throw new InvalidOperationException("No active flow document is loaded.");

            _activeGraph = _graphBuilder.Build(_activeFlow);
            _runtimeCoordinator = new RaceRuntimeCoordinator(_activeGraph, _resolver);

            _sectionLayoutTunerWindow?.BindGraph(_activeGraph);

            if (resetGrid)
            {
                _racerRows.Clear();
                _latestUserSnapshots.Clear();
                _outputFrameHistory.Clear();
                _gridRacers.Rows.Clear();
                ClearSelectedRacerDetails();
            }

            RefreshRaceOutputWindow();
        }

        private void CopyRuntimeSectionLayoutBackToActiveFlow()
        {
            if (_activeFlow == null || _activeGraph == null)
                return;

            foreach (var flowSection in _activeFlow.Sections)
            {
                var runtimeSection = _activeGraph.Sections
                    .FirstOrDefault(s => string.Equals(s.Side, flowSection.Side, StringComparison.OrdinalIgnoreCase));

                if (runtimeSection == null)
                    continue;

                flowSection.VisualScale = runtimeSection.VisualScale;
                flowSection.OffsetX = runtimeSection.OffsetX;
                flowSection.OffsetY = runtimeSection.OffsetY;
            }
        }

        private void SaveFlowToFile(string filePath)
        {
            if (_activeFlow == null)
                throw new InvalidOperationException("No active flow document is loaded.");

            CopyRuntimeSectionLayoutBackToActiveFlow();

            _activeFlow.Admin ??= new FlowMapAdminSettings();
            _activeFlow.Admin.PlaybackDelayMs = (int)_numPlaybackDelayMs.Value;

            if (_cmbTheme.SelectedItem is ThemeListItem selectedTheme)
            {
                _activeFlow.Admin.ThemeFile = selectedTheme.FileName;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(_activeFlow, options);
            File.WriteAllText(filePath, json);
        }

        private async Task DisconnectSocketAsync(bool clearGrid)
        {
            if (_socketCts != null)
            {
                try
                {
                    _socketCts.Cancel();
                }
                catch
                {
                }
            }

            if (_socketTask != null)
            {
                try
                {
                    await _socketTask;
                }
                catch
                {
                }
            }

            _socketTask = null;
            _socketCts?.Dispose();
            _socketCts = null;
            _socketClient = null;

            if (clearGrid)
            {
                _racerRows.Clear();
                _latestUserSnapshots.Clear();
                _outputFrameHistory.Clear();
                _gridRacers.Rows.Clear();
                ClearSelectedRacerDetails();
            }
        }

        private Task HandleSocketMessageAsync(string message, int? sessionFilter)
        {
            BeginInvoke(new Action(() =>
            {
                HandleSnapshotJson(message, sessionFilter);
            }));

            return Task.CompletedTask;
        }

        private void HandleSnapshotJson(string json, int? sessionFilter)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                var snapshot = JsonSerializer.Deserialize<BeetleRankSnapshotMessage>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (snapshot == null)
                    return;

                if (!string.Equals(snapshot.Type, "snapshot", StringComparison.OrdinalIgnoreCase))
                    return;

                if (_runtimeCoordinator != null)
                {
                    List<RacerRuntimeState> changed = _runtimeCoordinator.ApplySnapshot(
                        snapshot,
                        _telemetryMapper,
                        sessionFilter);

                    foreach (BeetleRankUserSnapshot user in snapshot.Users)
                    {
                        if (sessionFilter.HasValue && user.SessionCode != sessionFilter.Value)
                            continue;

                        string racerKey = BeetleRankTelemetryMapper.BuildRacerKey(user);
                        _latestUserSnapshots[racerKey] = user;

                        if (_runtimeCoordinator.Racers.TryGetValue(racerKey, out RacerRuntimeState? racerState))
                        {
                            UpsertRacerRowFromRuntime(user, racerState);
                        }
                        else
                        {
                            UpsertBasicRacerRow(user);
                        }
                    }

                    if (changed.Count > 0)
                    {
                        AppendLog($"Snapshot updated: {changed.Count} racer(s).");
                    }
                }
                else
                {
                    int updatedCount = 0;

                    foreach (BeetleRankUserSnapshot user in snapshot.Users)
                    {
                        if (sessionFilter.HasValue && user.SessionCode != sessionFilter.Value)
                            continue;

                        string racerKey = BeetleRankTelemetryMapper.BuildRacerKey(user);
                        _latestUserSnapshots[racerKey] = user;

                        UpsertBasicRacerRow(user);
                        updatedCount++;
                    }

                    if (updatedCount > 0)
                    {
                        AppendLog($"Snapshot updated: {updatedCount} racer(s).");
                    }
                }

                RefreshSelectedRacerDetailsFromSelection();
                RefreshRaceOutputWindow();
            }
            catch (Exception ex)
            {
                AppendLog($"Snapshot parse error: {ex.Message}");
            }
        }

        private void UpsertBasicRacerRow(BeetleRankUserSnapshot user)
        {
            string racerKey = BeetleRankTelemetryMapper.BuildRacerKey(user);

            DataGridViewRow row = GetOrCreateRacerRow(racerKey);

            row.Cells["RacerName"].Value = user.User;
            row.Cells["SessionCode"].Value = user.SessionCode.ToString();
            row.Cells["MapId"].Value = NormalizeMapText(user.Map);
            row.Cells["PosX"].Value = user.X.ToString("0.######");
            row.Cells["PosY"].Value = user.Y.ToString("0.######");
            row.Cells["PosZ"].Value = user.Z.ToString("0.######");
            row.Cells["LastConfirmed"].Value = "-";
            row.Cells["TargetNode"].Value = "-";
            row.Cells["PendingSplit"].Value = "-";
            row.Cells["BranchState"].Value = "-";
            row.Cells["StatusText"].Value = user.Active ? "Live" : "Inactive";
        }

        private void UpsertRacerRowFromRuntime(BeetleRankUserSnapshot user, RacerRuntimeState racer)
        {
            string racerKey = BeetleRankTelemetryMapper.BuildRacerKey(user);

            DataGridViewRow row = GetOrCreateRacerRow(racerKey);

            string lastConfirmed = racer.LastConfirmedNode?.Label ?? "-";
            string targetNode = racer.CurrentTargetNode?.Label ?? "-";
            string pendingSplit = racer.AwaitingBranchDecision
                ? (racer.PendingSplitNode?.Label ?? "Pending")
                : "-";

            string branchState = BuildBranchStateText(racer);

            string status = string.IsNullOrWhiteSpace(racer.StatusText)
                ? (user.Active ? "Live" : "Inactive")
                : racer.StatusText;

            if (!user.Active)
            {
                status = $"Inactive | {status}";
            }

            row.Cells["RacerName"].Value = racer.RacerName;
            row.Cells["SessionCode"].Value = user.SessionCode.ToString();
            row.Cells["MapId"].Value = racer.CurrentMapId > 0
                ? racer.CurrentMapId.ToString()
                : NormalizeMapText(user.Map);
            row.Cells["PosX"].Value = racer.WorldX.ToString("0.######");
            row.Cells["PosY"].Value = racer.WorldY.ToString("0.######");
            row.Cells["PosZ"].Value = racer.WorldZ.ToString("0.######");
            row.Cells["LastConfirmed"].Value = lastConfirmed;
            row.Cells["TargetNode"].Value = targetNode;
            row.Cells["PendingSplit"].Value = pendingSplit;
            row.Cells["BranchState"].Value = branchState;
            row.Cells["StatusText"].Value = status;
        }

        private DataGridViewRow GetOrCreateRacerRow(string racerKey)
        {
            if (_racerRows.TryGetValue(racerKey, out DataGridViewRow? existingRow))
                return existingRow;

            int rowIndex = _gridRacers.Rows.Add();
            DataGridViewRow row = _gridRacers.Rows[rowIndex];
            row.Tag = racerKey;
            _racerRows[racerKey] = row;
            return row;
        }

        private void GridRacers_SelectionChanged(object? sender, EventArgs e)
        {
            RefreshSelectedRacerDetailsFromSelection();
        }

        private void RefreshSelectedRacerDetailsFromSelection()
        {
            if (_gridRacers.SelectedRows.Count == 0)
            {
                ClearSelectedRacerDetails();
                return;
            }

            DataGridViewRow row = _gridRacers.SelectedRows[0];

            _lblSelectedRacerValue.Text = SafeCell(row, "RacerName");
            _lblSelectedSessionValue.Text = SafeCell(row, "SessionCode");
            _lblSelectedMapValue.Text = SafeCell(row, "MapId");
            _lblSelectedPositionValue.Text =
                $"{SafeCell(row, "PosX")}, {SafeCell(row, "PosY")}, {SafeCell(row, "PosZ")}";
            _lblLastConfirmedValue.Text = SafeCell(row, "LastConfirmed");
            _lblTargetNodeValue.Text = SafeCell(row, "TargetNode");
            _lblPendingSplitValue.Text = SafeCell(row, "PendingSplit");
            _lblBranchStateValue.Text = SafeCell(row, "BranchState");
            _lblStatusTextValue.Text = SafeCell(row, "StatusText");
        }

        private void SetConnectionState(bool connected, string text)
        {
            _btnConnect.Enabled = !connected;
            _btnDisconnect.Enabled = connected;

            _lblConnectionStatusValue.Text = text;
            _lblConnectionStatusValue.ForeColor =
                connected
                    ? Color.FromArgb(110, 220, 140)
                    : Color.FromArgb(220, 120, 120);
        }

        private void ClearSelectedRacerDetails()
        {
            _lblSelectedRacerValue.Text = "-";
            _lblSelectedSessionValue.Text = "-";
            _lblSelectedMapValue.Text = "-";
            _lblSelectedPositionValue.Text = "-";
            _lblLastConfirmedValue.Text = "-";
            _lblTargetNodeValue.Text = "-";
            _lblPendingSplitValue.Text = "-";
            _lblBranchStateValue.Text = "-";
            _lblStatusTextValue.Text = "-";
        }

        private void AppendLog(string message)
        {
            if (_txtLog == null)
                return;

            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

            if (_txtLog.TextLength > 0)
                _txtLog.AppendText(Environment.NewLine);

            _txtLog.AppendText(line);
        }

        private static bool TryParseSessionFilter(string text, out int? sessionFilter)
        {
            sessionFilter = null;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (int.TryParse(text, out int parsed))
            {
                sessionFilter = parsed;
                return true;
            }

            return false;
        }

        private static string NormalizeMapText(object? mapValue)
        {
            if (mapValue == null)
                return "-";

            return mapValue.ToString() ?? "-";
        }

        private static string BuildBranchStateText(RacerRuntimeState racer)
        {
            if (!racer.BranchLocked)
                return "-";

            string root = racer.ActiveBranchRootNode?.Label ?? "Split";
            string entry = racer.ActiveBranchEntryNode?.Label ?? "Entry";
            return $"{root} -> {entry}";
        }

        private static string GetSnapshotColorHex(BeetleRankUserSnapshot userSnapshot)
        {
            string[] possiblePropertyNames =
            {
                "ColorHex",
                "Color",
                "MarkerColorHex",
                "MarkerColor"
            };

            Type snapshotType = userSnapshot.GetType();

            foreach (string propertyName in possiblePropertyNames)
            {
                var property = snapshotType.GetProperty(propertyName);
                if (property == null)
                    continue;

                object? value = property.GetValue(userSnapshot);
                string? text = value?.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return "#FFFFFF";
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static Label MakeLabel(string text, int left, int top, int width)
        {
            return new Label
            {
                Left = left,
                Top = top,
                Width = width,
                Height = 20,
                Text = text,
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
        }

        private static void AddDetailRow(
            Control parent,
            string title,
            ref int y,
            out Label valueLabel,
            int valueHeight = 22)
        {
            var titleLabel = new Label
            {
                Left = 0,
                Top = y,
                Width = 280,
                Height = 20,
                Text = title,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold)
            };
            parent.Controls.Add(titleLabel);

            y += 20;

            valueLabel = new Label
            {
                Left = 0,
                Top = y,
                Width = 280,
                Height = valueHeight,
                Text = "-",
                ForeColor = Color.White,
                AutoEllipsis = true
            };
            parent.Controls.Add(valueLabel);

            y += valueHeight + 12;
        }

        private static string SafeCell(DataGridViewRow row, string columnName)
        {
            object? value = row.Cells[columnName].Value;
            return value?.ToString() ?? "-";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                _overlayWebServer?.Dispose();
            }
            catch
            {
            }

            base.OnFormClosed(e);
        }

        private sealed class ThemeListItem
        {
            public string DisplayName { get; }
            public string FileName { get; }
            public string FilePath { get; }

            public ThemeListItem(string displayName, string fileName, string filePath)
            {
                DisplayName = displayName;
                FileName = fileName;
                FilePath = filePath;
            }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
