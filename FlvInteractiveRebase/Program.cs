using CommandLine;
using CommandLine.Text;
using FlvInteractiveRebase.Amf;
using FlvInteractiveRebase.Fib;
using FlvInteractiveRebase.Flv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FlvInteractiveRebase
{
    internal static class Program
    {
        internal static bool Quite { get; private set; } = false;

        private static int Main(string[] args)
        {
            try
            {
                SentenceBuilder.Factory = () => FibSentenceBuilder.Singleton;
                return Parser.Default.ParseArguments<LaunchOptions.About, LaunchOptions.Parse, LaunchOptions.Build, LaunchOptions.Extract>(args)
                    .MapResult(
                    (LaunchOptions.About opts) => RunAboutAndReturnExitCode(opts),
                    (LaunchOptions.Parse opts) => RunParseAndReturnExitCode(opts),
                    (LaunchOptions.Build opts) => RunBuildAndReturnExitCode(opts),
                    (LaunchOptions.Extract opts) => RunExtractAndReturnExitCode(opts),
                    errs => 1);
            }
            catch (FlvException flv)
            {
                if (!Quite)
                    Console.WriteLine($"读写 flv 文件时发生错误: {flv.Message}\n{flv.StackTrace}");
                return 1;
            }
            catch (FibException fib)
            {
                if (!Quite)
                    Console.WriteLine("解析 fib 文件时发生错误: " + fib.Message);
                return 1;
            }
            catch (Exception e)
            {
                if (!Quite)
                    Console.WriteLine("运行时出现错误: \n" + e.ToString());
                return 1;
            }
        }

        private static int RunAboutAndReturnExitCode(LaunchOptions.About opts)
        {
            Console.WriteLine(@"FLV 文件编辑工具 fib (FlvInteractiveRebase) 录播姬QQ群内部测试版 beta 1
    By genteure ( fib-beta@danmuji.org )

本软件是一个命令行工具，不是双击打开运行的。

软件核心功能：
  - 对 FLV 进行 Tag 级别编辑处理，可用于解决各类奇葩问题
  - 使用简单的文本文件作为命令格式，方便手工编辑...
  - ...同时也方便编写脚本，实现自动化处理
  - 命令文件相对更小易于传输，方便协助不太懂 FLV 细节的人修复文件

软件主要用法：
注：假设软件文件名为 fib

1. 读取 FLV 文件，生成命令文件
  ./fib parse 有问题的.flv 命令.xml

2. 读取其他供参考或复制数据的 FLV 文件
  ./fib parse 其他.flv 其他命令.xml

3. 手动编辑命令文本文件

4. 使用修改过的命令文件生成新的 FLV 文件
  ./fib build 命令.xml 修复之后.flv

查看帮助请运行
  ./fib help
");

            if (!opts.SkipReadLine)
            {
                Console.WriteLine("按回车键关闭...");
                Console.ReadLine();
            }

            return 0;
        }

        private static int RunParseAndReturnExitCode(LaunchOptions.Parse opts)
        {
            Quite = opts.Quite;

            var input = new FileInfo(opts.InputPath);
            if (!input.Exists)
            {
                if (!Quite)
                    Console.WriteLine("FLV 文件不存在");
                return 1;
            }

            var fib = new FileInfo(opts.FibPath);
            if (fib.Exists && !opts.Overwrite)
            {
                if (!Quite)
                    Console.WriteLine("fib 文件已经存在，使用 --force 参数覆盖目标位置文件");
                return 1;
            }

            using var flv_stream = input.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            if (!Quite)
                Console.WriteLine("读取 FLV 文件中...");

            string hash;
            if (opts.SkipHash)
            {
                hash = string.Empty;
            }
            else
            {
                using var md5 = MD5.Create();
                var bytes = md5.ComputeHash(flv_stream);
                flv_stream.Position = 0;
                hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }

            var tags = FlvReader.ReadFlvFile(flv_stream);

            if (!Quite)
                Console.WriteLine("FLV 文件读取完毕，生成 fib 命令文件...");

            var file = new FlvInteractiveRebaseCommandFile
            {
                File = new FlvInteractiveRebaseCommandFile.FlvFile
                {
                    Hash = hash,
                    SkipHash = opts.SkipHash,
                    Path = input.FullName
                }
            };

            int index = 0;
            var memoryStream = new MemoryStream();
            file.Tags.AddRange(tags.Select(x =>
            {
                if (x.TagType == TagType.Script)
                {
                    memoryStream.SetLength(0);
                    FlvWriter.WriteTagData(memoryStream, flv_stream, x);
                    memoryStream.Position = 0;
                    var body = ScriptTagBody.Parse(memoryStream);
                    var json = body.ToJson();
                    return new FlvInteractiveRebaseCommandFile.FlvTag
                    {
                        Command = FlvInteractiveRebaseCommandFile.FlvTagCommand.Script,
                        Index = index++,
                        TimeStamp = TimeSpan.FromMilliseconds(x.TimeStamp),
                        Value = json,
                        Size = x.TagSize,
                        Offset = x.Position,
                        Type = x.TagType,
                        Header = x.Flag.HasFlag(TagFlag.Header),
                        Keyframe = x.Flag.HasFlag(TagFlag.Keyframe),
                    };
                }
                else
                {
                    return new FlvInteractiveRebaseCommandFile.FlvTag
                    {
                        Command = FlvInteractiveRebaseCommandFile.FlvTagCommand.Pick,
                        Index = index++,
                        TimeStamp = TimeSpan.FromMilliseconds(x.TimeStamp),
                        Size = x.TagSize,
                        Offset = x.Position,
                        Type = x.TagType,
                        Header = x.Flag.HasFlag(TagFlag.Header),
                        Keyframe = x.Flag.HasFlag(TagFlag.Keyframe),
                    };
                }
            }));

            using (var writer = new StreamWriter(fib.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None), new UTF8Encoding(false)))
            {
                FlvInteractiveRebaseCommandFile.Serialize(writer, file);
            }

            if (!Quite)
                Console.WriteLine("成功生成");

            return 0;
        }

        private static int RunBuildAndReturnExitCode(LaunchOptions.Build opts)
        {
            Quite = opts.Quite;

            return FileBuilder.RunBuildAndReturnExitCode(opts);
        }

        private static int RunExtractAndReturnExitCode(LaunchOptions.Extract opts)
        {
            Quite = opts.Quite;

            using var flv_stream = File.Open(opts.InputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var tags = FlvReader.ReadFlvFile(flv_stream, opts.TagNum + 1);

            if (tags.Length > opts.TagNum)
            {
                FlvTag tag = tags[opts.TagNum];
                if (string.IsNullOrWhiteSpace(opts.OutputPath))
                {
                    var s = new MemoryStream();
                    if (tag.TagType == TagType.Script && !opts.Byte)
                    {
                        FlvWriter.WriteTagData(s, flv_stream, tag);
                        s.Position = 0;
                        var body = ScriptTagBody.Parse(s);
                        Console.WriteLine(body.ToJson());
                    }
                    else
                    {
                        FlvWriter.WriteTag(s, flv_stream, tag);
                        var str = BitConverter.ToString(s.ToArray()).Replace("-", "");
                        str = string.Join('\n', str.SplitInParts(64));
                        Console.WriteLine(str);
                    }
                    return 0;
                }
                else
                {
                    var fi = new FileInfo(opts.OutputPath);
                    if (fi.Exists && !opts.Overwrite)
                    {
                        if (!Quite)
                            Console.WriteLine("文件已经存在，使用 --force 参数覆盖目标位置文件");
                        return 1;
                    }

                    using var output = fi.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    FlvWriter.WriteTag(output, flv_stream, tag);
                    return 0;
                }
            }
            else
            {
                if (!Quite)
                    Console.WriteLine($"文件中只有个 {tags.Length} Tag，而需要第 {opts.TagNum} 个 Tag");
                return 1;
            }
        }

        private static IEnumerable<ReadOnlyMemory<char>> SplitInParts(this string s, int partLength)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s));
            if (partLength <= 0)
                throw new ArgumentException("Part length has to be positive.", nameof(partLength));

            for (var i = 0; i < s.Length; i += partLength)
                yield return s.AsMemory().Slice(i, Math.Min(partLength, s.Length - i));
        }
    }
}
