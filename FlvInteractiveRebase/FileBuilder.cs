using FlvInteractiveRebase.Amf;
using FlvInteractiveRebase.Fib;
using FlvInteractiveRebase.Flv;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace FlvInteractiveRebase
{
    class FileBuilder
    {
        internal static int RunBuildAndReturnExitCode(LaunchOptions.Build opts)
        {
            var output = new FileInfo(opts.OutputPath);
            if (output.Exists && !opts.Overwrite)
            {
                if (!Program.Quite)
                    Console.WriteLine("目标文件已经存在，使用 --force 参数覆盖目标位置文件");
                return 1;
            }

            var fib = new FileInfo(opts.FibPath);
            if (!fib.Exists)
            {
                if (!Program.Quite)
                    Console.WriteLine("fib 文件不存在");
                return 1;
            }

            using var streamReader = new StreamReader(fib.Open(FileMode.Open, FileAccess.Read, FileShare.Read));

            if (!Program.Quite)
                Console.WriteLine("解析命令文件...");

            var cmdFile = FlvInteractiveRebaseCommandFile.Deserialize(streamReader);
            var cmdTags = cmdFile.Tags.Where(x => x.Command != FlvInteractiveRebaseCommandFile.FlvTagCommand.Drop).ToList();
            var flvs = new Dictionary<string, (Stream fs, IEnumerable<FlvTag> tags)>();

            { // 加载主文件
                var input = File.Open(cmdFile.File.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

                if (!cmdFile.File.SkipHash)
                {
                    using var md5 = MD5.Create();
                    var bytes = md5.ComputeHash(input);
                    input.Position = 0;
                    var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
                    if (hash != cmdFile.File.Hash)
                    {
                        if (!Program.Quite)
                            Console.WriteLine("主 FLV 文件 Hash 不匹配");
                        return 1;
                    }
                }

                var input_tags = FlvReader.ReadFlvFile(input);
                flvs.Add(string.Empty, (input, input_tags));

                if (!Program.Quite)
                    Console.WriteLine("主 FLV 文件读取完成...");
            }

            // 加载其他文件
            foreach (var item in cmdTags.Where(x => x.Command == FlvInteractiveRebaseCommandFile.FlvTagCommand.Pick && x.From != FlvInteractiveRebaseCommandFile.TagFrom.Main))
                switch (item.From)
                {
                    default:
                        break;
                    case FlvInteractiveRebaseCommandFile.TagFrom.File:
                        {
                            var fi = new FileInfo(item.FromFlvPath);
                            if (!flvs.ContainsKey(fi.FullName))
                            {
                                if (fi.Exists)
                                {
                                    var stream = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                                    var tags = FlvReader.ReadFlvFile(stream);
                                    flvs.Add(fi.FullName, (stream, tags));

                                    if (item.Index >= tags.Length)
                                    {
                                        if (!Program.Quite)
                                            Console.WriteLine($"文件 {item.FromFlvPath} 中只有 {tags.Length} 个 Tag，而命令需要第 {item.Index} 个");
                                        return 1;
                                    }

                                    if (!Program.Quite)
                                        Console.WriteLine($"文件 {fi.FullName} 读取完成...");
                                }
                                else
                                {
                                    if (!Program.Quite)
                                        Console.WriteLine($"文件 {item.FromFlvPath} 不存在");
                                    return 1;
                                }
                            }
                            item.FromFlvPath = fi.FullName;
                        }
                        break;
                    case FlvInteractiveRebaseCommandFile.TagFrom.Tag:
                        {
                            var fi = new FileInfo(item.FromTagPath);
                            if (!flvs.ContainsKey(fi.FullName))
                            {
                                if (fi.Exists)
                                {
                                    var stream = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                                    var tag = FlvReader.ReadFlvTag(stream);
                                    flvs.Add(fi.FullName, (stream, new[] { tag }));

                                    if (!Program.Quite)
                                        Console.WriteLine($"文件 {fi.FullName} 读取完成...");
                                }
                                else
                                {
                                    if (!Program.Quite)
                                        Console.WriteLine($"文件 {item.FromTagPath} 不存在");
                                    return 1;
                                }
                            }
                            item.Index = 0;
                            item.FromTagPath = fi.FullName;
                        }
                        break;
                }

            // 加载 Inline 和 Script
            {
                var ms = new MemoryStream();
                var inlineTags = new List<FlvTag>();
                foreach (var item in cmdTags.Skip(1).Where(x => x.Command == FlvInteractiveRebaseCommandFile.FlvTagCommand.Script))
                {
                    var amf = ScriptTagBody.Parse(item.Value).ToBytes();
                    var tag = new FlvTag
                    {
                        TagType = TagType.Script,
                        Position = ms.Position,
                        TagSize = amf.Length,
                    };
                    FlvWriter.WriteTagHeader(ms, tag);
                    ms.Write(amf);

                    item.FromFlvPath = FlvInteractiveRebaseCommandFile.InlineFileName;
                    item.Index = inlineTags.Count;
                    inlineTags.Add(tag);
                }
                foreach (var item in cmdTags.Where(x => x.Command == FlvInteractiveRebaseCommandFile.FlvTagCommand.Inline))
                {
                    var pos = ms.Position;
                    var fullBytes = StringToByteArray(Regex.Replace(item.Value, @"\s+", ""));
                    ms.Write(fullBytes);
                    ms.Position = pos;
                    var tag = FlvReader.ReadFlvTag(ms);
                    tag.Position = pos;

                    item.FromFlvPath = FlvInteractiveRebaseCommandFile.InlineFileName;
                    item.Index = inlineTags.Count;
                    inlineTags.Add(tag);
                }
                var sTag = cmdTags[0];
                if (sTag.Command == FlvInteractiveRebaseCommandFile.FlvTagCommand.Script)
                {
                    var amf = ScriptTagBody.Parse(sTag.Value);

                    var max_ts = cmdTags.Max(x => x.TimeStamp);
                    amf.Value["duration"] = (IScriptDataValue.Number)(max_ts.TotalSeconds + 1);

                    var amfBytes = amf.ToBytes();

                    var tag = new FlvTag
                    {
                        TagType = TagType.Script,
                        Position = ms.Position,
                        TagSize = amfBytes.Length,
                    };
                    FlvWriter.WriteTagHeader(ms, tag);
                    ms.Write(amfBytes);

                    sTag.FromFlvPath = FlvInteractiveRebaseCommandFile.InlineFileName;
                    sTag.Index = inlineTags.Count;
                    inlineTags.Add(tag);
                }
                flvs.Add(FlvInteractiveRebaseCommandFile.InlineFileName, (ms, inlineTags));
            }

            if (!Program.Quite)
                Console.WriteLine("开始写入 FLV 文件...");

            using (var output_fs = output.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                FlvWriter.WriteFile(output_fs, cmdTags, flvs);

            if (!Program.Quite)
                Console.WriteLine("写入文件成功");

            return 0;
        }

        private static byte[] StringToByteArray(string hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
    }
}
