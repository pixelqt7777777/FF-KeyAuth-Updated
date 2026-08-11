using System;

namespace CSharp_ImGui_Client
{
    public static class KeyAuthAppService
    {
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized && AuthSession.KeyAuth != null)
                return;

            AuthSession.KeyAuth = new api(
                name: KeyAuthConfig.AppName,
                ownerid: KeyAuthConfig.OwnerId,
                version: KeyAuthConfig.Version);

            AuthSession.KeyAuth.init();
            _initialized = true;
        }

        public static (bool ok, string message) LoginWithLicense(string licenseKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(licenseKey))
                    return (false, "Enter a license key.");

                EnsureInitialized();
                var auth = AuthSession.KeyAuth!;
                auth.license(licenseKey.Trim());

                if (!auth.response.success)
                    return (false, string.IsNullOrWhiteSpace(auth.response.message)
                        ? "Invalid license key."
                        : auth.response.message);

                ApplyUserSession(auth);
                return (true, "License authenticated.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool ok, string message) LoginWithCredentials(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                    return (false, "Enter username and password.");

                EnsureInitialized();
                var auth = AuthSession.KeyAuth!;
                auth.login(username.Trim(), password);

                if (!auth.response.success)
                    return (false, string.IsNullOrWhiteSpace(auth.response.message)
                        ? "Login failed. Check username and password."
                        : auth.response.message);

                ApplyUserSession(auth);
                return (true, "Welcome back.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static void ApplyUserSession(api auth)
        {
            AuthSession.IsAuthenticated = true;
            AuthSession.Username = string.IsNullOrWhiteSpace(auth.user_data.username)
                ? "Licensed"
                : auth.user_data.username;

            string days = "Active";

            try
            {
                if (auth.user_data.subscriptions != null &&
                    auth.user_data.subscriptions.Count > 0)
                {
                    string? d = auth.expirydaysleft("days", 0);
                    if (!string.IsNullOrWhiteSpace(d))
                        days = d + " Days";
                }
            }
            catch
            {
                days = "Active";
            }

            AuthSession.DaysLeft = days;
        }
    }
}
