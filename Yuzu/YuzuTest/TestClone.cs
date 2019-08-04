using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Yuzu.Clone;

namespace YuzuTest
{
	[TestClass]
	public class TestClone
	{
		[TestMethod]
		public void TestShallow()
		{
			var cl = new Cloner();

			{
				var src = new Sample1 { X = 9, Y = "qwe" };
				var dst = (Sample1)cl.ShallowCloneObject(src);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual(src.Y, dst.Y);
			}
			{
				var src = new Sample2 { X = 19, Y = "qwe" };
				var dst = cl.ShallowClone(src);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual(src.Y, dst.Y);
			}
			{
				var src = new SampleDict {
					Value = 1, Children = new Dictionary<string, SampleDict> {
						{ "a", new SampleDict { Value = 2 } }
					}
				};
				var dst = cl.ShallowClone(src);
				Assert.AreEqual(src.Value, dst.Value);
				Assert.AreEqual(src.Children, dst.Children);
			}
		}
	}
}
