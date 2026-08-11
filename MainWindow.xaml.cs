using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;


namespace CSharp_ImGui_Client
{
    public partial class MainWindow : Window
    {
        private readonly SocketClient client = new();
        private readonly EspOverlay _espOverlay = null;
        private bool isConnecting;
        private bool _disposed;
        private bool shouldPoll;

        private bool _suppressToggle;
        private bool _extEspRunning;
        private EspOverlay _extEspOverlay;

        private DispatcherTimer pollTimer;
        private ToggleButton _activeNavBtn;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // Set up poll timer (WPF style)
                pollTimer = new DispatcherTimer(DispatcherPriority.Background);
                pollTimer.Interval = TimeSpan.FromMilliseconds(1000);
                pollTimer.Tick += PollTimer_Tick;

                Loaded += MainWindow_Loaded;

                // Wire UI features
                WireNav();
                WireAim();
                WireEsp();
                WireMisc();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("crashlog.txt", "Constructor Exception:\r\n" + ex.ToString());
                throw;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load credentials
                if (!AuthSession.IsAuthenticated || string.IsNullOrWhiteSpace(AuthSession.Username))
                {
                    ProfileUserTxt.Text = "GUEST";
                    ProfileDaysTxt.Text = "";
                }
                else
                {
                    ProfileUserTxt.Text = (AuthSession.Username ?? "USER").ToUpper();
                    ProfileDaysTxt.Text = (AuthSession.DaysLeft ?? "ACTIVE").ToUpper();
                }

                client.OnConnectionChanged += OnConnectionChanged;
                client.OnMatchDataReceived += OnMatchDataReceived;
                client.OnPlayersReceived += OnPlayersReceived;
                client.OnDebugMessage += OnDebugMessage;

                // Select aimbot tab by default
                SetActiveNav(NavAimbotBtn);
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("crashlog.txt", "Loaded Event Exception:\r\n" + ex.ToString());
                throw;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_disposed) return;
            _disposed = true;

            StopExternalEsp();

            client.OnConnectionChanged -= OnConnectionChanged;
            client.OnMatchDataReceived -= OnMatchDataReceived;
            client.OnPlayersReceived -= OnPlayersReceived;
            client.OnDebugMessage -= OnDebugMessage;
            client.StopPolling();
            client.Disconnect();
            _espOverlay?.UpdatePlayers(Array.Empty<PlayerData>());
            _espOverlay?.Dispose();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #region Navigation
        private void WireNav()
        {
            // Default styling for nav buttons is ModernButton.
            // We highlight the active one.
        }

