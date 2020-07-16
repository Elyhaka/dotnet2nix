using System;
using System.Collections.Generic;
using CommandLine;

namespace dotnet2nix
{
    public class Options
    {
        [Option('f', "folder", Required = false, HelpText = "Lock file generation target.", Default = "./")]
        public string Folder { get; set; }

        [Option('s', "solution", Required = false, HelpText = "Generate in solution mode (enabling recursion).", Default = false)]
        public bool SolutionMode { get; set; }

        [Option('t', "target", Required = false, HelpText = "Select a target.", Default = "linux-x64")]
        public string Target { get; set; }
    }

    class Program
    {
        static void Main(string[] args) =>
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o => new Generator(o).Run().GetAwaiter().GetResult())
                .WithNotParsed(ArgsError);

        static void ArgsError(IEnumerable<Error> errs)
        {
            foreach (var err in errs)
            {
                Console.WriteLine(err.ToString());
            }
        }
    }
}
