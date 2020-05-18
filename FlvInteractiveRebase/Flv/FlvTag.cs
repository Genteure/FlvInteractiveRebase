using System;
using System.Diagnostics;

namespace FlvInteractiveRebase.Flv
{
    public enum TagType : int
    {
        Unknown = 0,
        Audio = 8,
        Video = 9,
        Script = 18,
    }

    [Flags]
    internal enum TagFlag : int
    {
        None = 0,
        Header = 1 << 0,
        Keyframe = 1 << 1,
        End = 1 << 2,
        SameAsLastTimestamp = 1 << 3,
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal struct FlvTag
    {
        public TagType TagType;
        public TagFlag Flag;
        public int TagSize;
        public int TimeStamp;
        public long Position;
        private string DebuggerDisplay => string.Format("{0}, {1}{2}{3}{4}, TS = {5}, Size = {6}, Pos = {7}",
                    TagType switch
                    {
                        TagType.Audio => "A",
                        TagType.Video => "V",
                        TagType.Script => "S",
                        _ => "?",
                    },
                    Flag.HasFlag(TagFlag.Keyframe) ? "K" : "-",
                    Flag.HasFlag(TagFlag.Header) ? "H" : "-",
                    Flag.HasFlag(TagFlag.End) ? "E" : "-",
                    Flag.HasFlag(TagFlag.SameAsLastTimestamp) ? "L" : "-",
                    TimeStamp,
                    TagSize,
                    Position);
    }
}
