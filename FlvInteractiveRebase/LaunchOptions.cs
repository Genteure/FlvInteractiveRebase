using CommandLine;

namespace FlvInteractiveRebase
{
    internal class LaunchOptions
    {
        [Verb("about", isDefault: true, HelpText = "本软件介绍和使用说明")]
        public class About
        {
            [Option('s', "skip", Default = false, HelpText = "跳过按回车，直接退出")]
            public bool SkipReadLine { get; set; } = false;
        }

        [Verb("parse", HelpText = "解析 FLV 文件到 fib.xml 格式")]
        public class Parse
        {
            [Value(0, HelpText = "FLV 文件的位置", Required = true)]
            public string InputPath { get; set; } = string.Empty;

            [Value(1, HelpText = "输出文件的位置", Required = true)]
            public string FibPath { get; set; } = string.Empty;

            [Option('f', "force", Default = false, HelpText = "覆盖输出位置的文件")]
            public bool Overwrite { get; set; } = false;

            [Option("skip-hash", Default = true, HelpText = "不检查 FLV 文件的 Hash")]
            public bool SkipHash { get; set; } = false;

            [Option('q', "quite", Default = false, HelpText = "不打印日志到控制台")]
            public bool Quite { get; set; } = false;

            [Option("show-nalu", Default = false, Hidden = true)]
            public bool ShowNalu { get; set; } = false;
        }

        [Verb("build", HelpText = "用 fib.xml 来构建一个 FLV 文件")]
        public class Build
        {
            [Value(0, HelpText = "fib.xml 文件的位置", Required = true)]
            public string FibPath { get; set; } = string.Empty;

            [Value(1, HelpText = "FLV 输出位置", Required = true)]
            public string OutputPath { get; set; } = string.Empty;

            [Option('f', "force", Default = false, HelpText = "覆盖输出位置的文件")]
            public bool Overwrite { get; set; } = false;

            [Option('q', "quite", Default = false, HelpText = "不打印日志到控制台")]
            public bool Quite { get; set; } = false;
        }

        [Verb("extract", HelpText = "从 FLV 文件中抽离一个 FLV Tag")]
        public class Extract
        {
            [Value(0, HelpText = "FLV 文件的位置", Required = true)]
            public string InputPath { get; set; } = string.Empty;

            [Value(1, HelpText = "从 0 开始的 Tag 序号", Required = true, Default = 0u)]
            public uint TagNum { get; set; } = 0;

            [Value(2, HelpText = "Tag 输出位置", Required = false)]
            public string OutputPath { get; set; } = string.Empty;

            [Option('b', "byte", Default = false, HelpText = "Script Tag 也输出二进制，而不是 JSON")]
            public bool Byte { get; set; } = false;

            [Option('f', "force", Default = false, HelpText = "覆盖输出位置的文件")]
            public bool Overwrite { get; set; } = false;

            [Option('q', "quite", Default = false, HelpText = "不打印日志到控制台")]
            public bool Quite { get; set; } = false;
        }
    }
}
