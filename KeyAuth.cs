using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace CSharp_ImGui_Client
{
    /// <summary>
    /// Minimal KeyAuth API 1.3 client used by this project.
    /// Supports init, license login, username/password login and subscription expiry.
    /// </summary>
    public class api
    {
        private const string Endpoint = "https://keyauth.win/api/1.3/";
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        public readonly string name;
        public readonly string ownerid;
        public readonly string version;
        public readonly string? path;

        private string? _sessionId;
        private bool _initialized;

        public response_class response { get; } = new response_class();
        public user_data_class user_data { get; } = new user_data_class();

        public api(string name, string ownerid, string version, string? path = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("KeyAuth app name is missing.", nameof(name));

            if (string.IsNullOrWhiteSpace(ownerid) || ownerid.Length != 10)
                throw new ArgumentException("KeyAuth Owner ID must be exactly 10 characters.", nameof(ownerid));

            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("KeyAuth app version is missing.", nameof(version));

            this.name = name;
            this.ownerid = ownerid;
            this.version = version;
            this.path = path;
        }

        public void init()
        {
            if (_initialized)
                return;

            var args = new Dictionary<string, string?>
            {
                ["type"] = "init",
                ["ver"] = version,
                ["name"] = name,
                ["ownerid"] = ownerid,
                ["hash"] = GetExecutableSha256(),
                ["token"] = "undefined",
                ["thash"] = "undefined"
            };

            var json = Request(args);
            ApplyResponse(json);

            if (!response.success)
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(response.message)
                        ? "KeyAuth initialization failed."
                        : response.message);

            _sessionId = GetString(json, "sessionid");
            if (string.IsNullOrWhiteSpace(_sessionId))
                throw new InvalidOperationException("KeyAuth did not return a session ID.");

            _initialized = true;
        }

        public void license(string key, string? code = null)
        {
            CheckInit();

            if (string.IsNullOrWhiteSpace(key))
            {
                response.success = false;
                response.message = "Enter a license key.";
                return;
            }

            var args = new Dictionary<string, string?>
            {
                ["type"] = "license",
                ["key"] = key.Trim(),
                ["sessionid"] = _sessionId,
                ["name"] = name,
                ["ownerid"] = ownerid,
                ["hwid"] = GetHwid(),
                ["code"] = code ?? string.Empty
            };

            var json = Request(args);
            ApplyResponse(json);
            if (response.success)
                ApplyUserData(json);
        }

        public void login(string username, string pass, string? code = null)
        {
            CheckInit();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(pass))
            {
                response.success = false;
                response.message = "Enter username and password.";
                return;
            }

            var args = new Dictionary<string, string?>
            {
                ["type"] = "login",
                ["username"] = username.Trim(),
                ["pass"] = pass,
                ["sessionid"] = _sessionId,
                ["name"] = name,
                ["ownerid"] = ownerid,
                ["hwid"] = GetHwid(),
                ["code"] = code ?? string.Empty
            };

            var json = Request(args);
            ApplyResponse(json);
            if (response.success)
                ApplyUserData(json);
        }

        public string? expirydaysleft(string type, int subscription)
        {
            CheckInit();

            if (user_data.subscriptions == null ||
                subscription < 0 ||
                subscription >= user_data.subscriptions.Count)
                return null;

            if (!long.TryParse(user_data.subscriptions[subscription].expiry, out long unixSeconds))
                return null;

            DateTimeOffset expiry = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            TimeSpan remaining = expiry - DateTimeOffset.Now;

            return type.ToLowerInvariant() switch
            {
                "months" => Math.Max(0, remaining.Days / 30).ToString(),
                "days" => Math.Max(0, (int)Math.Ceiling(remaining.TotalDays)).ToString(),
                "hours" => Math.Max(0, (int)Math.Ceiling(remaining.TotalHours)).ToString(),
                _ => null
            };
        }

        private void CheckInit()
        {
            if (!_initialized || string.IsNullOrWhiteSpace(_sessionId))
                throw new InvalidOperationException("KeyAuth is not initialized. Call init() first.");
        }

        private static JsonDocument Request(Dictionary<string, string?> parameters)
        {
            try
            {
                var parts = new List<string>();
                foreach (var pair in parameters)
                {
                    parts.Add(
                        Uri.EscapeDataString(pair.Key) + "=" +
                        Uri.EscapeDataString(pair.Value ?? string.Empty));
                }

                string url = Endpoint + "?" + string.Join("&", parts);
                string body = Http.GetStringAsync(url).GetAwaiter().GetResult();

                if (string.IsNullOrWhiteSpace(body))
                    throw new InvalidOperationException("KeyAuth returned an empty response.");

                return JsonDocument.Parse(body);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Unable to connect to KeyAuth: " + ex.Message, ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException("KeyAuth request timed out.", ex);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("KeyAuth returned an invalid response.", ex);
            }
        }

        private void ApplyResponse(JsonDocument json)
        {
            JsonElement root = json.RootElement;

            response.success =
                root.TryGetProperty("success", out JsonElement success) &&
                success.ValueKind == JsonValueKind.True;

            response.message = GetString(json, "message") ?? string.Empty;
        }

        private void ApplyUserData(JsonDocument json)
        {
            JsonElement root = json.RootElement;
            if (!root.TryGetProperty("info", out JsonElement info) ||
                info.ValueKind != JsonValueKind.Object)
                return;

            user_data.username = GetString(info, "username") ?? string.Empty;
            user_data.ip = GetString(info, "ip") ?? string.Empty;
            user_data.hwid = GetString(info, "hwid") ?? string.Empty;
            user_data.createdate = GetString(info, "createdate") ?? string.Empty;
            user_data.lastlogin = GetString(info, "lastlogin") ?? string.Empty;
            user_data.subscriptions = new List<Data>();

            if (info.TryGetProperty("subscriptions", out JsonElement subs) &&
                subs.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement sub in subs.EnumerateArray())
                {
                    user_data.subscriptions.Add(new Data
                    {
                        subscription = GetString(sub, "subscription") ?? string.Empty,
                        expiry = GetString(sub, "expiry") ?? string.Empty,
                        timeleft = GetString(sub, "timeleft") ?? string.Empty,
                        key = GetString(sub, "key") ?? string.Empty
                    });
                }
            }
        }

        private static string? GetString(JsonDocument json, string property) =>
            GetString(json.RootElement, property);

        private static string? GetString(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out JsonElement value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }

        private static string GetHwid()
        {
            try
            {
                return WindowsIdentity.GetCurrent().User?.Value
                       ?? Environment.MachineName;
            }
            catch
            {
                return Environment.MachineName;
            }
        }

        private static string GetExecutableSha256()
        {
            try
            {
                string? file = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(file) || !System.IO.File.Exists(file))
                    return "undefined";

                using var stream = System.IO.File.OpenRead(file);
                byte[] hash = SHA256.HashData(stream);
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            catch
            {
                return "undefined";
            }
        }

        public class response_class
        {
            public bool success { get; set; }
            public string message { get; set; } = string.Empty;
        }

        public class user_data_class
        {
            public string username { get; set; } = string.Empty;
            public string ip { get; set; } = string.Empty;
            public string hwid { get; set; } = string.Empty;
            public string createdate { get; set; } = string.Empty;
            public string lastlogin { get; set; } = string.Empty;
            public List<Data> subscriptions { get; set; } = new List<Data>();
        }

        public class Data
        {
            public string subscription { get; set; } = string.Empty;
            public string expiry { get; set; } = string.Empty;
            public string timeleft { get; set; } = string.Empty;
            public string key { get; set; } = string.Empty;
        }
    }
}
