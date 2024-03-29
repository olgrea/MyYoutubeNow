﻿using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using MyYoutubeNow.Options;
using MyYoutubeNow.Progress;
using MyYoutubeNow.Utils;
using NLog;
using NLog.Layouts;

namespace MyYoutubeNow
{
    public class CommandLineOptions
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
            var results = CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args);
            if (results.Errors.Any())
            {
                foreach (Error error in results.Errors) 
                    Console.WriteLine($"{error} - {error.Tag}");
                return;
            }

            await results.WithParsedAsync(Run);
        }
        
        public static async Task Run(CommandLineOptions options)
        {
            var myn = new MyYoutubeNowService(new InlineProgress());

            var target = new NLog.Targets.ConsoleTarget("logconsole");
            target.Layout = Layout.FromString("${message:withexception=true}");
            myn.LoggingConfig.AddTarget(target);
            myn.LoggingConfig.AddRule(LogLevel.Info, LogLevel.Fatal, target);
            myn.LoggingConfig.Apply();

            if (myn.IsVideo(options.Url))
            {
                await myn.DownloadAndConvertVideo(options.Url, new VideoOptions() { Split = options.Split });
            }
            else if(myn.IsPlaylist(options.Url))
            {
                await myn.DownloadAndConvertPlaylist(options.Url, new PlaylistOptions() { Concatenate = options.Concatenate });
            }
        }
    }

    internal class InlineProgress : IProgress
    {
        public void Report(double progress)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"{progress:P1}      ");
        }
    }
}
