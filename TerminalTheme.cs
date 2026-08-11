using System.Drawing;
using System.Windows.Forms;

namespace CSharp_ImGui_Client
{
    public static class TerminalTheme
    {
        public const int FormWidth = 420;
        public const int FormHeight = 380;
        public const int SidebarWidth = 118;
        public const int BridgeConfigWidth = 200;
        public const int RadarPanelWidth = 200;
        public const int InputFieldWidth = 155;
        public const int TitleBarHeight = 30;
        public const int RowHeight = 24;
        public const int NavButtonHeight = 32;
        public const int NavButtonSpacing = 32;
        public const int ContentPad = 10;
        public const int SectionHeaderPad = 24;
        public const int ProfilePanelHeight = 50;
        public const int BridgeTopHeight = 150;

        public static readonly Color Bg = Color.FromArgb(10, 10, 12);
        public static readonly Color TitleBar = Color.FromArgb(8, 8, 10);
        public static readonly Color Sidebar = Color.FromArgb(6, 6, 8);
        public static readonly Color SidebarActive = Color.FromArgb(14, 14, 18);
        public static readonly Color Content = Color.FromArgb(10, 10, 12);
        public static readonly Color PanelBg = Color.FromArgb(13, 13, 16);
        public static readonly Color PanelBorder = Color.FromArgb(32, 32, 38);
        public static readonly Color PanelBorderHover = Color.FromArgb(50, 50, 60);
        public static readonly Color Red = Color.FromArgb(220, 20, 20);
        public static readonly Color RedGlow = Color.FromArgb(80, 220, 20, 20);
        public static readonly Color Text = Color.FromArgb(245, 245, 248);
        public static readonly Color TextMuted = Color.FromArgb(115, 117, 128);
        public static readonly Color TextDim = Color.FromArgb(75, 76, 82);
        public static readonly Color InputBg = Color.FromArgb(18, 18, 22);
        public static readonly Color KeyBtn = Color.FromArgb(22, 22, 26);
        public static readonly Color LogGreen = Color.FromArgb(50, 215, 105);
        public static readonly Color AccentOrange = Color.FromArgb(235, 140, 20);

        public const string BrandName = "TANISH REGEDIT";

        public static Font MonoSmall = new Font("Segoe UI Semibold", 8F);
        public static Font Mono = new Font("Segoe UI", 9F);
        public static Font MonoBold = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        public static Font TitleBrand = new Font("Segoe UI Black", 10.5F, FontStyle.Bold);
        public static Font ModuleTitle = new Font("Segoe UI Semibold", 8F);
        public static Font SectionHeader = new Font("Segoe UI Black", 8.25F, FontStyle.Bold);

        public static void StyleTextBox(TextBox tb)
        {
            tb.BackColor = InputBg;
            tb.ForeColor = Text;
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Font = Mono;
        }

        public static void StyleApplyButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = PanelBorder;
            btn.BackColor = InputBg;
            btn.ForeColor = Text;
            btn.Font = MonoBold;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 28, 34);
        }
    }
}
