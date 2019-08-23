using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Yuzu;
using Yuzu.Binary;
using Yuzu.Clone;
using YuzuGenClone;

namespace YuzuTest
{
	public class BinaryCloner : AbstractCloner
	{
		private BinarySerializer bs = new BinarySerializer();
		private BinaryDeserializer bd = new BinaryDeserializer();

		public override object ShallowObject(object src) => throw new NotSupportedException();
		public override object DeepObject(object src)
		{
			bd.Options = Options;
			return bd.FromBytes(bs.ToBytes(src));
		}
		public override T Deep<T>(T src)
		{
			bd.Options = Options;
			return bd.FromBytes<T>(bs.ToBytes(src));
		}
	}

	[TestClass]
	public class TestClone
	{
		private void TestGen(
			Action<AbstractCloner> test,
			bool useCloner = true, bool useGen = true, bool useBinary = true)
		{
			if (useCloner)
				test(new Cloner());
			if (useGen)
				test(new ClonerGen());
			if (useBinary)
				test(new BinaryCloner());
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
			}, useBinary: false);
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
			}, useBinary: false);
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

		[TestMethod]
		public void TestSurrogate()
		{
			TestGen(cl => {
				var src = new SampleSurrogateColor { R = 123, G = 11, B = 77 };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.R, dst.R);
				Assert.AreEqual(src.G, dst.G);
				Assert.AreEqual(src.B, dst.B);
			});
			XAssert.Throws<YuzuException>(
				() => new Cloner().Deep(new SampleSurrogateColorIf()), "Both");
		}

		[TestMethod]
		public void TestCopyable()
		{
			TestGen(cl => {
				var src = new SampleWithCopyable { P = new SampleCopyable { X = 43 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.P, dst.P);
			}, useBinary: false);
			TestGen(cl => {
				cl.Options.Meta = new MetaOptions().AddOverride(typeof(SampleCopyable), o =>
					o.NegateAttr(typeof(YuzuCopyable))
				);
				var src = new SampleWithCopyable { P = new SampleCopyable { X = 43 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.P, dst.P);
				Assert.AreEqual(src.P.X, dst.P.X);
			}, useGen: false);
			TestGen(cl => {
				var src = new SampleWithCopyableItems {
					P = new Sample1 { X = 43 },
					L = new List<int> { 7, 8 }
				};
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.P, dst.P);
				Assert.AreEqual(src.L, dst.L);
			}, useBinary: false);
		}

		[TestMethod]
		public void TestMerge()
		{
			TestGen(cl => {
				var src = new SampleMerge();
				src.DI.Add(3, 4);
				src.LI.Add(33);
				src.M = new Sample1 { X = 768, Y = "ttt" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				CollectionAssert.AreEqual(src.DI, dst.DI);
				CollectionAssert.AreEqual(src.LI, dst.LI);
				Assert.AreNotEqual(src.M, dst.M);
				Assert.IsNull(dst.M);
			}, useBinary: false);
			TestGen(cl => {
				cl.Options.Meta = new MetaOptions().AddOverride(typeof(SampleMerge), o =>
					o.AddItem(nameof(SampleMerge.Make), i => i.AddAttr(new YuzuFactory()))
				);
				var src = new SampleMerge();
				src.DI.Add(3, 4);
				src.LI.Add(33);
				src.M = new Sample1 { X = 768, Y = "ttt" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				CollectionAssert.AreEqual(src.DI, dst.DI);
				CollectionAssert.AreEqual(src.LI, dst.LI);
				Assert.AreNotEqual(src.M, dst.M);
				// Generated cloner is not affected by meta override.
				if (cl is ClonerGenBase)
					Assert.IsNull(dst.M);
				else
					Assert.AreEqual(src.M.X, dst.M.X);
			});
			TestGen(cl => {
				var src = new SampleMergeNonPrimitive();
				src.DI.Add(3, new Sample1 { X = 13 });
				src.LI.Add(new Sample1 { X = 14 });
				src.M = new Sample1 { X = 15 };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.DI.Count, dst.DI.Count);
				Assert.AreEqual(src.DI[3].X, dst.DI[3].X);
				Assert.AreEqual(src.LI.Count, dst.LI.Count);
				Assert.AreEqual(src.LI[0].X, dst.LI[0].X);
				Assert.AreNotEqual(src.M, dst.M);
				Assert.AreEqual(src.M.X, dst.M.X);
			});
		}

		[TestMethod]
		public void TestInheritance()
		{
			TestGen(cl => {
				var src = new SampleDerivedB { FB = 99 };
				var dst = cl.Deep<SampleBase>(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.FB, ((SampleDerivedB)dst).FB);
			});
			TestGen(cl => {
				var src = new SampleSealed { FB = 99 };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.FB, dst.FB);
			});
			TestGen(cl => {
				cl.Options.AllowEmptyTypes = true;
				var src = new SampleEmptyDerivied { D = 98 };
				var dst = cl.Deep<Empty>(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.D, ((SampleEmptyDerivied)dst).D);
			});
			TestGen(cl => {
				var src = new SampleClassList {
					E = new List<SampleBase> {
						new SampleDerivedA(),
						new SampleDerivedB { FB = 9 },
						new SampleDerivedB { FB = 8 },
					}
				};
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreNotEqual(src.E, dst.E);
				Assert.AreEqual(src.E.Count, dst.E.Count);
				for (int i = 0; i < src.E.Count; ++i)
					Assert.AreNotEqual(src.E[i], dst.E[i]);
				Assert.IsInstanceOfType(dst.E[0], typeof(SampleDerivedA));
			});
		}

		[TestMethod]
		public void TestSerializeIf()
		{
			TestGen(cl => {
				var src = new Sample2 { X = 1, Y = "a" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual(src.Y, dst.Y);
			});
			TestGen(cl => {
				var src = new Sample2 { X = 1, Y = "1" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.X, dst.X);
				Assert.IsNull(dst.Y);
			});
			TestGen(cl => {
				var src = new SampleSerializeIf { X = 7, Y = new Sample1 { X = 7 } };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.X, dst.X);
				Assert.IsNull(dst.Y);
			});
			TestGen(cl => {
				var src = new Sample1 { X = 7, Y = "ttt" };
				var dst = cl.Deep(src);
				Assert.AreNotEqual(src, dst);
				Assert.AreEqual(src.X, dst.X);
				Assert.AreEqual("zzz", dst.Y);
			});
		}
		[TestMethod]
		public void TestSerializeItemIf()
		{
			TestGen(cl => {
				var src = new SampleCollection<int> { 5, 2, 4, 1 };

				var dst1 = cl.Deep(src);
				Assert.AreNotEqual(src, dst1);
				Assert.AreEqual(src.Count, dst1.Count);
				CollectionAssert.AreEqual(src.ToList(), dst1.ToList());

				src.Filter = 1;
				var dst2 = cl.Deep(src);
				CollectionAssert.AreEqual(new List<int> { 5, 4 }, dst2.ToList());

				src.Filter = 2;
				var dst3 = cl.Deep(src);
				CollectionAssert.AreEqual(new List<int> { 2, 4 }, dst3.ToList());

				src.Filter = 3;
				var dst4 = cl.Deep(src);
				Assert.AreEqual(0, dst4.Count);
			});
			TestGen(cl => {
				var src = new SampleCollection<Sample1>();
				foreach (var i in new List<int> { 5, 2, 4, 1 })
					src.Add(new Sample1 { X = i });

				var dst1 = cl.Deep(src);
				Assert.AreNotEqual(src, dst1);
				Assert.AreEqual(src.Count, dst1.Count);
				foreach (var t in src.Zip(dst1, Tuple.Create))
					Assert.AreEqual(t.Item1.X, t.Item2.X);

				src.Filter = 1;
				var dst2 = cl.Deep(src);
				Assert.AreEqual(2, dst2.Count);
				Assert.AreEqual(5, dst2.First().X);
				Assert.AreEqual(4, dst2.ElementAt(1).X);

				src.Filter = 3;
				var dst4 = cl.Deep(src);
				Assert.AreEqual(0, dst4.Count);
			});
			TestGen(cl => {
				var src = new SampleWithCollectionMerge();
				foreach (var i in new List<int> { 5, 2, 4, 1 })
					src.A.Add(i);

				var dst1 = cl.Deep(src);
				Assert.AreNotEqual(src, dst1);
				Assert.AreEqual(src.A.Count, dst1.A.Count);
				CollectionAssert.AreEqual(src.A.ToList(), dst1.A.ToList());

				src.A.Filter = 1;
				var dst2 = cl.Deep(src);
				CollectionAssert.AreEqual(new List<int> { 5, 4 }, dst2.A.ToList());

				src.A.Filter = 2;
				var dst3 = cl.Deep(src);
				CollectionAssert.AreEqual(new List<int> { 2, 4 }, dst3.A.ToList());

				src.A.Filter = 3;
				var dst4 = cl.Deep(src);
				Assert.AreEqual(0, dst4.A.Count);
			});
		}
	}
}
