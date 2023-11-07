using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using NLog.Config;

namespace MyYoutubeNow.Utils
{
    public static class Extensions
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return await Task.FromResult(item);
            }
        }

        public static void Apply(this LoggingConfiguration config)
        {
            NLog.LogManager.Configuration = config;
        }

        public static async Task<object> InvokeAsync(this MethodInfo @this, object obj, params object[] parameters)
        {
            var task = (Task)@this.Invoke(obj, parameters);
            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty.GetValue(task);
        }
        
        public static string RemoveInvalidChars(this string filename)
        {
            return string.Join("_", filename.Split(Path.GetInvalidFileNameChars()));
        }
        
        public static async Task DownloadAsync(this HttpClient httpClient, string url, string targetFilePath, long totalSize, IProgress<double> progress)
        {
            using (HttpResponseMessage response =
                httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
            {
                response.EnsureSuccessStatusCode();
                
                using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
                    fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var totalRead = 0L;
                    var buffer = new byte[8192];

                    do
                    {
                        var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                        if (read == 0)
                            break;

                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        progress.Report((double) totalRead / totalSize);
                    } while (true);
                }
            }
        }
    }
}