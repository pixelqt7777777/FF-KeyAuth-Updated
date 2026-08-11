using System;
using System.IO;

namespace CSharp_ImGui_Client
{
    public static class LibPaths
    {
        public const string AndroidAbi = "armeabi-v7a";

        public static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "lib_standalone")) &&
                    Directory.Exists(Path.Combine(dir, "app")))
                {
                    return Path.GetFullPath(dir);
                }

                DirectoryInfo? parent = Directory.GetParent(dir);
                if (parent == null)
                    break;
                dir = parent.FullName;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        public static string ResolveAndroidLib(string fileName)
        {
            string repoRoot = FindRepoRoot();
            string exeDir = AppContext.BaseDirectory;

            string[] candidates =
            {
                Path.Combine(exeDir, "libs", "android", "armeabi-v7a", fileName),
                Path.Combine(exeDir, "libs", "android", "arm64-v8a", fileName),
                Path.Combine(exeDir, "..", "..", "..", "libs", "android", "armeabi-v7a", fileName),
                Path.Combine(exeDir, "..", "..", "..", "libs", "android", "arm64-v8a", fileName),
                Path.Combine(repoRoot, "libs", "android", "armeabi-v7a", fileName),
                Path.Combine(repoRoot, "libs", "android", "arm64-v8a", fileName),
                Path.Combine(repoRoot, "lib_standalone", "libs", "armeabi-v7a", fileName),
                Path.Combine(repoRoot, "lib_standalone", "libs", "arm64-v8a", fileName),
                Path.Combine(repoRoot, "lib_standalone", "jni", "libs", "armeabi-v7a", fileName),
                Path.Combine(repoRoot, "lib_standalone", "jni", "libs", "arm64-v8a", fileName),
                Path.Combine(repoRoot, "app", "src", "main", "jniLibs", "armeabi-v7a", fileName),
                Path.Combine(repoRoot, "app", "src", "main", "jniLibs", "arm64-v8a", fileName),
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }

            return Path.Combine(repoRoot, "libs", "android", "armeabi-v7a", fileName);
        }

        public static bool IsExtracted()
        {
            string path = ResolveAndroidLib("libNXBackend.so");
            return File.Exists(path);
        }

        public static string ResolveWindowsDll(string fileName = "Prime X-Pnlz.dll")
        {
            string repoRoot = FindRepoRoot();
            string exeDir = AppContext.BaseDirectory;

            string[] candidates =
            {
                Path.Combine(exeDir, "libs", "windows", fileName),
                Path.Combine(repoRoot, "libs", "windows", fileName),
                Path.Combine(repoRoot, "CSharp_ImGui_Client", fileName),
                Path.Combine(exeDir, fileName),
            };

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }

            return Path.Combine(repoRoot, "libs", "windows", fileName);
        }
    }
}
