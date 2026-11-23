using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace Vape
{
    internal static class VapeService
    {
        private static TcpListener listener;
        private static Thread acceptThread;
        private static volatile bool running;

        public static bool IsRunning
        {
            get
            {
                return running;
            }
        }

        public static void Start()
        {
            if (running)
            {
                return;
            }
            VapeStore.EnsureLoaded();
            try
            {
                listener = new TcpListener(IPAddress.Any, 8080);
                listener.Start();
            }
            catch (Exception)
            {
                running = false;
                return;
            }
            running = true;
            LogLine("vape service listening on http://" + VapeLanAddress.Get() + ":8080"
                    + " (and 127.0.0.1:8080)");
            acceptThread = new Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        private static void AcceptLoop()
        {
            while (running)
            {
                Socket client;
                try
                {
                    client = listener.AcceptSocket();
                }
                catch (Exception)
                {
                    break;
                }
                Thread handler = new Thread(delegate()
                {
                    HandleClient(client);
                });
                handler.IsBackground = true;
                handler.Start();
            }
        }

        private static void HandleClient(Socket client)
        {
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 10000;
                    string requestText = ReadRequest(client);
                    if (requestText == null)
                    {
                        return;
                    }
                    int firstLineEnd = requestText.IndexOf("\r\n", StringComparison.Ordinal);
                    string requestLine = firstLineEnd >= 0
                            ? requestText.Substring(0, firstLineEnd)
                            : requestText;
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2)
                    {
                        return;
                    }
                    string method = parts[0];
                    string rawTarget = parts[1];
                    int queryStart = rawTarget.IndexOf('?');
                    string path = queryStart >= 0 ? rawTarget.Substring(0, queryStart) : rawTarget;
                    string query = queryStart >= 0 ? rawTarget.Substring(queryStart + 1) : "";
                    string body = ExtractBody(requestText);
                    Response response = Route(method, path, query, body);
                    byte[] payload = response.RawBody != null
                            ? response.RawBody
                            : Encoding.UTF8.GetBytes(response.Body);
                    string header = "HTTP/1.1 200 OK\r\nContent-Type: "
                            + response.ContentType + "\r\nContent-Length: "
                            + payload.Length + "\r\nConnection: close\r\n\r\n";
                    byte[] headerBytes = Encoding.ASCII.GetBytes(header);
                    client.Send(headerBytes);
                    client.Send(payload);
                    LogLine(method + " " + path + " -> 200");
                }
            }
            catch (Exception)
            {
            }
        }

        private class Response
        {
            public string Body;
            public string ContentType;
            public byte[] RawBody;

            public Response(string body, string contentType)
            {
                this.Body = body;
                this.ContentType = contentType;
            }

            public static Response Binary(byte[] data, string contentType)
            {
                Response response = new Response(null, contentType);
                response.RawBody = data;
                return response;
            }
        }

        private static string ReadRequest(Socket client)
        {
            byte[] buffer = new byte[8192];
            StringBuilder builder = new StringBuilder();
            int headerEnd = -1;
            int contentLength = 0;
            while (true)
            {
                if (headerEnd < 0)
                {
                    int read = client.Receive(buffer);
                    if (read <= 0)
                    {
                        break;
                    }
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    headerEnd = builder.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal);
                    if (headerEnd >= 0)
                    {
                        string headers = builder.ToString(0, headerEnd);
                        Match match = Regex.Match(headers, "Content-Length:\\s*(\\d+)",
                                RegexOptions.IgnoreCase);
                        contentLength = match.Success ? int.Parse(match.Groups[1].Value) : 0;
                    }
                }
                else
                {
                    int have = builder.Length - (headerEnd + 4);
                    if (have >= contentLength)
                    {
                        break;
                    }
                    int read = client.Receive(buffer);
                    if (read <= 0)
                    {
                        break;
                    }
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, read));
                }
                if (builder.Length > 4 * 1024 * 1024)
                {
                    break;
                }
            }
            string requestText = builder.ToString();
            return requestText.Length == 0 ? null : requestText;
        }

        private static string ExtractBody(string requestText)
        {
            int headerEnd = requestText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0)
            {
                return null;
            }
            string body = requestText.Substring(headerEnd + 4);
            return body.Length == 0 ? null : body;
        }

        private static string Json(string body)
        {
            return new Response(body, "application/json").Body;
        }

        /*
         * Local recovery connector: backs the recovered client's online API
         * with a self-contained profile, friends, and public-config store.
         */
        private static Response Route(string method, string path, string query, string body)
        {
            if (path == "/" || path == "/login" || path == "/index.html")
            {
                return new Response(WebsiteHtml(), "text/html");
            }
            if (path == "/api/web/pfp")
            {
                return PfpResponse();
            }
            if (path.StartsWith("/api/web/"))
            {
                return new Response(RouteWeb(method, path, query, body), "application/json");
            }
            if (path.EndsWith("/authenticated"))
            {
                VapeStore.EnsureLoaded();
                VapeStore.Profile profile = VapeStore.GetLocalProfile();
                if (profile != null)
                {
                    return new Response("{\"successful\":true,\"error\":null,\"data\":{"
                            + "\"userId\":" + profile.id + ","
                            + "\"username\":" + JsonString(profile.username) + ","
                            + "\"accountCreation\":" + JsonString(profile.createdAt) + ","
                            + "\"licensed\":true,"
                            + "\"registered\":true,"
                            + "\"profiles\":true,"
                            + "\"banned\":false}}", "application/json");
                }
                return new Response("{\"successful\":true,\"error\":null,\"data\":{"
                        + "\"userId\":-1,"
                        + "\"username\":null,"
                        + "\"accountCreation\":null,"
                        + "\"licensed\":true,"
                        + "\"registered\":false,"
                        + "\"profiles\":true,"
                        + "\"banned\":false}}", "application/json");
            }
            if (path.Contains("/register/"))
            {
                int marker = path.IndexOf("/register/");
                string username = path.Substring(marker + "/register/".Length);
                if (username.Length == 0 || username.Length > 16
                        || !Regex.IsMatch(username, "^[a-zA-Z0-9_.-]+$"))
                {
                    return new Response("{\"successful\":false,"
                            + "\"error\":\"Invalid username\",\"data\":null}", "application/json");
                }
                VapeStore.EnsureLoaded();
                VapeStore.Profile existing = VapeStore.FindProfileByUsername(username);
                if (existing != null)
                {
                    VapeStore.SetActiveProfile(existing.id);
                    LogLine("activated existing profile by name: " + existing.username
                            + " (#" + existing.id + ")");
                    return new Response("{\"successful\":true,\"error\":null,\"data\":true}",
                            "application/json");
                }
                VapeStore.Profile profile = VapeStore.CreateProfile(username);
                LogLine("registered profile: " + profile.username + " (#" + profile.id + ")");
                return new Response("{\"successful\":true,\"error\":null,\"data\":true}",
                        "application/json");
            }
            if (path.EndsWith("/profile/private/all"))
            {
                VapeStore.EnsureLoaded();
                StringBuilder profilesJson = new StringBuilder();
                Dictionary<string, string> saved = VapeStore.ListPrivateProfiles();
                bool first = true;
                foreach (KeyValuePair<string, string> entry in saved)
                {
                    if (!first)
                    {
                        profilesJson.Append(",");
                    }
                    first = false;
                    profilesJson.Append("\"" + entry.Key + "\":" + entry.Value);
                }
                return new Response("{\"successful\":true,\"error\":null,\"data\":{"
                        + "\"friends\":[],"
                        + "\"profiles\":{" + profilesJson + "},"
                        + "\"publicProfiles\":{},"
                        + "\"otherData\":[]}}", "application/json");
            }
            if (path.Contains("/profile/private/save/user"))
            {
                return new Response("{\"successful\":true,\"error\":null,\"data\":true}",
                        "application/json");
            }
            if (path.Contains("/profile/private/save/profile"))
            {
                string profileId = "00000000-0000-0000-0000-000000000000";
                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    Dictionary<string, object> payload =
                            serializer.Deserialize<Dictionary<string, object>>(body ?? "{}");
                    if (payload != null && payload.ContainsKey("profileId")
                            && payload["profileId"] != null)
                    {
                        profileId = Convert.ToString(payload["profileId"]);
                    }
                }
                catch (Exception)
                {
                }
                VapeStore.EnsureLoaded();
                VapeStore.SavePrivateProfile(profileId, body ?? "{}");
                LogLine("auto-saved private profile " + profileId);
                return new Response("{\"successful\":true,\"error\":null,\"data\":{}}",
                        "application/json");
            }
            if (path.EndsWith("/profile/public/tags"))
            {
                return new Response("{\"successful\":true,\"error\":null,"
                        + "\"data\":[\"legit\",\"pvp\",\"bedwars\",\"blatant\"]}", "application/json");
            }
            if (path.EndsWith("/profile/public/list"))
            {
                VapeStore.EnsureLoaded();
                List<VapeStore.PublicConfig> configs = VapeStore.ListConfigs();
                StringBuilder json = new StringBuilder();
                json.Append("{\"successful\":true,\"error\":null,\"data\":{");
                json.Append("\"content\":[");
                for (int index = 0; index < configs.Count; ++index)
                {
                    if (index > 0)
                    {
                        json.Append(",");
                    }
                    AppendConfigSummary(json, configs[index]);
                }
                json.Append("],");
                json.Append("\"last\":true,");
                json.Append("\"totalPages\":1,");
                json.Append("\"totalElements\":" + configs.Count + ",");
                json.Append("\"size\":20,");
                json.Append("\"numberOfElements\":" + configs.Count + "}}");
                return new Response(json.ToString(), "application/json");
            }
            if (path.EndsWith("/profile/public/create"))
            {
                return new Response(CreatePublicConfig(body), "application/json");
            }
            Match downloadMatch = Regex.Match(path,
                    "/profile/public/(\\d+)/download$");
            if (downloadMatch.Success)
            {
                VapeStore.EnsureLoaded();
                long configId = long.Parse(downloadMatch.Groups[1].Value);
                VapeStore.PublicConfig config = VapeStore.GetConfig(configId);
                if (config == null)
                {
                    return new Response("{\"successful\":false,\"error\":\"Not found\",\"data\":null}",
                            "application/json");
                }
                VapeStore.IncrementDownloads(configId);
                string downloadUuid = "00000000-0000-0000-0000-" + configId.ToString("000000000000");
                return new Response("{\"successful\":true,\"error\":null,\"data\":{"
                        + "\"uuid\":\"" + downloadUuid + "\","
                        + "\"profileId\":\"" + downloadUuid + "\","
                        + "\"name\":" + JsonString(config.name) + ","
                        + "\"vapeVersion\":\"4.21\","
                        + "\"data\":" + (config.data == null ? "{}" : config.data) + ","
                        + "\"metadata\":null}}", "application/json");
            }
            Match viewMatch = Regex.Match(path, "/profile/public/(\\d+)/view$");
            if (viewMatch.Success)
            {
                VapeStore.EnsureLoaded();
                long configId = long.Parse(viewMatch.Groups[1].Value);
                VapeStore.PublicConfig config = VapeStore.GetConfig(configId);
                if (config == null)
                {
                    return new Response("{\"successful\":false,\"error\":\"Not found\",\"data\":null}",
                            "application/json");
                }
                StringBuilder json = new StringBuilder();
                json.Append("{\"successful\":true,\"error\":null,\"data\":");
                AppendConfigDetail(json, config);
                json.Append("}");
                return new Response(json.ToString(), "application/json");
            }
            if (path.Contains("/settings/save/"))
            {
                int marker = path.IndexOf("/settings/save/");
                string scope = path.Substring(marker + "/settings/save/".Length);
                VapeStore.EnsureLoaded();
                VapeStore.SaveSettings(scope, body);
                LogLine("auto-saved settings scope '" + scope + "' ("
                        + (body == null ? 0 : body.Length) + " bytes)");
                return new Response(body == null ? "{}" : body, "application/json");
            }
            if (path.Contains("/settings/load/"))
            {
                int marker = path.IndexOf("/settings/load/");
                string scope = path.Substring(marker + "/settings/load/".Length);
                VapeStore.EnsureLoaded();
                string stored = VapeStore.GetSettings(scope);
                if (stored != null)
                {
                    return new Response("{\"successful\":true,\"error\":null,\"data\":"
                            + stored + "}", "application/json");
                }
                return new Response("{\"successful\":true,\"error\":null,\"data\":{}}",
                        "application/json");
            }
            if (path.EndsWith("/profile/public/review/view")
                    || path.EndsWith("/sharecode")
                    || path.EndsWith("/regenerate/sharecode"))
            {
                return new Response("{\"successful\":true,\"error\":null,\"data\":{}}",
                        "application/json");
            }
            return new Response("{\"successful\":true,\"error\":null,\"data\":true}",
                    "application/json");
        }

        private static byte[] pfpCache;
        private static string pfpCacheName;

        private static Response PfpResponse()
        {
            VapeStore.EnsureLoaded();
            VapeStore.Profile profile = VapeStore.GetLocalProfile();
            string mcName = profile != null ? profile.minecraftUsername : null;
            if (mcName != null && mcName.Length > 0)
            {
                lock (typeof(VapeService))
                {
                    if (VapeService.pfpCache != null
                            && VapeService.pfpCacheName == mcName)
                    {
                        return Response.Binary(VapeService.pfpCache, "image/png");
                    }
                }
                try
                {
                    using (System.Net.WebClient web = new System.Net.WebClient())
                    {
                        web.Proxy = null;
                        byte[] image = web.DownloadData(
                                "https://mc-heads.net/avatar/"
                                + Uri.EscapeDataString(mcName) + "/64");
                        lock (typeof(VapeService))
                        {
                            VapeService.pfpCache = image;
                            VapeService.pfpCacheName = mcName;
                        }
                        LogLine("skin head fetched for " + mcName);
                        return Response.Binary(image, "image/png");
                    }
                }
                catch (Exception)
                {
                }
            }
            return Response.Binary(Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJ"
                    + "AAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="),
                    "image/png");
        }

        private static string RouteWeb(string method, string path, string query, string body)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            if (path == "/api/web/state")
            {
                VapeStore.EnsureLoaded();
                VapeStore.Profile profile = VapeStore.GetLocalProfile();
                Dictionary<string, object> state = new Dictionary<string, object>();
                if (profile != null)
                {
                    Dictionary<string, object> profileJson = new Dictionary<string, object>();
                    profileJson["id"] = profile.id;
                    profileJson["username"] = profile.username;
                    profileJson["minecraftUsername"] = profile.minecraftUsername;
                    state["profile"] = profileJson;
                }
                else
                {
                    state["profile"] = null;
                }
                state["zeusOnline"] = VapeZeusServer.IsRunning;
                return serializer.Serialize(state);
            }
            if (path == "/api/web/register")
            {
                string username = QueryValue(query, body, "username");
                string password = QueryValue(query, body, "password");
                if (username == null || username.Length == 0 || username.Length > 16
                        || !Regex.IsMatch(username, "^[a-zA-Z0-9_.-]+$"))
                {
                    return "{\"ok\":false,\"error\":\"invalid username\"}";
                }
                if (password == null || password.Length < 3)
                {
                    return "{\"ok\":false,\"error\":\"password too short\"}";
                }
                VapeStore.EnsureLoaded();
                VapeStore.Profile profile = VapeStore.CreateProfile(username, password);
                if (profile == null)
                {
                    return "{\"ok\":false,\"error\":\"username taken\"}";
                }
                LogLine("registered account (web): " + profile.username);
                return "{\"ok\":true,\"username\":" + JsonString(profile.username) + "}";
            }
            if (path == "/api/web/login")
            {
                string username = QueryValue(query, body, "username");
                string password = QueryValue(query, body, "password");
                VapeStore.EnsureLoaded();
                VapeStore.Profile profile = VapeStore.Login(username, password);
                if (profile == null)
                {
                    return "{\"ok\":false,\"error\":\"wrong username or password\"}";
                }
                LogLine("login (web): " + profile.username);
                return "{\"ok\":true,\"username\":" + JsonString(profile.username) + "}";
            }
            return "{}";
        }

        private static Dictionary<string, object> StoreConfigToMap(VapeStore.PublicConfig config)
        {
            Dictionary<string, object> map = new Dictionary<string, object>();
            map["id"] = config.id;
            map["name"] = config.name;
            map["description"] = config.description;
            map["ownerName"] = config.ownerName;
            map["tags"] = config.tags;
            map["downloads"] = config.downloads;
            map["likes"] = config.likes;
            return map;
        }

        private static string QueryValue(string query, string body, string key)
        {
            foreach (string pair in (query ?? "").Split('&'))
            {
                int equals = pair.IndexOf('=');
                if (equals > 0 && pair.Substring(0, equals) == key)
                {
                    return Uri.UnescapeDataString(pair.Substring(equals + 1));
                }
            }
            if (body != null && body.TrimStart().StartsWith("{"))
            {
                try
                {
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    Dictionary<string, object> json = serializer.Deserialize<
                            Dictionary<string, object>>(body);
                    if (json != null && json.ContainsKey(key) && json[key] != null)
                    {
                        return Convert.ToString(json[key]);
                    }
                }
                catch (Exception)
                {
                }
            }
            return null;
        }

        private static string CreatePublicConfig(string body)
        {
            VapeStore.EnsureLoaded();
            VapeStore.Profile profile = VapeStore.GetLocalProfile();
            if (profile == null)
            {
                return "{\"successful\":false,\"error\":\"Not registered\",\"data\":null}";
            }
            string name = "Config";
            string description = "";
            List<string> tags = new List<string>();
            string dataJson = "{}";
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> payload =
                        serializer.Deserialize<Dictionary<string, object>>(body ?? "{}");
                if (payload != null)
                {
                    if (payload.ContainsKey("name") && payload["name"] != null)
                    {
                        name = Convert.ToString(payload["name"]);
                    }
                    if (payload.ContainsKey("description") && payload["description"] != null)
                    {
                        description = Convert.ToString(payload["description"]);
                    }
                    if (payload.ContainsKey("tags") && payload["tags"] != null)
                    {
                        foreach (string tag in ExtractTags(payload["tags"]))
                        {
                            tags.Add(tag);
                        }
                    }
                    if (payload.ContainsKey("profileData") && payload["profileData"] != null)
                    {
                        dataJson = serializer.Serialize(payload["profileData"]);
                    }
                    else if (payload.ContainsKey("data") && payload["data"] != null)
                    {
                        dataJson = serializer.Serialize(payload["data"]);
                    }
                }
            }
            catch (Exception)
            {
            }
            VapeStore.PublicConfig config = VapeStore.CreateConfig(name, description, tags,
                    profile.username, profile.id, dataJson);
            LogLine("created public config: " + config.name + " (#" + config.id + ")");
            StringBuilder json = new StringBuilder();
            json.Append("{\"successful\":true,\"error\":null,\"data\":");
            AppendConfigDetail(json, config);
            json.Append("}");
            return json.ToString();
        }

        private static List<string> ExtractTags(object tagsValue)
        {
            List<string> result = new List<string>();
            if (tagsValue == null)
            {
                return result;
            }
            if (tagsValue is string text)
            {
                foreach (string part in text.Split(new[] { ',', ' ', '\n', '\r', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = part.Trim();
                    if (trimmed.Length > 0)
                    {
                        result.Add(trimmed);
                    }
                }
                return result;
            }
            System.Collections.IEnumerable enumerable = tagsValue as System.Collections.IEnumerable;
            if (enumerable != null && !(tagsValue is System.Collections.IDictionary))
            {
                foreach (object item in enumerable)
                {
                    if (item != null)
                    {
                        string itemText = Convert.ToString(item);
                        if (itemText != null && itemText.Length > 0)
                        {
                            result.Add(itemText);
                        }
                    }
                }
                return result;
            }
            return result;
        }

        private static void AppendConfigSummary(StringBuilder json, VapeStore.PublicConfig config)
        {
            json.Append("{");
            json.Append("\"profileId\":" + config.id + ",");
            json.Append("\"owner\":{\"userId\":" + config.ownerId
                    + ",\"username\":" + JsonString(config.ownerName) + "},");
            json.Append("\"name\":" + JsonString(config.name) + ",");
            json.Append("\"version\":" + config.version + ",");
            json.Append("\"likes\":" + config.likes + ",");
            json.Append("\"dislikes\":0,");
            json.Append("\"tags\":" + JsonArray(config.tags) + ",");
            json.Append("\"shareCode\":null");
            json.Append("}");
        }

        private static void AppendConfigDetail(StringBuilder json, VapeStore.PublicConfig config)
        {
            json.Append("{");
            json.Append("\"profileId\":" + config.id + ",");
            json.Append("\"owner\":{\"userId\":" + config.ownerId
                    + ",\"username\":" + JsonString(config.ownerName) + "},");
            json.Append("\"name\":" + JsonString(config.name) + ",");
            json.Append("\"description\":" + JsonString(config.description) + ",");
            json.Append("\"data\":" + (config.data == null ? "{}" : config.data) + ",");
            json.Append("\"shareCode\":\"\",");
            json.Append("\"version\":" + config.version + ",");
            json.Append("\"likes\":" + config.likes + ",");
            json.Append("\"dislikes\":0,");
            json.Append("\"downloads\":" + config.downloads + ",");
            json.Append("\"creationDate\":" + JsonString(config.createdAt) + ",");
            json.Append("\"updatedDate\":null,");
            json.Append("\"tags\":" + JsonArray(config.tags) + ",");
            json.Append("\"reviews\":{\"content\":[],\"last\":true,\"totalPages\":0,"
                    + "\"totalElements\":0,\"size\":0,\"numberOfElements\":0},");
            json.Append("\"metadata\":{\"derivedFrom\":null,\"unreadNotifications\":0,"
                    + "\"shareCode\":\"\",\"listedPublicly\":true,"
                    + "\"shareCodeFriendsOnly\":false,\"uploadAnonymously\":false}");
            json.Append("}");
        }

        private static string JsonString(string value)
        {
            if (value == null)
            {
                return "null";
            }
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(value);
        }

        private static string JsonArray(List<string> values)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(values == null ? new List<string>() : values);
        }

        private static string WebsiteHtml()
        {
            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\">"
                + "<title>Vape V4 Login</title><style>"
                + "body{background:#181818;color:#e8e8e8;font-family:Segoe UI,Arial,sans-serif;"
                + "margin:0;display:flex;flex-direction:column;align-items:center;"
                + "min-height:100vh}"
                + ".card{background:#242424;border-radius:14px;padding:30px;margin-top:60px;"
                + "width:380px;box-shadow:0 0 30px rgba(255,255,255,0.05);text-align:center}"
                + "h1{font-size:36px;letter-spacing:4px;font-weight:800;margin:0 0 4px}"
                + ".sub{color:#888;font-size:13px;margin-bottom:22px}"
                + "input{width:100%;box-sizing:border-box;padding:12px;border-radius:8px;"
                + "border:1px solid #3e3e3e;background:#1a1a1a;color:#fff;font-size:14px;"
                + "margin-bottom:10px;outline:none}"
                + "input:focus{border-color:#16704f}"
                + "button{width:100%;padding:12px;border-radius:8px;border:none;cursor:pointer;"
                + "background:#16704f;color:#fff;font-size:14px;font-weight:600;margin-bottom:8px}"
                + "button.ghost{background:#2e2e2e;color:#ccc}"
                + ".pfp{width:84px;height:84px;border-radius:10px;margin-bottom:14px;"
                + "image-rendering:pixelated}"
                + ".row{display:flex;justify-content:space-between;padding:8px 0;"
                + "border-bottom:1px solid #2e2e2e;font-size:14px;text-align:left}"
                + ".hint{color:#888;font-size:12px;margin-top:14px;line-height:1.6}"
                + "#msg{color:#c06060;font-size:12px;min-height:16px}"
                + "</style></head><body>"
                + "<div class=\"card\"><h1>VAPE</h1>"
                + "<div class=\"sub\">V4 &middot; online login</div>"
                + "<div id=\"view\"></div></div>"
                + "<script>"
                + "function enc(s){return encodeURIComponent(s)}"
                + "function state(){fetch('/api/web/state').then(function(r){return r.json()})"
                + ".then(render)}"
                + "function render(s){var v=document.getElementById('view');"
                + "if(s.profile){var pfp="
                + "(s.profile.minecraftUsername?'<img class=pfp src=/api/web/pfp>':"
                + "'<div class=pfp style=\"background:#2e2e2e;line-height:84px;color:#555;font-size:11px\">no skin yet</div>');"
                + "v.innerHTML="
                + "pfp"
                + "+'<div class=row><span>Signed in</span><b>'+s.profile.username+'</b></div>'"
                + "+'<div class=row><span>Online service</span><b>'+(s.zeusOnline?'ready':'off')+'</b></div>'"
                + "+'<div class=row><span>Skin pfp</span><b>'+(s.profile.minecraftUsername||'waiting for game')+'</b></div>'"
                + "+'<div class=hint>Open Vape in-game, press <b>Login to Vape</b>, '"
                + "+'type your username. Friends and public profiles live inside the client.</div>';"
                + "}else{v.innerHTML="
                + "'<input id=u maxlength=16 placeholder=Username>'"
                + "+'<input id=p type=password placeholder=Password>'"
                + "+'<button onclick=reg()>Create account</button>'"
                + "+'<button class=ghost onclick=log()>Log in</button>'"
                + "+'<div id=msg></div>';}}"
                + "function post(url,u,p){fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},"
                + "body:JSON.stringify({username:u,password:p})}).then(function(r){return r.json()})"
                + ".then(function(j){if(j.ok){state()}else{"
                + "document.getElementById('msg').textContent=j.error}})}"
                + "function reg(){var u=document.getElementById('u').value;"
                + "var p=document.getElementById('p').value;if(!u||!p){return}"
                + "post('/api/web/register',u,p)}"
                + "function log(){var u=document.getElementById('u').value;"
                + "var p=document.getElementById('p').value;if(!u||!p){return}"
                + "post('/api/web/login',u,p)}"
                + "state();"
                + "</script></body></html>";
        }

        private static void LogLine(string text)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "vape-service.log");
                lock (typeof(VapeService))
                {
                    File.AppendAllText(logPath,
                            "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] "
                                    + text + Environment.NewLine);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
