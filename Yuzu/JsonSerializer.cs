using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using Yuzu.Grisu;
using Yuzu.Metadata;
using Yuzu.Util;

namespace Yuzu.Json
{
	[Flags]
	public enum JsonSaveClass
	{
		None = 0,
		Unknown = 1,
		KnownRoot = 2,
		KnownNonRoot = 4,
		UnknownPrimitive = 8,

		UnknownOrRoot = Unknown | KnownRoot,
	};

	public enum JsonUnknownNumberType
	{
		Double = 0,
		Minimal = 1,
	}

	public class JsonSerializeOptions
	{
		private int generation = 0;
		public int Generation { get { return generation; } }

		public string FieldSeparator = "\n";
		public string Indent = "\t";
		public string ClassTag = "class";
		public string ValueTag = "value";

		private int maxOnelineFields = 0;
		public int MaxOnelineFields { get { return maxOnelineFields; } set { maxOnelineFields = value; generation++; } }

		private bool enumAsString = false;
		public bool EnumAsString { get { return enumAsString; } set { enumAsString = value; generation++; } }

		public bool ArrayLengthPrefix = false;

		private JsonSaveClass saveClass = JsonSaveClass.Unknown;
		public JsonSaveClass SaveClass { get { return saveClass; } set { saveClass = value; generation++; } }
		[Obsolete("Use SaveClass instead")]
		public bool SaveRootClass {
			get { return SaveClass.HasFlag(JsonSaveClass.KnownRoot); }
			set { SaveClass = value ? JsonSaveClass.UnknownOrRoot : JsonSaveClass.Unknown; }
		}

		private bool ignoreCompact = false;
		public bool IgnoreCompact { get { return ignoreCompact; } set { ignoreCompact = value; generation++; } }

		public string DateFormat = "O";
		public string DateTimeOffsetFormat = "O";
		public string TimeSpanFormat = "c";

		private bool int64AsString = false;
		public bool Int64AsString { get { return int64AsString; } set { int64AsString = value; generation++; } }

		private bool decimalAsString = false;
		public bool DecimalAsString { get { return decimalAsString; } set { decimalAsString = value; generation++; } }

		public bool Unordered = false;

		private bool comments = false;
		public bool Comments { get { return comments; } set { comments = value; generation++; } }

		public bool BOM = false;

		private string floatingPointFormat = "";
		public string FloatingPointFormat {
			get { return floatingPointFormat; } set { floatingPointFormat = value; generation++; }
		}
		public JsonUnknownNumberType UnknownNumberType = JsonUnknownNumberType.Double;
	};

	public class JsonSerializer : AbstractWriterSerializer
	{
		public JsonSerializeOptions JsonOptions = new JsonSerializeOptions();

		public JsonSerializer() { InitWriters(); }

		private int depth = 0;

		private byte[] nullBytes = new byte[] { (byte)'n', (byte)'u', (byte)'l', (byte)'l' };

		private Dictionary<string, byte[]> strCache = new Dictionary<string, byte[]>();
		private byte[] StrToBytesCached(string s)
		{
			if (!strCache.TryGetValue(s, out byte[] b)) {
				b = Encoding.UTF8.GetBytes(s);
				strCache[s] = b;
			}
			return b;
		}

		private void WriteStr(string s) => writer.Write(Encoding.UTF8.GetBytes(s));
		private void WriteStrCached(string s) => writer.Write(StrToBytesCached(s));

		private void WriteFieldSeparator()
		{
			if (JsonOptions.FieldSeparator != String.Empty)
				WriteStrCached(JsonOptions.FieldSeparator);
		}

		private void WriteIndent()
		{
			if (JsonOptions.Indent == String.Empty)
				return;
			var b = StrToBytesCached(JsonOptions.Indent);
			for (int i = 0; i < depth; ++i)
				writer.Write(b);
		}

		private void WriteInt(object obj) => JsonIntWriter.WriteInt(writer, Convert.ToInt32(obj));
		private void WriteUInt(object obj) => JsonIntWriter.WriteUInt(writer, Convert.ToUInt32(obj));
		private void WriteLong(object obj) => JsonIntWriter.WriteLong(writer, (long)obj);
		private void WriteULong(object obj) => JsonIntWriter.WriteULong(writer, (ulong)obj);

