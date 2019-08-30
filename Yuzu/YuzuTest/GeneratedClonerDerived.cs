using System;

using Yuzu.Clone;

namespace YuzuGenClone
{
	public class ClonerGenDerived: ClonerGen
	{
		private static global::YuzuTest.SampleClonerGenDerived Clone_YuzuTest__SampleClonerGenDerived(Cloner cl, object src)
		{
			if (src == null) return null;
			if (src.GetType() != typeof(global::YuzuTest.SampleClonerGenDerived))
				return (global::YuzuTest.SampleClonerGenDerived)cl.DeepObject(src);
			var s = (global::YuzuTest.SampleClonerGenDerived)src;
			var result = new global::YuzuTest.SampleClonerGenDerived();
			result.S = (global::YuzuTest.Sample1)cl.DeepObject(s.S);
			return result;
		}

		static ClonerGenDerived()
		{
			clonerCache[typeof(global::YuzuTest.SampleClonerGenDerived)] = Clone_YuzuTest__SampleClonerGenDerived;
		}
	}
}
