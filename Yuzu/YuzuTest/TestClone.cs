using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Yuzu.Clone;
using YuzuGenClone;

namespace YuzuTest
{
	[TestClass]
	public class TestClone
	{
		private void TestGen(Action<Cloner> test)
		{
			test(new Cloner());
			test(new ClonerGen());
		}

		[TestMethod]
		public void TestShallow()
		{
			var cl = new Cloner();
			{
				var src = new Sample1 { X = 9, Y = "qwe" };
				var dst = (Sample1)cl.ShallowObject(src);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual(src.Y, dst.Y);
			}
			{
				var src = new Sample2 { X = 19, Y = "qwe" };
				var dst = cl.Shallow(src);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual(src.Y, dst.Y);
			}
			{
				var src = new SampleDict {
					Value = 1,
					Children = new Dictionary<string, SampleDict> {
						{ "a", new SampleDict { Value = 2 } }
					}
				};
				var dst = cl.Shallow(src);
				Assert.AreEqual(src.Value, dst.Value);
				Assert.AreEqual(src.Children, dst.Children);
			}
		}

		[TestMethod]
		public void TestBasic()
		{
			TestGen(cl => {
				var src = new Sample1 { X = 9, Y = "qwe" };
				var dst = (Sample1)cl.DeepObject(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual(src.Y, dst.Y);
			});
			TestGen(cl => {
				var src = new Sample2 { X = 19, Y = "qwe" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual(src.Y, dst.Y);
			});
			TestGen(cl => {
				var src = new Sample3 { S1 = new Sample1 { X = 19, Y = "qwe" } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.S1, dst.S1);
				Assert.AreEqual(src.S1.X, dst.S1.X);
				Assert.AreEqual(src.S1.Y, dst.S1.Y);
			});
			TestGen(cl => {
				var src = new SampleGenNoGen { NG = new SampleNoGen { Z = 11 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.NG, dst.NG);
				Assert.AreEqual(src.NG.Z, dst.NG.Z);
			});
		}

		[TestMethod]
		public void TestArray()
		{
			TestGen(cl => {
				var src = new int[5] { 2, 4, 5, 6, 8 };
				var dst = cl.Deep(src);
				CollectionAssert.AreEqual(src, dst);
			});
			TestGen(cl => {
				var src = new Sample1[] { new Sample1 { X = 33 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src[0], dst[0]);
				Assert.AreEqual(src[0].X, dst[0].X);
			});
			TestGen(cl => {
				var src = new SampleArray { A = null };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.IsNull(dst.A);
			});
			TestGen(cl => {
				var src = new SampleArrayOfClass { A = new Sample1[] { new Sample1 { X = 33 } } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.A[0], dst.A[0]);
				Assert.AreEqual(src.A[0].X, dst.A[0].X);
			});
		}

		[TestMethod]
		public void TestCollection()
		{

			TestGen(cl => {
				var src = new List<string> { "s1", "s2" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				CollectionAssert.AreEqual(src, dst);
			});
			TestGen(cl => {
				var src = new List<Sample1> { new Sample1 { X = 34 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.Count, dst.Count);
				Assert.AreNotEqual(src[0], dst[0]);
				Assert.AreEqual(src[0].X, dst[0].X);
			});
			TestGen(cl => {
				var src = new SampleList { E = new List<string> { "sq" } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.E.Count, dst.E.Count);
				Assert.AreNotEqual(src.E, dst.E);
				Assert.AreEqual(src.E[0], dst.E[0]);
			});
			TestGen(cl => {
				var src = new SampleMatrix {
					M = new List<List<int>> {
						new List<int>{ 1, 2, 3 },
						new List<int>{ 4 },
					}
				};
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.M, dst.M);
				Assert.AreNotEqual(src.M[0], dst.M[0]);
				CollectionAssert.AreEqual(src.M[0], dst.M[0]);
				CollectionAssert.AreEqual(src.M[1], dst.M[1]);
			});
			TestGen(cl => {
				var src = new SampleCollection<int> { 1, 5 };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				int[] srcA = new int[2], dstA = new int[2];
				src.CopyTo(srcA, 0);
				dst.CopyTo(dstA, 0);
				CollectionAssert.AreEqual(srcA, dstA);
			});
		}

		[TestMethod]
		public void TestDict()
		{
			TestGen(cl => {
				var src = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.Count, dst.Count);
				Assert.AreEqual(src["a"], dst["a"]);
				Assert.AreEqual(src["b"], dst["b"]);
			});
			TestGen(cl => {
				var src = new SampleDict {
					Value = 1,
					Children = new Dictionary<string, SampleDict> {
						{ "a", new SampleDict { Value = 2 } }
					}
				};
				var dst = cl.Deep(src);
				Assert.AreEqual(src.Value, dst.Value);
				Assert.AreNotEqual(src.Children, dst.Children);
				Assert.AreEqual(src.Children["a"].Value, dst.Children["a"].Value);
			});
			TestGen(cl => {
				var src = new SampleDictKeys {
					E = new Dictionary<SampleEnum, int> { { SampleEnum.E2, 6 } },
					I = null,
					K = new Dictionary<SampleKey, int> { { new SampleKey { V = 7 }, 8 } }
				};
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src.E, dst.E);
				Assert.AreNotEqual(src.K, dst.K);
				Assert.AreEqual(src.I, dst.I);
				Assert.AreEqual(src.E[SampleEnum.E2], dst.E[SampleEnum.E2]);
				Assert.AreEqual(src.K[new SampleKey { V = 7 }], dst.K[new SampleKey { V = 7 }]);
			});
		}

		[TestMethod]
		public void TestNullable()
		{
			TestGen(cl => {
				var src = new List<int?> { 3, null, 5 };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				CollectionAssert.AreEqual(src, dst);
			});
		}

		[TestMethod]
		public void TestObject()
		{
			TestGen(cl => {
				var src = new SampleObj { F = new int[] { 1 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.F, dst.F);
				CollectionAssert.AreEqual((int[])src.F, (int[])dst.F);
			});
			TestGen(cl => {
				var src = new SampleItemObj {
					L = new List<object> { new int[] { 1 }, 2, new Sample1() },
					D = new Dictionary<string, object> { { "abc", 5 } },
				};
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.L, dst.L);
				CollectionAssert.AreEqual((int[])src.L[0], (int[])dst.L[0]);
				Assert.AreEqual((int)src.L[1], (int)dst.L[1]);
				Assert.AreEqual(((Sample1)src.L[2]).X, ((Sample1)dst.L[2]).X);
				Assert.AreEqual((int)src.D["abc"], (int)dst.D["abc"]);
			});
		}

		[TestMethod]
		public void TestStruct()
		{
			TestGen(cl => {
				var src = new SamplePoint { X = 1, Y = 4 };
				var dst = cl.Deep(src);
				Assert.AreEqual(src, dst);
			});

			TestGen(cl => {
				var src = new SampleRect {
					A = new SamplePoint { X = 1, Y = 4 },
					B = new SamplePoint { X = 5, Y = 2 },
				};
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.A, dst.A);
				Assert.AreEqual(src.B, dst.B);
			});

			TestGen(cl => {
				var src = new SampleStructWithClass { A = new Sample1 { X = 14 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src.A, dst.A);
				Assert.AreEqual(src.A.X, dst.A.X);
			});
		}

		[TestMethod]
		public void TestFactory()
		{
			TestGen(cl => {
				var src = SamplePrivateConstructor.Make();
				src.X = 99;
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.X, dst.X);
			});
		}

		[TestMethod]
		public void TestBeforeAfter()
		{
			TestGen(cl => {
				var src = new SampleBeforeSerialization { X = "m" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual("m1", dst.X);
			});
			TestGen(cl => {
				var src = new SampleBefore2 { X = "m" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual("m231", dst.X);
			});
			TestGen(cl => {
				var src = new SampleAfterSerialization { X = "m" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual("m", dst.X);
				Assert.AreEqual("m1", src.X);
			});
			TestGen(cl => {
				var src = new SampleBeforeDeserialization();
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual("2X", dst.X);
				Assert.AreEqual("X", src.X);
			});
			TestGen(cl => {
				var src = new SampleAfterDeserialization { X = "m" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual("m1", dst.X);
				Assert.AreEqual("m", src.X);
			});
			TestGen(cl => {
				var src = new SampleAfter2 { X = "m" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual("m231", dst.X);
				Assert.AreEqual("m", src.X);
			});
		}
	}
}
