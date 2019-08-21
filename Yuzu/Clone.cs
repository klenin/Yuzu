using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Yuzu.CloneUtil;
using Yuzu.Metadata;
using Yuzu.Util;

namespace Yuzu.Clone
{
	public abstract class AbstractCloner
	{
		public CommonOptions Options;

		public abstract object ShallowObject(object src);
		public virtual T Shallow<T>(T src) => (T)ShallowObject(src);

		public abstract object DeepObject(object src);
		public virtual T Deep<T>(T obj) => (T)DeepObject(obj);
	}

	public class Cloner : AbstractCloner
	{
		public static Cloner Instance = new Cloner();

		private Dictionary<Type, Func<object, object>> clonerCache =
			new Dictionary<Type, Func<object, object>>();
		private Dictionary<Type, Action<object, object>> mergerCache =
			new Dictionary<Type, Action<object, object>>();

		public Cloner() { }

		protected Cloner(IDictionary<Type, Func<Cloner, object, object>> initClonerCache)
		{
			foreach (var kv in initClonerCache)
				clonerCache.Add(kv.Key, src => kv.Value(this, src));
		}

		public override object ShallowObject(object src)
		{
			var meta = Meta.Get(src.GetType(), Options);
			meta.BeforeSerialization.Run(src);
			var result = meta.Factory();
			meta.BeforeDeserialization.Run(result);
			foreach (var item in meta.Items)
				item.SetValue(result, item.GetValue(src));
			meta.AfterSerialization.Run(src);
			meta.AfterDeserialization.Run(result);
			return result;
		}

		public override T Deep<T>(T obj) => (T)DeepObject(obj);

		public static bool IsCopyable(Type t, CommonOptions options) =>
			Utils.IsCopyable(t) ?? Meta.Get(t, options).IsCopyable;

		private bool IsCopyable(Type t) => IsCopyable(t, Options);
		private bool IsCopyable(Meta.Item yi) => yi.IsCopyable || IsCopyable(yi.Type, Options);

		public Func<object, object> GetCloner(Type t)
		{
			Func<object, object> cloner;
			if (!clonerCache.TryGetValue(t, out cloner)) {
				cloner = MakeCloner(t);
				clonerCache.Add(t, cloner);
			}
			return cloner;
		}
		public Func<object, object> GetCloner<T>() => GetCloner(typeof(T));

		public Action<object, object> GetMerger(Type t)
		{
			Action<object, object> merger;
			if (!mergerCache.TryGetValue(t, out merger)) {
				merger = MakeMerger(t);
				mergerCache.Add(t, merger);
			}
			return merger;
		}
		public Action<object, object> GetMerger<T>() => GetMerger(typeof(T));

		private Func<object, object> MakeSurrogateCloner(Meta meta)
		{
			var sg = meta.Surrogate;
			if (sg.SurrogateType == null)
				return null;
			if (sg.FuncFrom == null || sg.FuncTo == null)
				throw new YuzuException("Both FromSurrogate and ToSurrogate must be defined for cloning");
			var surrogateCloner = GetCloner(sg.SurrogateType);
			return src => sg.FuncFrom(surrogateCloner(sg.FuncTo(src)));
		}

		private void MakeFieldCloners(Action<object, object>[] cloners, Meta meta)
		{
			int i = 0;
			foreach (var yi in meta.Items) {
				if (IsCopyable(yi)) continue;
				if (yi.SetValue != null) {
					var cloner = GetCloner(yi.Type);
					cloners[i++] = (dst, src) => yi.SetValue(dst, cloner(yi.GetValue(src)));
				}
				else {
					var merger = GetMerger(yi.Type);
					cloners[i++] = (dst, src) => merger(yi.GetValue(dst), yi.GetValue(src));
				}
			}
		}

