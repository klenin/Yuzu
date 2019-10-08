using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Yuzu
{
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class YuzuField : Attribute
	{
		public readonly string Alias;
		public YuzuField(string alias) { Alias = alias; }
	}

	// YuzuField attributes must have default constructors for YuzuAll to work.

	public class YuzuRequired : YuzuField
	{
		public YuzuRequired() : base(null) { }
		public YuzuRequired(string alias) : base(alias) { }
	}

	public class YuzuOptional : YuzuField
	{
		public YuzuOptional() : base(null) { }
		public YuzuOptional(string alias) : base(alias) { }
	}

	public class YuzuMember : YuzuField
	{
		public YuzuMember() : base(null) { }
		public YuzuMember(string alias) : base(alias) { }
	}

	[AttributeUsage(
		AttributeTargets.Field | AttributeTargets.Property |
		AttributeTargets.Class | AttributeTargets.Struct)]
	public class YuzuAlias : Attribute
	{
		public readonly string[] ReadAliases;
		public readonly string WriteAlias;
		public YuzuAlias(string alias) : base()
		{
			ReadAliases = new string[] { alias };
			WriteAlias = alias;
		}
		public YuzuAlias(string[] read = null, string write = null) : base()
		{
			ReadAliases = read;
			WriteAlias = write;
		}
	}

	public enum YuzuNoDefault { NoDefault };

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public abstract class YuzuSerializeCondition : Attribute
	{
		public abstract Func<object, object, bool> MakeChecker(Type tObj);
		public virtual MethodInfo GetMethod(Type t) => null;
		public virtual object GetDefault() => YuzuNoDefault.NoDefault;
	}

	public class YuzuSerializeIf : YuzuSerializeCondition
	{
		public readonly string Method;
		public YuzuSerializeIf(string method) { Method = method; }

		public override Func<object, object, bool> MakeChecker(Type tObj)
		{
			var fn = tObj.GetMethod(Method);
			if (fn == null)
				throw new YuzuException();
			var p = Expression.Parameter(typeof(object));
			var pf = Expression.Parameter(typeof(object));
			var e = Expression.Call(Expression.Convert(p, tObj), fn);
			return Expression.Lambda<Func<object, object, bool>>(e, p, pf).Compile();
		}
		public override MethodInfo GetMethod(Type t) => t.GetMethod(Method);
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class YuzuSerializeItemIf : Attribute
	{
		internal static Func<object, int, object, bool> MakeChecker(MethodInfo m)
		{
			var pObj = Expression.Parameter(typeof(object));
			var pIndex = Expression.Parameter(typeof(int));
			var pItem = Expression.Parameter(typeof(object));
			var e = Expression.Call(Expression.Convert(pObj, m.DeclaringType), m, pIndex, pItem);
			return Expression.Lambda<Func<object, int, object, bool>>(
				e, pObj, pIndex, pItem).Compile();
		}
	}

	public class YuzuDefault : YuzuSerializeCondition
	{
		public object Value;
		public YuzuDefault(object value)
		{
			Value = value;
		}

		private bool Check(object obj, object field) => !Value.Equals(field);
		public override Func<object, object, bool> MakeChecker(Type tObj) => Check;
		public override object GetDefault() => Value;
	}

	[AttributeUsage(
		AttributeTargets.Field | AttributeTargets.Property |
		AttributeTargets.Class | AttributeTargets.Struct)]
	public class YuzuCompact : Attribute { }

	[AttributeUsage(AttributeTargets.Method)]
	public class YuzuEventAttribute : Attribute { }
	public class YuzuBeforeSerialization : YuzuEventAttribute { }
	public class YuzuAfterSerialization : YuzuEventAttribute { }
	public class YuzuBeforeDeserialization : YuzuEventAttribute { }
	public class YuzuAfterDeserialization : YuzuEventAttribute { }

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class YuzuMerge : Attribute { }

	[Flags]
	public enum YuzuItemKind
	{
		None = 0,
		Field = 1,
		Property = 2,
		Any = 3,
	}

	public enum YuzuItemOptionality
	{
		None = 0,
		Optional = 1,
		Required = 2,
		Member = 3,
	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class YuzuMust : Attribute
	{
		public readonly YuzuItemKind Kind;
		public YuzuMust(YuzuItemKind kind = YuzuItemKind.Any) { Kind = kind; }
	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class YuzuAll : Attribute
	{
		public readonly YuzuItemOptionality Optionality = YuzuItemOptionality.Member;
		public readonly YuzuItemKind Kind;
		public YuzuAll(YuzuItemOptionality optionality, YuzuItemKind kind = YuzuItemKind.Any)
		{
			Optionality = optionality;
			Kind = kind;
		}
		public YuzuAll(YuzuItemKind kind = YuzuItemKind.Any) { Kind = kind; }
	}

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
	public class YuzuExclude : Attribute { }

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class YuzuAllowReadingFromAncestor : Attribute { }
	[AttributeUsage(AttributeTargets.Method)]
	public class YuzuFactory : Attribute { }
	[AttributeUsage(
		AttributeTargets.Field | AttributeTargets.Property |
		AttributeTargets.Class | AttributeTargets.Struct)]
	public class YuzuCopyable : Attribute { }

	[AttributeUsage(AttributeTargets.Method)]
	public class YuzuSurrogateIf : Attribute { }
	[AttributeUsage(AttributeTargets.Method)]
	public class YuzuToSurrogate : Attribute { }
	[AttributeUsage(AttributeTargets.Method)]
	public class YuzuFromSurrogate : Attribute { }

	public enum TagMode
	{
		Aliases = 0,
		Names,
		Ids,
	}

	public class MetaItemOverride
	{
		public MemberInfo Info;
		public ConcurrentBag<Attribute> Attributes = new ConcurrentBag<Attribute>();
		public ConcurrentBag<Type> NegatedAttributes = new ConcurrentBag<Type>();

		public MetaItemOverride AddAttr(Attribute attr)
		{
			Attributes.Add(attr);
			return this;
		}

		public MetaItemOverride NegateAttr(Type attrType)
		{
			NegatedAttributes.Add(attrType);
			return this;
		}

		public Attribute Attr(Type attrType) =>
			attrType == null || NegatedAttributes.Contains(attrType) ? null :
				Attributes.SingleOrDefault(a => a.GetType() == attrType) ??
				Info.GetCustomAttribute(attrType);

		public bool HasAttr(Type attrType) =>
			attrType != null && !NegatedAttributes.Contains(attrType) &&
				(Attributes.Any(a => a.GetType() == attrType) || Info.IsDefined(attrType));

	}

	public class MetaOverride: MetaItemOverride {
		public Type TypeInfo() => Info as Type;
		public ConcurrentDictionary<string, MetaItemOverride> Items =
			new ConcurrentDictionary<string, MetaItemOverride>();

		public new MetaOverride AddAttr(Attribute attr)
		{
			Attributes.Add(attr);
			return this;
		}

		public new MetaItemOverride NegateAttr(Type attrType)
		{
			NegatedAttributes.Add(attrType);
			return this;
		}

		public MetaOverride AddItem(string itemName, Action<MetaItemOverride> after = null)
		{
			var m = TypeInfo().GetMember(itemName);
			var item = new MetaItemOverride { Info = m[0] };
			if (Items.TryAdd(itemName, item))
				after?.Invoke(item);
			return this;
		}

		public MetaItemOverride Item(MemberInfo m)
		{
			if (Items.TryGetValue(m.Name, out MetaItemOverride item))
				return item;
			return new MetaItemOverride { Info = m };
		}
		public MetaItemOverride Item(string itemName) => Item(TypeInfo().GetMember(itemName)[0]);
	}

	public class MetaOptions
	{
		public static MetaOptions Default = new MetaOptions();

		public Type RequiredAttribute = typeof(YuzuRequired);
		public Type OptionalAttribute = typeof(YuzuOptional);
		public Type MemberAttribute = typeof(YuzuMember);
		public Type CompactAttribute = typeof(YuzuCompact);
		public Type SerializeConditionAttribute = typeof(YuzuSerializeCondition);
		public Type SerializeItemIfAttribute = typeof(YuzuSerializeItemIf);
		public Type BeforeSerializationAttribute = typeof(YuzuBeforeSerialization);
		public Type AfterSerializationAttribute = typeof(YuzuAfterSerialization);
		public Type BeforeDeserializationAttribute = typeof(YuzuBeforeDeserialization);
		public Type AfterDeserializationAttribute = typeof(YuzuAfterDeserialization);
		public Type MergeAttribute = typeof(YuzuMerge);
		public Type MustAttribute = typeof(YuzuMust);
		public Type AllAttribute = typeof(YuzuAll);
		public Type ExcludeAttribute = typeof(YuzuExclude);
		public Type AllowReadingFromAncestorAttribute = typeof(YuzuAllowReadingFromAncestor);
		public Type AliasAttribute = typeof(YuzuAlias);
		public Type FactoryAttribute = typeof(YuzuFactory);
		public Type CopyableAttribute = typeof(YuzuCopyable);

		public Type SurrogateIfAttribute = typeof(YuzuSurrogateIf);
		public Type ToSurrogateAttribute = typeof(YuzuToSurrogate);
		public Type FromSurrogateAttribute = typeof(YuzuFromSurrogate);

		public Func<Attribute, string> GetAlias = attr => (attr as YuzuField).Alias;
		public Func<Attribute, Type, Func<object, object, bool>> GetSerializeCondition =
			(attr, t) => (attr as YuzuSerializeCondition).MakeChecker(t);
		public Func<Attribute, Type, MethodInfo> GetSerializeMethod =
			(attr, t) => (attr as YuzuSerializeCondition).GetMethod(t);
		public Func<Attribute, object> GetDefault =
			attr => (attr as YuzuSerializeCondition).GetDefault();
		public Func<MethodInfo, Func<object, int, object, bool>> GetSerializeItemCondition =
			YuzuSerializeItemIf.MakeChecker;
		public Func<Attribute, YuzuItemKind> GetItemKind = attr => (attr as YuzuMust).Kind;
		public Func<Attribute, Tuple<YuzuItemOptionality, YuzuItemKind>> GetItemOptionalityAndKind =
			attr => Tuple.Create((attr as YuzuAll).Optionality, (attr as YuzuAll).Kind);
		public Func<Attribute, IEnumerable<string>> GetReadAliases = attr => (attr as YuzuAlias).ReadAliases;
		public Func<Attribute, string> GetWriteAlias = attr => (attr as YuzuAlias).WriteAlias;

		private ConcurrentDictionary<Type, MetaOverride> overrides =
			new ConcurrentDictionary<Type, MetaOverride>();

		public MetaOptions AddOverride(Type t, Action<MetaOverride> after = null)
		{
			var result = overrides.GetOrAdd(t, t1 => new MetaOverride { Info = t1 });
			after?.Invoke(result);
			return this;
		}

		public MetaOverride GetOverride(Type t)
		{
			MetaOverride result;
			if (overrides.TryGetValue(t, out result))
				return result;
			return new MetaOverride { Info = t };
		}
		public MetaItemOverride GetItem(MemberInfo m)
		{
			if (overrides.TryGetValue(m.DeclaringType, out MetaOverride over))
				return over.Item(m);
			return new MetaItemOverride { Info = m };
		}
	}
	public struct CommonOptions
	{
		public MetaOptions Meta;
		public TagMode TagMode;
		public bool AllowUnknownFields;
		public bool AllowEmptyTypes;
		public bool ReportErrorPosition;
		public bool CheckForEmptyCollections;
	}

	public class YuzuPosition
	{
		public readonly long Offset = 0;
		public YuzuPosition(long offset) { Offset = offset; }
		public override string ToString()
		{
			return "byte " + Offset.ToString();
		}
	}

	public class YuzuException: Exception
	{
		public readonly YuzuPosition Position = null;

		public YuzuException() { }

		public YuzuException(string message, YuzuPosition position = null): base(
			position == null ? message : message + " at " + position.ToString())
		{
			Position = position;
		}
	}

	public class YuzuUnknown: DynamicObject
	{
		public string ClassTag;
		public SortedDictionary<string, object> Fields = new SortedDictionary<string, object>();

		public static dynamic Dyn(object obj)
		{
			if (obj is IReadOnlyDictionary<string, object>) {
				var u = new YuzuUnknown();
				foreach (var p in obj as IReadOnlyDictionary<string, object>)
					u.Fields.Add(p.Key, p.Value);
				return u;
			}
			return obj;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result) =>
			Fields.TryGetValue(binder.Name, out result);

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			Fields[binder.Name] = value;
			return true;
		}
	}

	public class YuzuUnknownStorage
	{
		public struct Item
		{
			public string Name;
			public object Value;
			static public int Comparer(Item i1, Item i2) { return String.CompareOrdinal(i1.Name, i2.Name); }
		}
		public List<Item> Fields = new List<Item>();
		public bool IsOrdered { get; private set; }
		internal object Internal;

		public YuzuUnknownStorage() { IsOrdered = true; }

		public void Sort()
		{
			if (IsOrdered)
				return;
			Fields.Sort(Item.Comparer);
			IsOrdered = true;
		}
		public void Clear(bool clearMetadata = false)
		{
			Fields.Clear();
			IsOrdered = true;
			if (clearMetadata)
				Internal = null;
		}
		public virtual void Add(string name, object value)
		{
			Fields.Add(new Item { Name = name, Value = value });
			if (Fields.Count > 1 && IsOrdered)
				IsOrdered = Item.Comparer(Fields[0], Fields[1]) < 0;
		}
	}

	public class YuzuAssert : YuzuException
	{
		public YuzuAssert(string message = "") : base(message) { }
	}

	public abstract class AbstractSerializer
	{
		public CommonOptions Options = new CommonOptions();
		public abstract void ToWriter(object obj, BinaryWriter writer);
		public abstract string ToString(object obj);
		public abstract byte[] ToBytes(object obj);
		public abstract void ToStream(object obj, Stream target);

		protected Action<object> MakeDelegateAction(MethodInfo m) =>
			(Action<object>)Delegate.CreateDelegate(typeof(Action<object>), this, m);

		protected Action<object, TParam> MakeDelegateParam<TParam>(MethodInfo m) =>
			(Action<object, TParam>)Delegate.CreateDelegate(typeof(Action<object, TParam>), this, m);

		protected Action<object, TParam1, TParam2> MakeDelegateParam2<TParam1, TParam2>(MethodInfo m) =>
			(Action<object, TParam1, TParam2>)
				Delegate.CreateDelegate(typeof(Action<object, TParam1, TParam2>), this, m);

	}

	public abstract class AbstractWriterSerializer: AbstractSerializer
	{
		protected BinaryWriter writer;

		protected abstract void ToWriter(object obj);

		public override void ToWriter(object obj, BinaryWriter writer)
		{
			this.writer = writer;
			ToWriter(obj);
		}

		public override string ToString(object obj)
		{
			var ms = new MemoryStream();
			ToStream(obj, ms);
			return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
		}

		public override byte[] ToBytes(object obj)
		{
			var ms = new MemoryStream();
			ToStream(obj, ms);
			var result = ms.GetBuffer();
			Array.Resize(ref result, (int)ms.Length);
			return result;
		}

		public override void ToStream(object obj, Stream target)
		{
			ToWriter(obj, new BinaryWriter(target));
		}
	}

	public abstract class AbstractStringSerializer : AbstractSerializer
	{
		protected StringBuilder builder;

		protected abstract void ToBuilder(object obj);

		public override void ToWriter(object obj, BinaryWriter writer)
		{
			writer.Write(ToBytes(obj));
		}

		public override string ToString(object obj)
		{
			builder = new StringBuilder();
			ToBuilder(obj);
			return builder.ToString();
		}

		public override byte[] ToBytes(object obj)
		{
			return Encoding.UTF8.GetBytes(ToString(obj));
		}

		public override void ToStream(object obj, Stream target)
		{
			var b = ToBytes(obj);
			target.Write(b, 0, b.Length);
		}
	}

	public abstract class AbstractDeserializer
	{
		public CommonOptions Options = new CommonOptions();

		public abstract object FromReader(object obj, BinaryReader reader);
		public abstract object FromString(object obj, string source);
		public abstract object FromStream(object obj, Stream source);
		public abstract object FromBytes(object obj, byte[] bytes);

		public abstract object FromReader(BinaryReader reader);
		public abstract object FromString(string source);
		public abstract object FromStream(Stream source);
		public abstract object FromBytes(byte[] bytes);

		public abstract T FromReader<T>(BinaryReader reader);
		public abstract T FromString<T>(string source);
		public abstract T FromStream<T>(Stream source);
		public abstract T FromBytes<T>(byte[] bytes);
	}

	public interface IGenerator
	{
		string LineSeparator { get; set; }
		StreamWriter GenWriter { get; set; }
		void GenerateHeader();
		void GenerateFooter();
		void Generate<T>();
		void Generate(Type t);
	}
}
