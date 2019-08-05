using System;
using System.Reflection;

using Yuzu;
using Yuzu.Clone;

namespace YuzuGenClone
{
	public class ClonerGen: ClonerGenBase
	{
		private static object Clone_YuzuTest__Sample1(Cloner cl, object src)
		{
			var result = new global::YuzuTest.Sample1();
			var s = (global::YuzuTest.Sample1)src;
			result.X = s.X;
			result.Y = s.Y;
			return result;
		}

		private static object Clone_YuzuTest__Sample2(Cloner cl, object src)
		{
			var result = new global::YuzuTest.Sample2();
			var s = (global::YuzuTest.Sample2)src;
			result.X = s.X;
			result.Y = s.Y;
			return result;
		}

		private static object Clone_YuzuTest__Sample3(Cloner cl, object src)
		{
			var result = new global::YuzuTest.Sample3();
			var s = (global::YuzuTest.Sample3)src;
			result.S1 = (global::YuzuTest.Sample1)Clone_YuzuTest__Sample1(cl, s.S1);
			result.F = s.F;
			result.S2 = (global::YuzuTest.Sample2)Clone_YuzuTest__Sample2(cl, s.S2);
			return result;
		}

		private static object Clone_YuzuTest__SampleArray(Cloner cl, object src)
		{
			var result = new global::YuzuTest.SampleArray();
			var s = (global::YuzuTest.SampleArray)src;
			result.A = cl.CloneArrayPrimitive<global::System.String>(s.A);
			return result;
		}

		private static object Clone_YuzuTest__SampleDict(Cloner cl, object src)
		{
			var result = new global::YuzuTest.SampleDict();
			var s = (global::YuzuTest.SampleDict)src;
			result.Value = s.Value;
			var tmp1 = cl.GetCloner(typeof(global::YuzuTest.SampleDict));
			result.Children = cl.CloneIDictionaryPrimiviteKey<global::System.Collections.Generic.Dictionary<global::System.String,global::YuzuTest.SampleDict>, global::System.String, global::YuzuTest.SampleDict>(s.Children, tmp1);
			return result;
		}

		private static object Clone_YuzuTest__Color(Cloner cl, object src)
		{
			var result = new global::YuzuTest.Color();
			var s = (global::YuzuTest.Color)src;
			result.B = s.B;
			result.G = s.G;
			result.R = s.R;
			return result;
		}

		private static object Clone_YuzuTest__SamplePerson(Cloner cl, object src)
		{
			var result = new global::YuzuTest.SamplePerson();
			var s = (global::YuzuTest.SamplePerson)src;
			result.Name = s.Name;
			result.Birth = s.Birth;
			var tmp1 = cl.GetCloner(typeof(global::YuzuTest.SamplePerson));
			result.Children = cl.CloneCollection<global::System.Collections.Generic.List<global::YuzuTest.SamplePerson>, global::YuzuTest.SamplePerson>(s.Children, tmp1);
			result.EyeColor = (global::YuzuTest.Color)Clone_YuzuTest__Color(cl, s.EyeColor);
			return result;
		}

		static ClonerGen()
		{
			clonerCache[typeof(global::YuzuTest.Sample1)] = Clone_YuzuTest__Sample1;
			clonerCache[typeof(global::YuzuTest.Sample2)] = Clone_YuzuTest__Sample2;
			clonerCache[typeof(global::YuzuTest.Sample3)] = Clone_YuzuTest__Sample3;
			clonerCache[typeof(global::YuzuTest.SampleArray)] = Clone_YuzuTest__SampleArray;
			clonerCache[typeof(global::YuzuTest.SampleDict)] = Clone_YuzuTest__SampleDict;
			clonerCache[typeof(global::YuzuTest.Color)] = Clone_YuzuTest__Color;
			clonerCache[typeof(global::YuzuTest.SamplePerson)] = Clone_YuzuTest__SamplePerson;
		}
	}
}
