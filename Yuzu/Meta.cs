using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Yuzu.Util;

namespace Yuzu.Metadata
{
	public class Meta
	{
		private static ConcurrentDictionary<Tuple<Type, CommonOptions>, Meta> cache =
			new ConcurrentDictionary<Tuple<Type, CommonOptions>, Meta>();
		private static ConcurrentDictionary<CommonOptions, Dictionary<string, Type>> readAliasCache =
			new ConcurrentDictionary<CommonOptions, Dictionary<string, Type>>();

		public class Item : IComparable<Item>
		{
			private string id;

			public string Name;
			public string Alias;
			public string Id
			{
				get
				{
					if (id == null)
						id = IdGenerator.GetNextId();
					return id;
				}
			}
			public bool IsOptional;
			public bool IsCompact;
			public Func<object, object, bool> SerializeIf;
			public Type Type;
			public Func<object, object> GetValue;
			public Action<object, object> SetValue;
			public FieldInfo FieldInfo;
			public PropertyInfo PropInfo;

			public int CompareTo(Item yi) { return string.CompareOrdinal(Alias, yi.Alias); }

			public string Tag(CommonOptions options)
			{
				switch (options.TagMode) {
					case TagMode.Names:
						return Name;
					case TagMode.Aliases:
						return Alias;
					case TagMode.Ids:
						return Id;
					default:
						throw new YuzuAssert();
				}
			}
			public string NameTagged(CommonOptions options)
			{
				var tag = Tag(options);
				return Name + (tag == Name ? "" : " (" + tag + ")");
			}
		}

		public readonly Type Type;
		private MetaOptions Options;
		public readonly List<Item> Items = new List<Item>();
		public readonly bool IsCompact;
		public bool IsCopyable;
		public object Default { get; private set; }
		public YuzuItemKind Must = YuzuItemKind.None;
		public YuzuItemKind AllKind = YuzuItemKind.None;
		public YuzuItemOptionality AllOptionality = YuzuItemOptionality.None;
		public bool AllowReadingFromAncestor;
		public Surrogate Surrogate;
		public string WriteAlias;
		public int RequiredCount { get; private set; }
		public Func<object, int, object, bool> SerializeItemIf;

		private object defaultFactory() => Activator.CreateInstance(Type);
		public MethodInfo FactoryMethod;
		public Func<object> Factory;

		public Dictionary<string, Item> TagToItem = new Dictionary<string, Item>();
		public Func<object, YuzuUnknownStorage> GetUnknownStorage;

		public ActionList BeforeSerialization = new ActionList();
		public ActionList AfterSerialization = new ActionList();
		public ActionList BeforeDeserialization = new ActionList();
		public ActionList AfterDeserialization = new ActionList();

#if !iOS // Apple forbids code generation.
		private static Action<object, object> SetterGenericHelper<TTarget, TParam>(MethodInfo m)
		{
			var action =
				(Action<TTarget, TParam>)Delegate.CreateDelegate(typeof(Action<TTarget, TParam>), m);
			return (object target, object param) => action((TTarget)target, (TParam)param);
		}

		private static Func<object, object> GetterGenericHelper<TTarget, TReturn>(MethodInfo m)
		{
			var func =
				(Func<TTarget, TReturn>)Delegate.CreateDelegate(typeof(Func<TTarget, TReturn>), m);
			return (object target) => (object)func((TTarget)target);
		}

		private static Action<object, object> BuildSetter(MethodInfo m)
		{
			var helper = typeof(Meta).
				GetMethod(nameof(SetterGenericHelper), BindingFlags.Static | BindingFlags.NonPublic).
				MakeGenericMethod(m.DeclaringType, m.GetParameters()[0].ParameterType);
			return (Action<object, object>)helper.Invoke(null, new object[] { m });
		}

		private static Func<object, object> BuildGetter(MethodInfo m)
		{
			var helper = typeof(Meta).
				GetMethod(nameof(GetterGenericHelper), BindingFlags.Static | BindingFlags.NonPublic).
				MakeGenericMethod(m.DeclaringType, m.ReturnType);
			return (Func<object, object>)helper.Invoke(null, new object[] { m });
		}
#endif

