using System;
using System.Reflection;

using Yuzu;
using Yuzu.Clone;

namespace YuzuGenClone
{
	public class ClonerGen: ClonerGenBase
	{
		private static global::YuzuTest.Sample1 Clone_YuzuTest__Sample1(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.Sample1();
			var s = (global::YuzuTest.Sample1)src;
			result.X = s.X;
			result.Y = s.Y;
			return result;
		}

		private static global::YuzuTest.Sample2 Clone_YuzuTest__Sample2(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.Sample2();
			var s = (global::YuzuTest.Sample2)src;
			result.X = s.X;
			result.Y = s.Y;
			return result;
		}

		private static global::YuzuTest.Sample3 Clone_YuzuTest__Sample3(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.Sample3();
			var s = (global::YuzuTest.Sample3)src;
			result.S1 = Clone_YuzuTest__Sample1(cl, s.S1);
			result.F = s.F;
			result.S2 = Clone_YuzuTest__Sample2(cl, s.S2);
			return result;
		}

		private static global::YuzuTest.SampleGenNoGen Clone_YuzuTest__SampleGenNoGen(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.SampleGenNoGen();
			var s = (global::YuzuTest.SampleGenNoGen)src;
			result.NG = cl.Deep(s.NG);
			return result;
		}

		private static global::YuzuTest.SampleArray Clone_YuzuTest__SampleArray(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.SampleArray();
			var s = (global::YuzuTest.SampleArray)src;
			if (s.A != null) {
				result.A = new string[s.A.Length];
				Array.Copy(s.A, result.A, s.A.Length);
			}
			return result;
		}

		private static global::YuzuTest.SampleArrayOfClass Clone_YuzuTest__SampleArrayOfClass(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.SampleArrayOfClass();
			var s = (global::YuzuTest.SampleArrayOfClass)src;
			if (s.A != null) {
				result.A = new global::YuzuTest.Sample1[s.A.Length];
				for(int tmp1 = 0; tmp1 < s.A.Length; ++tmp1)
					result.A[tmp1] = Clone_YuzuTest__Sample1(cl, s.A[tmp1]);
			}
			return result;
		}

		private static global::YuzuTest.SampleList Clone_YuzuTest__SampleList(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.SampleList();
			var s = (global::YuzuTest.SampleList)src;
			if (s.E != null) {
				result.E = new global::System.Collections.Generic.List<string>();
				foreach (var tmp1 in s.E)
					result.E.Add(tmp1);
			}
			return result;
		}

		private static global::YuzuTest.SampleDict Clone_YuzuTest__SampleDict(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.SampleDict();
			var s = (global::YuzuTest.SampleDict)src;
			result.Value = s.Value;
			if (s.Children != null) {
				result.Children = new global::System.Collections.Generic.Dictionary<string, global::YuzuTest.SampleDict>();
				foreach (var tmp1 in s.Children)
					result.Children.Add(tmp1.Key, Clone_YuzuTest__SampleDict(cl, tmp1.Value));
			}
			return result;
		}

		private static global::YuzuTest.SampleDictKeys Clone_YuzuTest__SampleDictKeys(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.SampleDictKeys();
			var s = (global::YuzuTest.SampleDictKeys)src;
			if (s.E != null) {
				result.E = new global::System.Collections.Generic.Dictionary<global::YuzuTest.SampleEnum, int>();
				foreach (var tmp1 in s.E)
					result.E.Add(tmp1.Key, tmp1.Value);
			}
			if (s.I != null) {
				result.I = new global::System.Collections.Generic.Dictionary<int, int>();
				foreach (var tmp2 in s.I)
					result.I.Add(tmp2.Key, tmp2.Value);
			}
			if (s.K != null) {
				result.K = new global::System.Collections.Generic.Dictionary<global::YuzuTest.SampleKey, int>();
				var tmp4 = cl.GetCloner<global::YuzuTest.SampleKey>();
				foreach (var tmp3 in s.K)
					result.K.Add((global::YuzuTest.SampleKey)tmp4(tmp3.Key), tmp3.Value);
			}
			return result;
		}

		private static global::YuzuTest.SamplePerson Clone_YuzuTest__SamplePerson(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.SamplePerson();
			var s = (global::YuzuTest.SamplePerson)src;
			result.Name = s.Name;
			result.Birth = s.Birth;
			if (s.Children != null) {
				result.Children = new global::System.Collections.Generic.List<global::YuzuTest.SamplePerson>();
				foreach (var tmp1 in s.Children)
					result.Children.Add(Clone_YuzuTest__SamplePerson(cl, tmp1));
			}
			result.EyeColor = Clone_YuzuTest__Color(cl, s.EyeColor);
			return result;
		}

		private static global::YuzuTest.Color Clone_YuzuTest__Color(Cloner cl, object src)
		{
			if (src == null) return null;
			var result = new global::YuzuTest.Color();
			var s = (global::YuzuTest.Color)src;
			result.B = s.B;
			result.G = s.G;
			result.R = s.R;
			return result;
		}

		static ClonerGen()
		{
			clonerCache[typeof(global::YuzuTest.Sample1)] = Clone_YuzuTest__Sample1;
			clonerCache[typeof(global::YuzuTest.Sample2)] = Clone_YuzuTest__Sample2;
			clonerCache[typeof(global::YuzuTest.Sample3)] = Clone_YuzuTest__Sample3;
			clonerCache[typeof(global::YuzuTest.SampleGenNoGen)] = Clone_YuzuTest__SampleGenNoGen;
			clonerCache[typeof(global::YuzuTest.SampleArray)] = Clone_YuzuTest__SampleArray;
			clonerCache[typeof(global::YuzuTest.SampleArrayOfClass)] = Clone_YuzuTest__SampleArrayOfClass;
			clonerCache[typeof(global::YuzuTest.SampleList)] = Clone_YuzuTest__SampleList;
			clonerCache[typeof(global::YuzuTest.SampleDict)] = Clone_YuzuTest__SampleDict;
			clonerCache[typeof(global::YuzuTest.SampleDictKeys)] = Clone_YuzuTest__SampleDictKeys;
			clonerCache[typeof(global::YuzuTest.SamplePerson)] = Clone_YuzuTest__SamplePerson;
			clonerCache[typeof(global::YuzuTest.Color)] = Clone_YuzuTest__Color;
		}
	}
}
