using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.IO;

namespace CSharp_ImGui_Client
{
    /// <summary>
    /// Handles ADB discovery, port forwarding, .so deployment, injection
    /// into the Free Fire process running on BlueStacks, and mem_server deployment.
    /// </summary>
    public sealed class AdbManager
    {
        private const int BackendPort = 7777;
        private const int EspMemServerPort = 5556;
        private const string BstkSuPath = "/boot/android/android/system/xbin/bstk/su";

        private string _adbPath;
        private string _deviceSerial;
        private string _suPrefix;

        /// <summary>Log callback for UI integration.</summary>
        public Action<string> OnLog;

        private void Log(string msg) { if (OnLog != null) OnLog(msg); }

        // ───────────────────────────────────────────────────────────────
        //  ADB PATH RESOLUTION
        // ───────────────────────────────────────────────────────────────

        public string GetAdbPath()
        {
            if (_adbPath != null) return _adbPath;

            string localAdb = Path.Combine(AppContext.BaseDirectory, "adb.exe");
            if (File.Exists(localAdb)) { _adbPath = localAdb; return _adbPath; }

            string sdkAdb = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Android\Sdk\platform-tools\adb.exe");

            _adbPath = File.Exists(sdkAdb) ? sdkAdb : "adb.exe";
            return _adbPath;
        }

        // ───────────────────────────────────────────────────────────────
        //  LIB PATH RESOLUTION
        // ───────────────────────────────────────────────────────────────

        public static string ResolveAndroidLib(string fileName)
        {
            string exeDir = AppContext.BaseDirectory;
            string root = FindWorkspaceRoot(exeDir);

            string[] candidates =
            {
                Path.Combine(exeDir, "libs", "android", "armeabi-v7a", fileName),
                Path.Combine(exeDir, fileName),
                Path.Combine(root, "lib_standalone", "libs", "armeabi-v7a", fileName),
                Path.Combine(root, "CSharp_ImGui_Client", "libs", "android", "armeabi-v7a", fileName),
                Path.Combine(root, "app", "src", "main", "jniLibs", "armeabi-v7a", fileName),
                Path.Combine(root, "app", "src", "main", "jniLibs", fileName),
            };

            foreach (string c in candidates)
                if (File.Exists(c)) return Path.GetFullPath(c);

            return Path.Combine(exeDir, "libs", "android", "armeabi-v7a", fileName);
        }

        private static string ExtractEmbeddedLib(string fileName, string outDir)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                string resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
                if (resName == null) return Path.Combine(outDir, fileName);