		private struct ItemAttrs
		{
			private Attribute[] attrs;
			public Attribute Optional => attrs[0];
			public Attribute Required => attrs[1];
			public Attribute Member => attrs[2];
			public int Count;
			public Attribute Any() => Optional ?? Required ?? Member;

			public ItemAttrs(MemberInfo m, MetaOptions options, YuzuItemOptionality opt)
			{
				var attrTypes = new Type[] {
					options.OptionalAttribute,
					options.RequiredAttribute,
					options.MemberAttribute,
				};
				var over = options.GetItem(m);
				attrs = attrTypes.Select(t => over.Attr(t)).ToArray();
				Count = attrs.Count(a => a != null);
				if (Count == 0 && opt > 0 && attrTypes[(int)opt - 1] != null) {
					attrs[(int)opt - 1] = Activator.CreateInstance(attrTypes[(int)opt - 1]) as Attribute;
					Count = 1;
				}
			}
		}

		private bool IsNonEmptyCollection<T>(object obj, object value) =>
			value == null || ((ICollection<T>)value).Any();

		private bool IsNonEmptyCollectionConditional(object obj, object value, Meta collMeta)
		{
			if (value == null) return false;
			int index = 0;
			// Use non-generic IEnumerable to avoid boxing/unboxing.
			foreach (var i in (IEnumerable)value)
				if (collMeta.SerializeItemIf(value, index++, i)) return true;
			return false;
		}

		private bool IsEqualCollections<T>(object value, IEnumerable defColl) =>
			!Enumerable.SequenceEqual((IEnumerable<T>)value, (IEnumerable<T>)defColl);

		private Func<object, object, bool> GetSerializeIf(Item item, CommonOptions options)
		{
			if (Default == null)
				Default = Factory();
			var d = item.GetValue(Default);
			var icoll = Utils.GetICollection(item.Type);
			if (d == null || icoll == null)
				return (object obj, object value) => !object.Equals(value, d);
			var defColl = (IEnumerable)d;
			var collMeta = Get(item.Type, options);
			bool checkForEmpty = options.CheckForEmptyCollections && collMeta.SerializeItemIf != null;
			if (
				defColl.GetEnumerator().MoveNext() &&
				(!checkForEmpty || IsNonEmptyCollectionConditional(Default, defColl, collMeta))
			) {
				var m = Utils.GetPrivateCovariantGeneric(GetType(), nameof(IsEqualCollections), icoll);
				var eq = (Func<object, IEnumerable, bool>)Delegate.CreateDelegate(
					typeof(Func<object, IEnumerable, bool>), this, m);
				return (object obj, object value) => eq(value, defColl);
			}
			if (checkForEmpty)
				return (object obj, object value) => IsNonEmptyCollectionConditional(obj, value, collMeta);
			var mi = Utils.GetPrivateGeneric(
				GetType(), nameof(IsNonEmptyCollection), icoll.GetGenericArguments()[0]);
			return
				(Func<object, object, bool>)
				Delegate.CreateDelegate(typeof(Func<object, object, bool>), this, mi);
		}

		private void CheckCopyable(Type itemType, CommonOptions options)
		{
			var isCopyable = Utils.IsCopyable(itemType);
			if (isCopyable.HasValue) {
				if (!isCopyable.Value)
					IsCopyable = false;
			}
			else {
				if (Utils.IsStruct(itemType)) {
					var meta = Get(itemType, options);
					if (!meta.IsCopyable)
						IsCopyable = false;
				}
				else
					IsCopyable = false;
			}
		}

