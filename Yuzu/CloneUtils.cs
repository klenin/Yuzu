using System;
using System.Collections.Generic;
using System.Reflection;

namespace Yuzu.CloneUtil
{
	internal static class CloneUtils
	{
		public static T MakeDelegate<T>(MethodInfo m) where T : Delegate =>
			(T)Delegate.CreateDelegate(typeof(T), null, m);

		public static MethodInfo GetGeneric(string name, params Type[] parameters) =>
			typeof(CloneUtils).GetMethod(name, BindingFlags.Static | BindingFlags.Public).
				MakeGenericMethod(parameters);

		public static T[] CloneArray<T>(object src, Func<object, object> cloneElem)
		{
			if (src == null)
				return null;
			var a = (T[])src;
			var result = new T[a.Length];
			for (int i = 0; i < a.Length; ++i)
				result[i] = (T)cloneElem(a[i]);
			return result;
		}

		public static Array CloneArrayNDim(object src, Func<object, object> cloneElem)
		{
			if (src == null)
				return null;
			var arr = (Array)src;
			var lbs = new int[arr.Rank];
			var ubs = new int[arr.Rank];
			var lengths = new int[arr.Rank];
			for (int dim = 0; dim < arr.Rank; ++dim) {
				lbs[dim] = arr.GetLowerBound(dim);
				ubs[dim] = arr.GetUpperBound(dim);
				lengths[dim] = arr.GetLength(dim);
			}

			var dst = Array.CreateInstance(src.GetType().GetElementType(), lengths, lbs);
			if (arr.Length == 0)
				return dst;

			var indices = (int[])lbs.Clone();
			for (int dim = arr.Rank - 1; ;) {
				dst.SetValue(cloneElem(arr.GetValue(indices)), indices);
				if (indices[dim] == ubs[dim]) {
					for (; dim >= 0 && indices[dim] == ubs[dim]; --dim)
						indices[dim] = lbs[dim];
					if (dim < 0)
						break;
					++indices[dim];
					dim = arr.Rank - 1;
				}
				else
					++indices[dim];
			}
			return dst;
		}

		public static T[] CloneArrayPrimitive<T>(object src) =>
			src == null ? null : (T[])((Array)src).Clone();

		public static Array CloneArrayPrimitiveNDim(object src) =>
			src == null ? null : (Array)((Array)src).Clone();

		public static I CloneIDictionary<I, K, V>(
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

		public static I CloneIDictionaryPrimiviteKey<I, K, V>(
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

		public static I CloneIDictionaryPrimivite<I, K, V>(object src)
			where I : class, IDictionary<K, V>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var kv in (I)src)
				result.Add(kv);
			return result;
		}

		public static void MergeIDictionary<I, K, V>(
			object dst, object src, Func<object, object> cloneKey, Func<object, object> cloneValue
		) where I : class, IDictionary<K, V>, new()
		{
			if (src == null || dst == null)
				return;
			foreach (var kv in (I)src)
				((I)dst).Add((K)cloneKey(kv.Key), (V)cloneValue(kv.Value));
		}

		public static void MergeIDictionaryPrimiviteKey<I, K, V>(
			object dst, object src, Func<object, object> cloneValue
		) where I : class, IDictionary<K, V>, new()
		{
			if (src == null || dst == null)
				return;
			foreach (var kv in (I)src)
				((I)dst).Add(kv.Key, (V)cloneValue(kv.Value));
		}

		public static void MergeIDictionaryPrimivite<I, K, V>(object dst, object src)
			where I : class, IDictionary<K, V>, new()
		{
			if (src == null || dst == null)
				return;
			foreach (var kv in (I)src)
				((I)dst).Add(kv);
		}

		public static I CloneCollection<I, E>(object src, Func<object, object> cloneElem)
			where I : class, ICollection<E>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var item in (I)src)
				result.Add((E)cloneElem(item));
			return result;
		}

		public static I CloneCollectionPrimitive<I, E>(object src)
			where I : class, ICollection<E>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			foreach (var item in (I)src)
				result.Add(item);
			return result;
		}

		public static I CloneCollectionIf<I, E>(
			object src, Func<object, object> cloneElem, Func<object, int, object, bool> cond
		) where I : class, ICollection<E>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			int index = 0;
			foreach (var item in (I)src)
				if (cond(src, index++, item))
					result.Add((E)cloneElem(item));
			return result;
		}

		public static I CloneCollectionPrimitiveIf<I, E>(object src, Func<object, int, object, bool> cond)
			where I : class, ICollection<E>, new()
		{
			if (src == null)
				return null;
			var result = new I();
			int index = 0;
			foreach (var item in (I)src)
				if (cond(src, index++, item))
					result.Add(item);
			return result;
		}

		public static void MergeCollection<I, E>(object dst, object src, Func<object, object> cloneElem)
			where I : class, ICollection<E>
		{
			if (src == null || dst == null)
				return;
			foreach (var item in (I)src)
				((I)dst).Add((E)cloneElem(item));
		}

		public static void MergeCollectionPrimitive<I, E>(object dst, object src)
			where I : class, ICollection<E>
		{
			if (src == null || dst == null)
				return;
			foreach (var item in (I)src)
				((I)dst).Add(item);
		}

		public static void MergeCollectionIf<I, E>(
			object dst, object src, Func<object, object> cloneElem, Func<object, int, object, bool> cond
		) where I : class, ICollection<E>
		{
			if (src == null || dst == null)
				return;
			int index = 0;
			foreach (var item in (I)src)
				if (cond(src, index++, item))
					((I)dst).Add((E)cloneElem(item));
		}

		public static void MergeCollectionPrimitiveIf<I, E>(
			object dst, object src, Func<object, int, object, bool> cond
		) where I : class, ICollection<E>
		{
			if (src == null || dst == null)
				return;
			int index = 0;
			foreach (var item in (I)src)
				if (cond(src, index++, item))
					((I)dst).Add(item);
		}

		public static object ValueCopy(object src) => src;
		public static object CloneNull(object src) => null;
	}
}
