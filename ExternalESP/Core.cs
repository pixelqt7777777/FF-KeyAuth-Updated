using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace CSharp_ImGui_Client.ExternalESP
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

        internal static uint CurrentGame;

        internal static ulong LocalPlayer;
        internal static Vector3 LocalMainCamera;
        public static ConcurrentDictionary<long, Entity> Entities = new();


        public static Vector3 CameraForward { get; internal set; }
        public static Vector3 CameraRight { get; internal set; }

    }
}