		private void AddItem(MemberInfo m, CommonOptions options, bool must, bool all)
		{
			var ia = new ItemAttrs(m, Options, all ? AllOptionality : YuzuItemOptionality.None);
			if (ia.Count == 0) {
				if (must)
					throw Error("Item {0} must be serialized", m.Name);
				return;
			}
			if (ia.Count != 1)
				throw Error("More than one of optional, required and member attributes for field '{0}'", m.Name);
			var attrs = Options.GetItem(m);
			var serializeIf = attrs.Attr(Options.SerializeIfAttribute);
			var item = new Item {
				Alias = Options.GetAlias(ia.Any()) ?? m.Name,
				IsOptional = ia.Required == null,
				IsCompact = attrs.HasAttr(Options.CompactAttribute),
				SerializeIf = serializeIf != null ? Options.GetSerializeCondition(serializeIf, Type) : null,
				Name = m.Name,
			};
			if (!item.IsOptional)
				RequiredCount += 1;
			var merge = attrs.HasAttr(Options.MergeAttribute);

			switch (m.MemberType) {
				case MemberTypes.Field:
					var f = m as FieldInfo;
					if (!f.IsPublic)
						throw Error("Non-public item '{0}'", f.Name);
					item.Type = f.FieldType;
					item.GetValue = f.GetValue;
					if (!merge)
						item.SetValue = f.SetValue;
					item.FieldInfo = f;
					break;
				case MemberTypes.Property:
					var p = m as PropertyInfo;
					var getter = p.GetGetMethod();
					if (getter == null)
						throw Error("No getter for item '{0}'", p.Name);
					item.Type = p.PropertyType;
					var setter = p.GetSetMethod();
#if iOS // Apple forbids code generation.
					item.GetValue = obj => p.GetValue(obj, Utils.ZeroObjects);
					if (!merge && setter != null)
						item.SetValue = (obj, value) => p.SetValue(obj, value, Utils.ZeroObjects);
#else
					if (Utils.IsStruct(Type)) {
						item.GetValue = obj => p.GetValue(obj, Utils.ZeroObjects);
						if (!merge && setter != null)
							item.SetValue = (obj, value) => p.SetValue(obj, value, Utils.ZeroObjects);
					} else {
						item.GetValue = BuildGetter(getter);
						if (!merge && setter != null)
							item.SetValue = BuildSetter(setter);
					}
#endif
					item.PropInfo = p;
					break;
				default:
					throw Error("Member type {0} not supported", m.MemberType);
			}
			if (item.SetValue == null) {
				if (!item.Type.IsClass && !item.Type.IsInterface || item.Type == typeof(object))
					throw Error("Unable to either set or merge item {0}", item.Name);
			}
			var over = Options.GetOverride(item.Type);
			if (over.HasAttr(Options.CompactAttribute))
				item.IsCompact = true;
			if (!over.HasAttr(Options.CopyableAttribute))
				CheckCopyable(item.Type, options);

			if (ia.Member != null && item.SerializeIf == null && !Type.IsAbstract && !Type.IsInterface)
				item.SerializeIf = GetSerializeIf(item, options);
			Items.Add(item);
		}

		private void AddMethod(MethodInfo m)
		{
			var attrs = Options.GetItem(m);
			if (attrs.HasAttr(Options.SerializeItemIfAttribute)) {
				if (SerializeItemIf != null)
					throw Error("Duplicate SerializeItemIf");
				if (Utils.GetIEnumerable(Type) == null)
					throw Error("SerializeItemIf may only be used inside of IEnumerable");
				SerializeItemIf = YuzuSerializeItemIf.MakeChecker(m);
			}
			BeforeSerialization.MaybeAdd(m, Options.BeforeSerializationAttribute);
			AfterSerialization.MaybeAdd(m, Options.AfterSerializationAttribute);
			BeforeDeserialization.MaybeAdd(m, Options.BeforeDeserializationAttribute);
			AfterDeserialization.MaybeAdd(m, Options.AfterDeserializationAttribute);

			if (attrs.HasAttr(Options.FactoryAttribute)) {
				if (FactoryMethod != null)
					throw Error("Duplicate Factory: '{0}' and '{1}'", FactoryMethod.Name, m.Name);
				if (!m.IsStatic || m.GetParameters().Length > 0)
					throw Error("Factory '{0}' must be a static method without parameters", m.Name);
				FactoryMethod = m;
				Factory = (Func<object>)Delegate.CreateDelegate(typeof(Func<object>), m);
			}

			Surrogate.ProcessMethod(m);
		}