		private Func<object, object> MakeCloner(Type t)
		{
			if (IsCopyable(t)) return CloneUtils.ValueCopy;
			if (t.IsGenericType) {
				var g = t.GetGenericTypeDefinition();
				if (g == typeof(Nullable<>)) {
					var c = GetCloner(t.GetGenericArguments()[0]);
					return src => src ?? c(src);
				}
			}
			if (t.IsArray) {
				var e = t.GetElementType();
				if (IsCopyable(e)) {
					var m = CloneUtils.GetGeneric(nameof(CloneUtils.CloneArrayPrimitive), e);
					return CloneUtils.MakeDelegate<Func<object, object>>(m);
				}
				else {
					var cloneElem = GetCloner(e);
					var m = CloneUtils.GetGeneric(nameof(CloneUtils.CloneArray), e);
					var d = CloneUtils.MakeDelegate<Func<object, Func<object, object>, object>>(m);
					return src => d(src, cloneElem);
				}
			}
			{
				var idict = Utils.GetIDictionary(t);
				if (idict != null) {
					var a = idict.GetGenericArguments();
					if (!IsCopyable(a[0])) {
						var сk = GetCloner(a[0]);
						var сv = GetCloner(a[1]);
						var m = CloneUtils.GetGeneric(nameof(CloneUtils.CloneIDictionary), t, a[0], a[1]);
						var d = CloneUtils.MakeDelegate<
							Func<object, Func<object, object>, Func<object, object>, object>>(m);
						return obj => d(obj, сk, сv);
					}
					else if (!IsCopyable(a[1])) {
						var сv = GetCloner(a[1]);
						var m = CloneUtils.GetGeneric(
							nameof(CloneUtils.CloneIDictionaryPrimiviteKey), t, a[0], a[1]);
						var d = CloneUtils.MakeDelegate<Func<object, Func<object, object>, object>>(m);
						return obj => d(obj, сv);
					}
					else {
						var m = CloneUtils.GetGeneric(
							nameof(CloneUtils.CloneIDictionaryPrimivite), t, a[0], a[1]);
						return CloneUtils.MakeDelegate<Func<object, object>>(m);
					}
				}
			}
			if (t == typeof(object))
				return DeepObject;
			{
				var icoll = Utils.GetICollection(t);
				if (icoll != null) {
					var a = icoll.GetGenericArguments();
					if (!IsCopyable(a[0])) {
						var сe = GetCloner(a[0]);
						var m = CloneUtils.GetGeneric(nameof(CloneUtils.CloneCollection), t, a[0]);
						var d = CloneUtils.MakeDelegate<Func<object, Func<object, object>, object>>(m);
						return obj => d(obj, сe);
					}
					else {
						var m = CloneUtils.GetGeneric(nameof(CloneUtils.CloneCollectionPrimitive), t, a[0]);
						return CloneUtils.MakeDelegate<Func<object, object>>(m);
					}
				}
			}
			if (t.IsClass || t.IsInterface || Utils.IsStruct(t)) {
				var meta = Meta.Get(t, Options);
				var surrogateCloner = MakeSurrogateCloner(meta);
				if (surrogateCloner != null)
					return surrogateCloner;
				if (meta.Items.Count == 0)
					return src => meta.Factory();
				var copyable = meta.Items.Where(IsCopyable).ToList();
				// Initialize 'cloners' lazily to prevent infinite recursion.
				var cloners = new Action<object, object>[meta.Items.Count - copyable.Count];
				// Duplicate code to optimize fast path.
				if (!meta.HasAnyTrigger() && cloners.Length == 0) {
					return src => {
						if (src == null)
							return null;
						if (src.GetType() != t)
							return DeepObject(src);
						var result = meta.Factory();
						foreach (var yi in copyable)
							yi.SetValue(result, yi.GetValue(src));
						return result;
					};
				}
				if (meta.HasAnyTrigger()) {
					return src => {
						if (src == null)
							return null;
						if (src.GetType() != t)
							return DeepObject(src);
						meta.BeforeSerialization.Run(src);
						var result = meta.Factory();
						if (cloners.Length > 0 && cloners[0] == null)
							MakeFieldCloners(cloners, meta);
						meta.BeforeDeserialization.Run(result);
						foreach (var yi in copyable)
							yi.SetValue(result, yi.GetValue(src));
						foreach (var cloner in cloners)
							cloner(result, src);
						meta.AfterSerialization.Run(src);
						meta.AfterDeserialization.Run(result);
						return result;
					};
				}
				else {
					return src => {
						if (src == null)
							return null;
						if (src.GetType() != t)
							return DeepObject(src);
						var result = meta.Factory();
						if (cloners[0] == null)
							MakeFieldCloners(cloners, meta);
						foreach (var yi in copyable)
							yi.SetValue(result, yi.GetValue(src));
						foreach (var cloner in cloners)
							cloner(result, src);
						return result;
					};
				}
			}
			throw new NotImplementedException("Unable to clone type: " + t.FullName);
		}

