using System.Runtime.InteropServices;

namespace CSharp_ImGui_Client
{
    /// <summary>
    /// Wire-compatible structs matching the C++ Request / Response / PlayerData
    /// defined in TanishRegedit.cpp backend. Field names use PascalCase but are laid out
    /// in the exact same memory order as the C++ structs (Pack=4 = ARM32 default).
    /// </summary>

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct CustomVector3
    {
        public float X;
        public float Y;
        public float Z;

        public CustomVector3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static CustomVector3 operator -(CustomVector3 a, CustomVector3 b)
            => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public float Length()
            => MathF.Sqrt(X * X + Y * Y + Z * Z);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    /// <summary>
    /// Matches C++ struct:
    ///   struct Request {
    ///       int Mode;
    ///       bool boolean;
    ///       int value;
    ///       int ScreenWidth;
    ///       int ScreenHeight;
    ///   };
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Request
    {
        public int Mode;
        [MarshalAs(UnmanagedType.U1)]
        public bool Boolean;       // C++ field: boolean
        public int Value;          // C++ field: value
        public int ScreenWidth;
        public int ScreenHeight;
    }

    /// <summary>
    /// Matches C++ struct PlayerData.
    /// char name[2000] → ByValTStr SizeConst=2000.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
    public struct PlayerData
    {
        public CustomVector3 HeadPosition;       // C++: headPosition
        public CustomVector3 BottomPlayerPosition; // C++: bottomPlayerPosition
        public float Health;                      // C++: health
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2000)]
        public string Name;                       // C++: name[2000]
        [MarshalAs(UnmanagedType.U1)]
        public bool IsDieing;                     // C++: isDieing
        [MarshalAs(UnmanagedType.U1)]
        public bool IsBot;                        // C++: isBot
        [MarshalAs(UnmanagedType.U1)]
        public bool IsInVehicle;                  // C++: isInVehicle
        public float Distance;                    // C++: distance
    }

    /// <summary>
    /// Matches C++ struct Response.
    /// Players array is fixed at maxplayerCount (60).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Ansi)]
    public struct Response
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool Success;                      // C++: Success
        public int PlayerCount;                   // C++: PlayerCount
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
        public PlayerData[] Players;              // C++: Players[maxplayerCount]
        public int MatchAlive;                    // C++: matchAlive
        public int RemainingTimeSeconds;          // C++: remainingTimeSeconds
        public int EnemyCount;                    // C++: enemyCount
        public int BotCount;                      // C++: botCount
    }

    /// <summary>Helper to marshal structs to/from byte arrays.</summary>
    public static class StructConverter
    {
        public static byte[] StructToBytes<T>(T str) where T : struct
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return arr;
        }

        public static T BytesToStruct<T>(byte[] arr) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(arr, 0, ptr, size);
                object obj = Marshal.PtrToStructure(ptr, typeof(T));
                if (obj == null) return default;
                return (T)obj;
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