		private void ExploreType(Type t, CommonOptions options)
		{
			const BindingFlags bindingFlags =
				BindingFlags.Static | BindingFlags.Instance |
				BindingFlags.Public | BindingFlags.NonPublic |
				BindingFlags.FlattenHierarchy;
			foreach (var m in t.GetMembers(bindingFlags)) {
				var attrs = Options.GetItem(m);
				if (attrs.HasAttr(Options.ExcludeAttribute))
					continue;
				switch (m.MemberType) {
					case MemberTypes.Field:
						var f = m as FieldInfo;
						if (f.FieldType == typeof(YuzuUnknownStorage)) {
							if (GetUnknownStorage != null)
								throw Error("Duplicated unknown storage in field {0}", m.Name);
							GetUnknownStorage = obj => (YuzuUnknownStorage)f.GetValue(obj);
						}
						else
							AddItem(m, options,
								Must.HasFlag(YuzuItemKind.Field) && f.IsPublic,
								AllKind.HasFlag(YuzuItemKind.Field) && f.IsPublic);
						break;
					case MemberTypes.Property:
						var p = m as PropertyInfo;
						var g = p.GetGetMethod();
						if (p.PropertyType == typeof(YuzuUnknownStorage)) {
							if (GetUnknownStorage != null)
								throw Error("Duplicated unknown storage in field {0}", m.Name);
#if iOS // Apple forbids code generation.
							GetUnknownStorage = obj => (YuzuUnknownStorage)p.GetValue(obj, Utils.ZeroObjects);
#else
							var getter = BuildGetter(g);
							GetUnknownStorage = obj => (YuzuUnknownStorage)getter(obj);
#endif
						}
						else
							AddItem(m, options,
								Must.HasFlag(YuzuItemKind.Property) && g != null,
								AllKind.HasFlag(YuzuItemKind.Property) && g != null);
						break;
					case MemberTypes.Method:
						AddMethod(m as MethodInfo);
						break;
				}
			}
		}

		private Meta(Type t)
		{
			Type = t;
			Options = MetaOptions.Default;
		}

		private static Func<CommonOptions, Dictionary<string, Type>> MakeReadAliases =
			CommonOptions => new Dictionary<string, Type>();

		private void CheckForNoFields(CommonOptions options)
		{
			if (Surrogate.SurrogateType != null)
				return;
			if (Utils.GetIEnumerable(Type) != null) {
				if (Items.Count > 0)
					throw Error("Serializable fields in collection are not supported");
			}
			else if (
				!options.AllowEmptyTypes && Items.Count == 0 && !(Type.IsInterface || Type.IsAbstract)
			)
				throw Error("No serializable fields");
		}

		public bool HasAnyTrigger() =>
			BeforeSerialization.Actions.Any() || AfterSerialization.Actions.Any() ||
			BeforeDeserialization.Actions.Any() || AfterDeserialization.Actions.Any();

		private Meta(Type t, CommonOptions options)
		{
			Type = t;
			Factory = defaultFactory;
			Options = options.Meta ?? MetaOptions.Default;
			IsCopyable = Utils.IsStruct(t);
			var over = Options.GetOverride(t);
			IsCompact = over.HasAttr(Options.CompactAttribute);
			var must = over.Attr(Options.MustAttribute);
			if (must != null)
				Must = Options.GetItemKind(must);
			var all = over.Attr(Options.AllAttribute);
			if (all != null) {
				var ok = Options.GetItemOptionalityAndKind(all);
				AllOptionality = ok.Item1;
				AllKind = ok.Item2;
			}

			Surrogate = new Surrogate(Type, Options);
			foreach (var i in t.GetInterfaces())
				ExploreType(i, options);
			ExploreType(t, options);
			Surrogate.Complete();
			CheckForNoFields(options);

			Items.Sort();
			Item prev = null;
			foreach (var i in Items) {
				if (prev != null && prev.CompareTo(i) == 0)
					throw Error("Duplicate item {0} / {1}", i.Name, i.Alias);
				prev = i;
			}
			var prevTag = "";
			foreach (var i in Items) {
				var tag = i.Tag(options);
				if (tag == "")
					throw Error("Empty tag for field '{0}'", i.Name);
				foreach (var ch in tag)
					if (ch <= ' ' || ch >= 127)
						throw Error("Bad character '{0}' in tag for field '{1}'", ch, i.Name);
				if (tag == prevTag)
					throw Error("Duplicate tag '{0}' for field '{1}'", tag, i.Name);
				prevTag = tag;
				TagToItem.Add(tag, i);
			}

			AllowReadingFromAncestor = over.HasAttr(Options.AllowReadingFromAncestorAttribute);
			if (AllowReadingFromAncestor) {
				var ancestorMeta = Get(t.BaseType, options);
				if (ancestorMeta.Items.Count != Items.Count)
					throw Error(
						"Allows reading from ancestor {0}, but has {1} items instead of {2}",
						t.BaseType.Name, Items.Count, ancestorMeta.Items.Count);
			}

			var alias = over.Attr(Options.AliasAttribute);
			if (alias != null) {
				var aliases = Options.GetReadAliases(alias);
				if (aliases != null) {
					Dictionary<string, Type> readAliases = readAliasCache.GetOrAdd(options, MakeReadAliases);
					foreach (var a in aliases) {
						if (String.IsNullOrWhiteSpace(a))
							throw Error("Empty read alias");
						Type duplicate;
						if (readAliases.TryGetValue(a, out duplicate))
							throw Error("Read alias '{0}' was already defined for '{1}'", a, duplicate.Name);
						readAliases.Add(a, t);
					}
				}
				WriteAlias = Options.GetWriteAlias(alias);
				if (WriteAlias != null && WriteAlias == "")
					throw Error("Empty write alias");
			}

			if (over.HasAttr(Options.CopyableAttribute))
				IsCopyable = true;
			else if (HasAnyTrigger())
				IsCopyable = false;
		}

