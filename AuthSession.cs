namespace CSharp_ImGui_Client
{
    public static class AuthSession
    {
        public static api? KeyAuth { get; set; }
        public static string Username { get; set; } = "User";
        public static string DaysLeft { get; set; } = "—";
        public static bool IsAuthenticated { get; set; }
    }
}