                string outPath = Path.Combine(outDir, fileName);
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream != null)
                {
                    using var fs = new System.IO.FileStream(outPath, System.IO.FileMode.Create);
                    stream.CopyTo(fs);
                }
                return outPath;
            }
            catch { return Path.Combine(outDir, fileName); }
        }

        private static string FindWorkspaceRoot(string startDir)
        {
            string dir = startDir;
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, "lib_standalone")) ||
                    Directory.Exists(Path.Combine(dir, "CSharp_ImGui_Client")))
                    return dir;
                string parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
            return startDir;
        }

        // ───────────────────────────────────────────────────────────────
        //  BLUESTACKS PORT DISCOVERY
        // ───────────────────────────────────────────────────────────────

        private static IEnumerable<int> GetBlueStacksAdbPorts()
        {
            var ports = new HashSet<int>();
            string[] registryKeys =
            {
                @"SOFTWARE\BlueStacks_msi5",
                @"SOFTWARE\BlueStacks_nxt",
                @"SOFTWARE\BlueStacks"
            };

            foreach (string keyPath in registryKeys)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                    string userDir = key?.GetValue("UserDefinedDir") as string;
                    if (string.IsNullOrWhiteSpace(userDir)) continue;

                    string confPath = Path.Combine(userDir, "bluestacks.conf");
                    if (!File.Exists(confPath)) continue;

                    foreach (string line in File.ReadAllLines(confPath))
                    {
                        var m = Regex.Match(line, @"adb_port=""(\d+)""");
                        if (m.Success && int.TryParse(m.Groups[1].Value, out int p))
                            ports.Add(p);
                    }
                }
                catch { }
            }

            // BlueStacks common ADB ports
            foreach (int p in new[] { 5555, 5554, 5565, 5575, 5585, 5595, 62001, 62025, 62026, 62027 })
                ports.Add(p);

            return ports;
        }

        // ───────────────────────────────────────────────────────────────
        //  DEVICE DISCOVERY
        // ───────────────────────────────────────────────────────────────

        private static List<string> ParseAdbDevices(string devicesOut)
        {
            var devices = new List<string>();
            foreach (string line in devicesOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.Contains("\tdevice", StringComparison.Ordinal))
                    devices.Add(line.Split('\t')[0].Trim());
            }
            return devices;
        }

        private bool HasAdbDevice(string devicesOut)
        {
            foreach (string line in devicesOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.Contains("\tdevice", StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private bool DeviceHasBstkSu(string adbPath, string serial)
        {
            string prev = _deviceSerial;
            _deviceSerial = serial;
            try
            {
                RunAdb(adbPath, $"shell \"test -x {BstkSuPath} && echo BSTK_OK\"", out string probe);
                return probe.Contains("BSTK_OK", StringComparison.Ordinal);
            }
            finally { _deviceSerial = prev; }
        }

        private string SelectPreferredDevice(string adbPath, List<string> devices)
        {
            if (devices.Count == 1) return devices[0];

            foreach (string d in devices)
                if (DeviceHasBstkSu(adbPath, d)) { Log($"[ADB] BlueStacks root device: {d}"); return d; }

            foreach (string d in devices)
                if (d.StartsWith("127.0.0.1:", StringComparison.Ordinal)) return d;

            foreach (string d in devices)
                if (d.StartsWith("emulator-", StringComparison.Ordinal)) return d;

            return devices[0];
        }

        private bool EnsureDevice(string adb)
        {
            // Try existing devices first
            RunAdb(adb, "start-server", out _);
            if (RunAdb(adb, "devices", out string initial) && HasAdbDevice(initial))
            {
                var existing = ParseAdbDevices(initial);
                _deviceSerial = SelectPreferredDevice(adb, existing);
                Log($"[ADB] Device found: {_deviceSerial}");
                return true;
            }

            // Try connecting to BlueStacks ports
            Log("[ADB] No device — trying BlueStacks ADB ports...");
            foreach (int port in GetBlueStacksAdbPorts())
            {
                RunAdb(adb, $"connect 127.0.0.1:{port}", out string connectOut);
                if (connectOut.Contains("connected", StringComparison.OrdinalIgnoreCase))
                {
                    Log($"[ADB] Linked emulator at 127.0.0.1:{port}");
                    break;
                }
            }

            Thread.Sleep(500);

            if (RunAdb(adb, "devices", out string afterConnect) && HasAdbDevice(afterConnect))
            {
                var devices = ParseAdbDevices(afterConnect);
                _deviceSerial = SelectPreferredDevice(adb, devices);
                Log($"[ADB] Device selected: {_deviceSerial}");
                return true;
            }

            Log("[ADB Error] No emulator found. Open BlueStacks → Settings → Advanced → enable Android Debug Bridge.");
            return false;
        }

        // ───────────────────────────────────────────────────────────────
        //  SU PREFIX DETECTION
        // ───────────────────────────────────────────────────────────────

        private string DetectSuPrefix(string adb)
        {
            if (_suPrefix != null) return _suPrefix;

            // BlueStacks bstk/su is highest priority
            string[] candidates = {
                BstkSuPath,
                "/system/xbin/bstk/su",
                "su",
                "/sbin/su",
                "/system/xbin/su",
                "/system/bin/su"
            };

            foreach (var su in candidates)
            {
                if (RunDeviceAdb(adb, $"shell {su} -c id", out string output) && output.Contains("uid=0"))
                {
                    _suPrefix = su;
                    return _suPrefix;
                }
            }

            _suPrefix = "su";
            return _suPrefix;
        }

        // ───────────────────────────────────────────────────────────────
        //  MAIN DEPLOY PIPELINE
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Full auto-deploy: find ADB device, forward port, push libs, inject into Free Fire, wait for backend.
        /// </summary>
        public bool AutoDeployAndInject()
        {
            try
            {
                string adb = GetAdbPath();
                Log($"[ADB] Using: {adb}");

                _deviceSerial = null;
                bool deviceFound = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    if (EnsureDevice(adb)) { deviceFound = true; break; }
                    Log($"[ADB] Retry {attempt}/3...");
                    Thread.Sleep(2000);
                }
                if (!deviceFound) return false;

                string su = DetectSuPrefix(adb);
                Log($"[ADB] Root prefix: {su}");

                // Remove old forwards, set new one
                RunDeviceAdb(adb, "forward --remove-all", out _);
                Log($"[ADB] Port forward tcp:{BackendPort}...");
                if (!RunDeviceAdb(adb, $"forward tcp:{BackendPort} tcp:{BackendPort}", out _))
                {
                    Log("[ADB Error] Port forward failed.");
                    return false;
                }

                string libBackend = ResolveAndroidLib("libNXBackend.so");
                string libInjector = ResolveAndroidLib("libinjectEmulator.so");

                Log($"[ADB] Backend: {libBackend}");
                Log($"[ADB] Injector: {libInjector}");

                if (!File.Exists(libBackend))
                {
                    Log("[ADB Error] libNXBackend.so not found. Run lib_standalone\\build.bat first.");
                    return false;
                }
                if (!File.Exists(libInjector))
                {
                    Log("[ADB Error] libinjectEmulator.so not found.");
                    return false;
                }

                // Check if game is running, launch if not
                bool gameRunning = IsGameRunning(adb);
                if (!gameRunning)
                {
                    Log("[ADB] Launching Free Fire...");
                    RunDeviceAdb(adb, "shell monkey -p com.dts.freefireth -c android.intent.category.LAUNCHER 1", out _);
                    RunDeviceAdb(adb, "shell monkey -p com.dts.freefiremax -c android.intent.category.LAUNCHER 1", out _);
                    Log("[ADB] Waiting 12s for game to load...");
                    Thread.Sleep(12000);
                }
                else
                {
                    Log("[ADB] Game already running.");
                    Thread.Sleep(2000);
                }

                // Push files
                Log("[ADB] Pushing injector...");
                RunDeviceAdb(adb, $"push \"{libInjector}\" /data/local/tmp/libinject", out _);

                Log("[ADB] Pushing libNXBackend.so...");
                RunDeviceAdb(adb, $"push \"{libBackend}\" /data/local/tmp/libNXBackend.so", out _);

                // Set permissions
                Log("[ADB] Setting permissions...");
                RunDeviceAdb(adb, $"shell {su} -c \"chmod 755 /data/local/tmp/libinject /data/local/tmp/libNXBackend.so\"", out _);

                // Inject with -open -dl_memfd flags (BlueStacks style)
                Log("[ADB] Injecting backend into Free Fire...");
                bool injected = false;
                string[] packages = { "com.dts.freefireth", "com.dts.freefiremax" };
                string[] injMethods = { "-open -dl_memfd", "-open", "" };

                for (int attempt = 1; attempt <= 3 && !injected; attempt++)
                {
                    foreach (string pkg in packages)
                    {
                        if (injected) break;
                        foreach (string method in injMethods)
                        {
                            if (injected) break;
                            string methodArg = string.IsNullOrEmpty(method) ? "" : $" {method}";
                            string cmd = $"shell {su} -c \"/data/local/tmp/libinject -pkg {pkg} -lib /data/local/tmp/libNXBackend.so{methodArg}\"";

                            Log($"[ADB] Inject attempt {attempt}: {pkg} {method}");
                            RunDeviceAdb(adb, cmd, out string output);

                            if (output.Contains("Injection succeeded", StringComparison.OrdinalIgnoreCase) ||
                                output.Contains("already injected", StringComparison.OrdinalIgnoreCase) ||
                                output.Contains("success", StringComparison.OrdinalIgnoreCase))
                            {
                                injected = true;
                                Log($"[ADB] Injection succeeded via {pkg} {method}!");
                            }
                        }
                    }

                    if (!injected && attempt < 3)
                    {
                        Log($"[ADB] Attempt {attempt} failed, waiting 3s...");
                        Thread.Sleep(3000);
                    }
                }

                if (!injected)
                {
                    Log("[ADB Error] Injection failed. Ensure game is open and root is available (BlueStacks ADB must be enabled).");
                    return false;
                }

                // Wait for backend port
                Log($"[ADB] Waiting for backend on port {BackendPort}...");
                if (!WaitForBackendPort(adb, BackendPort, 25000))
                {
                    Log("[ADB Warning] Port not detected, trying direct connect anyway...");
                    Thread.Sleep(3000);
                }

                // Re-establish port forward
                RunDeviceAdb(adb, $"forward --remove tcp:{BackendPort}", out _);
                RunDeviceAdb(adb, $"forward tcp:{BackendPort} tcp:{BackendPort}", out _);

                Log("[ADB] Backend is ready!");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[ADB Error] {ex.Message}");
                return false;
            }
        }

        // ───────────────────────────────────────────────────────────────
        //  MEM_SERVER (External ESP)
        // ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Deploy and start mem_server for External ESP, then forward port 5556.
        /// </summary>
        public bool DeployAndStartMemServer()
        {
            try
            {
                string adb = GetAdbPath();
                _deviceSerial = null;

                if (!EnsureDevice(adb)) return false;

                string su = DetectSuPrefix(adb);
                string remotePath = "/data/local/tmp/mem_server";

                // Resolve mem_server binary
                string localMemServer = FindMemServer();
                if (localMemServer == null)
                {
                    Log("[ESP] mem_server not found next to exe or in project. Cannot start External ESP.");
                    return false;
                }

                Log($"[ESP] mem_server: {localMemServer}");

                // Check if mem_server already exists and is executable
                RunDeviceAdb(adb, $"shell stat -c %s {remotePath}", out string sizeOut);
                bool exists = long.TryParse(sizeOut.Trim(), out long size) && size > 1000;

                if (!exists)
                {
                    Log("[ESP] Pushing mem_server...");
                    RunDeviceAdb(adb, $"push \"{localMemServer}\" {remotePath}", out _);
                    RunDeviceAdb(adb, $"shell {su} -c \"chmod +x {remotePath}\"", out _);
                    Thread.Sleep(1500);
                }

                // Kill old instance
                RunDeviceAdb(adb, $"shell {su} -c \"pkill mem_server\"", out _);
                Thread.Sleep(500);

                // Start mem_server in background
                Log("[ESP] Starting mem_server...");
                var psi = new ProcessStartInfo(adb, $"-s {_deviceSerial} shell \"{su} -c {remotePath} &\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
                Thread.Sleep(2000);

                // Forward port 5556 for abstract socket esp_socket
                RunDeviceAdb(adb, $"forward --remove tcp:{EspMemServerPort}", out _);
                RunDeviceAdb(adb, $"forward tcp:{EspMemServerPort} localabstract:esp_socket", out _);

                Log($"[ESP] mem_server ready on port {EspMemServerPort}!");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[ESP Error] {ex.Message}");
                return false;
            }
        }

        public void StopMemServer()
        {
            try
            {
                string adb = GetAdbPath();
                string su = DetectSuPrefix(adb);
                RunDeviceAdb(adb, $"shell {su} -c \"pkill mem_server\"", out _);
                RunDeviceAdb(adb, $"forward --remove tcp:{EspMemServerPort}", out _);
                Log("[ESP] mem_server stopped.");
            }
            catch { }
        }

        /// <summary>
        /// Find mem_server binary: next to exe, or in source tree.
        /// </summary>
        private string FindMemServer()
        {
            string exeDir = AppContext.BaseDirectory;
            string root = FindWorkspaceRoot(exeDir);

            string[] candidates =
            {
                Path.Combine(exeDir, "mem_server"),
                Path.Combine(root, "CSharp_ImGui_Client", "mem_server"),
            };

            foreach (string c in candidates)
                if (File.Exists(c)) return Path.GetFullPath(c);

            // Try to extract from embedded resource
            return TryExtractEmbeddedMemServer(exeDir);
        }

        private string TryExtractEmbeddedMemServer(string outDir)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                string resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("mem_server", StringComparison.OrdinalIgnoreCase));
                if (resName == null) return null;

                string outPath = Path.Combine(outDir, "mem_server");
                using var stream = asm.GetManifestResourceStream(resName);
                using var fs = new System.IO.FileStream(outPath, System.IO.FileMode.Create);
                stream.CopyTo(fs);
                Log($"[ESP] Extracted embedded mem_server to {outPath}");
                return outPath;
            }
            catch { return null; }
        }

        public ulong GetIl2CppBaseAddress(string pkg)
        {
            try
            {
                string adb = GetAdbPath();
                string su = DetectSuPrefix(adb);

                // Get PID
                RunDeviceAdb(adb, $"shell ps | grep -w {pkg}", out string psOut);
                if (string.IsNullOrWhiteSpace(psOut)) return 0;

                string pid = null;
                foreach (string line in psOut.Split('\n'))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2) { pid = parts[1]; break; }
                }
                if (pid == null) return 0;

                RunDeviceAdb(adb, $"shell \"{su} -c 'cat /proc/{pid}/maps | grep libil2cpp.so'\"", out string mapsOut);
                if (string.IsNullOrWhiteSpace(mapsOut)) return 0;

                string baseAddrStr = mapsOut.Split('-')[0].Trim();
                return ulong.Parse(baseAddrStr, System.Globalization.NumberStyles.HexNumber);
            }
            catch { return 0; }
        }

        public int GetGamePid(string pkg)
        {
            try
            {
                string adb = GetAdbPath();
                RunDeviceAdb(adb, $"shell ps | grep -w {pkg}", out string psOut);
                foreach (string line in psOut.Split('\n'))
                {
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int pid)) return pid;
                }
                return 0;
            }
            catch { return 0; }
        }

        // ───────────────────────────────────────────────────────────────
        //  HELPERS
        // ───────────────────────────────────────────────────────────────

        private bool IsGameRunning(string adb)
        {
            foreach (string pkg in new[] { "com.dts.freefireth", "com.dts.freefiremax" })
            {
                RunDeviceAdb(adb, $"shell pidof {pkg}", out string pidOut);
                if (!string.IsNullOrWhiteSpace(pidOut) && int.TryParse(pidOut.Trim().Split('\n')[0].Trim(), out _))
                    return true;
            }
            return false;
        }

        private bool WaitForBackendPort(string adb, int port, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string hexPort = port.ToString("X4");
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                RunDeviceAdb(adb, $"shell \"netstat -tln 2>/dev/null | grep :{port} || ss -tln 2>/dev/null | grep :{port}\"", out string portOut);
                if (portOut.Contains($":{port}", StringComparison.Ordinal)) return true;

                RunDeviceAdb(adb, "shell cat /proc/net/tcp", out string tcp);
                if (tcp.Contains(hexPort, StringComparison.OrdinalIgnoreCase)) return true;

                RunDeviceAdb(adb, "shell cat /proc/net/tcp6", out string tcp6);
                if (tcp6.Contains(hexPort, StringComparison.OrdinalIgnoreCase)) return true;

                Thread.Sleep(1500);
            }
            return false;
        }

        private bool RunDeviceAdb(string adbPath, string arguments, out string output)
        {
            string prefix = string.IsNullOrEmpty(_deviceSerial) ? "" : $"-s {_deviceSerial} ";
            return RunAdb(adbPath, prefix + arguments, out output);
        }

        private bool RunAdb(string adbPath, string arguments, out string output)
        {
            output = string.Empty;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                if (proc == null) return false;

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();

                if (!proc.WaitForExit(20000))
                {
                    try { proc.Kill(); } catch { }
                    return false;
                }

                output = stdout + stderr;
                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }
        }
    }
}