		private static Func<Tuple<Type, CommonOptions>, Meta> MakeMeta = key => new Meta(key.Item1, key.Item2);
		public static Meta Get(Type t, CommonOptions options) =>
			cache.GetOrAdd(Tuple.Create(t, options), MakeMeta);

		public static Type GetTypeByReadAlias(string alias, CommonOptions options)
		{
			Dictionary<string, Type> readAliases;
			if (!readAliasCache.TryGetValue(options, out readAliases))
				return null;
			Type result;
			return readAliases.TryGetValue(alias, out result) ? result : null;
		}

		internal static Meta Unknown = new Meta(typeof(YuzuUnknown));

		private YuzuException Error(string format, params object[] args) =>
			new YuzuException("In type '" + Type.FullName + "': " + String.Format(format, args));

		private static bool HasItems(Type t, MetaOptions options)
		{
			const BindingFlags bindingFlags =
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
			var over = options.GetOverride(t);
			var all = over.Attr(options.AllAttribute);
			var k = all != null ? options.GetItemOptionalityAndKind(all).Item2 : YuzuItemKind.None;
			foreach (var m in t.GetMembers(bindingFlags)) {
				var attrs = over.Item(m);
				if (
					m.MemberType != MemberTypes.Field && m.MemberType != MemberTypes.Property ||
					attrs.HasAttr(options.ExcludeAttribute)
				)
					continue;
				if (
					k.HasFlag(YuzuItemKind.Field) && m.MemberType == MemberTypes.Field ||
					k.HasFlag(YuzuItemKind.Property) && m.MemberType == MemberTypes.Property ||
					new ItemAttrs(m, options, YuzuItemOptionality.None).Any() != null
				)
					return true;
			}
			return false;
		}

		public static List<Type> Collect(Assembly assembly, MetaOptions options = null)
		{
			var result = new List<Type>();
			var q = new Queue<Type>(assembly.GetTypes());
			while (q.Count > 0) {
				var t = q.Dequeue();
				if (HasItems(t, options ?? MetaOptions.Default) && !t.IsGenericTypeDefinition)
					result.Add(t);
				foreach (var nt in t.GetNestedTypes())
					q.Enqueue(nt);
			}
			return result;
		}

		public const int FoundNonPrimitive = -1;
		public int CountPrimitiveChildren(CommonOptions options)
		{
			int result = 0;
			foreach (var yi in Items) {
				if (yi.Type.IsPrimitive || yi.Type.IsEnum || yi.Type == typeof(string)) {
					result += 1;
				} else if (yi.IsCompact) {
					var c = Get(yi.Type, options).CountPrimitiveChildren(options);
					if (c == FoundNonPrimitive) return FoundNonPrimitive;
					result += c;
				} else
					return FoundNonPrimitive;
			}
			return result;
		}
	}

}
