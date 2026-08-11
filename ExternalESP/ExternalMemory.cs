using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CSharp_ImGui_Client.ExternalESP
{
    /// <summary>External process memory read/write and AoB scan (HD-Player / BlueStacks).</summary>
    public class ExternalMemory
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, IntPtr nSize, IntPtr lpNumberOfBytesWritten);

        public struct PatternData
        {
            public byte[] pattern { get; set; }
            public byte[] mask { get; set; }
        }

        public struct MemoryPage
        {
            public IntPtr Start;
            public int Size;

            public MemoryPage(IntPtr start, int size)
            {
                Start = start;
                Size = size;
            }
        }

        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public UIntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        public int processId;
        public IntPtr _processHandle;

        public bool SetProcess(string[] processNames)
        {
            processId = 0;
            foreach (Process process in Process.GetProcesses())
            {
                if (Array.Exists(processNames, name => name.Equals(process.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    processId = process.Id;
                    break;
                }
            }

            if (processId <= 0)
                return false;

            _processHandle = OpenProcess(ProcessAccessFlags.AllAccess, false, processId);
            return _processHandle != IntPtr.Zero;
        }

        public async Task<IEnumerable<long>> AoBScan(string bytePattern)
        {
            return await AobScan(bytePattern);
        }

        private async Task<IEnumerable<long>> AobScan(string pattern)
        {
            PatternData patternData = GetPatternDataFromPattern(pattern);
            var addressRet = new List<long>();

            await Task.Run(() =>
            {
                var pages = new List<MemoryPage>();
                IntPtr address = IntPtr.Zero;
                while (VirtualQueryEx(_processHandle, address, out MEMORY_BASIC_INFORMATION info,
                           (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) ==
                       (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)))
                {
                    if (CanReadPage(info))
                        pages.Add(new MemoryPage(info.BaseAddress, (int)info.RegionSize.ToUInt64()));

                    address = (IntPtr)((long)info.BaseAddress + (long)info.RegionSize);
                }

                int patternLength = patternData.pattern.Length;
                Parallel.ForEach(pages, page =>
                {
                    byte[] buffer = new byte[page.Size];
                    if (!ReadProcessMemory(_processHandle, page.Start, buffer, (IntPtr)page.Size, out _))
                        return;

                    int index = -patternLength;
                    do
                    {
                        index = FindPattern(buffer, patternData.pattern, patternData.mask, index + patternLength);
                        if (index >= 0)
                        {
                            lock (addressRet)
                                addressRet.Add((long)page.Start + index);
                        }
                    } while (index != -1);
                });
            });

            return addressRet.OrderBy(c => c);
        }

        public bool CanReadPage(MEMORY_BASIC_INFORMATION page)
        {
            return page.State == 4096 && page.Type == 131072 && page.Protect == 4;
        }

        private static PatternData GetPatternDataFromPattern(string pattern)
        {
            string[] parts = pattern.Split(' ');
            return new PatternData
            {
                pattern = parts.Select(s => s.Contains("??") ? (byte)0x00 : byte.Parse(s, NumberStyles.HexNumber)).ToArray(),
                mask = parts.Select(s => s.Contains("??") ? (byte)0x00 : (byte)0xFF).ToArray()
            };
        }

        public bool AobReplace(long address, string bytePattern)
        {
            try
            {
                byte[] bytes = StringToByteArray(bytePattern);
                return WriteProcessMemory(_processHandle, (IntPtr)address, bytes, (IntPtr)bytes.Length, IntPtr.Zero);
            }
            catch
            {
                return false;
            }
        }

        private static byte[] StringToByteArray(string hexString)
        {
            return hexString.Split(' ').Select(hex => byte.Parse(hex, NumberStyles.HexNumber)).ToArray();
        }

        private static int FindPattern(byte[] body, byte[] pattern, byte[] masks, int start = 0)
        {
            if (body.Length == 0 || pattern.Length == 0 || start > body.Length - pattern.Length || pattern.Length > body.Length)
                return -1;

            for (int i = start; i <= body.Length - pattern.Length; i++)
            {
                if ((body[i] & masks[0]) != (pattern[0] & masks[0]))
                    continue;

                bool match = true;
                for (int n = pattern.Length - 1; n >= 1; n--)
                {
                    if ((body[i + n] & masks[n]) != (pattern[n] & masks[n]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return i;
            }

            return -1;
        }
    }

    [Flags]
    public enum ProcessAccessFlags
    {
        AllAccess = 0x001F0FFF,
        VmRead = 0x0010,
        VmWrite = 0x0020,
        VmOperation = 0x0008
    }
}
