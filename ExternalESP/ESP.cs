using ImGuiNET;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;



namespace CSharp_ImGui_Client.ExternalESP
{
    internal class ESP : ClickableTransparentOverlay.Overlay
    {
        IntPtr hWnd;
        IntPtr HDPlayer;
        private const short DefaultMaxHealth = 200; // Default maximum health
        protected override unsafe void Render()
        {
            if (!Core.HaveMatrix) return;

            CreateHandle();
            string text = "Tanish Regedit Always On Top ";
            var windowWidth = Core.Width;
            var windowHeight = Core.Height;
            var textSize = ImGui.CalcTextSize(text);
            var textPosX = (windowWidth - textSize.X) / 2;
            var textPosY = 80;
            uint textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 1.0f, 1.0f)); // White color
            uint shadowColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.0f, 0.0f, 0.5f)); // Shadow color (black with transparency)

            var drawList = ImGui.GetForegroundDrawList();

            // Draw the text multiple times to simulate bold effect
            var offsets = new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) };
            foreach (var offset in offsets)
            {
                drawList.AddText(new Vector2(textPosX + offset.X, textPosY + offset.Y), shadowColor, text);

            }

            // Draw the main text on top
            drawList.AddText(new Vector2(textPosX, textPosY), textColor, text);


            if (Config.AimFovC)
            {
                DrawSmoothCircle(Config.Aimfov, ColorToUint32(Config.Aimfovcolor), 1.0f);
            }

            var tmp = Core.Entities;

            // Handle window styles
            string windowName = "Overlay";
            hWnd = FindWindow(null, windowName);
            HDPlayer = FindWindow("BlueStacksApp", null);

            if (hWnd != IntPtr.Zero)
            {
                int extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
            }
            else
            {
                Console.WriteLine("The window was not found.");
            }

            int enemyCount = 0; // Initialize enemy count

            foreach (var entity in tmp.Values)
            {

                if (entity.IsDead || !entity.IsKnown)
                {
                    continue;
                }
                var dist = Vector3.Distance(Core.LocalMainCamera, entity.Head);

                if (dist > Config.espran) continue;

                var headScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                enemyCount++; // Increment the enemy count
                var bottomScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Root, Core.Width, Core.Height);

                if (headScreenPos.X < 1 || headScreenPos.Y < 1) continue;
                if (bottomScreenPos.X < 1 || bottomScreenPos.Y < 1) continue;

                float CornerHeight = Math.Abs(headScreenPos.Y - bottomScreenPos.Y);
                
                // Perfectly adjusted ESP Box coords
                float boxY = headScreenPos.Y - (CornerHeight * 0.12f);
                float boxBottom = bottomScreenPos.Y + (CornerHeight * 0.05f);
                float adjustedHeight = boxBottom - boxY;
                float adjustedWidth = adjustedHeight * 0.55f;
                float boxX = headScreenPos.X - (adjustedWidth / 2f);

                if (Config.ESPLine)
                {
                    if (Config.espcfx == false)
                    {
                        DrawFilledCircle(20f, 5.0f);
                        if (!entity.IsKnocked)
                        {
                            ImGui.GetBackgroundDrawList().AddLine(new Vector2(Core.Width / 2f, 20f), headScreenPos, ColorToUint32(Config.ESPLineColor), 1f);
                        }
                        else
                        {
                            ImGui.GetBackgroundDrawList().AddLine(new Vector2(Core.Width / 2f, 20f), headScreenPos, ColorToUint32(Color.Red), 1f);
                        }

                    }
                    else
                    {
                        DrawFilledCircle(Core.Height - 20f, 5.0f);
                        if (!entity.IsKnocked)
                        {
                            ImGui.GetBackgroundDrawList().AddLine(new Vector2(Core.Width / 2f, Core.Height - 20f), headScreenPos, ColorToUint32(Config.ESPLineColor), 1f);
                        }
                        else
                        {
                            ImGui.GetBackgroundDrawList().AddLine(new Vector2(Core.Width / 2f, Core.Height - 20f), headScreenPos, ColorToUint32(Color.Red), 1f);
                        }

                    }

                }

                if (Config.ESPBox)
                {
                    uint boxColor = ColorToUint32(Config.ESPBoxColor);
                    DrawCorneredBox(boxX, boxY, adjustedWidth, adjustedHeight, boxColor, 1.5f);
                }

                if (Config.ESPFillBox)
                {
                    uint boxColor = ColorToUint32(Color.FromArgb((int)(0.2f * 255), Config.ESPFillBoxColor));
                    DrawFilledBox(boxX, boxY, adjustedWidth, adjustedHeight, boxColor);
                }

                if (Config.ESPBox2)
                {
                    uint boxColor = ColorToUint32(Config.ESPBoxColor);
                    Draw3dBox(boxX, boxY, adjustedWidth, adjustedHeight, boxColor, 1.5f);
                }

                if (Config.ESPName)
                {
                    var nameText = string.IsNullOrWhiteSpace(entity.Name) ? "BOT" : entity.Name;
                    var distText = $"  {MathF.Round(dist)}M";

                    Vector2 nameSize = ImGui.CalcTextSize(nameText);
                    Vector2 distSize = ImGui.CalcTextSize(distText);

                    float totalWidth = nameSize.X + distSize.X;
                    float maxHeight = Math.Max(nameSize.Y, distSize.Y);

                    var namePosition = new Vector2(headScreenPos.X - (totalWidth / 2f), boxY - maxHeight - 8);

                    if (Config.espbg)
                    {
                        // Draw the black background with 70% opacity
                        drawList.AddRectFilled(
                            namePosition - new Vector2(6, 3), 
                            namePosition + new Vector2(totalWidth + 6, maxHeight + 3), 
                            ColorToUint32(Color.FromArgb(180, 0, 0, 0)) 
                        );

                        // Draw the border stroke
                        drawList.AddRect(
                            namePosition - new Vector2(6, 3), 
                            namePosition + new Vector2(totalWidth + 6, maxHeight + 3), 
                            ColorToUint32(Color.FromArgb(255, 100, 0, 0)), 
                            1f, 
                            ImDrawFlags.None,
                            1f 
                        );
                    }

                    // Draw name in white
                    drawList.AddText(namePosition, ColorToUint32(Color.White), nameText);

                    // Draw distance in yellow
                    drawList.AddText(new Vector2(namePosition.X + nameSize.X, namePosition.Y), ColorToUint32(Color.Yellow), distText);
                }

                if (Config.ESPHealth)
                {
                    float healthBarWidth = 4f;
                    float healthBarGap = 5f;
                    float healthBarX = boxX - healthBarGap - healthBarWidth;
                    float healthBarY = boxY;
                    float healthBarHeight = adjustedHeight;

                    DrawVerticalHealthBar(entity.Health, DefaultMaxHealth, healthBarX, healthBarY, healthBarWidth, healthBarHeight, entity.IsKnocked);
                }

                if (Config.ESPSkeleton)
                {
                    DrawSkeleton(entity);
                }
            }

            // Display the total enemy count (moved outside the loop)
            string totalPlayersText = $"Enemy Detected: {enemyCount}";
            var totalPlayersTextSize = ImGui.CalcTextSize(totalPlayersText);
            var totalPlayersTextPosX = (windowWidth - totalPlayersTextSize.X) / 2;
            var totalPlayersTextPosY = textPosY + textSize.Y + 20; // Position below "HeX Corporation" text

            // Draw shadow for "Total Players" text
            foreach (var offset in offsets)
            {
                drawList.AddText(new Vector2(totalPlayersTextPosX + offset.X, totalPlayersTextPosY + offset.Y), shadowColor, totalPlayersText);
            }

            // Draw the main "Total Players" text on top
            drawList.AddText(new Vector2(totalPlayersTextPosX, totalPlayersTextPosY), textColor, totalPlayersText);
        }
        public void DrawFilledBox(float X, float Y, float W, float H, uint color)
        {
            var vList = ImGui.GetForegroundDrawList();
            vList.AddRectFilled(new Vector2(X, Y), new Vector2(X + W, Y + H), color);
        }

        public void DrawFilledCircle(float centerY, float radius, int numSegments = 64)
        {
            var vList = ImGui.GetBackgroundDrawList();

            // Set the center of the circle at the middle of the screen horizontally (Core.Width / 2f)
            float centerX = Core.Width / 2f;
            uint color = ColorToUint32(Color.FromArgb((int)(1f * 255), 225, 0, 0)); // Red color with full opacity

            // Shadow parameters
            float shadowOffset = 1.5f; // The subtle offset of the shadow from the circle
            uint shadowColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f)); // Semi-transparent black for a soft shadow

            // Draw shadow (a larger circle slightly offset behind the main one)
            vList.AddCircleFilled(new Vector2(centerX, centerY), radius + shadowOffset, shadowColor, numSegments);

            // Draw main circle
            vList.AddCircleFilled(new Vector2(centerX, centerY), radius, color, numSegments);
        }
        public void DrawCorneredBox(float X, float Y, float W, float H, uint color, float thickness)
        {
            var vList = ImGui.GetForegroundDrawList();

            float lineW = W / 3;
            float lineH = H / 3;

            // Draw black outline/shadow first for high contrast and readability on any background
            uint blackColor = ColorToUint32(Color.FromArgb(255, 0, 0, 0));
            float outlineThickness = thickness + 1.5f;

            vList.AddLine(new Vector2(X, Y - outlineThickness / 2), new Vector2(X, Y + lineH), blackColor, outlineThickness);
            vList.AddLine(new Vector2(X - outlineThickness / 2, Y), new Vector2(X + lineW, Y), blackColor, outlineThickness);
            vList.AddLine(new Vector2(X + W - lineW, Y), new Vector2(X + W + outlineThickness / 2, Y), blackColor, outlineThickness);
            vList.AddLine(new Vector2(X + W, Y - outlineThickness / 2), new Vector2(X + W, Y + lineH), blackColor, outlineThickness);
            vList.AddLine(new Vector2(X, Y + H - lineH), new Vector2(X, Y + H + outlineThickness / 2), blackColor, outlineThickness);
            vList.AddLine(new Vector2(X - outlineThickness / 2, Y + H), new Vector2(X + lineW, Y + H), blackColor, outlineThickness);
            vList.AddLine(new Vector2(X + W - lineW, Y + H), new Vector2(X + W + outlineThickness / 2, Y + H), blackColor, outlineThickness);
            vList.AddLine(new Vector2(X + W, Y + H - lineH), new Vector2(X + W, Y + H + outlineThickness / 2), blackColor, outlineThickness);

            // Draw primary colored corner lines
            vList.AddLine(new Vector2(X, Y - thickness / 2), new Vector2(X, Y + lineH), color, thickness);
            vList.AddLine(new Vector2(X - thickness / 2, Y), new Vector2(X + lineW, Y), color, thickness);
            vList.AddLine(new Vector2(X + W - lineW, Y), new Vector2(X + W + thickness / 2, Y), color, thickness);
            vList.AddLine(new Vector2(X + W, Y - thickness / 2), new Vector2(X + W, Y + lineH), color, thickness);
            vList.AddLine(new Vector2(X, Y + H - lineH), new Vector2(X, Y + H + thickness / 2), color, thickness);
            vList.AddLine(new Vector2(X - thickness / 2, Y + H), new Vector2(X + lineW, Y + H), color, thickness);
            vList.AddLine(new Vector2(X + W - lineW, Y + H), new Vector2(X + W + thickness / 2, Y + H), color, thickness);
            vList.AddLine(new Vector2(X + W, Y + H - lineH), new Vector2(X + W, Y + H + thickness / 2), color, thickness);
        }

      
        public void Draw3dBox(float X, float Y, float W, float H, uint color, float thickness)
        {

            var vList = ImGui.GetForegroundDrawList();

            Vector3[] screentions = new Vector3[]
            {
        new Vector3(X, Y, 0),                // Top-left front
        new Vector3(X, Y + H, 0),            // Bottom-left front
        new Vector3(X + W, Y + H, 0),        // Bottom-right front
        new Vector3(X + W, Y, 0),            // Top-right front
        new Vector3(X, Y, -W),               // Top-left back
        new Vector3(X, Y + H, -W),           // Bottom-left back
        new Vector3(X + W, Y + H, -W),       // Bottom-right back
        new Vector3(X + W, Y, -W)            // Top-right back
            };

            // Draw front face
            vList.AddLine(new Vector2(screentions[0].X, screentions[0].Y), new Vector2(screentions[1].X, screentions[1].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[1].X, screentions[1].Y), new Vector2(screentions[2].X, screentions[2].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[2].X, screentions[2].Y), new Vector2(screentions[3].X, screentions[3].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[3].X, screentions[3].Y), new Vector2(screentions[0].X, screentions[0].Y), color, thickness);

            // Draw back face
            vList.AddLine(new Vector2(screentions[4].X, screentions[4].Y), new Vector2(screentions[5].X, screentions[5].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[5].X, screentions[5].Y), new Vector2(screentions[6].X, screentions[6].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[6].X, screentions[6].Y), new Vector2(screentions[7].X, screentions[7].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[7].X, screentions[7].Y), new Vector2(screentions[4].X, screentions[4].Y), color, thickness);

            // Draw connecting lines
            vList.AddLine(new Vector2(screentions[0].X, screentions[0].Y), new Vector2(screentions[4].X, screentions[4].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[1].X, screentions[1].Y), new Vector2(screentions[5].X, screentions[5].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[2].X, screentions[2].Y), new Vector2(screentions[6].X, screentions[6].Y), color, thickness);
            vList.AddLine(new Vector2(screentions[3].X, screentions[3].Y), new Vector2(screentions[7].X, screentions[7].Y), color, thickness);

        }
        private void DrawSkeleton(Entity entity)
        {
            var drawList = ImGui.GetForegroundDrawList();
            uint lineColor = ColorToUint32(Config.ESPSkeletonColor); // Color for the skeleton lines
            uint circleColor = ColorToUint32(Color.Red); // Color for the circle around the head

            // Convert entity positions to screen space
            var headScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
            Vector2 headCorePos = headScreenPos;
            var leftWristScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightWrist, Core.Width, Core.Height);
            var spineScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Spine, Core.Width, Core.Height);
            var hipScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Hip, Core.Width, Core.Height);
            var rootScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.Root, Core.Width, Core.Height);
            var rightCalfScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightCalf, Core.Width, Core.Height);
            var leftCalfScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftCalf, Core.Width, Core.Height);
            var rightFootScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightFoot, Core.Width, Core.Height);
            var leftFootScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftFoot, Core.Width, Core.Height);
            var rightWristScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightWrist, Core.Width, Core.Height);
            var leftHandScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftHand, Core.Width, Core.Height);
            var leftShoulderScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftShoulder, Core.Width, Core.Height);
            var rightShoulderScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightShoulder, Core.Width, Core.Height);
            var rightWristJointScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightWristJoint, Core.Width, Core.Height);
            var leftWristJointScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftWristJoint, Core.Width, Core.Height);
            var leftElbowScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.LeftElbow, Core.Width, Core.Height);
            var rightElbowScreenPos = W2S.WorldToScreen(Core.CameraMatrix, entity.RightElbow, Core.Width, Core.Height);
     
            // Draw skeleton lines


            DrawLine(drawList, spineScreenPos, rightShoulderScreenPos, lineColor); // Spine to Right Shoulder
            DrawLine(drawList, spineScreenPos, hipScreenPos, lineColor);// Spine to hip


            DrawLine(drawList, spineScreenPos, leftShoulderScreenPos, lineColor); // Spine to Left Shoulder
            DrawLine(drawList, leftShoulderScreenPos, rightElbowScreenPos, lineColor); // Left Shoulder to Left Elbow
            DrawLine(drawList, leftElbowScreenPos, rightWristJointScreenPos, lineColor); // Left Elbow to Left Wrist Joint
            // Left Wrist Joint to Left Wrist

            DrawLine(drawList, rightShoulderScreenPos, leftElbowScreenPos, lineColor); // Right Shoulder to Left Elbow
                                                                                       //  DrawLine(drawList, rightElbowScreenPos, leftWristJointScreenPos, lineColor); // Right Elbow to Left Wrist Joint
                                                                                       // Right Wrist Joint to Left Wrist

            DrawLine(drawList, hipScreenPos, rightFootScreenPos, lineColor);// Hip to Right Calf
            DrawLine(drawList, hipScreenPos, leftFootScreenPos, lineColor);// Hip to Left Calf


            // Draw a small circle around the head
            float distance = entity.Distance; // Assume entity.Distance is the distance to the player in game units

            // Calculate the circle radius based on distance (e.g., closer = larger, farther = smaller)
            float baseRadius = 50.0f; // Adjust this base value as needed
            float circleRadius = baseRadius / distance;

            // Draw the circle on the head if the head is visible on screen
            if (headScreenPos.X > 0 && headScreenPos.Y > 0)
            {
                drawList.AddCircle(headScreenPos, circleRadius, circleColor, 30); // 30 segments for the circle
            }

            // Add additional code here to draw the rest of the skeleton using the updated bone positions
        }
        private void DrawLine(ImDrawListPtr drawList, Vector2 startPos, Vector2 endPos, uint color)
        {
            if (startPos.X > 0 && startPos.Y > 0 && endPos.X > 0 && endPos.Y > 0)
            {
                drawList.AddLine(startPos, endPos, color, 1.5f); // Adjust thickness as needed
            }
        }

        public void DrawSmoothCircle(float radius, uint color, float thickness, int segments = 64)
        {
            var vList = ImGui.GetForegroundDrawList();
            var io = ImGui.GetIO();
            float centerX = io.DisplaySize.X / 2;
            float centerY = io.DisplaySize.Y / 2;

            vList.AddCircle(new Vector2(centerX, centerY), radius, color, segments, thickness);
        }

        public void DrawVerticalHealthBar(short health, short maxHealth, float X, float Y, float width, float height, bool isKnocked)
        {
            var vList = ImGui.GetForegroundDrawList();

            // Prevent division by zero and ensure healthPercentage is between 0 and 1
            if (maxHealth <= 0) maxHealth = 100; // Fallback to a default max health
            float healthPercentage = Math.Clamp((float)health / maxHealth, 0f, 1f);
            float healthHeight = height * healthPercentage;

            // Determine the color based on health percentage
            Color healthColor;

            if (isKnocked)
            {
                healthColor = Color.Red;
            }
            else
            {
                if (healthPercentage < 0.3f)
                {
                    healthColor = Color.FromArgb(255, 255, 0, 0); // Red
                }
                else if (healthPercentage < 0.8f)
                {
                    healthColor = Color.FromArgb(255, 255, 255, 0); // Yellow
                }
                else
                {
                    healthColor = Color.FromArgb(255, 86, 255, 43); // Green (matches screenshot)
                }
            }

            // Draw the full health bar background (unfilled part) - charcoal color
            uint bgColor = ColorToUint32(Color.FromArgb(180, 20, 20, 20));
            vList.AddRectFilled(new Vector2(X, Y), new Vector2(X + width, Y + height), bgColor);

            // Draw the health portion representing current health (starts from bottom, grows up)
            vList.AddRectFilled(new Vector2(X, Y + height - healthHeight), new Vector2(X + width, Y + height), ColorToUint32(healthColor));

            // Draw the black outline around the health bar
            vList.AddRect(new Vector2(X, Y), new Vector2(X + width, Y + height), ColorToUint32(Color.Black), 0f, ImDrawFlags.None, 1f);
        }
        static uint ColorToUint32(Color color)
        {
            return ImGui.ColorConvertFloat4ToU32(new Vector4(
            (float)(color.R / 255.0),
                (float)(color.G / 255.0),
                (float)(color.B / 255.0),
                (float)(color.A / 255.0)));
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        const uint WDA_NONE = 0x00000000;
        const uint WDA_MONITOR = 0x00000001;
        const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;
        void CreateHandle()
        {
            RECT rect;
            GetWindowRect(Core.Handle, out rect);
            int x = rect.left;
            int y = rect.top;
            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;
            ImGui.SetWindowSize(new Vector2((float)width, (float)height));
            ImGui.SetWindowPos(new Vector2((float)x, (float)y));
            Size = new System.Drawing.Size(width, height);
            Position = new System.Drawing.Point(x, y);

            Core.Width = width;
            Core.Height = height;
            if (Config.StreamMode)
            {
                SetWindowDisplayAffinity(hWnd, WDA_EXCLUDEFROMCAPTURE);
            }
            else
            {
                SetWindowDisplayAffinity(hWnd, WDA_NONE);
            }
        }
    }
}
