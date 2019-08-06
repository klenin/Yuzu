using System;
using System.Collections.Generic;
using System.Reflection;

using Yuzu.Metadata;
using Yuzu.Util;

namespace Yuzu.Clone
{
	public class Cloner
	{
		public CommonOptions Options;
		public static Cloner Instance = new Cloner();

		private Dictionary<Type, Func<object, object>> clonerCache =
			new Dictionary<Type, Func<object, object>>();

		public Cloner() { }

		protected Cloner(IDictionary<Type, Func<Cloner, object, object>> initClonerCache)
		{
			foreach (var kv in initClonerCache)
				clonerCache.Add(kv.Key, src => kv.Value(this, src));
		}

		public T Shallow <T>(T src) => (T)ShallowObject(src);

		public object ShallowObject(object src)
		{
			var meta = Meta.Get(src.GetType(), Options);
			var result = meta.Factory();
			foreach (var item in meta.Items)
				item.SetValue(result, item.GetValue(src));
			return result;
		}

		public T Deep<T>(T obj) => (T)DeepObject(obj);

		public static bool IsCopyable(Type t) =>
			t.IsPrimitive || t.IsValueType || t == typeof(string);

		protected T[] CloneArray<T>(object src, Func<object, object> cloneElem)
		{
			if (src == null)
				return null;
			var a = (T[])src;
			var result = new T[a.Length];
			for (int i = 0; i < a.Length; ++i)
				result[i] = (T)cloneElem(a[i]);
			return result;
		}

		protected T[] CloneArrayPrimitive<T>(object src)
		{
			if (src == null)
				return null;
			var a = (T[])src;
			var result = new T[a.Length];
			Array.Copy(a, result, a.Length);
			return result;
		}

		protected I CloneIDictionary<I, K, V>(
			object src, Func<object, object> cloneKey, Func<object, object> cloneValue
		) where I : class, IDictionary<K, V>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var kv in (I)src)
				result.Add((K)cloneKey(kv.Key), (V)cloneValue(kv.Value));
			return result;
		}

		protected I CloneIDictionaryPrimiviteKey<I, K, V>(
			object src, Func<object, object> cloneValue
		) where I : class, IDictionary<K, V>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var kv in (I)src)
				result.Add(kv.Key, (V)cloneValue(kv.Value));
			return result;
		}

		protected I CloneIDictionaryPrimivite<I, K, V>(object src)
			where I : class, IDictionary<K, V>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var kv in (I)src)
				result.Add(kv);
			return result;
		}

		protected I CloneCollection<I, E>(object src, Func<object, object> cloneElem)
			where I : class, ICollection<E>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var item in (I)src)
				result.Add((E)cloneElem(item));
			return result;
		}

		protected I CloneCollectionPrimitive<I, E>(object src)
			where I : class, ICollection<E>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var item in (I)src)
				result.Add(item);
			return result;
		}

		public static object ValueCopy(object src) => src;
		private static object CloneNull(object src) => null;

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

		protected Func<object, object> MakeDelegateFunc(MethodInfo m) =>
			(Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), this, m);

		protected Func<object, TParam, object> MakeDelegateParam<TParam>(MethodInfo m) =>
			(Func<object, TParam, object>)Delegate.CreateDelegate(
				typeof(Func<object, TParam, object>), this, m);

		protected Func<object, TParam1, TParam2, object> MakeDelegateParam2<TParam1, TParam2>(MethodInfo m) =>
			(Func<object, TParam1, TParam2, object>)Delegate.CreateDelegate(
				typeof(Func<object, TParam1, TParam2, object>), this, m);

		private Func<object, object> MakeCloner(Type t)
		{
			if (IsCopyable(t)) return ValueCopy;
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
					var m = Utils.GetPrivateGeneric(GetType(), nameof(CloneArrayPrimitive), e);
					return MakeDelegateFunc(m);
				}
				else {
					var cloneElem = GetCloner(e);
					var m = Utils.GetPrivateGeneric(GetType(), nameof(CloneArray), e);
					var d = MakeDelegateParam<Func<object, object>>(m);
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
						var m = Utils.GetPrivateGeneric(
							GetType(), nameof(CloneIDictionary), t, a[0], a[1]);
						var d = MakeDelegateParam2<Func<object, object>, Func<object, object>>(m);
						return obj => d(obj, сk, сv);
					}
					else if (!IsCopyable(a[1])) {
						var сv = GetCloner(a[1]);
						var m = Utils.GetPrivateGeneric(
							GetType(), nameof(CloneIDictionaryPrimiviteKey), t, a[0], a[1]);
						var d = MakeDelegateParam<Func<object, object>>(m);
						return obj => d(obj, сv);
					}
					else {
						var m = Utils.GetPrivateGeneric(
							GetType(), nameof(CloneIDictionaryPrimivite), t, a[0], a[1]);
						return MakeDelegateFunc(m);
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
						var m = Utils.GetPrivateGeneric(GetType(), nameof(CloneCollection), t, a[0]);
						var d = MakeDelegateParam<Func<object, object>>(m);
						return obj => d(obj, сe);
					}
					else {
						var m = Utils.GetPrivateGeneric(
							GetType(), nameof(CloneCollectionPrimitive), t, a[0]);
						return MakeDelegateFunc(m);
					}
				}
			}
			if (t.IsClass || t.IsInterface || Utils.IsStruct(t)) {
				var meta = Meta.Get(t, Options);
				if (meta.Items.Count == 0)
					return CloneNull;
				var cloners = new Func<object, object>[meta.Items.Count];
				return src => {
					if (cloners[0] == null) {
						// Initialize 'cloners' lazily to prevent infinite recursion.
						int i = 0;
						foreach (var item in meta.Items)
							cloners[i++] = GetCloner(item.Type);
					}
					if (src == null)
						return null;
					var result = meta.Factory();
					int j = 0;
					foreach (var item in meta.Items)
						item.SetValue(result, cloners[j++](item.GetValue(src)));
					return result;
				};

			}
			throw new NotImplementedException("Unable to clone type: " + t.FullName);
		}

		public object DeepObject(object src) => GetCloner(src.GetType())(src);
	}
}
