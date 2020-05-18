using FlvInteractiveRebase.Fib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlvInteractiveRebase.Flv
{
    internal static class FlvWriter
    {
        public static void WriteFile(Stream target, IEnumerable<FlvInteractiveRebaseCommandFile.FlvTag> tags, Dictionary<string, (Stream fs, IEnumerable<FlvTag> tags)> data)
        {
            target.Write(new byte[] { (byte)'F', (byte)'L', (byte)'V', 1, 5, 0, 0, 0, 9, 0, 0, 0, 0 }, 0, 13);

            foreach (var tag in tags)
            {
                (Stream fs, FlvTag flvTag) = PickTag(tag, data);
                flvTag.TimeStamp = (int)tag.TimeStamp.TotalMilliseconds;
                WriteTag(target, fs, flvTag);
            }
        }

        private static (Stream fs, FlvTag flvTag) PickTag(FlvInteractiveRebaseCommandFile.FlvTag tag, Dictionary<string, (Stream fs, IEnumerable<FlvTag> tags)> data)
        {
            (Stream fs, IEnumerable<FlvTag> tags) = data[tag.From switch
            {
                FlvInteractiveRebaseCommandFile.TagFrom.File => tag.FromFlvPath,
                FlvInteractiveRebaseCommandFile.TagFrom.Tag => tag.FromTagPath,
                _ => string.Empty,
            }];

            return (fs, flvTag: tags.ElementAt(tag.Index));
        }

        public static void WriteTag(Stream target, Stream source, FlvTag tag)
        {
            WriteTagHeader(target, tag);
            WriteTagData(target, source, tag);
            target.Write(BitConverter.GetBytes(tag.TagSize + 11).ToBE(), 0, 4);
        }

        public static void WriteTagHeader(Stream target, FlvTag tag)
        {
            var head = new byte[11];
            head[0] = (byte)tag.TagType;
            var size = BitConverter.GetBytes(tag.TagSize).ToBE();
            Buffer.BlockCopy(size, 1, head, 1, 3);

            byte[] timing = BitConverter.GetBytes(tag.TimeStamp).ToBE();
            Buffer.BlockCopy(timing, 1, head, 4, 3);
            Buffer.BlockCopy(timing, 0, head, 7, 1);

            target.Write(head, 0, 11);
        }

        public static void WriteTagData(Stream target, Stream source, FlvTag tag)
        {
            source.Seek(tag.Position + 11, SeekOrigin.Begin);
            source.CopyBytes(target, tag.TagSize);
        }
    }
}
