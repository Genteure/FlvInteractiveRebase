using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace FlvInteractiveRebase.Amf
{
    public class ScriptTagBody
    {
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.DateTimeOffset,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
        };

        public IScriptDataValue.String Name { get; set; } = string.Empty;

        public IScriptDataValue.Object Value { get; set; } = new IScriptDataValue.Object();

        public static ScriptTagBody Parse(string json) => JsonConvert.DeserializeObject<ScriptTagBody>(json, settings)!;

        public string ToJson() => JsonConvert.SerializeObject(this, settings);

        public static ScriptTagBody Parse(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            return Parse(ms);
        }

        public static ScriptTagBody Parse(Stream stream)
        {
            return Parse(new BigEndianBinaryReader(stream, Encoding.UTF8, true));
        }

        public static ScriptTagBody Parse(BigEndianBinaryReader binaryReader)
        {
            if (IScriptDataValue.Parse(binaryReader) is IScriptDataValue.String stringName)
            {
                return new ScriptTagBody
                {
                    Name = stringName,
                    Value = ((IScriptDataValue.Parse(binaryReader)) switch
                    {
                        IScriptDataValue.EcmaArray value => value,
                        IScriptDataValue.Object value => value,
                        IScriptDataValue any => throw new AmfException($"type of ScriptTagBody.Value ({any.Type}) is not supported"),
                        _ => throw new AmfException("type of ScriptTagBody.Value is not supported"),
                    })
                };
            }
            else
            {
                throw new AmfException("ScriptTagBody.Name is not String");
            }
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            WriteTo(ms);
            return ms.ToArray();
        }

        public void WriteTo(Stream stream)
        {
            Name.WriteTo(stream);
            Value.WriteTo(stream);
        }
    }
}
