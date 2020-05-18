using FlvInteractiveRebase.Flv;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace FlvInteractiveRebase.Fib
{
    public class FlvInteractiveRebaseCommandFile
    {
        public static readonly string InlineFileName = ".";
        private static readonly XmlSerializer xmlSerializer = new XmlSerializer(typeof(FlvInteractiveRebaseCommandFile));
        private const string COMMENT = @"
文件说明

本 XML 文件是软件 fib (FlvInteractiveRebase) 的命令文件
可以使用 ./fib build 来根据本文件中的内容生成 FLV 文件
如果需要传输本文件，强烈推荐压缩成压缩包，可以显著降低文件大小

在下面 Tag 中：

Command 是对此 Tag 执行的操作，可选：
- D, Drop 丢弃，也可以直接删除整个 Tag element
- P, Pick 使用
- I, Inline 用 HEX 字符串生成 Tag  TIP: 内容是 ./fib extract 不带输出路径时的运行结果
- S, Script 用 Script Tag JSON 生成 Tag

Index 是 Tag 在原 FLV 文件中的序号，注意 *不是* 在新文件中的序号。

TimeStamp 是 Tag 在新文件中会使用的时间戳，格式是 ISO 8601 的 Durations
https://en.wikipedia.org/wiki/ISO_8601#Durations

From 会使 fib 从另一个 FLV 中获取 Tag （见下面的示例）
Raw 会使 fib 从另一个文件中读取一个独立的 Tag （见下面的示例）

只有上述五个参数会对输出的新 FLV 文件产生影响
下面的五个参数均为展示用，不影响输出的新文件

Type 是 Tag 的类型 (Script, Audio, Video)
Size 是 Tag 的数据大小，不包括 11 字节 Tag Header 的大小
Offset 是 Tag 在原文件中开始的位置

Header 为 true 时说明这是个 Header Tag
Keyframe 为 true 时说明这是个关键帧

示例：
<Tag Command=""Pick"" Index=""0"" TimeStamp=""PT0S"" Type=""Script"" Size=""1094"" Offset=""13"" />
<Tag Command=""Drop"" Index=""0"" TimeStamp=""PT0S"" Type=""Script"" Size=""1094"" Offset=""13"" />
<Tag Command=""D"" Index=""0"" TimeStamp=""PT0S"" Type=""Script"" Size=""1094"" Offset=""13"" />
<Tag Command=""Pick"" Index=""2"" TimeStamp=""PT0S"" Type=""Video"" Size=""34"" Offset=""1160"" Keyframe=""true"" Header=""true"" />
<Tag Command=""P"" Index=""1"" TimeStamp=""PT0S"" From=""D:\视频\另一个视频文件.flv"" />
<Tag Command=""P"" Index=""0"" TimeStamp=""PT0S"" Raw=""D:\视频\一个独立的Tag.blob"" />
";

        [XmlElement("Source")]
        public FlvFile File { get; set; } = new FlvFile();

        [XmlAnyElement("TagsXmlComment")]
        public XmlComment TagsXmlComment { get { return new XmlDocument().CreateComment(COMMENT); } set { } }

        public List<FlvTag> Tags { get; set; } = new List<FlvTag>();

        public static void Serialize(TextWriter textWriter, FlvInteractiveRebaseCommandFile file) => xmlSerializer.Serialize(textWriter, file);

        public static FlvInteractiveRebaseCommandFile Deserialize(TextReader textReader)
        {
            if (xmlSerializer.Deserialize(textReader) is FlvInteractiveRebaseCommandFile file)
                return file;
            else
                throw new FibException("输入文件不是 fib 命令文件");
        }

        public class FlvFile
        {
            [XmlAttribute, DefaultValue(false)]
            public bool SkipHash { get; set; }

            [XmlAttribute, DefaultValue("")]
            public string Hash { get; set; } = string.Empty;

            [XmlText]
            public string Path { get; set; } = string.Empty;
        }

        public enum FlvTagCommand
        {
            Drop = 0,
            D = 0,
            Pick = 1,
            P = 1,
            Inline = 2,
            I = 2,
            Script = 3,
            S = 3,
        }

        [XmlType("Tag")]
        public class FlvTag
        {
            [XmlAttribute]
            public FlvTagCommand Command { get; set; } = FlvTagCommand.Drop;

            [XmlAttribute]
            public int Index { get; set; } = -1;

            [XmlAttribute]
            public TimeSpan TimeStamp { get; set; }

            [XmlAttribute]
            public TagType Type { get; set; } = TagType.Unknown;

            [XmlAttribute]
            public int Size { get; set; } = -1;

            [XmlAttribute]
            public long Offset { get; set; } = 0;

            [XmlAttribute, DefaultValue(false)]
            public bool Keyframe { get; set; } = false;

            [XmlAttribute, DefaultValue(false)]
            public bool Header { get; set; } = false;

            [XmlText, DefaultValue("")]
            public string Value { get; set; } = string.Empty;

            [XmlAttribute("From"), DefaultValue("")]
            public string FromFlvPath { get; set; } = string.Empty;

            [XmlAttribute("Raw"), DefaultValue("")]
            public string FromTagPath { get; set; } = string.Empty;

            [XmlIgnore]
            public TagFrom From
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(FromFlvPath))
                        return TagFrom.File;
                    if (!string.IsNullOrWhiteSpace(FromTagPath))
                        return TagFrom.Tag;
                    return TagFrom.Main;
                }
            }
        }

        public enum TagFrom
        {
            Main,
            File,
            Tag,
        }
    }
}