		private Action<object, object> MakeMerger(Type t)
		{
			{
				var idict = Utils.GetIDictionary(t);
				if (idict != null) {
					var a = idict.GetGenericArguments();
					if (!IsCopyable(a[0])) {
						var сk = GetCloner(a[0]);
						var сv = GetCloner(a[1]);
						var m = CloneUtils.GetGeneric(nameof(CloneUtils.MergeIDictionary), t, a[0], a[1]);
						var d = CloneUtils.MakeDelegate<
							Action<object, object, Func<object, object>, Func<object, object>>>(m);
						return (dst, src) => d(dst, src, сk, сv);
					}
					else if (!IsCopyable(a[1])) {
						var сv = GetCloner(a[1]);
						var m = CloneUtils.GetGeneric(
							nameof(CloneUtils.MergeIDictionaryPrimiviteKey), t, a[0], a[1]);
						var d = CloneUtils.MakeDelegate<Action<object, object, Func<object, object>>>(m);
						return (dst, src) => d(dst, src, сv);
					}
					else {
						var m = CloneUtils.GetGeneric(
							nameof(CloneUtils.MergeIDictionaryPrimivite), t, a[0], a[1]);
						return CloneUtils.MakeDelegate<Action<object, object>>(m);
					}
				}
			}
			{
				var icoll = Utils.GetICollection(t);
				if (icoll != null) {
					var a = icoll.GetGenericArguments();
					if (!IsCopyable(a[0])) {
						var сe = GetCloner(a[0]);
						var m = CloneUtils.GetGeneric(nameof(CloneUtils.MergeCollection), t, a[0]);
						var d = CloneUtils.MakeDelegate<Action<object, object, Func<object, object>>>(m);
						return (dst, src) => d(dst, src, сe);
					}
					else {
						var m = CloneUtils.GetGeneric(
							nameof(CloneUtils.MergeCollectionPrimitive), t, a[0]);
						return CloneUtils.MakeDelegate<Action<object, object>>(m);
					}
				}
			}
			if (t.IsClass || t.IsInterface || Utils.IsStruct(t)) {
				var meta = Meta.Get(t, Options);
				if (meta.Items.Count == 0)
					return (dst, src) => {};
				var copyable = meta.Items.Where(IsCopyable).ToList();
				// Initialize 'cloners' lazily to prevent infinite recursion.
				var cloners = new Action<object, object>[meta.Items.Count - copyable.Count];
				// Duplicate code to optimize fast path.
				if (!meta.HasAnyTrigger() && cloners.Length == 0) {
					return (dst, src) => {
						if (src == null || dst == null)
							return;
						foreach (var yi in copyable)
							yi.SetValue(dst, yi.GetValue(src));
					};
				}
				if (meta.HasAnyTrigger()) {
					return (dst, src) => {
						if (src == null || dst == null)
							return;
						meta.BeforeSerialization.Run(src);
						var result = meta.Factory();
						if (cloners.Length > 0 && cloners[0] == null)
							MakeFieldCloners(cloners, meta);
						meta.BeforeDeserialization.Run(result);
						foreach (var yi in copyable)
							yi.SetValue(result, yi.GetValue(src));
						foreach (var cloner in cloners)
							cloner(result, src);
						meta.AfterSerialization.Run(src);
						meta.AfterDeserialization.Run(result);
					};
				}
				else {
					return (dst, src) => {
						if (src == null || dst == null)
							return;
						if (cloners[0] == null)
							MakeFieldCloners(cloners, meta);
						foreach (var yi in copyable)
							yi.SetValue(dst, yi.GetValue(src));
						foreach (var cloner in cloners)
							cloner(dst, src);
					};
				}
			}
			throw new NotImplementedException("Unable to merge type: " + t.FullName);
		}

		public override object DeepObject(object src) => GetCloner(src.GetType())(src);
	}
}
