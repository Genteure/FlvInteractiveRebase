using System;
using System.Collections.Generic;
using System.IO;

namespace FlvInteractiveRebase.Flv
{
    internal static class FlvReader
    {
        public static FlvTag[] ReadFlvFile(Stream stream) => ReadFlvFile(stream, 0);

        public static FlvTag[] ReadFlvFile(Stream stream, uint tagNum)
        {
            if (stream.ReadByte() != 'F' || stream.ReadByte() != 'L' || stream.ReadByte() != 'V' || stream.ReadByte() != 1)
            {
                throw new FlvException("输入数据不是 FLV");
            }
            stream.ReadByte();
            if (stream.ReadByte() != 0 || stream.ReadByte() != 0 || stream.ReadByte() != 0 || stream.ReadByte() != 9)
            {
                throw new FlvException("FLV 文件版本不支持");
            }

            return ReadAllTagsFromstream(stream, tagNum);
        }

        public static FlvTag ReadFlvTag(Stream stream)
        {
            const string Message = "输入数据不是 FLV Tag";
            if ((stream.Length - stream.Position) < 11)
                throw new FlvException(Message);

            byte[] b = new byte[4];

            FlvTag tag = new FlvTag
            {
                Position = 0,
                TagType = (TagType)stream.ReadByte()
            };

            if (tag.TagType != TagType.Audio && tag.TagType != TagType.Video && tag.TagType != TagType.Script)
            {
                throw new FlvException(Message);
            }
            // ----------------------------- //
            b[0] = 0;
            if (3 != stream.Read(b, 1, 3))
            {
                throw new FlvException(Message);
            }
            tag.TagSize = BitConverter.ToInt32(b.ToBE(), 0);
            // ----------------------------- //
            if (3 != stream.Read(b, 1, 3))
            {
                throw new FlvException(Message);
            }
            var temp = stream.ReadByte();
            if (temp == -1)
            {
                throw new FlvException(Message);
            }
            b[0] = (byte)temp;
            tag.TimeStamp = BitConverter.ToInt32(b.ToBE(), 0);
            // ----------------------------- //
            switch (tag.TagType)
            {
                case TagType.Audio:
                    {
                        if (4 != stream.Read(b, 0, 4)) { throw new FlvException(Message); }
                        tag.Flag = stream.ReadByte() == 0 ? TagFlag.Header : TagFlag.None;
                        var totalSkip = tag.TagSize - 2;
                        if (totalSkip != stream.SkipBytes(totalSkip)) { throw new FlvException(Message); }
                    }
                    break;
                case TagType.Video:
                    {
                        if (3 != stream.Read(b, 0, 3)) { throw new FlvException(Message); }
                        if (stream.ReadByte() == 0x17)
                        {
                            tag.Flag |= TagFlag.Keyframe;
                        }
                        switch (stream.ReadByte())
                        {
                            case 0:
                                tag.Flag |= TagFlag.Header;
                                break;
                            case 2:
                                tag.Flag |= TagFlag.End;
                                break;
                        }
                        var totalSkip = tag.TagSize - 2;
                        if (totalSkip != stream.SkipBytes(totalSkip)) { throw new FlvException(Message); }
                    }
                    break;
                default:
                    {
                        var totalSkip = 3 + tag.TagSize;
                        if (totalSkip != stream.SkipBytes(totalSkip)) { throw new FlvException(Message); }
                    }
                    break;
            }
            // ----------------------------- //
            return tag;
        }

        private static FlvTag[] ReadAllTagsFromstream(Stream stream, uint tagNum)
        {
            List<FlvTag> tags = new List<FlvTag>(1024);

            byte[] b = new byte[4];

            do
            {
                FlvTag tag = new FlvTag();
                // ----------------------------- //
                if (4 != stream.Read(b, 0, 4))
                {
                    break;
                }
                // ----------------------------- //
                tag.Position = stream.Position;
                tag.TagType = (TagType)stream.ReadByte();
                if (tag.TagType != TagType.Audio && tag.TagType != TagType.Video && tag.TagType != TagType.Script)
                {
                    break;
                }
                // ----------------------------- //
                b[0] = 0;
                if (3 != stream.Read(b, 1, 3))
                {
                    break;
                }
                tag.TagSize = BitConverter.ToInt32(b.ToBE(), 0);
                // ----------------------------- //
                if (3 != stream.Read(b, 1, 3))
                {
                    break;
                }
                var temp = stream.ReadByte();
                if (temp == -1)
                {
                    break;
                }
                b[0] = (byte)temp;
                tag.TimeStamp = BitConverter.ToInt32(b.ToBE(), 0);
                // ----------------------------- //
                switch (tag.TagType)
                {
                    case TagType.Audio:
                        {
                            if (4 != stream.Read(b, 0, 4)) { goto break_out; }
                            tag.Flag = stream.ReadByte() == 0 ? TagFlag.Header : TagFlag.None;
                            var totalSkip = tag.TagSize - 2;
                            if (totalSkip != stream.SkipBytes(totalSkip)) { goto break_out; }
                        }
                        break;
                    case TagType.Video:
                        {
                            if (3 != stream.Read(b, 0, 3)) { goto break_out; }
                            if (stream.ReadByte() == 0x17)
                            {
                                tag.Flag |= TagFlag.Keyframe;
                            }
                            switch (stream.ReadByte())
                            {
                                case 0:
                                    tag.Flag |= TagFlag.Header;
                                    break;
                                case 2:
                                    tag.Flag |= TagFlag.End;
                                    break;
                            }
                            var totalSkip = tag.TagSize - 2;
                            if (totalSkip != stream.SkipBytes(totalSkip)) { goto break_out; }
                        }
                        break;
                    default:
                        {
                            var totalSkip = 3 + tag.TagSize;
                            if (totalSkip != stream.SkipBytes(totalSkip))
                            {
                                goto break_out;
                            }
                        }
                        break;
                }
                // ----------------------------- //
                tags.Add(tag);
            } while (tagNum == 0 ? true : tags.Count < tagNum);

        break_out:

            return tags.ToArray();
        }
    }
}
