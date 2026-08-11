using System;
using System.IO;
using System.Reflection;

namespace CSharp_ImGui_Client
{
    public static class AppPaths
    {
        public const string AndroidAbiArm64 = "arm64-v8a";
        public const string AndroidAbiArm32 = "armeabi-v7a";
        public const string AndroidAbi = AndroidAbiArm32;
        private const long MinInjectorBytes = 450_000;

        public static string AppDirectory { get; } = GetAppDirectory();

        public static string LogsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TanishRegedit",
                "Logs");

        public static string AndroidLibsDirectory =>
            Path.Combine(AppDirectory, "libs", "android", AndroidAbi);

        private static string GetAppDirectory()
        {
            string? processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                string? dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrEmpty(dir))
                    return Path.GetFullPath(dir);
            }

            return Path.GetFullPath(AppContext.BaseDirectory);
        }

        public static void EnsureNativeLibraries()
        {
            Directory.CreateDirectory(AndroidLibsDirectory);

            ExtractEmbeddedIfMissing("libNXBackend.so");

            string injectorDest = Path.Combine(AndroidLibsDirectory, "libinjectEmulator.so");
            if (!File.Exists(injectorDest))
                ExtractEmbeddedIfMissing("libinjectEmulator.so");
        }

        private static void ExtractEmbeddedIfMissing(string fileName, bool forceReplaceIfStale = false)
        {
            string dest = Path.Combine(AndroidLibsDirectory, fileName);
            if (File.Exists(dest))
            {
                if (!forceReplaceIfStale || !IsStaleNativeLib(fileName, dest))
                    return;
                try { File.Delete(dest); } catch { }
            }

            string resourceSuffix = $"NativeLibs.android.{AndroidAbi}.{fileName}";
            Assembly asm = Assembly.GetExecutingAssembly();
            string? fullName = null;
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    fullName = name;
                    break;
                }
            }

            if (fullName == null)
                return;

            using Stream? input = asm.GetManifestResourceStream(fullName);
            if (input == null)
                return;

            using FileStream output = File.Create(dest);
            input.CopyTo(output);
        }

        private static bool IsStaleNativeLib(string fileName, string dest)
        {
            if (!fileName.Equals("libinjectEmulator.so", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                return new FileInfo(dest).Length < MinInjectorBytes;
            }
            catch
            {
                return true;
            }
        }
    }
}
