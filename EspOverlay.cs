using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ImGuiNET;

namespace CSharp_ImGui_Client
{
    public sealed class EspOverlay : ClickableTransparentOverlay.Overlay, IDisposable
    {
        private const short DefaultMaxHealth = 200;
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;
        private const uint WDA_NONE = 0x00000000;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        private const float NameplateIdW = 22f;
        private const float NameplateBarH = 22f;
        private const float NameplateHealthH = 4f;
        private const float NameplateMinWidth = 130f;

        public bool EspBox = false;
        public bool EspName = false;
        public bool EspHealth = false;
        public bool EspDistance = false;
        public bool EspLine = false;
        public bool EspSkeleton = false;

        private int _w, _h;
        public int Width => _w;
        public int Height => _h;

        [DllImport("user32.dll")] private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int count);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref POINT lpPoint);
        [DllImport("user32.dll")] private static extern long GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern long SetWindowLong(IntPtr hwnd, int index, long dwNewLong);
        [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        protected override unsafe void Render()
        {
            CreateHandle();

            if (!Core.HaveMatrix) return;

            string text = "Tanish Regedit";
            var windowWidth = _w;
            var windowHeight = _h;
            var textSize = ImGui.CalcTextSize(text);
            var textPosX = (windowWidth - textSize.X) / 2;
            var textPosY = 50f;
            uint textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f));
            uint shadowColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.6f));

            var drawList = ImGui.GetForegroundDrawList();

            var offsets = new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) };
            foreach (var offset in offsets)
                drawList.AddText(new Vector2(textPosX + offset.X, textPosY + offset.Y), shadowColor, text);
            drawList.AddText(new Vector2(textPosX, textPosY), textColor, text);

            IntPtr overlayWnd = FindWindow(null, "Overlay");
            if (overlayWnd != IntPtr.Zero)
            {
                long extendedStyle = GetWindowLong(overlayWnd, GWL_EXSTYLE);
                SetWindowLong(overlayWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
            }

            var tmp = Core.Entities;
            int enemyCount = 0;

            foreach (var entity in tmp.Values)
            {
                if (entity.IsDead || !entity.IsKnown) continue;

                var dist = entity.Distance;
                if (dist > ExtEspConfig.EspRange || dist < 1f) continue;

                var headScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                var bottomScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Root, Core.Width, Core.Height);

                if (headScreenPos.X < 1 || headScreenPos.Y < 1) continue;
                if (bottomScreenPos.X < 1 || bottomScreenPos.Y < 1) continue;

                float CornerHeight = MathF.Abs(headScreenPos.Y - bottomScreenPos.Y);
                float CornerWidth = CornerHeight * 0.65f;
                if (CornerHeight < 5f || CornerHeight > 1000f) continue;

                enemyCount++;

                uint lineColor = ColorToUint32(ExtEspConfig.ESPLineColor);
                uint boxColor = ColorToUint32(ExtEspConfig.ESPBoxColor);

                if (EspLine)
                {
                    DrawFilledCircle(20f, 5.0f);
                    if (!entity.IsKnocked)
                        ImGui.GetBackgroundDrawList().AddLine(new Vector2(windowWidth / 2f, 20f), headScreenPos, lineColor, 1.5f);
                    else
                        ImGui.GetBackgroundDrawList().AddLine(new Vector2(windowWidth / 2f, 20f), headScreenPos, ColorToUint32(Color.Red), 1.5f);
                }

                if (EspBox)
                {
                    DrawCorneredBox(headScreenPos.X - (CornerWidth / 2), headScreenPos.Y, CornerWidth, CornerHeight, boxColor, 1.5f);
                }

                if (EspName || EspHealth || EspDistance)
                {
                    string nameDisplay = SanitizeEspDisplayName(entity.Name);
                    string distStr = $"{MathF.Round(dist)}M";
                    int teamDigit = Math.Abs((int)(entity.Address % 10));
                    float healthFraction = entity.Health > 0 ? Math.Clamp((float)entity.Health / DefaultMaxHealth, 0f, 1f) : 1f;

                    ComputeEspNameplateSize(nameDisplay, distStr, out float nameplateW, out float totalH);
                    Vector2 nameplatePos = new Vector2(headScreenPos.X - (nameplateW / 2f), headScreenPos.Y - 10f - totalH);

                    DrawEspInfoNameplate(drawList, nameplatePos, nameplateW, nameDisplay, distStr, teamDigit, healthFraction, entity.IsKnocked, EspDistance);
                }

                if (EspHealth)
                {
                    float barH = CornerHeight;
                    float barW = 5f;
                    float barX = headScreenPos.X + (CornerWidth / 2f) + 6f;
                    float barY = headScreenPos.Y;
                    DrawHealthBarModern(drawList, barX, barY, barW, barH, entity.Health, DefaultMaxHealth, entity.IsKnocked);
                }

                if (EspSkeleton)
                    DrawSkeleton(entity);
            }

            string totalPlayersText = $"Enemy Detected: {enemyCount}";
            var totalPlayersTextSize = ImGui.CalcTextSize(totalPlayersText);
            var totalPlayersTextPosX = (windowWidth - totalPlayersTextSize.X) / 2;
            var totalPlayersTextPosY = textPosY + textSize.Y + 15f;

            foreach (var offset2 in offsets)
                drawList.AddText(new Vector2(totalPlayersTextPosX + offset2.X, totalPlayersTextPosY + offset2.Y), shadowColor, totalPlayersText);
            drawList.AddText(new Vector2(totalPlayersTextPosX, totalPlayersTextPosY), textColor, totalPlayersText);
        }

        private static string SanitizeEspDisplayName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Training Bot";
            string t = name.Trim('\0', '\u200B', '\t');
            if (t.Length > 512) return t.Substring(0, 509) + "...";
            return t;
        }

        private static void ComputeEspNameplateSize(string nameDisplay, string distStr, out float width, out float totalHeight)
        {
            const float padMain = 5f;
            Vector2 n = ImGui.CalcTextSize(nameDisplay);
            Vector2 d = ImGui.CalcTextSize(distStr);
            width = NameplateIdW + padMain + n.X + 8f + d.X + padMain;
            width = Math.Max(NameplateMinWidth, width);
            totalHeight = NameplateBarH + NameplateHealthH;
        }

        private static void DrawEspInfoNameplate(
            ImDrawListPtr dl,
            Vector2 topLeft,
            float nameplateW,
            string nameDisplay,
            string distStr,
            int teamDigit,
            float healthFraction,
            bool knocked,
            bool drawDistance)
        {
            const float padMain = 6f;
            float barH = NameplateBarH;
            float healthH = NameplateHealthH;
            float totalH = barH + healthH;
            teamDigit = Math.Abs(teamDigit % 10);

            Vector2 tl = topLeft;
            Vector2 br = tl + new Vector2(nameplateW, totalH);

            uint bgHealthTrack = ImGui.ColorConvertFloat4ToU32(new Vector4(0.04f, 0.04f, 0.05f, 0.95f));
            dl.AddRectFilled(new Vector2(tl.X, tl.Y + barH), br, bgHealthTrack);

            float hp = Math.Clamp(healthFraction, 0f, 1f);
            uint hpCol = knocked
                ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.25f, 0.25f, 1f))
                : ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.95f, 0.2f, 1f));
            if (nameplateW * hp > 0.5f)
                dl.AddRectFilled(new Vector2(tl.X, tl.Y + barH), new Vector2(tl.X + nameplateW * hp, br.Y), hpCol);

            dl.AddRectFilled(tl, tl + new Vector2(NameplateIdW, barH),
                ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)));
            dl.AddRect(tl, tl + new Vector2(NameplateIdW, barH),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f)), 0f, ImDrawFlags.None, 1f);

            string idStr = teamDigit.ToString();
            Vector2 idSz = ImGui.CalcTextSize(idStr);
            dl.AddText(tl + new Vector2((NameplateIdW - idSz.X) * 0.5f, (barH - idSz.Y) * 0.5f),
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f)), idStr);

            Vector2 mainMin = tl + new Vector2(NameplateIdW, 0f);
            Vector2 mainMax = tl + new Vector2(nameplateW, barH);
            uint cTL = ImGui.ColorConvertFloat4ToU32(new Vector4(0.07f, 0.32f, 0.38f, 0.98f));
            uint cTR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.14f, 0.52f, 0.56f, 0.98f));
            uint cBL = ImGui.ColorConvertFloat4ToU32(new Vector4(0.06f, 0.26f, 0.32f, 0.98f));
            uint cBR = ImGui.ColorConvertFloat4ToU32(new Vector4(0.11f, 0.42f, 0.48f, 0.98f));
            dl.AddRectFilledMultiColor(mainMin, mainMax, cTL, cTR, cBR, cBL);

            Vector2 nameSz = ImGui.CalcTextSize(nameDisplay);
            float nameX = mainMin.X + padMain;
            float textY = tl.Y + (barH - nameSz.Y) * 0.5f;
            dl.AddText(new Vector2(nameX, textY), ColorToUint32(Color.White), nameDisplay);

            if (drawDistance)
            {
                Vector2 distSz = ImGui.CalcTextSize(distStr);
                float distX = mainMax.X - padMain - distSz.X;
                dl.AddText(new Vector2(distX, textY),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.93f, 0.2f, 1f)), distStr);
            }

            uint borderBlack = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.92f));
            dl.AddRect(tl, br, borderBlack, 0f, ImDrawFlags.None, 1f);
        }

        private static void DrawHealthBarModern(ImDrawListPtr dl, float x, float y, float w, float h, short health, short maxHealth, bool knocked)
        {
            if (maxHealth <= 0) maxHealth = 100;
            float hp = Math.Clamp((float)health / maxHealth, 0f, 1f);

            uint trackBg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.04f, 0.04f, 0.05f, 0.9f));
            dl.AddRectFilled(new Vector2(x, y), new Vector2(x + w, y + h), trackBg);

            uint hpColor = knocked
                ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.25f, 0.25f, 1f))
                : hp < 0.3f
                    ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.2f, 0.2f, 1f))
                    : hp < 0.6f
                        ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.85f, 0.1f, 1f))
                        : ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.95f, 0.2f, 1f));

            float fillH = h * hp;
            if (fillH > 0.5f)
                dl.AddRectFilled(new Vector2(x, y + h - fillH), new Vector2(x + w, y + h), hpColor);

            uint border = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.85f));
            dl.AddRect(new Vector2(x, y), new Vector2(x + w, y + h), border, 0f, ImDrawFlags.None, 1f);
        }

        private void DrawSkeleton(Entity entity)
        {
            var drawList = ImGui.GetForegroundDrawList();
            uint lineColor = ColorToUint32(ExtEspConfig.ESPSkeletonColor);
            uint circleColor = ColorToUint32(Color.White);

            var headScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
            var spineScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Spine, Core.Width, Core.Height);
            var hipScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Hip, Core.Width, Core.Height);
            var leftShoulderScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftShoulder, Core.Width, Core.Height);
            var rightShoulderScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightShoulder, Core.Width, Core.Height);
            var leftElbowScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftElbow, Core.Width, Core.Height);
            var rightElbowScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightElbow, Core.Width, Core.Height);
            var leftWristJointScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftWristJoint, Core.Width, Core.Height);
            var rightWristJointScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightWristJoint, Core.Width, Core.Height);
            var rightFootScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightFoot, Core.Width, Core.Height);
            var leftFootScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftFoot, Core.Width, Core.Height);

            DrawLine(drawList, spineScreenPos, rightShoulderScreenPos, lineColor);
            DrawLine(drawList, spineScreenPos, hipScreenPos, lineColor);
            DrawLine(drawList, spineScreenPos, leftShoulderScreenPos, lineColor);
            DrawLine(drawList, leftShoulderScreenPos, rightElbowScreenPos, lineColor);
            DrawLine(drawList, leftElbowScreenPos, rightWristJointScreenPos, lineColor);
            DrawLine(drawList, rightShoulderScreenPos, leftElbowScreenPos, lineColor);
            DrawLine(drawList, hipScreenPos, rightFootScreenPos, lineColor);
            DrawLine(drawList, hipScreenPos, leftFootScreenPos, lineColor);

            float distance = entity.Distance;
            float baseRadius = 50.0f;
            float circleRadius = baseRadius / distance;

            if (headScreenPos.X > 0 && headScreenPos.Y > 0)
                drawList.AddCircle(headScreenPos, circleRadius, circleColor, 30);
        }

        public void DrawCorneredBox(float X, float Y, float W, float H, uint color, float thickness)
        {
            var vList = ImGui.GetForegroundDrawList();
            float lineW = W / 3;
            float lineH = H / 3;
            vList.AddLine(new Vector2(X, Y - thickness / 2), new Vector2(X, Y + lineH), color, thickness);
            vList.AddLine(new Vector2(X - thickness / 2, Y), new Vector2(X + lineW, Y), color, thickness);
            vList.AddLine(new Vector2(X + W - lineW, Y), new Vector2(X + W + thickness / 2, Y), color, thickness);
            vList.AddLine(new Vector2(X + W, Y - thickness / 2), new Vector2(X + W, Y + lineH), color, thickness);
            vList.AddLine(new Vector2(X, Y + H - lineH), new Vector2(X, Y + H + thickness / 2), color, thickness);
            vList.AddLine(new Vector2(X - thickness / 2, Y + H), new Vector2(X + lineW, Y + H), color, thickness);
            vList.AddLine(new Vector2(X + W - lineW, Y + H), new Vector2(X + W + thickness / 2, Y + H), color, thickness);
            vList.AddLine(new Vector2(X + W, Y + H - lineH), new Vector2(X + W, Y + H + thickness / 2), color, thickness);
        }

        public void DrawFilledCircle(float centerY, float radius, int numSegments = 64)
        {
            var vList = ImGui.GetBackgroundDrawList();
            float centerX = _w / 2f;
            uint color = ColorToUint32(Color.FromArgb(255, 225, 0, 0));
            float shadowOffset = 1.5f;
            uint shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f));
            vList.AddCircleFilled(new Vector2(centerX, centerY), radius + shadowOffset, shadow, numSegments);
            vList.AddCircleFilled(new Vector2(centerX, centerY), radius, color, numSegments);
        }

        private void DrawLine(ImDrawListPtr drawList, Vector2 startPos, Vector2 endPos, uint color)
        {
            if (startPos.X > 0 && startPos.Y > 0 && endPos.X > 0 && endPos.Y > 0)
                drawList.AddLine(startPos, endPos, color, 1.5f);
        }

        static uint ColorToUint32(Color color)
        {
            return ImGui.ColorConvertFloat4ToU32(new Vector4(
                (float)(color.R / 255.0),
                (float)(color.G / 255.0),
                (float)(color.B / 255.0),
                (float)(color.A / 255.0)));
        }

        private void CreateHandle()
        {
            IntPtr bsWnd = FindEmulatorWindow();
            if (bsWnd == IntPtr.Zero) return;

            IntPtr renderWnd = FindGameRenderWindow(bsWnd);
            RECT rect;
            GetClientRect(renderWnd, out rect);
            var pt = new POINT { X = 0, Y = 0 };
            ClientToScreen(renderWnd, ref pt);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return;

            ImGui.SetWindowSize(new Vector2((float)width, (float)height));
            ImGui.SetWindowPos(new Vector2((float)pt.X, (float)pt.Y));
            Size = new Size(width, height);
            Position = new Point(pt.X, pt.Y);
            _w = width;
            _h = height;
            Core.Width = width;
            Core.Height = height;

            IntPtr overlayWnd = FindWindow(null, "Overlay");
            if (overlayWnd != IntPtr.Zero)
            {
                if (ExtEspConfig.StreamMode)
                    SetWindowDisplayAffinity(overlayWnd, WDA_EXCLUDEFROMCAPTURE);
                else
                    SetWindowDisplayAffinity(overlayWnd, WDA_NONE);
            }
        }

        private static IntPtr FindEmulatorWindow()
        {
            IntPtr hwnd = FindWindow("BlueStacksApp", null);
            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd)) return hwnd;

            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var sb = new System.Text.StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                string title = sb.ToString();
                if (title.Contains("MSI App Player", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("BlueStacks", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("App Player", StringComparison.OrdinalIgnoreCase))
                { found = hWnd; return false; }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static IntPtr FindGameRenderWindow(IntPtr parent)
        {
            IntPtr best = IntPtr.Zero;
            int bestArea = 0;
            EnumChildWindows(parent, (hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd) || !GetClientRect(hWnd, out RECT r)) return true;
                int area = (r.Right - r.Left) * (r.Bottom - r.Top);
                if (area > bestArea) { bestArea = area; best = hWnd; }
                return true;
            }, IntPtr.Zero);
            return (best != IntPtr.Zero && bestArea > 120000) ? best : parent;
        }

        public new void Dispose()
        {
            base.Dispose();
        }

        public void UpdatePlayers(PlayerData[] players)
        {
        }
    }
}
