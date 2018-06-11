using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Tweetinvi;

namespace _100DaysOfCode.Azure.Functions
{
    public static class PublishTweet
    {
        /// <summary>
        /// Format string specifying the location of the daily log. The value interpolated will be a formatted date.
        /// </summary>
        /// <remarks>
        /// Had considered using a Git client library to clone the repo but figured it's just much simpler to download
        /// the specific log over HTTP since the repo is public anyway.
        /// </remarks>
        private static readonly string _urlFormat =
            "https://raw.githubusercontent.com/stvnhrlnd/100DaysOfCode/master/Steven/{0}/README.md";

        /// <summary>
        /// The format of the date to use in the folder name.
        /// </summary>
        /// <remarks>
        /// Using this format so that the folders appear in date order in the repo.
        /// </remarks>
        private static readonly string _dateFormat = "yyyy-MM-dd";

        /// <summary>
        /// The function entry point.
        /// </summary>
        /// <param name="timerInfo"></param>
        /// <param name="traceWriter"></param>
        /// <returns></returns>
        /// <remarks>
        /// Runs at 1am daily which gives me until then to push up the log for the previous day. Was originally using
        /// a GitHub webhook but I couldn't be bothered with the checks required to see if the function had already
        /// been run (as multiple pushes would result in duplicate tweets). It only needs to run once a day anyway.
        /// </remarks>
        [FunctionName("PublishTweet")]
        public static async Task Run([TimerTrigger("0 0 1 * * *")]TimerInfo timerInfo, TraceWriter traceWriter)
        {
            traceWriter.Info($"PublishTweet function executed at: {DateTime.Now}");

            var markdown = await DownloadLog();
            traceWriter.Info($"Markdown:\n{markdown}");

            var tweetText = GetTweetText(markdown);
            traceWriter.Info($"Tweet text:\n{tweetText}");

            Auth.SetUserCredentials(
                GetEnvironmentVariable("TwitterConsumerKey"),
                GetEnvironmentVariable("TwitterConsumerSecret"),
                GetEnvironmentVariable("TwitterAccessToken"),
                GetEnvironmentVariable("TwitterAccessSecret"));
            Tweet.PublishTweet(tweetText);
        }

        /// <summary>
        /// Downloads the log for yesterday (since the function runs at 1am).
        /// </summary>
        /// <returns>The Markdown content of the log.</returns>
        private static async Task<string> DownloadLog()
        {
            using (var client = new HttpClient())
            {
                var dateString = DateTime.Now.AddDays(-1).ToString(_dateFormat);
                var requestUri = string.Format(_urlFormat, dateString);
                var response = await client.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }

        /// <summary>
        /// Extracts the tweet text from the given Markdown.
        /// </summary>
        /// <param name="markdown">The Markdown content of the log.</param>
        /// <returns>
        /// The title (with hash sign stripped) and everything up until the first second level header or the end of the
        /// string. Two new lines and the hashtag are added onto the end. Assumes that ATX Headers are used and that the
        /// Markdown begins with a first level header.
        /// </returns>
        /// <remarks>
        /// Extremely quick and dirty way to do this but whatever.
        /// </remarks>
        private static string GetTweetText(string markdown)
        {
            var h2Index = markdown.IndexOf("\n##");
            var tweetText = h2Index == -1 ? markdown.Substring(1) : markdown.Substring(1, h2Index);
            tweetText = tweetText.Trim() + "\n\n#100DaysOfCode";
            return tweetText;
        }

        /// <summary>
        /// Gets an environment variable or app setting.
        /// </summary>
        /// <param name="variable">The name of the variable or app setting.</param>
        /// <returns>The value of the variable or app setting.</returns>
        private static string GetEnvironmentVariable(string variable)
        {
            return Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.Process);
        }
    }
}
