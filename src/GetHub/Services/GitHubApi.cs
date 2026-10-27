using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace GetHub.Services
{
    // Shared plumbing for the authenticated GitHub calls (account, issues and
    // projects). The downloader keeps its own long-timeout client because archive
    // downloads are much slower than these metadata requests.
    public static class GitHubApi
    {
        public const string UserAgent = "GetHub/1.0";
        public const string ApiRoot = "https://api.github.com";
        public const string GraphQlUrl = "https://api.github.com/graphql";

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        static GitHubApi()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
            }
        }

        public static HttpClient CreateClient()
        {
            return CreateClient(TimeSpan.FromSeconds(45));
        }

        public static HttpClient CreateClient(TimeSpan timeout)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = true;
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            HttpClient client = new HttpClient(handler);
            client.Timeout = timeout;
            return client;
        }

        public static HttpRequestMessage CreateRequest(string url, string token, string accept)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd(
                string.IsNullOrEmpty(accept) ? "application/vnd.github+json" : accept);
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            }

            return request;
        }

        public static async Task<HttpResponseMessage> SendAsync(
            HttpClient client,
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                throw new GitHubException("GitHub did not answer in time. Check your connection and try again.");
            }
            catch (HttpRequestException ex)
            {
                throw new GitHubException(
                    "Could not reach GitHub. Check your connection and try again. (" + ex.Message + ")",
                    ex);
            }
        }

        public static async Task<string> GetStringAsync(
            string url,
            string token,
            CancellationToken cancellationToken)
        {
            using (HttpClient client = CreateClient())
            using (HttpRequestMessage request = CreateRequest(url, token, "application/vnd.github+json"))
            using (HttpResponseMessage response = await SendAsync(client, request, cancellationToken).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                ThrowIfFailed(response, body);
                return body;
            }
        }

        public static async Task<string> PostJsonAsync(
            string url,
            string token,
            string json,
            CancellationToken cancellationToken)
        {
            using (HttpClient client = CreateClient())
            using (HttpRequestMessage request = CreateRequest(url, token, "application/vnd.github+json"))
            {
                request.Method = HttpMethod.Post;
                request.Content = new StringContent(json ?? "{}", Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await SendAsync(client, request, cancellationToken).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    ThrowIfFailed(response, body);
                    return body;
                }
            }
        }

        public static void ThrowIfFailed(HttpResponseMessage response, string body)
        {
            if (response == null || response.IsSuccessStatusCode)
            {
                return;
            }

            int status = (int)response.StatusCode;
            string message = ReadApiMessage(body);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new GitHubException(
                    "GitHub rejected the access token. It may have expired or been revoked. Sign in again.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                if (string.IsNullOrEmpty(message) &&
                    response.Headers.RetryAfter != null)
                {
                    throw new GitHubException(
                        "GitHub's API rate limit was reached. Wait a few minutes, or sign in with an account token.");
                }

                throw new GitHubException(
                    string.IsNullOrEmpty(message)
                        ? "GitHub refused the request. The token may be missing a required permission."
                        : "GitHub refused the request: " + message);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new GitHubException(
                    "GitHub could not find this resource. It may have been deleted, or the token may not have access to it.");
            }

            throw new GitHubException(
                string.IsNullOrEmpty(message)
                    ? "GitHub returned HTTP " + status.ToString(CultureInfo.InvariantCulture) + "."
                    : "GitHub returned HTTP " + status.ToString(CultureInfo.InvariantCulture) + ": " + message);
        }

        public static string ReadApiMessage(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return string.Empty;
            }

            try
            {
                Dictionary<string, object> map = DeserializeObject(body);
                object value;
                if (map != null && map.TryGetValue("message", out value) && value != null)
                {
                    string message = Convert.ToString(value, CultureInfo.InvariantCulture);
                    object errors;
                    if (map.TryGetValue("errors", out errors))
                    {
                        string detail = DescribeErrors(errors);
                        if (!string.IsNullOrEmpty(detail))
                        {
                            message += " (" + detail + ")";
                        }
                    }

                    return message;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string DescribeErrors(object errors)
        {
            List<object> list = AsList(errors);
            if (list == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (object entry in list)
            {
                Dictionary<string, object> map = entry as Dictionary<string, object>;
                if (map == null)
                {
                    continue;
                }

                string message = GetString(map, "message");
                if (string.IsNullOrEmpty(message))
                {
                    message = GetString(map, "code");
                }

                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(message);
            }

            return builder.ToString();
        }

        public static Dictionary<string, object> DeserializeObject(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object>();
            }

            return Serializer.Deserialize<Dictionary<string, object>>(json);
        }

        public static List<object> DeserializeArray(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new List<object>();
            }

            return Serializer.Deserialize<List<object>>(json);
        }

        public static Dictionary<string, object> AsMap(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static List<object> AsList(object value)
        {
            List<object> list = value as List<object>;
            if (list != null)
            {
                return list;
            }

            // JavaScriptSerializer turns nested JSON arrays into object[] (or an
            // ArrayList on older runtimes), not List<object>. Normalise both so
            // callers always get the same collection type.
            System.Collections.IList other = value as System.Collections.IList;
            if (other == null)
            {
                return null;
            }

            List<object> copy = new List<object>(other.Count);
            foreach (object entry in other)
            {
                copy.Add(entry);
            }

            return copy;
        }

        public static List<object> GetList(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value))
            {
                return AsList(value) ?? new List<object>();
            }

            return new List<object>();
        }

        public static Dictionary<string, object> GetMap(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value))
            {
                return AsMap(value);
            }

            return null;
        }

        public static string GetString(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        public static int GetInt(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return 0;
        }

        public static bool GetBool(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return false;
        }

        public static DateTime GetDate(Dictionary<string, object> map, string key)
        {
            string text = GetString(map, key);
            if (string.IsNullOrEmpty(text))
            {
                return DateTime.MinValue;
            }

            DateTime parsed;
            if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return DateTime.MinValue;
        }

        public static string EscapeJson(string value)
        {
            return Serializer.Serialize(value == null ? string.Empty : value);
        }

        public static string ReadHeader(HttpResponseMessage response, string name)
        {
            if (response == null)
            {
                return string.Empty;
            }

            IEnumerable<string> values;
            if (response.Headers.TryGetValues(name, out values))
            {
                foreach (string value in values)
                {
                    return value;
                }
            }

            return string.Empty;
        }

        public static void WriteAtomic(string path, string contents)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, contents, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporary, path);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