		private void WriteLongAsString(object obj)
		{
			writer.Write((byte)'"');
			JsonIntWriter.WriteLong(writer, (long)obj);
			writer.Write((byte)'"');
		}

		private void WriteULongAsString(object obj)
		{
			writer.Write((byte)'"');
			JsonIntWriter.WriteULong(writer, (ulong)obj);
			writer.Write((byte)'"');
		}

		private void WriteDouble(object obj) => DoubleWriter.Write((double)obj, writer);
		private void WriteDoubleFormat(object obj) =>
			WriteStr(((double)obj).ToString(JsonOptions.FloatingPointFormat, CultureInfo.InvariantCulture));

		private void WriteSingle(object obj) => DoubleWriter.Write((float)obj, writer);
		private void WriteSingleFormat(object obj) =>
			WriteStr(((float)obj).ToString(JsonOptions.FloatingPointFormat, CultureInfo.InvariantCulture));

		private void WriteDecimal(object obj) =>
			WriteStr(((decimal)obj).ToString(CultureInfo.InvariantCulture));

		private void WriteDecimalAsString(object obj) =>
			WriteUnescapedString(((decimal)obj).ToString(CultureInfo.InvariantCulture));

		private void WriteUnescapedString(object obj)
		{
			writer.Write((byte)'"');
			WriteStrCached(obj.ToString());
			writer.Write((byte)'"');
		}

		private void WriteChar(object obj) =>
			JsonStringWriter.WriteEscapedString(writer, obj.ToString());

		private void WriteNullableEscapedString(object obj)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			JsonStringWriter.WriteEscapedString(writer, obj.ToString());
		}

		private void WriteBool(object obj) => WriteStrCached((bool)obj ? "true" : "false");

		private static byte[] localTimeZone = Encoding.ASCII.GetBytes(DateTime.Now.ToString("%K"));

		private void WriteDateTime(object obj)
		{
			var d = (DateTime)obj;
			// 'Roundtrip' format is guaranteed to be ASCII-clean.
			if (JsonOptions.DateFormat == "O") {
				writer.Write((byte)'"');
				JsonIntWriter.WriteInt4Digits(writer, d.Year);
				writer.Write((byte)'-');
				JsonIntWriter.WriteInt2Digits(writer, d.Month);
				writer.Write((byte)'-');
				JsonIntWriter.WriteInt2Digits(writer, d.Day);
				writer.Write((byte)'T');
				JsonIntWriter.WriteInt2Digits(writer, d.Hour);
				writer.Write((byte)':');
				JsonIntWriter.WriteInt2Digits(writer, d.Minute);
				writer.Write((byte)':');
				JsonIntWriter.WriteInt2Digits(writer, d.Second);
				writer.Write((byte)'.');
				JsonIntWriter.WriteInt7Digits(writer, (int)(d.Ticks % TimeSpan.TicksPerSecond));
				switch (d.Kind) {
					case DateTimeKind.Local:
						writer.Write(localTimeZone);
						break;
					case DateTimeKind.Unspecified:
						break;
					case DateTimeKind.Utc:
						writer.Write((byte)'Z');
						break;
				}
				writer.Write((byte)'"');
			}
			else
				JsonStringWriter.WriteEscapedString(
					writer, d.ToString(JsonOptions.DateFormat, CultureInfo.InvariantCulture));
		}

		private void WriteDateTimeOffset(object obj)
		{
			var d = (DateTimeOffset)obj;
			JsonStringWriter.WriteEscapedString(
				writer, d.ToString(JsonOptions.DateTimeOffsetFormat, CultureInfo.InvariantCulture));
		}

		private void WriteTimeSpan(object obj)
		{
			var t = (TimeSpan)obj;
			// 'Constant' format is guaranteed to be ASCII-clean.
			if (JsonOptions.TimeSpanFormat == "c") {
				writer.Write((byte)'"');
				if (t.Ticks < 0) {
					writer.Write((byte)'-');
					t = t.Duration();
				}
				var d = t.Days;
				if (d > 0) {
					JsonIntWriter.WriteInt(writer, d);
					writer.Write((byte)'.');
				}
				JsonIntWriter.WriteInt2Digits(writer, t.Hours);
				writer.Write((byte)':');
				JsonIntWriter.WriteInt2Digits(writer, t.Minutes);
				writer.Write((byte)':');
				JsonIntWriter.WriteInt2Digits(writer, t.Seconds);
				var f = (int)(t.Ticks % TimeSpan.TicksPerSecond);
				if (f > 0) {
					writer.Write((byte)'.');
					JsonIntWriter.WriteInt7Digits(writer, f);
				}
				writer.Write((byte)'"');
			}
			else
				JsonStringWriter.WriteEscapedString(
					writer, t.ToString(JsonOptions.TimeSpanFormat, CultureInfo.InvariantCulture));
		}

