using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using MyYoutubeNow.Utils;
using NLog;
using NLog.Layouts;

namespace MyYoutubeNow
{
    public class Options
    {
        [Value(0, Required = true, HelpText = "The url of the video/playlist. ")]
        public string Url { get; set; }

        [Option('s', Default = false, HelpText = "Split a video with chapters and convert them into mp3s.")]
        public bool Split { get; set; }

        [Option('c', Hidden = true, Default = false, HelpText = "Concatenate videos of a playlist into a single mp3.")]
        public bool Concatenate { get; set; }
    }

    public class CLI
    {
        static async Task Main(string[] args)
        {
            var results = CommandLine.Parser.Default.ParseArguments<Options>(args);
            if (results.Errors.Any())
            {
                foreach (Error error in results.Errors) 
                    Console.WriteLine($"{error} - {error.Tag}");
                return;
            }

            await results.WithParsedAsync(Run);
        }
        
        public static async Task Run(Options options)
        {
            var myn = new MyYoutubeNowService(new InlineProgress());

            var target = new NLog.Targets.ConsoleTarget("logconsole");
            target.Layout = Layout.FromString("${message:withexception=true}");
            myn.LoggingConfig.AddTarget(target);
            myn.LoggingConfig.AddRule(LogLevel.Info, LogLevel.Fatal, target);
            myn.LoggingConfig.Apply();

            if (MyYoutubeNowService.IsVideo(options.Url))
            {
                await myn.ConvertVideo(options.Url, options.Split);
            }
            else if(MyYoutubeNowService.IsPlaylist(options.Url))
            {
                await myn.ConvertPlaylist(options.Url, options.Concatenate);
            }
        }
    }

    internal class InlineProgress : IProgressReport
    {
        public void Report(double progress)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"{progress:P1}      ");
        }
    }
}
