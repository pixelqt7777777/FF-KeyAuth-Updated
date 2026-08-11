using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharp_ImGui_Client.ExternalESP
{
    public enum TargetingMode
    {
        ClosestToCrosshair,
        Target360,
        LowestHealth,
        ClosestToPlayer
    }

    internal class Config
    {


        internal static bool NoRecoil = false;
        internal static bool SilentAim = false;
        internal static bool FastReload = false;
        internal static bool RapidFire = false;
        internal static float RapidFireSpeed = 1.0f;
        public static bool enableAimBot = false;
        public static int AimbotKey = 0x01; // Left Mouse Button (VK_LBUTTON = 0x01, Right = 0x02)
        public static TargetingMode TargetingMode = TargetingMode.ClosestToCrosshair;
        public static bool AimBotRage = false;
        public static float AimBotSmooth = 1f;
        public static float AimBotMaxDistance = 500f;




        internal static bool IgnoreKnocked = false;
        internal static bool ESPName = false;
        internal static bool AimFovC = true;
        internal static bool FixEsp = false;
        internal static bool minimap = false;
        internal static bool ESPLine = false;
        internal static bool ESPFillBox = false;
        internal static bool ESPBox2 = false;
        internal static bool espbg = false;
        internal static bool ESPHealth = false;
        internal static bool ESPSkeleton = false;
        internal static bool StreamMode = false;
        internal static bool EspUp = true;
        internal static bool EspBottom = false;
        internal static bool ESPclosest = false;
        internal static bool ESPBox = false;
        internal static bool ESPDistance = false;
        internal static bool AimBot = false;
        internal static bool BoxGlow = false;

        internal static int Aimfov = 300;

        internal static string linePosition = "Up";

        // Fordwer Enemy

        public static bool EnemyPull360 = false;
        public static float EnemyPullStrength = 0.65f;
        public static int EnemyPullMaxDistance = 250;
        public static int EnemyPullTickMs = 6;
        internal static int SmoothnessX = 0;

        internal static int espran = 150;
        internal static int AimBotFov = 400;

        internal static Color Aimfovcolor = Color.White;
        internal static Color NameCheat = Color.White;
        internal static Color ESPLineColor = Color.White;
        internal static Color ESPFillBoxColor = Color.White;
        internal static Color ESPBoxColor = Color.White;
        internal static Color ESPSkeletonColor = Color.White;

        internal static bool SpinBot = false;

        internal static float GlowRadius = 15;
        internal static float FeatherAmount = 15;
        internal static float GlowOpacity = 0.2f;


        internal static bool espcfx = true;
    }
}