		private bool CondTrue(object obj, int index, object item) => true;

		private void WriteIEnumerable<T>(object obj) => WriteIEnumerableConditional<T>(obj, CondTrue);

		private void WriteIEnumerableConditional<T>(object obj, Func<object, int, object, bool> condition)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var list = (IEnumerable<T>)obj;
			var wf = GetWriteFunc(typeof(T));
			writer.Write((byte)'[');
			var isFirst = true;
			try {
				depth += 1;
				int index = -1;
				foreach (var elem in list) {
					index += 1;
					if (!condition(obj, index, elem))
						continue;
					if (!isFirst)
						writer.Write((byte)',');
					isFirst = false;
					WriteFieldSeparator();
					WriteIndent();
					wf(elem);
				}
				if (!isFirst)
					WriteFieldSeparator();
			}
			finally {
				depth -= 1;
			}
			if (!isFirst)
				WriteIndent();
			writer.Write((byte)']');
		}

		private void WriteIDictionary<K, V>(object obj)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var dict = (IDictionary<K, V>)obj;
			var wf = GetWriteFunc(typeof(V));
			writer.Write((byte)'{');
			if (dict.Count > 0) {
				try {
					depth += 1;
					WriteFieldSeparator();
					var isFirst = true;
					foreach (var elem in dict) {
						WriteSep(ref isFirst);
						WriteIndent();
						// TODO: Option to not escape dictionary keys.
						JsonStringWriter.WriteEscapedString(writer, elem.Key.ToString());
						writer.Write((byte)':');
						wf(elem.Value);
					}
					WriteFieldSeparator();
				}
				finally {
					depth -= 1;
				}
				WriteIndent();
			}
			writer.Write((byte)'}');
		}

		private void WriteArray<T>(object obj)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var array = (T[])obj;
			writer.Write((byte)'[');
			if (array.Length > 0) {
				var wf = GetWriteFunc(typeof(T));
				try {
					depth += 1;
					if (JsonOptions.ArrayLengthPrefix) {
						WriteIndent();
						JsonIntWriter.WriteInt(writer, array.Length);
					}
					var isFirst = !JsonOptions.ArrayLengthPrefix;
					foreach (var elem in array) {
						if (!isFirst)
							writer.Write((byte)',');
						isFirst = false;
						WriteFieldSeparator();
						WriteIndent();
						wf(elem);
					}
					WriteFieldSeparator();
				}
				finally {
					depth -= 1;
				}
				WriteIndent();
			}
			writer.Write((byte)']');
		}

		private void WriteArrayNDim(object obj, Action<object> writeElemFunc)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var array = (Array)obj;
			writer.Write((byte)'[');
			if (array.Length == 0) {
				writer.Write((byte)']');
				return;
			}
			++depth;
			var ubs = new int[array.Rank];
			var lbs = new int[array.Rank];
			var indices = new int[array.Rank];
			for (int dim = 0; dim < indices.Length; ++dim) {
				indices[dim] = lbs[dim] = array.GetLowerBound(dim);
				ubs[dim] = array.GetUpperBound(dim);
			}
			for (int dim = 0; dim >= 0;) {
				if (indices[dim] > ubs[dim]) {
					while (dim >= 0 && indices[dim] >= ubs[dim]) {
						indices[dim] = lbs[dim];
						--dim;
						--depth;
						WriteFieldSeparator();
						WriteIndent();
						writer.Write((byte)']');
					}
					if (dim < 0)
						break;
					++indices[dim];
					if (indices[dim] > lbs[dim])
						writer.Write((byte)',');
				}
				for (; dim < indices.Length - 1; ++dim) {
					indices[dim + 1] = lbs[dim + 1];
					WriteFieldSeparator();
					WriteIndent();
					writer.Write((byte)'[');
					++depth;
				}
				if (indices[dim] > lbs[dim])
					writer.Write((byte)',');
				WriteFieldSeparator();
				WriteIndent();
				writeElemFunc(array.GetValue(indices));
				++indices[dim];
			}
		}

		private void WriteTypedPrimitive(object obj, Type t)
		{
			writer.Write((byte)'{');
			depth += 1;
			WriteFieldSeparator();
			bool isFirst = true;
			WriteName(JsonOptions.ClassTag, ref isFirst);
			WriteUnescapedString(TypeSerializer.Serialize(t));
			WriteName(JsonOptions.ValueTag, ref isFirst);
			GetWriteFunc(t)(obj);
			WriteFieldSeparator();
			depth -= 1;
			WriteIndent();
			writer.Write((byte)'}');
		}

		// List<object>
		private void WriteAny(object obj)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var t = obj.GetType();
			if (t == typeof(object))
				throw new YuzuException("WriteAny of unknown type");
			if (IsUserObject(t)) {
				var meta = Meta.Get(t, Options);
				var sg = meta.Surrogate;
				Action<object> surrogateWriter = GetSurrogateWriter(meta);
				if (sg.FuncTo == null || (sg.FuncIf != null && !sg.FuncIf(obj)))
					// Ignore compact since class name is always required.
					WriteObject(obj, null, null);
				else
					surrogateWriter(sg.FuncTo(obj));
			}
			else if (JsonOptions.SaveClass.HasFlag(JsonSaveClass.UnknownPrimitive))
				WriteTypedPrimitive(obj, t);
			else
				GetWriteFunc(t)(obj);
		}

		private Stack<object> objStack = new Stack<object>();

		private void WriteAction(object obj)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var a = obj as MulticastDelegate;
			if (a.Target != objStack.Peek())
				throw new NotImplementedException();
			WriteUnescapedString(a.Method.Name);
		}

		private void WriteNullable(object obj, Action<object> normalWrite)
		{
			if (obj == null)
				writer.Write(nullBytes);
			else
				normalWrite(obj);
		}

		private void WriteUnknown(object obj)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var u = (YuzuUnknown)obj;
			writer.Write((byte)'{');
			WriteFieldSeparator();
			objStack.Push(obj);
			try {
				depth += 1;
				var isFirst = true;
				WriteName(JsonOptions.ClassTag, ref isFirst);
				WriteUnescapedString(u.ClassTag);
				foreach (var f in u.Fields) {
					WriteName(f.Key, ref isFirst);
					GetWriteFunc(f.Value.GetType())(f.Value);
				}
				if (!isFirst)
					WriteFieldSeparator();
			}
			finally {
				depth -= 1;
				objStack.Pop();
			}
			WriteIndent();
			writer.Write((byte)'}');
		}

		private bool IsOneline(Meta meta)
		{
			var r = meta.CountPrimitiveChildren(Options);
			return r != Meta.FoundNonPrimitive && r <= JsonOptions.MaxOnelineFields;
		}

		private Dictionary<Type, Action<object>> writerCache = new Dictionary<Type, Action<object>>();
		private int jsonOptionsGeneration = 0;

		private void InitWriters()
		{
			writerCache[typeof(sbyte)] = WriteInt;
			writerCache[typeof(byte)] = WriteUInt;
			writerCache[typeof(short)] = WriteInt;
			writerCache[typeof(ushort)] = WriteUInt;
			writerCache[typeof(int)] = WriteInt;
			writerCache[typeof(uint)] = WriteUInt;
			if (JsonOptions.Int64AsString) {
				writerCache[typeof(long)] = WriteLongAsString;
				writerCache[typeof(ulong)] = WriteULongAsString;
			}
			else {
				writerCache[typeof(long)] = WriteLong;
				writerCache[typeof(ulong)] = WriteULong;
			}
			writerCache[typeof(bool)] = WriteBool;
			writerCache[typeof(char)] = WriteChar;
			if (string.IsNullOrEmpty(JsonOptions.FloatingPointFormat)) {
				writerCache[typeof(double)] = WriteDouble;
				writerCache[typeof(float)] = WriteSingle;
			}
			else {
				writerCache[typeof(double)] = WriteDoubleFormat;
				writerCache[typeof(float)] = WriteSingleFormat;
			}
			if (JsonOptions.DecimalAsString)
				writerCache[typeof(decimal)] = WriteDecimalAsString;
			else
				writerCache[typeof(decimal)] = WriteDecimal;
			writerCache[typeof(DateTime)] = WriteDateTime;
			writerCache[typeof(DateTimeOffset)] = WriteDateTimeOffset;
			writerCache[typeof(TimeSpan)] = WriteTimeSpan;
			writerCache[typeof(Guid)] = WriteUnescapedString;
			writerCache[typeof(string)] = WriteNullableEscapedString;
			writerCache[typeof(object)] = WriteAny;
			writerCache[typeof(YuzuUnknown)] = WriteUnknown;
		}

		private Action<object> GetWriteFunc(Type t, bool cachedOnly = false)
		{
			if (jsonOptionsGeneration != JsonOptions.Generation) {
				writerCache.Clear();
				InitWriters();
				jsonOptionsGeneration = JsonOptions.Generation;
			}

			if (writerCache.TryGetValue(t, out Action<object> result))
				return result;
			if (cachedOnly)
				return null;
			result = MakeWriteFunc(t);
			writerCache[t] = result;
			return result;
		}

		private static Type[] systemTypes = {
			typeof(string), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid),
		};

		// This condition must be equivalent to MakeWriteFunc returning WriteObject*.
		private bool IsUserObject(Type t)
		{
			if (systemTypes.Contains(t)) return false;
			if (Utils.IsStruct(t)) return true;
			if (!t.IsClass && !t.IsInterface) return false;
			if (t.IsGenericType) {
				var g = t.GetGenericTypeDefinition();
				if (g == typeof(Action<>) || g == typeof(Nullable<>))
					return false;
			}
			if (Utils.GetICollection(t) != null || Utils.GetIDictionary(t) != null)
				return false;
			return true;
		}

		private Action<object> GetSurrogateWriter(Meta meta)
		{
			var sg = meta.Surrogate;
			if (sg.FuncTo == null)
				return null;
			var st = sg.SurrogateType;
			if (!IsUserObject(st))
				return GetWriteFunc(st);
			var sgMeta = Meta.Get(st, Options);
			// Unpack conditional compact surrogate for compact field to allow detection.
			if (!sgMeta.IsCompact || JsonOptions.IgnoreCompact || meta.IsCompact && sg.FuncIf != null) {
				var fieldWriters1 = GetFieldWriters(meta);
				return obj => WriteObject(obj, meta, fieldWriters1);
			}
			var fieldWriters = GetFieldWriters(sgMeta);
			if (IsOneline(sgMeta))
				return obj => WriteObjectCompactOneline(obj, sgMeta, fieldWriters);
			else
				return obj => WriteObjectCompact(obj, sgMeta, fieldWriters);
		}

		private Action<object> MakeObjectWriteFunc(Meta meta)
		{
			Action<object> writeFunc;
			var fieldWriters = GetFieldWriters(meta, cachedOnly: !Utils.IsStruct(meta.Type));
			if (!fieldWriters.Contains(null)) {
				// Fast case -- all field writers are already cached.
				if (!meta.IsCompact || JsonOptions.IgnoreCompact)
					writeFunc = obj => WriteObject(obj, meta, fieldWriters);
				else if (IsOneline(meta))
					writeFunc = obj => WriteObjectCompactOneline(obj, meta, fieldWriters);
				else
					writeFunc = obj => WriteObjectCompact(obj, meta, fieldWriters);
			}
			else {
				// Slow case -- there may be a recursuve type, so calculate writers lazily.
				if (!meta.IsCompact || JsonOptions.IgnoreCompact) {
					List<Action<object>> fw = null;
					writeFunc = obj => {
						fw = fw ?? GetFieldWriters(meta);
						WriteObject(obj, meta, fw);
					};
				}
				else if (IsOneline(meta)) {
					List<Action<object>> fw = null;
					writeFunc = obj => {
						fw = fw ?? GetFieldWriters(meta);
						WriteObjectCompactOneline(obj, meta, fw);
					};
				}
				else {
					List<Action<object>> fw = null;
					writeFunc = obj => {
						fw = fw ?? GetFieldWriters(meta);
						WriteObjectCompact(obj, meta, fw);
					};
				}
			}
			return writeFunc;
		}

		private Action<object> MakeWriteFunc(Type t)
		{
			if (t.IsEnum) {
				if (JsonOptions.EnumAsString)
					return WriteUnescapedString;
				else
					return GetWriteFunc(Enum.GetUnderlyingType(t));
			}
			if (t.IsGenericType) {
				var g = t.GetGenericTypeDefinition();
				if (g == typeof(Action<>)) {
					return WriteAction;
				}
				if (g == typeof(Nullable<>)) {
					var w = GetWriteFunc(t.GetGenericArguments()[0]);
					return obj => WriteNullable(obj, w);
				}
			}
			if (t.IsArray) {
				if (t.GetArrayRank() > 1) {
					var wf = GetWriteFunc(t.GetElementType());
					return obj => WriteArrayNDim(obj, wf);
				}
				return MakeDelegateAction(
					Utils.GetPrivateCovariantGeneric(GetType(), nameof(WriteArray), t));
			}

			var idict = Utils.GetIDictionary(t);
			if (idict != null) {
				return MakeDelegateAction(
					Utils.GetPrivateCovariantGenericAll(GetType(), nameof(WriteIDictionary), t));
			}

			var ienum = Utils.GetIEnumerable(t);
			if (ienum != null) {
				var meta = Meta.Get(t, Options); // Check for serializable fields.
				if (meta.SerializeItemIf != null) {
					var m = Utils.GetPrivateCovariantGeneric(
						GetType(), nameof(WriteIEnumerableConditional), ienum);
					var d = MakeDelegateParam<Func<object, int, object, bool>>(m);
					return obj => d(obj, meta.SerializeItemIf);
				}
				return MakeDelegateAction(
					Utils.GetPrivateCovariantGeneric(GetType(), nameof(WriteIEnumerable), ienum));
			}
			if (t.IsSubclassOf(typeof(YuzuUnknown)))
				return WriteUnknown;
			if (Utils.IsStruct(t) || t.IsClass || t.IsInterface) {
				var meta = Meta.Get(t, Options);
				var sg = meta.Surrogate;
				Action<object> surrogateWriter = GetSurrogateWriter(meta);
				if (sg.FuncTo != null && sg.FuncIf == null)
					return obj => surrogateWriter(sg.FuncTo(obj));

				var writeFunc = MakeObjectWriteFunc(meta);

				if (sg.FuncTo != null && sg.FuncIf != null)
					return obj => {
						if (sg.FuncIf(obj))
							surrogateWriter(sg.FuncTo(obj));
						else
							writeFunc(obj);
					};

				return writeFunc;
			}
			throw new NotImplementedException(t.Name);
		}

		private void WriteSep(ref bool isFirst)
		{
			if (!isFirst) {
				writer.Write((byte)',');
				WriteFieldSeparator();
			}
			isFirst = false;
		}

		private void WriteName(string name, ref bool isFirst)
		{
			WriteSep(ref isFirst);
			WriteIndent();
			WriteUnescapedString(name);
			writer.Write((byte)':');
		}

		private void WriteUnknownStorageItem(YuzuUnknownStorage.Item item, ref bool isFirst)
		{
			WriteName(item.Name, ref isFirst);
			GetWriteFunc(item.Value.GetType())(item.Value);
		}

		private List<Action<object>> GetFieldWriters(Meta meta, bool cachedOnly = false) =>
			meta.Items.Select(yi => GetWriteFunc(yi.Type, cachedOnly)).ToList();

		private bool NeedToSaveClass(bool isTypeUnknown, bool isRoot) =>
			isTypeUnknown && JsonOptions.SaveClass.HasFlag(JsonSaveClass.Unknown) ||
			isRoot && JsonOptions.SaveClass.HasFlag(JsonSaveClass.KnownRoot) ||
			JsonOptions.SaveClass.HasFlag(JsonSaveClass.KnownNonRoot);

		private void WriteObject(object obj, Meta meta, List<Action<object>> fieldWriters)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var expectedType = meta == null ? null : meta.Type;
			var actualType = obj.GetType();
			if (meta == null || meta.Type != actualType) {
				meta = Meta.Get(actualType, Options);
				fieldWriters = GetFieldWriters(meta);
			}
			meta.BeforeSerialization.Run(obj);
			writer.Write((byte)'{');
			WriteFieldSeparator();
			objStack.Push(obj);
			try {
				depth += 1;
				var isFirst = true;
				if (
					NeedToSaveClass(
						isTypeUnknown: expectedType != actualType || meta.WriteAlias != null,
						isRoot: objStack.Count == 1)
				) {
					WriteName(JsonOptions.ClassTag, ref isFirst);
					WriteUnescapedString(meta.WriteAlias ?? TypeSerializer.Serialize(actualType));
				}
				int fieldIndex = -1;
				var storage = meta.GetUnknownStorage == null ? null : meta.GetUnknownStorage(obj);
				// Duplicate code to optimize fast-path without unknown storage.
				if (storage == null || storage.Fields.Count == 0 || JsonOptions.Unordered) {
					foreach (var yi in meta.Items) {
						fieldIndex += 1;
						var value = yi.GetValue(obj);
						if (yi.SerializeCond != null && !yi.SerializeCond(obj, value))
							continue;
						WriteName(yi.Tag(Options), ref isFirst);
						fieldWriters[fieldIndex](value);
					}
					// If Unordered, dump all unknown fields after all known ones.
					if (storage != null)
						for (var storageIndex = 0; storageIndex < storage.Fields.Count; ++storageIndex)
							WriteUnknownStorageItem(storage.Fields[storageIndex], ref isFirst);
				}
				else {
					// Merge unknown and known fields.
					storage.Sort();
					var storageIndex = 0;
					foreach (var yi in meta.Items) {
						fieldIndex += 1;
						var value = yi.GetValue(obj);
						if (yi.SerializeCond != null && !yi.SerializeCond(obj, value))
							continue;
						var name = yi.Tag(Options);
						for (; storageIndex < storage.Fields.Count; ++storageIndex) {
							var si = storage.Fields[storageIndex];
							if (String.CompareOrdinal(si.Name, name) >= 0)
								break;
							WriteUnknownStorageItem(si, ref isFirst);
						}
						WriteName(name, ref isFirst);
						fieldWriters[fieldIndex](value);
					}
					for (; storageIndex < storage.Fields.Count; ++storageIndex)
						WriteUnknownStorageItem(storage.Fields[storageIndex], ref isFirst);
				}
				if (!isFirst)
					WriteFieldSeparator();
			}
			finally {
				depth -= 1;
				objStack.Pop();
			}
			WriteIndent();
			writer.Write((byte)'}');
			meta.AfterSerialization.Run(obj);
		}

		private void WriteObjectCompact(object obj, Meta meta, List<Action<object>> fieldWriters)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var actualType = obj.GetType();
			if (meta.Type != actualType)
				throw new YuzuException(String.Format(
					"Attempt to write compact type {0} instead of {1}", actualType.Name, meta.Type.Name));
			meta.BeforeSerialization.Run(obj);
			writer.Write((byte)'[');
			WriteFieldSeparator();
			var isFirst = true;
			objStack.Push(obj);
			try {
				depth += 1;
				int index = 0;
				foreach (var yi in meta.Items) {
					WriteSep(ref isFirst);
					WriteIndent();
					fieldWriters[index++](yi.GetValue(obj));
				}
			}
			finally {
				depth -= 1;
				objStack.Pop();
			};
			if (!isFirst)
				WriteFieldSeparator();
			WriteIndent();
			writer.Write((byte)']');
			meta.AfterSerialization.Run(obj);
		}

		private void WriteObjectCompactOneline(object obj, Meta meta, List<Action<object>> fieldWriters)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var actualType = obj.GetType();
			if (meta.Type != actualType)
				throw new YuzuException(String.Format(
					"Attempt to write compact type {0} instead of {1}", actualType.Name, meta.Type.Name));
			meta.BeforeSerialization.Run(obj);
			writer.Write((byte)'[');
			var isFirst = true;
			objStack.Push(obj);
			try {
				int index = 0;
				foreach (var yi in meta.Items) {
					if (!isFirst)
						writer.Write((byte)',');
					isFirst = false;
					fieldWriters[index++](yi.GetValue(obj));
				}
			}
			finally {
				objStack.Pop();
			};
			writer.Write((byte)']');
			meta.AfterSerialization.Run(obj);
		}

		protected override void ToWriter(object obj)
		{
			if (obj == null) {
				writer.Write(nullBytes);
				return;
			}
			var t = obj.GetType();
			if (JsonOptions.SaveClass.HasFlag(JsonSaveClass.UnknownPrimitive) && !IsUserObject(t))
				WriteTypedPrimitive(obj, t);
			else
				GetWriteFunc(t)(obj);
		}
	}

}
