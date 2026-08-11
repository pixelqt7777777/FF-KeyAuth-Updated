using System.Collections.Concurrent;
using System.Numerics;

namespace CSharp_ImGui_Client
{
    internal static class Core
    {
        public static uint GameInstance;
        public static Matrix4x4 ViewMatrix;
        internal static IntPtr Handle;
        internal static int Width = -1;
        internal static int Height = -1;
        internal static bool HaveMatrix = false;
        internal static Matrix4x4 CameraMatrix;
        public static uint CurrentGame;
        public static ulong LocalPlayer;
        public static Vector3 LocalMainCamera;
        public static ConcurrentDictionary<long, Entity> Entities = new();
        public static bool Running = false;
    }
}