        private void NavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton btn)
            {
                SetActiveNav(btn);
            }
        }

        private void SetActiveNav(ToggleButton btn)
        {
            if (btn == null) return;

            if (_activeNavBtn != null && _activeNavBtn != btn)
            {
                _activeNavBtn.IsChecked = false;
            }

            _activeNavBtn = btn;
            _activeNavBtn.IsChecked = true;

            // Toggle panels
            string panelName = btn.Tag.ToString();
            BridgePanel.Visibility = panelName == "BridgePanel" ? Visibility.Visible : Visibility.Collapsed;
            AimbotPanel.Visibility = panelName == "AimbotPanel" ? Visibility.Visible : Visibility.Collapsed;
            EspPanel.Visibility = panelName == "EspPanel" ? Visibility.Visible : Visibility.Collapsed;
            MiscPanel.Visibility = panelName == "MiscPanel" ? Visibility.Visible : Visibility.Collapsed;

            // Update Module Header
            ModuleTitleTxt.Text = panelName == "BridgePanel" ? "BRIDGE CONTROL HUB"
                                : panelName == "AimbotPanel" ? "AIMBOT ENGINE CONTROL"
                                : panelName == "EspPanel" ? "ESP MATRIX OVERLAYS"
                                : "MISC UTILITIES MODULE";
        }
        #endregion

        #region Connection
        private async void ConnectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (client.Connected)
            {
                StopPolling();
                client.StopPolling();
                client.Disconnect();
                _espOverlay?.UpdatePlayers(Array.Empty<PlayerData>());
                SetStatus(false);
                ConnectBtn.Content = "CONNECT";
                return;
            }
            await ConnectToServer();
        }

        private async Task ConnectToServer()
        {
            if (isConnecting || client.Connected) return;
            isConnecting = true;
            SetStatus("busy");
            ConnectBtn.Content = "CONNECTING...";

            try
            {
                if (!LibPaths.IsExtracted())
                {
                    SetStatus(false);
                    ConnectBtn.Content = "CONNECT";
                    Log("libNXBackend.so not found.");
                    isConnecting = false;
                    return;
                }

                Log("Deploying backend to emulator...");
                var adb = new AdbManager { OnLog = msg => Log(msg) };
                bool deployed = await Task.Run(() => adb.AutoDeployAndInject());
                if (!deployed)
                {
                    SetStatus(false);
                    ConnectBtn.Content = "CONNECT";
                    Log("Auto-deploy failed.");
                    isConnecting = false;
                    return;
                }

                int port = 7777;
                if (!int.TryParse(PortBox.Text.Trim(), out port)) port = 7777;
                string host = IpBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

                client.Log("Connecting to " + host + ":" + port + "...");

                bool connected = await client.Connect(host, port);
                if (!connected)
                {
                    SetStatus(false);
                    ConnectBtn.Content = "CONNECT";
                    isConnecting = false;
                    client.Log("Connection failed.");
                    return;
                }

                SetStatus(true);
                ConnectBtn.Content = "DISCONNECT";
                client.Log("Connected!");
                await Task.Delay(100);

                // Setup screen overlay sizes
                int sw = 1920;
                int sh = 1080;
                client.Send(new Request { Mode = 1, Boolean = true, Value = 0, ScreenWidth = sw, ScreenHeight = sh });
                client.Send(new Request { Mode = 3, Boolean = true, Value = 0, ScreenWidth = sw, ScreenHeight = sh });
                client.Send(new Request { Mode = 56, Boolean = true, Value = 0, ScreenWidth = sw, ScreenHeight = sh });
                client.Log($"Sent Init, EnableESP, SpoofName. Viewport: {sw}x{sh}");

                StartPolling();
            }
            catch (Exception ex)
            {
                SetStatus(false);
                ConnectBtn.Content = "CONNECT";
                client.Log("Connect failed: " + ex.Message);
            }
            finally
            {
                isConnecting = false;
            }
        }

        private void SetStatus(bool online)
        {
            if (online)
            {
                StatusTxt.Text = "Status: ONLINE";
                StatusDot.Fill = (Brush)FindResource("AccentGreen");
                StatusGlow.Fill = (Brush)FindResource("AccentGreen");
                if (ConnStateTxt != null) ConnStateTxt.Text = "ONLINE";
            }
            else
            {
                StatusTxt.Text = "Status: OFF";
                StatusDot.Fill = (Brush)FindResource("AccentRed");
                StatusGlow.Fill = (Brush)FindResource("AccentRed");
                if (ConnStateTxt != null) ConnStateTxt.Text = "OFFLINE";
            }
        }

        private void SetStatus(string mode)
        {
            if (mode == "busy")
            {
                StatusTxt.Text = "Status: CONNECTING...";
                StatusDot.Fill = (Brush)FindResource("AccentOrange");
                StatusGlow.Fill = (Brush)FindResource("AccentOrange");
                if (ConnStateTxt != null) ConnStateTxt.Text = "CONNECTING";
            }
        }
        #endregion

        #region Features Setup
        private void WireAim()
        {
            WireAimToggle(trCoverHitExtend, 889, "Silent Kill");
            WireAimToggle(trGlobalSpeedKey, 890, "Global Speed Key");
            WireAimToggle(trGlobalSpeed, 892, "Global Speed");
            WireAimToggle(trFastFireKey, 893, "Fast Fire Key");
            WireAimToggle(trFastFire, 894, "Fast Fire");
            WireAimToggle(trFastReload, 7002, "Fast Reload");
            WireAimToggle(trAimLock, 500, "Aim Lock");
        }

        private void WireEsp()
        {
            trEnableAll.CheckedChanged += (_, _) =>
            {
                if (_suppressToggle) return;
                bool enabled = trEnableAll.Checked;
                ToggleFeature(3, enabled, "Enable ESP");
            };

            WireEspToggle(trEspLine, "ESP Line");
            WireEspToggle(trEspBox, "ESP Box");
            WireEspToggle(trEspHealth, "ESP Health");
            WireEspToggle(trEspName, "ESP Name");
            WireEspToggle(trEspDistance, "ESP Distance");

            WireAimToggle(trGhostOn, 149, "Ghost On");
            WireAimToggle(trHideDamage, 527, "Hide Damage");

            if (_espOverlay != null)
            {
                _espOverlay.EspBox = trEspBox.Checked;
                _espOverlay.EspName = trEspName.Checked;
                _espOverlay.EspHealth = trEspHealth.Checked;
                _espOverlay.EspDistance = trEspDistance.Checked;
                _espOverlay.EspLine = trEspLine.Checked;
            }
        }

        private void WireMisc()
        {
            WireAimToggle(trSpeedRun, 507, "Speed Run");
            WireAimToggle(trFlyHackOp, 5657, "Fly Hack OP");
            WireAimToggle(trFlyX80, 5194, "Fly x80");
            WireAimToggle(trFlyX40, 5193, "Fly x40");
            WireAimToggle(trSpoofName, 56, "Spoof Name");
            WireAimToggle(trAutoJump, 7865, "Auto Jump");
            WireAimToggle(trAutoRevive, 7896, "Auto Revive");
            WireAimToggle(trMedikitRun, 13, "Medikit Run");
            WireAimToggle(trTeleKill, 19, "Tele Kill");
            WireAimToggle(trResetGuest, 12, "Reset Guest");
        }

        private void WireAimToggle(TerminalFeatureRow toggle, int mode, string friendlyName)
        {
            toggle.CheckedChanged += (_, _) =>
            {
                if (_suppressToggle) return;
                ToggleFeature(mode, toggle.Checked, friendlyName);
            };
        }

        private void WireEspToggle(TerminalFeatureRow toggle, string friendlyName)
        {
            toggle.CheckedChanged += (_, _) =>
            {
                if (_suppressToggle) return;
                if (_espOverlay != null)
                {
                    _espOverlay.EspBox = trEspBox.Checked;
                    _espOverlay.EspName = trEspName.Checked;
                    _espOverlay.EspHealth = trEspHealth.Checked;
                    _espOverlay.EspDistance = trEspDistance.Checked;
                    _espOverlay.EspLine = trEspLine.Checked;
                }
                if (_extEspOverlay != null)
                {
                    _extEspOverlay.EspBox = trEspBox.Checked;
                    _extEspOverlay.EspName = trEspName.Checked;
                    _extEspOverlay.EspHealth = trEspHealth.Checked;
                    _extEspOverlay.EspDistance = trEspDistance.Checked;
                    _extEspOverlay.EspLine = trEspLine.Checked;
                }
                if (toggle.Checked)
                    EnsureEspMasterEnabled();
            };
        }

        private void EnsureEspMasterEnabled()
        {
            if (!trEnableAll.Checked)
            {
                _suppressToggle = true;
                trEnableAll.Checked = true;
                _suppressToggle = false;
                ToggleFeature(3, true, "Enable ESP");
            }
        }

        private void ToggleFeature(int mode, bool enabled, string friendlyName)
        {
            if (!client.Connected)
            {
                Log($"Not connected - cannot toggle {friendlyName}.");
                return;
            }

            try
            {
                int sw = 1920;
                int sh = 1080;
                var req = new Request { Mode = mode, Boolean = enabled, Value = 0, ScreenWidth = sw, ScreenHeight = sh };
                client.Send(req);
                string state = enabled ? "ON" : "OFF";
                Log($"{friendlyName} set to {state}");
            }
            catch (Exception ex)
            {
                Log($"Toggle {friendlyName} failed: {ex.Message}");
            }
        }
        #endregion

        #region External ESP Setup
        private async void StartExtEspBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_extEspRunning)
            {
                StopExternalEsp();
                return;
            }

            try
            {
                StartExtEspBtn.IsEnabled = false;
                Log("Starting external ESP...");

                var processes = Process.GetProcessesByName("HD-Player");
                if (processes.Length == 0)
                {
                    Log("Emulator not found. Start BlueStacks first.");
                    return;
                }

                var process = processes[0];
                Core.Handle = process.MainWindowHandle;

                var adb = new AdbManager { OnLog = msg => Log(msg) };
                bool memServerOk = await Task.Run(() => adb.DeployAndStartMemServer());
                if (!memServerOk)
                {
                    Log("Failed to start mem_server.");
                    return;
                }

                string pkg = "com.dts.freefireth";
                int currentPid = await Task.Run(() => adb.GetGamePid(pkg));
                if (currentPid == 0)
                {
                    pkg = "com.dts.freefiremax";
                    currentPid = await Task.Run(() => adb.GetGamePid(pkg));
                }
                if (currentPid == 0)
                {
                    Log("Game process not found. Open Free Fire first.");
                    return;
                }

                ulong moduleAddr = await Task.Run(() => adb.GetIl2CppBaseAddress(pkg));
                if (moduleAddr == 0)
                {
                    Log("libil2cpp.so not found in game process.");
                    return;
                }
                Offsets.Il2Cpp = moduleAddr;
                Log($"libil2cpp.so base: 0x{moduleAddr:X}");

                var readClient = new SocketClientESP();
                var writeClient = new SocketClientESP();
                if (!readClient.Connect("127.0.0.1", 5556) || !writeClient.Connect("127.0.0.1", 5556))
                {
                    Log("Failed to connect to mem_server on port 5556.");
                    return;
                }

                SocketMemory.Initialize(readClient, writeClient, currentPid);
                Log("External ESP connected to mem_server!");

                Core.Running = true;
                Core.Entities = new();
                new Thread(Data.Work) { IsBackground = true, Priority = ThreadPriority.Highest }.Start();

                _extEspOverlay = new EspOverlay();
                _extEspOverlay.EspBox = trEspBox.Checked;
                _extEspOverlay.EspName = trEspName.Checked;
                _extEspOverlay.EspHealth = trEspHealth.Checked;
                _extEspOverlay.EspDistance = trEspDistance.Checked;
                _extEspOverlay.EspLine = trEspLine.Checked;
                _ = Task.Run(async () => await _extEspOverlay.Start());

                _extEspRunning = true;
                StartExtEspBtn.Content = "STOP EXTERNAL ESP";
                StartExtEspBtn.Background = new SolidColorBrush(Color.FromRgb(30, 130, 30));
                Log("TANISH REGEDIT ESP STARTED");
            }
            catch (Exception ex)
            {
                Log($"External ESP error: {ex.Message}");
            }
            finally
            {
                StartExtEspBtn.IsEnabled = true;
            }
        }

        private void StopExternalEsp()
        {
            Core.Running = false;
            SocketMemory.Stop();
            _extEspOverlay?.Close();
            _extEspOverlay = null;
            _extEspRunning = false;
            StartExtEspBtn.Content = "START EXTERNAL ESP";
            StartExtEspBtn.Background = (Brush)FindResource("AccentRed");
            Log("External ESP stopped.");
        }
        #endregion

        #region Polling and Client Callbacks
        private void StartPolling()
        {
            if (shouldPoll) return;
            shouldPoll = true;
            pollTimer.Start();
        }

        private void StopPolling()
        {
            shouldPoll = false;
            pollTimer.Stop();
        }

        private async void PollTimer_Tick(object sender, EventArgs e)
        {
            if (!client.Connected) return;

            try
            {
                int sw = 1920;
                int sh = 1080;
                var response = await client.SendPoll(99, sw, sh);
                if (response == null) return;
                ApplyPollResponse(response.Value);
            }
            catch { }
        }

        private void OnConnectionChanged(object sender, bool connected)
        {
            if (!Dispatcher.CheckAccess())
            {
                try { Dispatcher.BeginInvoke(() => OnConnectionChanged(sender, connected)); } catch { }
                return;
            }
            if (!connected)
            {
                StopPolling();
                _espOverlay?.UpdatePlayers(Array.Empty<PlayerData>());
                SetStatus(false);
                ConnectBtn.Content = "CONNECT";
                client.Log("Disconnected.");
            }
        }

        private void OnMatchDataReceived(object sender, MatchData e)
        {
            if (!Dispatcher.CheckAccess())
            {
                try { Dispatcher.BeginInvoke(() => OnMatchDataReceived(sender, e)); } catch { }
                return;
            }
            MatchStateTxt.Text = "Match: " + e.MatchState;
            RemainingTimeTxt.Text = "Timer: " + e.RemainingTime;
            PlayerCountTxt.Text = "Entities: " + e.PlayerCount;
        }

        private void OnPlayersReceived(object sender, PlayerData[] players)
        {
            if (!Dispatcher.CheckAccess())
            {
                try { Dispatcher.BeginInvoke(() => OnPlayersReceived(sender, players)); } catch { }
                return;
            }
            UpdatePlayerList(players);
            _espOverlay?.UpdatePlayers(players);
        }

        private void OnDebugMessage(object sender, string msg)
        {
            Log(msg);
        }

        private void ApplyPollResponse(Response response)
        {
            MatchStateTxt.Text = "Match: " + (response.MatchAlive == 1 ? "In Game" : "Lobby");
            RemainingTimeTxt.Text = "Timer: " + response.RemainingTimeSeconds;
            PlayerCountTxt.Text = "Entities: " + response.PlayerCount;
        }

        private void UpdatePlayerList(PlayerData[] players)
        {
            PlayersListView.Items.Clear();
            if (players == null || players.Length == 0) return;

            foreach (var p in players)
            {
                if (string.IsNullOrEmpty(p.Name)) continue;
                string type = p.IsBot ? "Bot" : "Player";
                
                // Add anonymous item matching columns
                PlayersListView.Items.Add(new PlayerRowItem
                {
                    Name = p.Name,
                    Distance = ((int)p.Distance).ToString(),
                    Health = ((int)p.Health).ToString(),
                    Type = type
                });
            }
        }

        private void Log(string msg)
        {
            if (LogBox == null) return;
            if (!Dispatcher.CheckAccess())
            {
                try { Dispatcher.BeginInvoke(() => Log(msg)); } catch { }
                return;
            }

            LogBox.AppendText(msg + Environment.NewLine);
            LogBox.ScrollToEnd();
        }

        public class PlayerRowItem
        {
            public string Name { get; set; }
            public string Distance { get; set; }
            public string Health { get; set; }
            public string Type { get; set; }
        }
        #endregion
    }
}
