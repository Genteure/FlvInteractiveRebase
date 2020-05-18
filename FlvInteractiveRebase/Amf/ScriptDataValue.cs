using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace FlvInteractiveRebase.Amf
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    [JsonConverter(typeof(JsonSubtypes), nameof(Type))]
    [JsonSubtypes.KnownSubType(typeof(Number), ScriptDataValueType.Number)]
    [JsonSubtypes.KnownSubType(typeof(Boolean), ScriptDataValueType.Boolean)]
    [JsonSubtypes.KnownSubType(typeof(String), ScriptDataValueType.String)]
    [JsonSubtypes.KnownSubType(typeof(Object), ScriptDataValueType.Object)]
    [JsonSubtypes.KnownSubType(typeof(Null), ScriptDataValueType.Null)]
    [JsonSubtypes.KnownSubType(typeof(Undefined), ScriptDataValueType.Undefined)]
    [JsonSubtypes.KnownSubType(typeof(Reference), ScriptDataValueType.Reference)]
    [JsonSubtypes.KnownSubType(typeof(EcmaArray), ScriptDataValueType.EcmaArray)]
    [JsonSubtypes.KnownSubType(typeof(StrictArray), ScriptDataValueType.StrictArray)]
    [JsonSubtypes.KnownSubType(typeof(Date), ScriptDataValueType.Date)]
    [JsonSubtypes.KnownSubType(typeof(LongString), ScriptDataValueType.LongString)]
    public interface IScriptDataValue
    {
        [JsonProperty]
        ScriptDataValueType Type { get; }

        void WriteTo(Stream stream);

        public static IScriptDataValue Parse(BigEndianBinaryReader binaryReader)
        {
            var type = (ScriptDataValueType)binaryReader.ReadByte();
            switch (type)
            {
                case ScriptDataValueType.Number:
                    return (Number)binaryReader.ReadDouble();
                case ScriptDataValueType.Boolean:
                    return (Boolean)binaryReader.ReadBoolean();
                case ScriptDataValueType.String:
                    return ReadScriptDataString(binaryReader, false) ?? string.Empty;
                case ScriptDataValueType.Object:
                    {
                        var result = new Object();
                        while (true)
                        {
                            var propertyName = ReadScriptDataString(binaryReader, true);
                            if (propertyName is null)
                                break;

                            var propertyData = Parse(binaryReader);
                            result.Add(propertyName, propertyData);
                        }
                        return result;
                    }
                case ScriptDataValueType.MovieClip:
                    throw new AmfException("MovieClip is not supported");
                case ScriptDataValueType.Null:
                    return new Null();
                case ScriptDataValueType.Undefined:
                    return new Undefined();
                case ScriptDataValueType.Reference:
                    return (Reference)binaryReader.ReadUInt16();
                case ScriptDataValueType.EcmaArray:
                    {
                        binaryReader.ReadUInt32();
                        var result = new EcmaArray();
                        while (true)
                        {
                            var propertyName = ReadScriptDataString(binaryReader, true);
                            if (propertyName is null)
                                break;

                            var propertyData = Parse(binaryReader);
                            result.Add(propertyName, propertyData);
                        }
                        return result;
                    }
                case ScriptDataValueType.ObjectEndMarker:
                    throw new AmfException("Read ObjectEndMarker");
                case ScriptDataValueType.StrictArray:
                    {
                        var length = binaryReader.ReadUInt32();
                        var result = new StrictArray();
                        for (int i = 0; i < length; i++)
                        {
                            var value = Parse(binaryReader);
                            result.Add(value);
                        }
                        return result;
                    }
                case ScriptDataValueType.Date:
                    {
                        var dateTime = binaryReader.ReadDouble();
                        var offset = binaryReader.ReadInt16();
                        return new Date(dateTime, offset);
                    }
                case ScriptDataValueType.LongString:
                    {
                        var length = binaryReader.ReadUInt32();
                        if (length > int.MaxValue)
                        {
                            throw new AmfException($"LongString larger than {int.MaxValue} is not supported.");
                        }
                        else
                        {
                            var bytes = binaryReader.ReadBytes((int)length);
                            var str = Encoding.UTF8.GetString(bytes);
                            return (LongString)str;
                        }
                    }
                default:
                    throw new Exception("Unknown ScriptDataValueType");
            }

            static String? ReadScriptDataString(BigEndianBinaryReader binaryReader, bool expectObjectEndMarker)
            {
                var length = binaryReader.ReadUInt16();
                if (length == 0)
                {
                    if (expectObjectEndMarker && binaryReader.ReadByte() != 9)
                        throw new AmfException("ObjectEndMarker not matched.");
                    return null;
                }
                return Encoding.UTF8.GetString(binaryReader.ReadBytes(length));
            }
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ScriptDataValueType : byte
        {
            Number = 0,
            Boolean = 1,
            String = 2,
            Object = 3,
            MovieClip = 4,
            Null = 5,
            Undefined = 6,
            Reference = 7,
            EcmaArray = 8,
            ObjectEndMarker = 9,
            StrictArray = 10,
            Date = 11,
            LongString = 12,
        }

        [DebuggerDisplay("AmfNumber, {Value}")]
        public class Number : IScriptDataValue
        {
            public ScriptDataValueType Type => ScriptDataValueType.Number;

            [JsonProperty]
            public double Value { get; set; }

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);
                var buffer = new byte[sizeof(double)];
                BinaryPrimitives.WriteInt64BigEndian(buffer, BitConverter.DoubleToInt64Bits(Value));
                stream.Write(buffer);
            }

            public static bool operator ==(Number left, Number right) => EqualityComparer<Number>.Default.Equals(left, right);
            public static bool operator !=(Number left, Number right) => !(left == right);
            public override bool Equals(object? obj) => obj is Number number && Value == number.Value;
            public override int GetHashCode() => HashCode.Combine(Value);
            public static implicit operator double(Number number) => number.Value;
            public static explicit operator int(Number number) => (int)number.Value;
            public static implicit operator Number(double number) => new Number { Value = number };
            public static explicit operator Number(int number) => new Number { Value = number };
        }

        [DebuggerDisplay("AmfBoolean, {Value}")]
        public class Boolean : IScriptDataValue
        {
            public ScriptDataValueType Type => ScriptDataValueType.Boolean;

            [JsonProperty]
            public bool Value { get; set; }

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);
                stream.WriteByte((byte)(Value ? 1 : 0));
            }

            public override bool Equals(object? obj) => obj is Boolean boolean && Value == boolean.Value;
            public override int GetHashCode() => HashCode.Combine(Value);
            public static bool operator ==(Boolean left, Boolean right) => EqualityComparer<Boolean>.Default.Equals(left, right);
            public static bool operator !=(Boolean left, Boolean right) => !(left == right);
            public static implicit operator bool(Boolean boolean) => boolean.Value;
            public static implicit operator Boolean(bool boolean) => new Boolean { Value = boolean };
        }

        [DebuggerDisplay("AmfString, {Value}")]
        public class String : IScriptDataValue
        {
            public ScriptDataValueType Type => ScriptDataValueType.String;

            [JsonProperty(Required = Required.Always)]
            public string Value { get; set; } = string.Empty;

            public void WriteTo(Stream stream)
            {
                var bytes = Encoding.UTF8.GetBytes(Value);
                if (bytes.Length > ushort.MaxValue)
                    throw new AmfException($"Cannot write more than {ushort.MaxValue} into ScriptDataString");

                var buffer = new byte[sizeof(ushort)];
                BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)bytes.Length);

                stream.WriteByte((byte)Type);
                stream.Write(buffer);
                stream.Write(bytes);
            }

            public override bool Equals(object? obj) => obj is String @string && Value == @string.Value;
            public override int GetHashCode() => HashCode.Combine(Value);
            public static bool operator ==(String left, String right) => EqualityComparer<String>.Default.Equals(left, right);
            public static bool operator !=(String left, String right) => !(left == right);
            public static implicit operator string(String @string) => @string.Value;
            public static implicit operator String(string @string) => new String { Value = @string };
            public static implicit operator String(LongString @string) => new String { Value = @string.Value };
        }

        [DebuggerTypeProxy(typeof(AmfDictionaryDebugView))]
        [DebuggerDisplay("AmfObject, Count = {Count}")]
        public class Object : IScriptDataValue, IDictionary<string, IScriptDataValue>, ICollection<KeyValuePair<string, IScriptDataValue>>, IEnumerable<KeyValuePair<string, IScriptDataValue>>, IReadOnlyCollection<KeyValuePair<string, IScriptDataValue>>, IReadOnlyDictionary<string, IScriptDataValue>
        {
            public ScriptDataValueType Type => ScriptDataValueType.Object;

            [JsonProperty]
            public Dictionary<string, IScriptDataValue> Value { get; set; } = new Dictionary<string, IScriptDataValue>();

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);

                foreach (var item in Value)
                {
                    // key
                    var bytes = Encoding.UTF8.GetBytes(item.Key);
                    if (bytes.Length > ushort.MaxValue)
                        throw new AmfException($"Cannot write more than {ushort.MaxValue} into ScriptDataString");

                    var buffer = new byte[sizeof(ushort)];
                    BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)bytes.Length);

                    stream.Write(buffer);
                    stream.Write(bytes);

                    // value
                    item.Value.WriteTo(stream);
                }

                stream.Write(new byte[] { 0, 0, 9 });
            }

            public IScriptDataValue this[string key] { get => ((IDictionary<string, IScriptDataValue>)Value)[key]; set => ((IDictionary<string, IScriptDataValue>)Value)[key] = value; }
            public ICollection<string> Keys => ((IDictionary<string, IScriptDataValue>)Value).Keys;
            public ICollection<IScriptDataValue> Values => ((IDictionary<string, IScriptDataValue>)Value).Values;
            IEnumerable<string> IReadOnlyDictionary<string, IScriptDataValue>.Keys => ((IReadOnlyDictionary<string, IScriptDataValue>)Value).Keys;
            IEnumerable<IScriptDataValue> IReadOnlyDictionary<string, IScriptDataValue>.Values => ((IReadOnlyDictionary<string, IScriptDataValue>)Value).Values;
            public int Count => ((IDictionary<string, IScriptDataValue>)Value).Count;
            public bool IsReadOnly => ((IDictionary<string, IScriptDataValue>)Value).IsReadOnly;
            public void Add(string key, IScriptDataValue value) => ((IDictionary<string, IScriptDataValue>)Value).Add(key, value);
            public void Add(KeyValuePair<string, IScriptDataValue> item) => ((IDictionary<string, IScriptDataValue>)Value).Add(item);
            public void Clear() => ((IDictionary<string, IScriptDataValue>)Value).Clear();
            public bool Contains(KeyValuePair<string, IScriptDataValue> item) => ((IDictionary<string, IScriptDataValue>)Value).Contains(item);
            public bool ContainsKey(string key) => ((IDictionary<string, IScriptDataValue>)Value).ContainsKey(key);
            public void CopyTo(KeyValuePair<string, IScriptDataValue>[] array, int arrayIndex) => ((IDictionary<string, IScriptDataValue>)Value).CopyTo(array, arrayIndex);
            public IEnumerator<KeyValuePair<string, IScriptDataValue>> GetEnumerator() => ((IDictionary<string, IScriptDataValue>)Value).GetEnumerator();
            public bool Remove(string key) => ((IDictionary<string, IScriptDataValue>)Value).Remove(key);
            public bool Remove(KeyValuePair<string, IScriptDataValue> item) => ((IDictionary<string, IScriptDataValue>)Value).Remove(item);
            public bool TryGetValue(string key, [MaybeNullWhen(false)] out IScriptDataValue value) => ((IDictionary<string, IScriptDataValue>)Value).TryGetValue(key, out value!);
            IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<string, IScriptDataValue>)Value).GetEnumerator();
            public static implicit operator Dictionary<string, IScriptDataValue>(Object @object) => @object.Value;
            public static implicit operator Object(Dictionary<string, IScriptDataValue> @object) => new Object { Value = @object };
            public static implicit operator Object(EcmaArray ecmaArray) => new Object { Value = ecmaArray };
        }

        [DebuggerDisplay("AmfNull")]
        public class Null : IScriptDataValue
        {
            public ScriptDataValueType Type => ScriptDataValueType.Null;

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);
            }

            public override bool Equals(object? obj) => obj is Null;
            public override int GetHashCode() => 0;
            public static bool operator ==(Null left, Null right) => EqualityComparer<Null>.Default.Equals(left, right);
            public static bool operator !=(Null left, Null right) => !(left == right);
        }

        [DebuggerDisplay("AmfUndefined")]
        public class Undefined : IScriptDataValue
        {
            public ScriptDataValueType Type => ScriptDataValueType.Undefined;

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);
            }

            public override bool Equals(object? obj) => obj is Undefined;
            public override int GetHashCode() => 0;
            public static bool operator ==(Undefined left, Undefined right) => EqualityComparer<Undefined>.Default.Equals(left, right);
            public static bool operator !=(Undefined left, Undefined right) => !(left == right);
        }

        [DebuggerDisplay("AmfReference, {Value}")]
        public class Reference : IScriptDataValue
        {
            public ScriptDataValueType Type => ScriptDataValueType.Reference;

            [JsonProperty]
            public ushort Value { get; set; }

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);

                var buffer = new byte[sizeof(ushort)];
                BinaryPrimitives.WriteUInt16BigEndian(buffer, Value);
                stream.Write(buffer);
            }

            public override bool Equals(object? obj) => obj is Reference reference && Value == reference.Value;
            public override int GetHashCode() => HashCode.Combine(Value);
            public static bool operator ==(Reference left, Reference right) => EqualityComparer<Reference>.Default.Equals(left, right);
            public static bool operator !=(Reference left, Reference right) => !(left == right);
            public static implicit operator ushort(Reference reference) => reference.Value;
            public static implicit operator Reference(ushort number) => new Reference { Value = number };
        }

        [DebuggerTypeProxy(typeof(AmfDictionaryDebugView))]
        [DebuggerDisplay("AmfEcmaArray, Count = {Count}")]
        public class EcmaArray : IScriptDataValue, IDictionary<string, IScriptDataValue>, ICollection<KeyValuePair<string, IScriptDataValue>>, IEnumerable<KeyValuePair<string, IScriptDataValue>>, IReadOnlyCollection<KeyValuePair<string, IScriptDataValue>>, IReadOnlyDictionary<string, IScriptDataValue>
        {
            public ScriptDataValueType Type => ScriptDataValueType.EcmaArray;

            [JsonProperty]
            public Dictionary<string, IScriptDataValue> Value { get; set; } = new Dictionary<string, IScriptDataValue>();

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);

                {
                    var buffer = new byte[sizeof(uint)];
                    BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)Value.Count);
                    stream.Write(buffer);
                }

                foreach (var item in Value)
                {
                    // key
                    var bytes = Encoding.UTF8.GetBytes(item.Key);
                    if (bytes.Length > ushort.MaxValue)
                        throw new AmfException($"Cannot write more than {ushort.MaxValue} into ScriptDataString");

                    var buffer = new byte[sizeof(ushort)];
                    BinaryPrimitives.WriteUInt16BigEndian(buffer, (ushort)bytes.Length);

                    stream.Write(buffer);
                    stream.Write(bytes);

                    // value
                    item.Value.WriteTo(stream);
                }

                stream.Write(new byte[] { 0, 0, 9 });
            }

            public IScriptDataValue this[string key] { get => ((IDictionary<string, IScriptDataValue>)Value)[key]; set => ((IDictionary<string, IScriptDataValue>)Value)[key] = value; }
            public ICollection<string> Keys => ((IDictionary<string, IScriptDataValue>)Value).Keys;
            public ICollection<IScriptDataValue> Values => ((IDictionary<string, IScriptDataValue>)Value).Values;
            IEnumerable<string> IReadOnlyDictionary<string, IScriptDataValue>.Keys => ((IReadOnlyDictionary<string, IScriptDataValue>)Value).Keys;
            IEnumerable<IScriptDataValue> IReadOnlyDictionary<string, IScriptDataValue>.Values => ((IReadOnlyDictionary<string, IScriptDataValue>)Value).Values;
            public int Count => ((IDictionary<string, IScriptDataValue>)Value).Count;
            public bool IsReadOnly => ((IDictionary<string, IScriptDataValue>)Value).IsReadOnly;
            public void Add(string key, IScriptDataValue value) => ((IDictionary<string, IScriptDataValue>)Value).Add(key, value);
            public void Add(KeyValuePair<string, IScriptDataValue> item) => ((IDictionary<string, IScriptDataValue>)Value).Add(item);
            public void Clear() => ((IDictionary<string, IScriptDataValue>)Value).Clear();
            public bool Contains(KeyValuePair<string, IScriptDataValue> item) => ((IDictionary<string, IScriptDataValue>)Value).Contains(item);
            public bool ContainsKey(string key) => ((IDictionary<string, IScriptDataValue>)Value).ContainsKey(key);
            public void CopyTo(KeyValuePair<string, IScriptDataValue>[] array, int arrayIndex) => ((IDictionary<string, IScriptDataValue>)Value).CopyTo(array, arrayIndex);
            public IEnumerator<KeyValuePair<string, IScriptDataValue>> GetEnumerator() => ((IDictionary<string, IScriptDataValue>)Value).GetEnumerator();
            public bool Remove(string key) => ((IDictionary<string, IScriptDataValue>)Value).Remove(key);
            public bool Remove(KeyValuePair<string, IScriptDataValue> item) => ((IDictionary<string, IScriptDataValue>)Value).Remove(item);
            public bool TryGetValue(string key, [MaybeNullWhen(false)] out IScriptDataValue value) => ((IDictionary<string, IScriptDataValue>)Value).TryGetValue(key, out value!);
            IEnumerator IEnumerable.GetEnumerator() => ((IDictionary<string, IScriptDataValue>)Value).GetEnumerator();
            public static implicit operator Dictionary<string, IScriptDataValue>(EcmaArray ecmaArray) => ecmaArray.Value;
            public static implicit operator EcmaArray(Dictionary<string, IScriptDataValue> ecmaArray) => new EcmaArray { Value = ecmaArray };
            public static implicit operator EcmaArray(Object @object) => new EcmaArray { Value = @object };
        }

        [DebuggerTypeProxy(typeof(AmfCollectionDebugView))]
        [DebuggerDisplay("AmfStrictArray, Count = {Count}")]
        public class StrictArray : IScriptDataValue, IList<IScriptDataValue>, ICollection<IScriptDataValue>, IEnumerable<IScriptDataValue>, IReadOnlyCollection<IScriptDataValue>, IReadOnlyList<IScriptDataValue>
        {
            public ScriptDataValueType Type => ScriptDataValueType.StrictArray;

            [JsonProperty]
            public List<IScriptDataValue> Value { get; set; } = new List<IScriptDataValue>();

            public void WriteTo(Stream stream)
            {
                stream.WriteByte((byte)Type);

                var buffer = new byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)Value.Count);
                stream.Write(buffer);

                foreach (var item in Value)
                    item.WriteTo(stream);
            }

            public IScriptDataValue this[int index] { get => ((IList<IScriptDataValue>)Value)[index]; set => ((IList<IScriptDataValue>)Value)[index] = value; }
            public int Count => ((IList<IScriptDataValue>)Value).Count;
            public bool IsReadOnly => ((IList<IScriptDataValue>)Value).IsReadOnly;
            public void Add(IScriptDataValue item) => ((IList<IScriptDataValue>)Value).Add(item);
            public void Clear() => ((IList<IScriptDataValue>)Value).Clear();
            public bool Contains(IScriptDataValue item) => ((IList<IScriptDataValue>)Value).Contains(item);
            public void CopyTo(IScriptDataValue[] array, int arrayIndex) => ((IList<IScriptDataValue>)Value).CopyTo(array, arrayIndex);
            public IEnumerator<IScriptDataValue> GetEnumerator() => ((IList<IScriptDataValue>)Value).GetEnumerator();
            public int IndexOf(IScriptDataValue item) => ((IList<IScriptDataValue>)Value).IndexOf(item);
            public void Insert(int index, IScriptDataValue item) => ((IList<IScriptDataValue>)Value).Insert(index, item);
            public bool Remove(IScriptDataValue item) => ((IList<IScriptDataValue>)Value).Remove(item);
            public void RemoveAt(int index) => ((IList<IScriptDataValue>)Value).RemoveAt(index);
            IEnumerator IEnumerable.GetEnumerator() => ((IList<IScriptDataValue>)Value).GetEnumerator();
            public static implicit operator List<IScriptDataValue>(StrictArray strictArray) => strictArray.Value;
            public static implicit operator StrictArray(List<IScriptDataValue> values) => new StrictArray { Value = values };
        }

        [DebuggerDisplay("AmfDate, {Value}")]
        public class Date : IScriptDataValue
        {
            public Date() { }
            public Date(DateTimeOffset value) => Value = value;
            public Date(double dateTime, short localDateTimeOffset) => Value = DateTimeOffset.FromUnixTimeMilliseconds((long)dateTime).ToOffset(TimeSpan.FromMinutes(localDateTimeOffset));

            public ScriptDataValueType Type => ScriptDataValueType.Date;

            [JsonProperty]
            public DateTimeOffset Value { get; set; }

            public void WriteTo(Stream stream)
            {
                var dateTime = (double)Value.ToUnixTimeMilliseconds();
                var localDateTimeOffset = (short)Value.Offset.TotalMinutes;
                var buffer1 = new byte[sizeof(double)];
                var buffer2 = new byte[sizeof(ushort)];
                BinaryPrimitives.WriteInt64BigEndian(buffer1, BitConverter.DoubleToInt64Bits(dateTime));
                BinaryPrimitives.WriteInt16BigEndian(buffer2, localDateTimeOffset);
                stream.WriteByte((byte)Type);
                stream.Write(buffer1);
                stream.Write(buffer2);
            }

            public override bool Equals(object? obj) => obj is Date date && Value.Equals(date.Value);
            public override int GetHashCode() => HashCode.Combine(Value);
            public static bool operator ==(Date left, Date right) => EqualityComparer<Date>.Default.Equals(left, right);
            public static bool operator !=(Date left, Date right) => !(left == right);
            public static implicit operator DateTimeOffset(Date date) => date.Value;
            public static implicit operator Date(DateTimeOffset date) => new Date(date);
            public static implicit operator DateTime(Date date) => date.Value.DateTime;
            public static implicit operator Date(DateTime date) => new Date(date);
        }

        [DebuggerDisplay("AmfLongString, {Value}")]
        public class LongString : IScriptDataValue
        {
            public ScriptDataValueType Type => ScriptDataValueType.LongString;

            [JsonProperty(Required = Required.Always)]
            public string Value { get; set; } = string.Empty;

            public void WriteTo(Stream stream)
            {
                var bytes = Encoding.UTF8.GetBytes(Value);

                var buffer = new byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)bytes.Length);

                stream.WriteByte((byte)Type);
                stream.Write(buffer);
                stream.Write(bytes);
            }

            public override bool Equals(object? obj) => obj is LongString @string && Value == @string.Value;
            public override int GetHashCode() => HashCode.Combine(Value);
            public static bool operator ==(LongString left, LongString right) => EqualityComparer<LongString>.Default.Equals(left, right);
            public static bool operator !=(LongString left, LongString right) => !(left == right);
            public static implicit operator string(LongString @string) => @string.Value;
            public static implicit operator LongString(string @string) => new LongString { Value = @string };
            public static implicit operator LongString(String @string) => new LongString { Value = @string.Value };
        }

        private sealed class AmfCollectionDebugView
        {
            private readonly ICollection<IScriptDataValue> _collection;

            public AmfCollectionDebugView(ICollection<IScriptDataValue> collection) => _collection = collection ?? throw new ArgumentNullException(nameof(collection));

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public IScriptDataValue[] Items => _collection.ToArray();
        }

        private sealed class AmfDictionaryDebugView
        {
            private readonly IDictionary<string, IScriptDataValue> _dict;

            public AmfDictionaryDebugView(IDictionary<string, IScriptDataValue> dictionary) => _dict = dictionary ?? throw new ArgumentNullException(nameof(dictionary));

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public KeyValuePairDebugView<string, IScriptDataValue>[] Items
                => _dict.Select(x => new KeyValuePairDebugView<string, IScriptDataValue>(x.Key, x.Value)).ToArray();
        }

        [DebuggerDisplay("{Key}: {Value}")]
        private sealed class KeyValuePairDebugView<K, V>
        {
            public KeyValuePairDebugView(K key, V value)
            {
                Key = key;
                Value = value;
            }

            public K Key { get; }
            public V Value { get; }
        }
    }
}
