﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Yuzu;
using Yuzu.Binary;
using Yuzu.Metadata;
using Yuzu.Unsafe;
using YuzuGenBin;
using YuzuTestAssembly;

namespace YuzuTest.Binary
{
	[TestClass]
	public class TestBinary
	{
		private string VarintXS(long value)
		{
			string result = "";
			do {
				byte b = (byte)(value & 0x7f);
				value >>= 7;
				if (value != 0) {
					b |= 0x80;
				}
				result += b.ToString("X2") + " ";
			} while (value != 0);
			return result;
		}

		private string XS(IEnumerable<byte> bytes) =>
			String.Join(" ", bytes.Select(b => b.ToString("X2")));

		private byte[] SX(string s) =>
			s.Split(' ').Select(p => Byte.Parse(p, NumberStyles.AllowHexSpecifier)).ToArray();

		private string XS(string s) =>
			VarintXS(s.Length) + XS(s.ToCharArray().Select(ch => (byte)ch));

		private string XS(Type t) =>
			XS(t.FullName + ", YuzuTest");

		private string XS(RoughType rt) =>
			((byte)rt).ToString("X2");

		private string XS(params string[] s) =>
			String.Join(" ", s.Select(XS));

		private string XS(string s, RoughType rt) =>
			XS(s) + " " + XS(rt);

		private string XS(string s1, RoughType rt1, string s2, RoughType rt2) =>
			XS(s1, rt1) + " " + XS(s2, rt2);

		private string XS(string s1, RoughType rt1, string s2, RoughType rt2, string s3, RoughType rt3) =>
			XS(s1, rt1) + " " + XS(s2, rt2) + " " + XS(s3, rt3);

		private void CheckDeserializers(Action<BinaryDeserializer> a)
		{
			a(new BinaryDeserializer());
			a(new BinaryDeserializerGen());
		}

		[TestMethod]
		public void TestXS()
		{
			Assert.AreEqual("01 FF", XS(new byte[] { 1, 255 }));
			Assert.AreEqual("02 41 42", XS("AB"));
			Assert.AreEqual("20", XS(RoughType.Record));
		}

		[TestMethod]
		public void TestSimple()
		{
			var bs = new BinarySerializer();
			bs.Options.AllowEmptyTypes = true;
			Assert.AreEqual(
				XS(RoughType.Record) + " 01 00 " + XS(typeof(Empty)) + " 00 00 00 00",
				XS(bs.ToBytes(new Empty())));

			var v1 = new Sample1 { X = 345, Y = "test" };

			bs.ClearClassIds();
			var result = bs.ToBytes(v1);
			Assert.AreEqual(
				XS(RoughType.Record) + " 01 00 " + XS(typeof(Sample1)) + " 02 00 " +
				XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 59 01 00 00 02 00 " + XS("test") + " 00 00", XS(result));
			Sample1 v2 = new Sample1();

			var bd = new BinaryDeserializer();
			bd.FromBytes(v2, result);
			Assert.AreEqual(v1.X, v2.X);
			Assert.AreEqual(v1.Y, v2.Y);

			bd.FromBytes(v2, new byte[] { 0x20, 01, 00, 01, 00, 0xE7, 03, 00, 00, 00, 00 });
			Assert.AreEqual(999, v2.X);
			Assert.AreEqual(v1.Y, v2.Y);

			v1.X = int.MaxValue;
			bd.FromBytes(v2, bs.ToBytes(v1));
			Assert.AreEqual(v1.X, v2.X);

			v1.X = int.MaxValue;
			bd.FromBytes(v2, bs.ToBytes(v1));
			Assert.AreEqual(v1.X, v2.X);
		}

		[TestMethod]
		public void TestLong()
		{
			var bs = new BinarySerializer();
			var v1 = new SampleLong { S = -1L << 33, U = 1UL << 33 };

			var result = bs.ToBytes(v1);
			Assert.AreEqual(
				XS(RoughType.Record) + " 01 00 " + XS(typeof(SampleLong)) + " 02 00 " +
				XS("S", RoughType.Long, "U", RoughType.ULong) +
				" 01 00 00 00 00 00 FE FF FF FF 02 00 00 00 00 00 02 00 00 00 00 00", XS(result));

			var v2 = new SampleLong();
			var bd = new BinaryDeserializer();
			bd.FromBytes(v2, result);
			Assert.AreEqual(v1.S, v2.S);
			Assert.AreEqual(v1.U, v2.U);

			v1.S = long.MinValue;
			v1.U = ulong.MaxValue;
			bd.FromBytes(v2, bs.ToBytes(v1));
			Assert.AreEqual(v1.S, v2.S);
			Assert.AreEqual(v1.U, v2.U);
		}

		[TestMethod]
		public void TestSmallTypes()
		{
			var bs = new BinarySerializer();
			var v1 = new SampleSmallTypes { Ch = 'A', Sh = -2000, USh = 2001, B = 198, Sb = -109 };

			var result = bs.ToBytes(v1);
			Assert.AreEqual(
				XS(RoughType.Record) + " 01 00 " + XS(typeof(SampleSmallTypes)) +
				" 05 00 " + XS("B", RoughType.Byte, "Ch", RoughType.Char) + " " +
				XS("Sb", RoughType.SByte, "Sh", RoughType.Short, "USh", RoughType.UShort) +
				" 01 00 C6 02 00 41 03 00 93 04 00 30 F8 05 00 D1 07 00 00", XS(result));

			var v2 = new SampleSmallTypes();
			var bd = new BinaryDeserializer();
			bd.FromBytes(v2, result);
			Assert.AreEqual(v1.Ch, v2.Ch);
			Assert.AreEqual(v1.USh, v2.USh);
			Assert.AreEqual(v1.Sh, v2.Sh);
			Assert.AreEqual(v1.B, v2.B);
			Assert.AreEqual(v1.Sb, v2.Sb);

			v2 = (SampleSmallTypes)((new BinaryDeserializerGen()).FromBytes(result));
			Assert.AreEqual(v1.Ch, v2.Ch);
			Assert.AreEqual(v1.USh, v2.USh);
			Assert.AreEqual(v1.Sh, v2.Sh);
			Assert.AreEqual(v1.B, v2.B);
			Assert.AreEqual(v1.Sb, v2.Sb);


			bd.FromBytes(v2, new byte[] {
				0x20, 01, 00, 01, 00, 255, 02, 00, 65 + 25, 03, 00, 256 - 128,
				04, 00, 00, 128, 05, 00, 255, 127, 00, 00 });
			Assert.AreEqual('Z', v2.Ch);
			Assert.AreEqual(32767, v2.USh);
			Assert.AreEqual(-32768, v2.Sh);
			Assert.AreEqual(255, v2.B);
			Assert.AreEqual(-128, v2.Sb);
		}

		[TestMethod]
		public void TestNested()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;

			var v = new Sample3 {
				S1 = new Sample1 { X = 345, Y = "test" },
				F = 222,
				S2 = new Sample2 { X = -346, Y = "test1" },
			};

			var result = bs.ToBytes(v);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(Sample3)) + " 03 00 " +
				XS("S1", RoughType.Record, "F", RoughType.Int, "S2", RoughType.Record) +
				" 01 00 02 00 " + XS(typeof(Sample1)) +
				" 02 00 " + XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 59 01 00 00 02 00 " + XS("test") + " 00 00 " +
				"02 00 DE 00 00 00 " +
				"03 00 03 00 " + XS(typeof(Sample2)) +
				" 02 00 " + XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 A6 FE FF FF 02 00 " + XS("test1") + " 00 00 00 00",
				XS(result));

			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Names;
			var w = new Sample3();
			bd.FromBytes(w, result);
			Assert.AreEqual(v.S1.X, w.S1.X);
			Assert.AreEqual(v.S1.Y, w.S1.Y);
			Assert.AreEqual(v.F, w.F);
			Assert.AreEqual(v.S2.X, w.S2.X);
			Assert.AreEqual(v.S2.Y, w.S2.Y);
		}

		[TestMethod]
		public void TestGenerated()
		{
			var str =
				"20 01 00 " + XS(typeof(Sample3)) + " 03 00 " +
				XS("S1", RoughType.Record, "F", RoughType.Int, "S2", RoughType.Record) +
				" 01 00 02 00 " + XS(typeof(Sample1)) +
				" 02 00 " + XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 59 01 00 00 02 00 " + XS("test") + " 00 00 " +
				"02 00 DE 00 00 00 " +
				"03 00 03 00 " + XS(typeof(Sample2)) +
				" 02 00 " + XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 A6 FE FF FF 02 00 " + XS("test1") + " 00 00 00 00";

			var bd = new BinaryDeserializerGen();
			bd.Options.TagMode = TagMode.Names;
			var w = (Sample3)bd.FromBytes(SX(str));
			Assert.AreEqual(345, w.S1.X);
			Assert.AreEqual("test", w.S1.Y);
			Assert.AreEqual(222, w.F);
			Assert.AreEqual(-346, w.S2.X);
			Assert.AreEqual("test1", w.S2.Y);

			var w1 = new Sample1();
			bd.FromBytes(w1, SX("20 02 00 01 00 58 00 00 00 00 00"));
			Assert.IsInstanceOfType(w1, typeof(Sample1));
			Assert.AreEqual(88, w1.X);

			var w2 = bd.FromBytes(SX("20 02 00 01 00 63 00 00 00 00 00"));
			Assert.IsInstanceOfType(w2, typeof(Sample1));
			Assert.AreEqual(99, ((Sample1)w2).X);

			var w3 = new SampleMemberI();
			bd.FromBytes(w3, SX(
				"20 04 00 " + XS(typeof(SampleMemberI)) + " 01 00 " + XS("X", RoughType.Int) + " 00 00"));
			Assert.AreEqual(71, ((SampleMemberI)w3).X);
		}

		[TestMethod]
		public void TestGeneratedDerived()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializerGenDerived();
			var v = new SampleMergeNonPrimitive { M = new Sample1 { X = 33 } };
			var w = bd.FromBytes<SampleMergeNonPrimitive>(bs.ToBytes(v));
			Assert.AreEqual(v.M.X, w.M.X);
		}

		[TestMethod]
		public void TestEnum()
		{
			var bs = new BinarySerializer();

			var v = new Sample4 { E = SampleEnum.E3 };

			var result1 = bs.ToBytes(v);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(Sample4)) + " 01 00 " + XS("E", RoughType.Int) +
				" 01 00 02 00 00 00 00 00",
				XS(result1));

			CheckDeserializers(bd => {
				var w = new Sample4();
				bd.FromBytes(w, result1);
				Assert.AreEqual(SampleEnum.E3, w.E);
			});

			var vb = new SampleEnumMemberTyped { Eb = SampleEnumByte.EB3, El = SampleEnumLong.Large };

			var result1b = (new BinarySerializer()).ToBytes(vb);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleEnumMemberTyped)) +
				" 02 00 " + XS("Eb", RoughType.Byte, "El", RoughType.Long) +
				" 01 00 02 02 00 00 00 00 00 00 00 04 00 00 00",
				XS(result1b));

			CheckDeserializers(bd => {
				var w = new SampleEnumMemberTyped();
				bd.FromBytes(w, result1b);
				Assert.AreEqual(vb.Eb, w.Eb);
				Assert.AreEqual(vb.El, w.El);
			});
		}

		[TestMethod]
		public void TestBool()
		{
			var bs = new BinarySerializer();

			var v = new SampleBool { B = true };

			var result1 = bs.ToBytes(v);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleBool)) +
				" 01 00 " + XS("B", RoughType.Bool) + " 01 00 01 00 00",
				XS(result1));

			var bd = new BinaryDeserializer();
			var w = new SampleBool();
			bd.FromBytes(w, result1);
			Assert.AreEqual(true, w.B);
		}

		[TestMethod]
		public void TestFloat()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;

			var v = new SampleFloat { F = 1e-20f, D = -3.1415e100d };

			var result1 = bs.ToBytes(v);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleFloat)) + " 02 00 " +
				XS("F", RoughType.Float, "D", RoughType.Double) +
				" 01 00 08 E5 3C 1E 02 00 CA DC 09 3E BE B9 CC D4 00 00",
				XS(result1));

			var w = new SampleFloat();
			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Names;
			bd.FromBytes(w, result1);
			Assert.AreEqual(v.F, w.F);
			Assert.AreEqual(v.D, w.D);
		}

		[TestMethod]
		public void TestFloatInfNan()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;
			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Names;

			var v1 = new SampleFloat { F = float.NaN, D = double.NaN };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleFloat)) + " 02 00 " +
				XS("F", RoughType.Float, "D", RoughType.Double) +
				" 01 00 00 00 C0 FF 02 00 00 00 00 00 00 00 F8 FF 00 00",
				XS(result1));
			var w1 = bd.FromBytes<SampleFloat>(result1);
			Assert.IsTrue(float.IsNaN(w1.F));
			Assert.IsTrue(double.IsNaN(w1.D));

			var v2 = new SampleFloat { F = float.PositiveInfinity, D = double.NegativeInfinity };
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual("20 01 00 01 00 00 00 80 7F 02 00 00 00 00 00 00 00 F0 FF 00 00", XS(result2));
			var w2 = bd.FromBytes<SampleFloat>(result2);
			Assert.IsTrue(float.IsPositiveInfinity(w2.F));
			Assert.IsTrue(double.IsNegativeInfinity(w2.D));
		}

		[TestMethod]
		public void TestDecimal()
		{
			var bs = new BinarySerializer();

			var v = new SampleDecimal { N = -12.34m };

			var result1 = bs.ToBytes(v);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleDecimal)) + " 01 00 " +
				XS("N", RoughType.Decimal) +
				" 01 00" + " D2 04 00 00 00 00 00 00 00 00 00 00" + " 00 00 02 80" + " 00 00",
				XS(result1));

			CheckDeserializers(bd => {
				var w = bd.FromBytes<SampleDecimal>(result1);
				Assert.AreEqual(v.N, w.N);
			});
		}

		[TestMethod]
		public void TestNullable()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var bdg = new BinaryDeserializerGen();

			var v1 = new SampleNullable { N = null };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleNullable)) + " 01 00 " + XS("N", RoughType.Nullable) + " 05 " +
				"01 00 01 00 00",
				XS(result1));
			var w1 = bd.FromBytes<SampleNullable>(result1);
			Assert.AreEqual(v1.N, w1.N);
			var w1g = bdg.FromBytes<SampleNullable>(result1);
			Assert.AreEqual(v1.N, w1g.N);

			var v2 = new SampleNullable { N = 997 };
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"20 01 00 01 00 00 E5 03 00 00 00 00",
				XS(result2));
			var w2 = bd.FromBytes<SampleNullable>(result2);
			Assert.AreEqual(v2.N, w2.N);
			var w2g = bdg.FromBytes<SampleNullable>(result2);
			Assert.AreEqual(v2.N, w2g.N);

			var v3 = new List<SamplePoint?> { new SamplePoint { X = -1, Y = -2 }, null };
			var result3 = bs.ToBytes(v3);
			Assert.AreEqual(
				"21 12 20 02 00 00 00 00 02 00 " + XS(typeof(SamplePoint)) + " 02 00 " +
				XS("X", RoughType.Int, "Y", RoughType.Int) + " FF FF FF FF FE FF FF FF 01",
				XS(result3));
			var w3 = bd.FromBytes<List<SamplePoint?>>(result3);
			Assert.AreEqual(v3.Count, w3.Count);
			Assert.AreEqual(v3[0].Value.X, w3[0].Value.X);
			Assert.AreEqual(v3[0].Value.Y, w3[0].Value.Y);
			Assert.IsNull(w3[1]);
		}

		[TestMethod]
		public void TestMemberOrder()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;
			var result = bs.ToBytes(new SampleMethodOrder());
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleMethodOrder)) + " 04 00 " +
				XS("F1", RoughType.Int, "P1", RoughType.Int) + " " +
				XS("F2", RoughType.Int, "P2", RoughType.Int) +
				" 01 00 00 00 00 00 02 00 00 00 00 00 03 00 00 00 00 00 04 00 00 00 00 00 00 00",
				XS(result));

			var bd = new BinaryDeserializer();
			bd.Options.AllowUnknownFields = true;

			var v1 = new SampleOrder { StarterPackOfferEndTime = 11, StartGoldInitialized = true };
			bs.ClearClassIds();
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS("SampleOrder") + " 02 00 " +
				XS("StartGoldInitialized", RoughType.Bool) + " " +
				XS("StarterPackOfferEndTime", RoughType.Int) +
				" 01 00 01 02 00 0B 00 00 00 00 00", XS(result1));

			bd.Options.Meta = new MetaOptions(); // Avoid duplicate alias.
			var v2 = bd.FromBytes<SampleOrderExt>(result1);
			Assert.AreEqual(11, v2.StarterPackOfferEndTime);
			Assert.IsTrue(v2.StartGoldInitialized);
		}

		[TestMethod]
		public void TestUnordered()
		{
			var bd = new BinaryDeserializer();
			bd.Options.AllowUnknownFields = true;
			bd.BinaryOptions.Unordered = true;

			var v1 = bd.FromBytes<SampleOrder>(SX(
				"20 01 00 " + XS("SampleOrder") + " 02 00 " +
				XS("StarterPackOfferEndTime", RoughType.Int) + " " +
				XS("StartGoldInitialized", RoughType.Bool) +
				" 01 00 0B 00 00 00 02 00 01 00 00"
			));
			Assert.AreEqual(11, v1.StarterPackOfferEndTime);
			Assert.IsTrue(v1.StartGoldInitialized);
		}

		[TestMethod]
		public void TestClassNames()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleBase)) + " 01 00 " + XS("FBase", RoughType.Int) +
				" 01 00 00 00 00 00 00 00",
				XS(bs.ToBytes(new SampleBase())));
			Assert.AreEqual(
				"20 02 00 " + XS(typeof(SampleDerivedA)) + " 02 00 " +
				XS("FBase", RoughType.Int, "FA", RoughType.Int) +
				" 01 00 00 00 00 00 02 00 00 00 00 00 00 00",
				XS(bs.ToBytes(new SampleDerivedA())));

			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Names;
			var v = bd.FromBytes(SX(
				"20 01 00 " + XS(typeof(SampleDerivedB)) + " 02 00 " +
				XS("FBase", RoughType.Int, "FB", RoughType.Int) +
				" 01 00 03 00 00 00 02 00 07 00 00 00 00 00"));
			Assert.IsInstanceOfType(v, typeof(SampleDerivedB));
			var b = (SampleDerivedB)v;
			Assert.AreEqual(3, b.FBase);
			Assert.AreEqual(7, b.FB);
		}

		[TestMethod]
		public void TestRec()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var v1 = new SampleRec { Child = new SampleRec { S = "x" }, S = "a" };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleRec)) + " 02 00 " +
				XS("Child", RoughType.Record, "S", RoughType.String) +
				" 01 00 01 00 01 00 00 00 02 00 " + XS("x") + " 00 00 02 00 " + XS("a") + " 00 00",
				XS(result1));
			var w1 = bd.FromBytes<SampleRec>(result1);
			Assert.AreEqual("a", w1.S);
			Assert.AreEqual("x", w1.Child.S);
			Assert.IsNull(w1.Child.Child);
		}

		[TestMethod]
		public void TestList()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;
			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Names;

			var v0 = new SampleList { E = new List<string> { "a", "b", "c" } };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleList)) + " 01 00 " +
				XS("E", RoughType.Sequence) + " " + XS(RoughType.String) +
				" 01 00 03 00 00 00 " + XS("a", "b", "c") + " 00 00",
				XS(result0));
			var w0 = new SampleList();
			bd.FromBytes(w0, result0);
			CollectionAssert.AreEqual(v0.E, w0.E);

			var v1 = new SampleTree { Value = 11, Children = new List<SampleTree>() };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 02 00 " + XS(typeof(SampleTree)) + " 02 00 " +
				XS("Value", RoughType.Int, "Children", RoughType.Sequence) + " " + XS(RoughType.Record) +
				" 01 00 0B 00 00 00 02 00 00 00 00 00 00 00",
				XS(result1));
			Assert.AreEqual("20 02 00 01 00 0B 00 00 00 02 00 00 00 00 00 00 00", XS(bs.ToBytes(v1)));
			var w1 = new SampleTree();
			bd.FromBytes(w1, result1);
			Assert.AreEqual(0, w1.Children.Count);

			var v2 = new SampleTree {
				Value = 11,
				Children = new List<SampleTree> {
					new SampleTree {
						Value = 12,
						Children = new List<SampleTree>(),
					},
					new SampleTree {
						Value = 13,
					}
				}
			};
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"20 02 00 01 00 0B 00 00 00 02 00 02 00 00 00 " +
				"02 00 01 00 0C 00 00 00 02 00 00 00 00 00 00 00 " +
				"02 00 01 00 0D 00 00 00 02 00 FF FF FF FF 00 00 00 00",
				XS(result2));
			SampleTree w2 = new SampleTree();
			bd.FromBytes(w2, result2);
			Assert.AreEqual(v2.Value, w2.Value);
			Assert.AreEqual(v2.Children.Count, w2.Children.Count);
			Assert.AreEqual(v2.Children[0].Value, w2.Children[0].Value);
			Assert.AreEqual(v2.Children[1].Children, w2.Children[1].Children);

			Assert.AreEqual(
				"20 03 00 " + XS(typeof(SampleEmptyList)) + " 01 00 " +
				XS("E", RoughType.Sequence) + " " + XS(RoughType.String) + " 00 00",
				XS(bs.ToBytes(new SampleEmptyList())));
			Assert.AreEqual(
				"20 03 00 01 00 FF FF FF FF 00 00",
				XS(bs.ToBytes(new SampleEmptyList { E = null })));
		}

		[TestMethod]
		public void TestCollection()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v0 = new SampleWithCollection();
			v0.A.Add(new SampleInterfaced { X = 9 });
			v0.B.Add(7);
			v0.B.Add(6);
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleWithCollection)) + " 02 00 " +
				XS("A", RoughType.Sequence) + " " + XS(RoughType.Record) + " " +
				XS("B", RoughType.Sequence) + " " + XS(RoughType.Int) +
				" 01 00 01 00 00 00 02 00 " + XS(typeof(SampleInterfaced)) +
				" 01 00 " + XS("X", RoughType.Int) +
				" 01 00 09 00 00 00 00 00" +
				" 02 00 02 00 00 00 07 00 00 00 06 00 00 00 00 00",
				XS(result0));

			var w0 = new SampleWithCollection();
			bd.FromBytes(w0, result0);
			Assert.AreEqual(1, w0.A.Count);
			Assert.IsInstanceOfType(w0.A.First(), typeof(SampleInterfaced));
			Assert.AreEqual(9, w0.A.First().X);
			CollectionAssert.AreEqual(new int[] { 7, 6 }, w0.B.ToList());

			var w1 = (SampleWithCollection)((new BinaryDeserializerGen()).FromBytes(result0));
			Assert.AreEqual(1, w1.A.Count);
			Assert.IsInstanceOfType(w1.A.First(), typeof(SampleInterfaced));
			Assert.AreEqual(9, w1.A.First().X);
			CollectionAssert.AreEqual(new int[] { 7, 6 }, w1.B.ToList());

			var v2 = new SampleConcreteCollection { 2, 5, 4 };
			var result1 = bs.ToBytes(v2);
			Assert.AreEqual("21 05 03 00 00 00 02 00 00 00 05 00 00 00 04 00 00 00", XS(result1));
			SampleConcreteCollection w2 = new SampleConcreteCollection();
			bd.FromBytes(w2, result1);
			CollectionAssert.AreEqual(v2.ToList(), w2.ToList());
		}

		[TestMethod]
		public void TestCollectionDefault()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var v1 = new SampleWithCollectionDefault();
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleWithCollectionDefault)) + " 01 00 " +
				XS("B", RoughType.Sequence) + " " + XS(RoughType.Int) + " 00 00", XS(result1));
			var w1 = bd.FromBytes<SampleWithCollectionDefault>(result1);
			Assert.AreEqual(1, w1.B.Count);
			Assert.AreEqual(1, w1.B[0]);
		}

		[TestMethod]
		public void TestSerializeItemIf()
		{
			var bs = new BinarySerializer();
			var v1 = new SampleCollection<int> { 5, 2, 4, 1 };
			Assert.AreEqual(
				"21 05 04 00 00 00 05 00 00 00 02 00 00 00 04 00 00 00 01 00 00 00",
				XS(bs.ToBytes(v1)));
			v1.Filter = 1;
			Assert.AreEqual("21 05 02 00 00 00 05 00 00 00 04 00 00 00", XS(bs.ToBytes(v1)));
			v1.Filter = 2;
			Assert.AreEqual("21 05 02 00 00 00 02 00 00 00 04 00 00 00", XS(bs.ToBytes(v1)));

			var s1 =
				"20 01 00 " + XS(typeof(SampleWithCollection)) + " 02 00 " +
				XS("A", RoughType.Sequence) + " " + XS(RoughType.Record) + " " +
				XS("B", RoughType.Sequence) + " " + XS(RoughType.Int);
			var v2 = new SampleWithCollection();
			v2.B.Add(5);
			Assert.AreEqual(
				s1 + " 01 00 00 00 00 00 02 00 01 00 00 00 05 00 00 00 00 00",
				XS(bs.ToBytes(v2)));
			v2.B.Filter = 3;
			Assert.AreEqual(
				"20 01 00 01 00 00 00 00 00 02 00 00 00 00 00 00 00",
				XS(bs.ToBytes(v2)));

			var bs1 = new BinarySerializer();
			bs1.Options.CheckForEmptyCollections = true;
			Assert.AreEqual(
				s1 + " 01 00 00 00 00 00 00 00",
				XS(bs1.ToBytes(v2)));
			var v3 = new SampleWithCollectionDefaultNonSerializable();
			v3.B.Add(2);
			var result3 = bs1.ToBytes(v3);
			Assert.AreEqual(
				"20 02 00 " + XS(typeof(SampleWithCollectionDefaultNonSerializable)) + " 01 00 " +
				XS("B", RoughType.Sequence) + " " + XS(RoughType.Int) + " 00 00", XS(result3));
		}

		[TestMethod]
		public void TestIEnumerable()
		{
			var bs = new BinarySerializer();

			var v0 = new SampleIEnumerable();
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleIEnumerable)) + " 01 00 " +
				XS("L", RoughType.Sequence) + " " + XS(RoughType.Int) +
				" 01 00 03 00 00 00 01 00 00 00 02 00 00 00 03 00 00 00 00 00",
				XS(result0));
		}

		[TestMethod]
		public void TestTopLevelList()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v0 = new List<string> { "a", "b", "c" };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual("21 10 03 00 00 00 " + XS("a", "b", "c"), XS(result0));

			var w0 = new List<string>();
			bd.FromBytes(w0, result0);
			CollectionAssert.AreEqual(v0, w0);
			bd.FromBytes(w0, new byte[] { 0x21, 0x10, 0, 0, 0, 0 });
			CollectionAssert.AreEqual(v0, w0);
			bd.FromBytes(w0, result0);
			CollectionAssert.AreEqual(new List<string> { "a", "b", "c", "a", "b", "c" }, w0);
		}

		[TestMethod]
		public void TestTopLevelDict()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v0 = new Dictionary<string, int> { { "a", 1 }, { "b", 2 } };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"22 10 05 02 00 00 00 " + XS("a") + " 01 00 00 00 " + XS("b") + " 02 00 00 00",
				XS(result0));

			var w0 = new Dictionary<string, int>();
			bd.FromBytes(w0, result0);
			CollectionAssert.AreEqual(v0, w0);
			bd.FromBytes(w0, new byte[] { 0x22, 0x10, 05, 0, 0, 0, 0 });
			CollectionAssert.AreEqual(v0, w0);
			bd.FromBytes(w0, SX("22 10 05 01 00 00 00 " + XS("c") + " 03 00 00 00"));
			CollectionAssert.AreEqual(
				new Dictionary<string, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } }, w0);
		}

		[TestMethod]
		public void TestDictionary()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;
			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Names;
			var bdg = new BinaryDeserializerGen();
			bdg.Options.TagMode = TagMode.Names;

			var v0 = new SampleDict {
				Value = 3, Children = new Dictionary<string, SampleDict> {
				{ "a", new SampleDict { Value = 5, Children = new Dictionary<string, SampleDict>() } },
				{ "b", new SampleDict { Value = 7 } },
			}
			};
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleDict)) + " 02 00 " +
				XS("Value", RoughType.Int, "Children", RoughType.Mapping) + " 10 20" +
				" 01 00 03 00 00 00 02 00 02 00 00 00 " + XS("a") +
				" 01 00 01 00 05 00 00 00 02 00 00 00 00 00 00 00 " +
				XS("b") + " 01 00 01 00 07 00 00 00 02 00 FF FF FF FF 00 00 00 00",
				XS(result0));

			var w0 = new SampleDict();
			bd.FromBytes(w0, result0);
			Assert.AreEqual(v0.Value, w0.Value);
			Assert.AreEqual(v0.Children.Count, w0.Children.Count);
			Assert.AreEqual(v0.Children["a"].Value, w0.Children["a"].Value);

			var w0g = (SampleDict)bdg.FromBytes(result0);
			Assert.AreEqual(v0.Value, w0g.Value);
			Assert.AreEqual(v0.Children.Count, w0g.Children.Count);
			Assert.AreEqual(v0.Children["a"].Value, w0g.Children["a"].Value);

			var v1 = new Dictionary<string, int> { { "", 0 } };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual("22 10 05 01 00 00 00 00 00 00 00 00 00", XS(result1));
			var w1 = bd.FromBytes<Dictionary<string, int>>(result1);
			Assert.AreEqual(1, w1.Count);
			Assert.AreEqual(0, w1[""]);
		}

		[TestMethod]
		public void TestSortedDictionary()
		{
			var bs = new BinarySerializer();

			var v0 = new SampleSortedDict { d = new SortedDictionary<string, int> { { "a", 3 }, { "b", 4 } } };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleSortedDict)) + " 01 00 " + XS("d") +
				" 22 10 05 01 00 02 00 00 00 " + XS("a") + " 03 00 00 00 " + XS("b") + " 04 00 00 00 00 00",
				XS(result0));

			CheckDeserializers(bd => {
				var w0 = bd.FromBytes<SampleSortedDict>(result0);
				Assert.AreEqual(v0.d.Count, w0.d.Count);
				Assert.AreEqual(v0.d["a"], w0.d["a"]);
				Assert.AreEqual(v0.d["b"], w0.d["b"]);
			});
		}

		[TestMethod]
		public void TestDictionaryKeys()
		{
			var bs = new BinarySerializer();

			var v0 = new SampleDictKeys {
				I = new Dictionary<int, int> { { 5, 7 } },
				E = new Dictionary<SampleEnum, int> { { SampleEnum.E2, 8 } },
				K = new Dictionary<SampleKey, int> { { new SampleKey { V = 3 }, 9 } },
			};
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleDictKeys)) + " 03 00 " +
				XS("E", RoughType.Mapping) + " 05 05 " +
				XS("I", RoughType.Mapping) + " 05 05 " +
				XS("K", RoughType.Mapping) + " 20 05" +
				" 01 00 01 00 00 00 01 00 00 00 08 00 00 00 " +
				"02 00 01 00 00 00 05 00 00 00 07 00 00 00 " +
				"03 00 01 00 00 00 02 00 " + XS(typeof(SampleKey)) +
				" 01 00 " + XS("V", RoughType.Int) +
				" 01 00 03 00 00 00 00 00 09 00 00 00 00 00", XS(result0));

			CheckDeserializers(bd => {
				var w = new SampleDictKeys();
				bd.FromBytes(w, result0);
				Assert.AreEqual(1, w.I.Count);
				Assert.AreEqual(7, w.I[5]);
				Assert.AreEqual(1, w.E.Count);
				Assert.AreEqual(8, w.E[SampleEnum.E2]);
				Assert.AreEqual(1, w.K.Count);
				Assert.AreEqual(9, w.K[new SampleKey { V = 3 }]);
			});
		}

		[TestMethod]
		public void TestArray()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var bdg = new BinaryDeserializerGen();

			var v0 = new SampleArray { A = new string[] { "a", "b", "c" } };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleArray)) + " 01 00 " + XS("A", RoughType.Sequence) +
				" 10 01 00 03 00 00 00 " + XS("a", "b", "c") + " 00 00",
				XS(result0));
			var w0 = new SampleArray();
			bd.FromBytes(w0, result0);
			CollectionAssert.AreEqual(v0.A, w0.A);
			var w0g = (SampleArray)bdg.FromBytes(result0);
			CollectionAssert.AreEqual(v0.A, w0g.A);

			var v2 = new SampleArray();
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual("20 01 00 01 00 FF FF FF FF 00 00", XS(result2));
			var w2 = new SampleArray();
			bd.FromBytes(w2, result2);
			CollectionAssert.AreEqual(v2.A, w2.A);
			var w2g = (SampleArray)bdg.FromBytes(result2);
			CollectionAssert.AreEqual(v2.A, w2g.A);
		}

		[TestMethod]
		public void TestArrayOfArray()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var bdg = new BinaryDeserializerGen();

			var v0 = new SampleArrayOfArray { A = new int[][] { new int[] { 1, 2, 3 }, new int[] { 4, 5 } } };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleArrayOfArray)) + " 01 00 " + XS("A", RoughType.Sequence) +
				" 21 05 01 00 02 00 00 00" +
				" 03 00 00 00 01 00 00 00 02 00 00 00 03 00 00 00" +
				" 02 00 00 00 04 00 00 00 05 00 00 00" +
				" 00 00",
				XS(result0));

			var w0 = new SampleArrayOfArray();
			bd.FromBytes(w0, result0);
			Assert.AreEqual(2, w0.A.Length);
			CollectionAssert.AreEqual(v0.A[0], w0.A[0]);
			CollectionAssert.AreEqual(v0.A[1], w0.A[1]);

			var w1 = (SampleArrayOfArray)bdg.FromBytes(result0);
			Assert.AreEqual(2, w1.A.Length);
			CollectionAssert.AreEqual(v0.A[0], w1.A[0]);
			CollectionAssert.AreEqual(v0.A[1], w1.A[1]);
		}

		[TestMethod]
		public void TestArrayNDim()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var bdg = new BinaryDeserializerGen();

			var v0 = new SampleArrayNDim {
				A = new int[2, 3] { { 1, 2, 3 }, { 4, 5, 6 } },
				B = new string[1, 1, 1] { { { "x" } } },
			};
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleArrayNDim)) + " 02 00 " +
				XS("A", RoughType.NDimArray) + " 02 05 " + XS("B", RoughType.NDimArray) + " 03 10 " +
				"01 00 02 00 00 00 03 00 00 00 00 " +
				"01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00 05 00 00 00 06 00 00 00 " +
				"02 00 01 00 00 00 01 00 00 00 01 00 00 00 00 " + XS("x") + " 00 00",
				XS(result0));
			var w0 = new SampleArrayNDim();
			bd.FromBytes(w0, result0);
			v0.AssertAreEqual(w0);

			var w0g = bdg.FromBytes<SampleArrayNDim>(result0);
			v0.AssertAreEqual(w0g);

			var resultNull = bs.ToBytes(new SampleArrayNDim());
			Assert.AreEqual("20 01 00 01 00 FF FF FF FF 02 00 FF FF FF FF 00 00", XS(resultNull));
			var wNull = bd.FromBytes<SampleArrayNDim>(resultNull);
			Assert.IsNull(wNull.A);
			Assert.IsNull(wNull.B);

			var v1 = new SampleArrayNDim {
				A = new int[3, 1] { { 1 }, { 2 }, { 3 } },
				B = new string[0, 0, 0],
			};
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"\n20 01 00 01 00 03 00 00 00 01 00 00 00 00 01 00 00 00 02 00 00 00 03 00 00 00 " +
				"02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
				"\n" + XS(result1));
			var w1 = bd.FromBytes<SampleArrayNDim>(result1);
			v1.AssertAreEqual(w1);

			var v2 = new SampleArrayNDim {
				A = (int[,])Array.CreateInstance(typeof(int), new int[] { 1, 1 }, new int[] { -1, 10 })
			};
			v2.A[-1, 10] = 7;
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"\n20 01 00 01 00 01 00 00 00 01 00 00 00 01 FF FF FF FF 0A 00 00 00 " +
				"07 00 00 00 02 00 FF FF FF FF 00 00",
				"\n" + XS(result2));
			var w2 = bd.FromBytes<SampleArrayNDim>(result2);
			v2.AssertAreEqual(w2);
		}

		[TestMethod]
		public void TestClassList()
		{
			var bs = new BinarySerializer();

			var v = new SampleClassList {
				E = new List<SampleBase> {
					new SampleDerivedA(),
					new SampleDerivedB { FB = 9 },
					new SampleDerivedB { FB = 8 },
				}
			};

			var result = bs.ToBytes(v);

			CheckDeserializers(bd => {
				var w = (SampleClassList)bd.FromBytes(result);
				Assert.AreEqual(3, w.E.Count);
				Assert.IsInstanceOfType(w.E[0], typeof(SampleDerivedA));
				Assert.IsInstanceOfType(w.E[1], typeof(SampleDerivedB));
				Assert.AreEqual(9, ((SampleDerivedB)w.E[1]).FB);
				Assert.IsInstanceOfType(w.E[2], typeof(SampleDerivedB));
				Assert.AreEqual(8, ((SampleDerivedB)w.E[2]).FB);
			});
		}

		[TestMethod]
		public void TestMatrix()
		{
			var src =
				"20 01 00 " + XS(typeof(SampleMatrix)) + " 01 00 " + XS("M") + " 21 21 05 01 00 " +
				"04 00 00 00 03 00 00 00 01 00 00 00 02 00 00 00 03 00 00 00 " +
				"02 00 00 00 04 00 00 00 05 00 00 00 " +
				"01 00 00 00 06 00 00 00 00 00 00 00 00 00";
			var w = new SampleMatrix();
			CheckDeserializers(bd => {
				bd.FromBytes(w, SX(src));
				Assert.AreEqual(4, w.M.Count);
				CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, w.M[0]);
				CollectionAssert.AreEqual(new int[] { 4, 5 }, w.M[1]);
				CollectionAssert.AreEqual(new int[] { 6 }, w.M[2]);
				Assert.AreEqual(0, w.M[3].Count);
			});

			var bs = new BinarySerializer();
			Assert.AreEqual(src, XS(bs.ToBytes(w)));
		}

		private void CheckSampleRect(SampleRect expected, SampleRect actual)
		{
			Assert.AreEqual(expected.A.X, actual.A.X);
			Assert.AreEqual(expected.A.Y, actual.A.Y);
			Assert.AreEqual(expected.B.X, actual.B.X);
			Assert.AreEqual(expected.B.Y, actual.B.Y);
		}

		private void AssertEqualSampleStructWithProps(SampleStructWithProps expected, SampleStructWithProps actual)
		{
			Assert.AreEqual(expected.A, actual.A);
			Assert.AreEqual(expected.P.X, actual.P.X);
			Assert.AreEqual(expected.P.Y, actual.P.Y);
		}

		[TestMethod]
		public void TestStruct()
		{
			var v = new SampleRect {
				A = new SamplePoint { X = 33, Y = 44 },
				B = new SamplePoint { X = 55, Y = 66 },
			};
			var bs = new BinarySerializer();
			var result = bs.ToBytes(v);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleRect)) + " 02 00 " +
				XS("A", RoughType.Record, "B", RoughType.Record) +
				" 01 00 02 00 " + XS(typeof(SamplePoint)) + " 02 00 " +
				XS("X", RoughType.Int, "Y", RoughType.Int) +
				" 21 00 00 00 2C 00 00 00 " +
				"02 00 02 00 37 00 00 00 42 00 00 00 00 00",
				XS(result));

			var bd = new BinaryDeserializer();
			var w = new SampleRect();
			bd.FromBytes(w, result);
			CheckSampleRect(v, w);

			var bdg = new BinaryDeserializerGen();
			w = (SampleRect)bdg.FromBytes(result);
			CheckSampleRect(v, w);

			var p = (SamplePoint)bdg.FromBytes(new SamplePoint(), SX("20 02 00 22 00 00 00 2D 00 00 00"));
			Assert.AreEqual(34, p.X);
			Assert.AreEqual(45, p.Y);

			var v2 = new SampleStructWithProps { A = 37, P = new SamplePoint { X = 9, Y = 1 } };
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"20 03 00 " + XS(typeof(SampleStructWithProps)) + " 02 00 " +
				XS("A", RoughType.Int, "P", RoughType.Record) +
				" 25 00 00 00 02 00 09 00 00 00 01 00 00 00",
				XS(result2));
			var w2 = bd.FromBytes<SampleStructWithProps>(result2);
			AssertEqualSampleStructWithProps(v2, w2);

			var w2g = bdg.FromBytes<SampleStructWithProps>(result2);
			AssertEqualSampleStructWithProps(v2, w2g);

			var v3 = new SampleStructWithProps[2] {
				new SampleStructWithProps { A = 41, P = new SamplePoint { X = 19, Y = 1 } },
				new SampleStructWithProps { A = 42, P = new SamplePoint { X = 18, Y = 2 } },
			};

			var result3 = bs.ToBytes(v3);
			Assert.AreEqual(
				"21 20 02 00 00 00" +
				" 03 00 29 00 00 00 02 00 13 00 00 00 01 00 00 00" +
				" 03 00 2A 00 00 00 02 00 12 00 00 00 02 00 00 00",
				XS(result3));
			var w3 = bd.FromBytes<SampleStructWithProps[]>(result3);
			Assert.AreEqual(v3.Length, w3.Length);
			AssertEqualSampleStructWithProps(v3[0], w3[0]);
			AssertEqualSampleStructWithProps(v3[1], w3[1]);
		}

		[TestMethod]
		public void TestInterface()
		{
			var bs = new BinarySerializer();
			var v1 = new SampleInterfaceField { I = new SampleInterfaced { X = 34 } };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleInterfaceField)) + " 01 00 " + XS("I", RoughType.Record) +
				" 01 00 02 00 " + XS(typeof(SampleInterfaced)) + " 01 00 " + XS("X", RoughType.Int) +
				" 01 00 22 00 00 00 00 00 00 00",
				XS(result1));

			var w1 = new SampleInterfaceField();
			var bd = new BinaryDeserializer();
			bd.FromBytes(w1, result1);
			Assert.IsInstanceOfType(w1.I, typeof(SampleInterfaced));
			Assert.AreEqual(34, w1.I.X);

			var w1g = new SampleInterfaceField();
			var bdg = new BinaryDeserializerGen();
			bdg.FromBytes(w1g, result1);
			Assert.IsInstanceOfType(w1g.I, typeof(SampleInterfaced));
			Assert.AreEqual(34, w1g.I.X);

			var w1n = (SampleInterfaceField)bd.FromBytes(new byte[] { 0x20, 01, 00, 01, 00, 00, 00, 00, 00 });
			Assert.AreEqual(null, w1n.I);
			var w1ng = (SampleInterfaceField)bdg.FromBytes(new byte[] { 0x20, 01, 00, 01, 00, 00, 00, 00, 00 });
			Assert.AreEqual(null, w1ng.I);

			var v2 = new List<ISample> { null, new SampleInterfaced { X = 37 } };
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual("21 20 02 00 00 00 00 00 02 00 01 00 25 00 00 00 00 00", XS(result2));

			var w2 = new List<ISample>();
			bd.FromBytes(w2, result2);
			Assert.AreEqual(2, w2.Count);
			Assert.IsNull(w2[0]);
			Assert.AreEqual(37, w2[1].X);

			ISampleField v3 = new SampleInterfacedField { X = 41 };
			var result3 = bs.ToBytes(v3);
			Assert.AreEqual(
				"20 03 00 " + XS(typeof(SampleInterfacedField)) + " 01 00 " + XS("X", RoughType.Int) +
				" 01 00 29 00 00 00 00 00", XS(result3));
			var w3 = (ISampleField)bd.FromBytes(result3);
			Assert.AreEqual(41, w3.X);
		}

		[TestMethod]
		public void TestAbstract()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			SampleAbstract v1 = new SampleConcrete { XX = 81 };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleConcrete)) + " 01 00 " + XS("XX", RoughType.Int) +
				" 01 00 51 00 00 00 00 00", XS(result1));

			var w1 = bd.FromBytes<SampleAbstract>(result1);
			Assert.AreEqual((v1 as SampleConcrete).XX, (w1 as SampleConcrete).XX);
			var w1g = (SampleConcrete)(new BinaryDeserializerGen().FromBytes(result1));
			Assert.AreEqual((v1 as SampleConcrete).XX, w1g.XX);

			var v2 = new List<SampleAbstract>();
			v2.Add(new SampleConcrete { XX = 51 });

			var w2 = bd.FromBytes<List<SampleAbstract>>(bs.ToBytes(v2));
			Assert.AreEqual(v2.Count, w2.Count);
			Assert.AreEqual((v2[0] as SampleConcrete).XX, (w2[0] as SampleConcrete).XX);
		}

		[TestMethod]
		public void TestGeneric()
		{
			var bs = new BinarySerializer();
			var v1 = new SampleInterfaceField { I = new SampleInterfacedGeneric<string> { X = 35, G = "qq" } };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleInterfaceField)) + " 01 00 " + XS("I", RoughType.Record) +
				" 01 00 02 00 " + XS("YuzuTest.SampleInterfacedGeneric`1[[System.String]], YuzuTest") +
				" 02 00 " + XS("G", RoughType.String, "X", RoughType.Int) +
				" 01 00 " + XS("qq") + " 02 00 23 00 00 00 00 00 00 00",
				XS(result1));
			CheckDeserializers(bd => {
				var w1 = (SampleInterfaceField)bd.FromBytes(new SampleInterfaceField(), result1);
				Assert.AreEqual(w1.I.X, 35);
				Assert.AreEqual((w1.I as SampleInterfacedGeneric<string>).G, "qq");
			});
		}

		[TestMethod]
		public void TestDefault()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var bdg = new BinaryDeserializerGen();

			/*var v1 = new Sample1 { X = 6, Y = "ttt" };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(Sample1)) + " 02 00 " +
				XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 06 00 00 00 00 00", XS(result1));
			var w1 = (Sample1)bd.FromBytes(result1);
			Assert.AreEqual(6, w1.X);
			Assert.AreEqual("zzz", w1.Y);
			var w1g = (Sample1)bdg.FromBytes(result1);
			Assert.AreEqual(6, w1g.X);
			Assert.AreEqual("zzz", w1g.Y);

			var v2 = new Sample2 { X = 5, Y = "5" };
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"20 02 00 " + XS(typeof(Sample2)) + " 02 00 " +
				XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 05 00 00 00 00 00", XS(result2));
			Assert.IsInstanceOfType(bd.FromBytes(result2), typeof(Sample2));

			var v3 = new SampleDefault();
			var result3 = bs.ToBytes(new SampleDefault());
			Assert.AreEqual(
				"20 03 00 " + XS(typeof(SampleDefault)) + " 03 00 " +
				XS("A", RoughType.Int, "B", RoughType.String, "P", RoughType.Record) + " 00 00",
				XS(result3));
			Assert.IsInstanceOfType(bd.FromBytes(result3), typeof(SampleDefault));
			v3.B = "z";
			var result3m = bs.ToBytes(v3);
			Assert.AreEqual("20 03 00 02 00 " + XS("z") + " 00 00", XS(result3m));
			var w3 = new SampleDefault();
			bd.FromBytes(w3, result3m);
			Assert.AreEqual(3, w3.A);
			Assert.AreEqual("z", w3.B);
			Assert.AreEqual(new SamplePoint { X = 7, Y = 2 }, w3.P);
			*/
			var result4 = SX(
				"20 01 00 " + XS(typeof(SampleDefault)) + " 02 00 " +
				XS("A", RoughType.Int, "P", RoughType.Record) + " 01 00 05 00 00 00 " +
				"02 00 02 00 " + XS(typeof(SamplePoint)) + " 02 00 " +
				XS("X", RoughType.Int, "Y", RoughType.Int) + " 04 00 00 00 06 00 00 00 " +
				"00 00"
			);
			bdg.ClearClassIds();
			var w4 = bdg.FromBytes<SampleDefault>(result4);
			Assert.AreEqual(5, w4.A);
			Assert.AreEqual("default", w4.B);
			Assert.AreEqual(new SamplePoint { X = 4, Y = 6 }, w4.P);
		}

		[TestMethod]
		public void TestObject()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v1 = new SampleObj { F = 123.4f };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleObj)) + " 01 00 " + XS("F", RoughType.Any) +
				" 01 00 " + XS(RoughType.Float) + " CD CC F6 42 00 00", XS(result1));

			var w = new SampleObj();
			bd.FromBytes(w, result1);
			Assert.AreEqual(v1.F, w.F);
			var wg = new SampleObj();
			(new BinaryDeserializerGen()).FromBytes(wg, result1);
			Assert.AreEqual(v1.F, wg.F);

			var bin1 = SX("20 01 00 01 00 21 02 03 00 00 00 01 02 03 00 00");
			bd.FromBytes(w, bin1);
			CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, (List<byte>)w.F);
			dynamic d1 = YuzuUnknown.Dyn(bd.FromBytes(bin1));
			CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, d1.F);

			bd.FromBytes(w, SX(
				"20 01 00 01 00 22 10 03 02 00 00 00 " +
				XS("a") + " 01 00 " + XS("b") + " 02 00 00 00"));
			CollectionAssert.AreEqual(
				new Dictionary<string, short>() { { "a", 1 }, { "b", 2 } },
				(Dictionary<string, short>)w.F);

			Assert.AreEqual(
				typeof(Dictionary<string, object>),
				bd.FromBytes(new byte[] { 0x22, 0x10, (byte)RoughType.Any, 00, 00, 00, 00 }).GetType());
			CollectionAssert.AreEqual(
				(List<object>)bd.FromBytes(SX("21 11 02 00 00 00 01 05 10 " + XS("abc"))),
				new object[] { (sbyte)5, "abc" });

			Assert.AreEqual((short)266, bd.FromBytes<object>(SX("03 0A 01")));

			Assert.AreEqual(
				"21 11 02 00 00 00 10 " + XS("q") + " 21 05 01 00 00 00 01 00 00 00",
				XS(bs.ToBytes(new List<object> { "q", new List<int> { 1 } })));
		}

		[TestMethod]
		public void TestNewFields()
		{
			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Aliases;
			bd.Options.AllowUnknownFields = true;

			var w = new SampleTree();
			bd.FromBytes(w, SX(
				"20 01 00 " + XS(typeof(SampleTree)) + " 03 00 " +
				XS("a", RoughType.Int, "a1", RoughType.Sequence) + " 10 " +
				XS("b", RoughType.Sequence) +
				" 20 01 00 09 00 00 00 02 00 00 00 00 00 03 00 FF FF FF FF 00 00"));
			Assert.AreEqual(9, w.Value);
			Assert.AreEqual(null, w.Children);

			bd.ClearClassIds();

			bd.FromBytes(w, SX(
				"20 01 00 " + XS(typeof(SampleTree)) + " 04 00 " +
				XS("a", RoughType.Int, "a1", RoughType.Byte) + " " +
				XS("b", RoughType.Sequence) + " 20 " + XS("x", RoughType.Record) +
				" 01 00 0A 00 00 00 02 00 00 04 00 00 00 00 00"));
			Assert.AreEqual(10, w.Value);

			bd.FromBytes(w, SX(
				"20 01 00 01 00 0B 00 00 00 00 00"));
			Assert.AreEqual(11, w.Value);
		}

		[TestMethod]
		public void TestUnknownStorage()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			bd.Options.AllowUnknownFields = true;

			var data =
				"20 01 00 " + XS(typeof(SampleUnknown)) + " 03 00 " +
				XS("A", RoughType.String, "X", RoughType.Int, "Z", RoughType.Bool) +
				" 01 00 " + XS("qq") + " 02 00 02 01 00 00 03 00 01 00 00";
			var w = new SampleUnknown();
			bd.FromBytes(w, SX(data));
			Assert.AreEqual(258, w.X);
			Assert.AreEqual(2, w.Storage.Fields.Count);
			Assert.AreEqual("A", w.Storage.Fields[0].Name);
			Assert.AreEqual("qq", w.Storage.Fields[0].Value);
			Assert.AreEqual("Z", w.Storage.Fields[1].Name);
			Assert.AreEqual(true, w.Storage.Fields[1].Value);

			Assert.AreEqual(data, XS(bs.ToBytes(w)));

			bd.FromBytes(w, SX("20 01 00 00 00"));
			Assert.AreEqual(0, w.Storage.Fields.Count);

			Assert.AreEqual("20 01 00 00 00", XS(bs.ToBytes(new SampleUnknown())));

			bd.ClearClassIds();
			bs.ClearClassIds();
			bd.FromBytes(w, SX(
				"20 01 00 " + XS(typeof(SampleUnknown)) + " 02 00 " +
				XS("A", RoughType.String, "Z", RoughType.Bool) +
				" 01 00 " + XS("tt") + " 02 00 01 00 00"));
			Assert.AreEqual(2, w.Storage.Fields.Count);
			Assert.AreEqual("A", w.Storage.Fields[0].Name);
			Assert.AreEqual("tt", w.Storage.Fields[0].Value);
			Assert.AreEqual("Z", w.Storage.Fields[1].Name);
			Assert.AreEqual(true, w.Storage.Fields[1].Value);
			Assert.AreEqual(258, w.X);

			w.X = 0;
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleUnknown)) + " 03 00 " +
				XS("A", RoughType.String, "X", RoughType.Int, "Z", RoughType.Bool) +
				" 01 00 " + XS("tt") + " 03 00 01 00 00",
				XS(bs.ToBytes(w)));

			bs.ClearClassIds();
			bs.ToBytes(new SampleUnknown());
			XAssert.Throws<YuzuException>(() => bs.ToBytes(w), "SampleUnknown");

			bs.ClearClassIds();
			bd.ClearClassIds();
			var data2 =
				"20 01 00 " + XS(typeof(SampleUnknown)) + " 02 00 " +
				XS("A", RoughType.Record, "X", RoughType.Int) +
				" 01 00 02 00 " + XS("NewType") + " 01 00 " + XS("Fld", RoughType.SByte) +
				" 01 00 FE 00 00 02 00 14 00 00 00 00 00";
			var w2 = bd.FromBytes<SampleUnknown>(SX(data2));
			Assert.AreEqual(1, w2.Storage.Fields.Count);
			Assert.AreEqual("A", w2.Storage.Fields[0].Name);
			var u2 = (YuzuUnknown)w2.Storage.Fields[0].Value;
			Assert.AreEqual("NewType", u2.ClassTag);
			Assert.AreEqual(1, u2.Fields.Count);
			Assert.AreEqual((sbyte)-2, u2.Fields["Fld"]);
			Assert.AreEqual(20, w2.X);

			Assert.AreEqual("\n" + data2, "\n" + XS(bs.ToBytes(w2)));
		}

		[TestMethod]
		public void TestUnknownStorageDict()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			bd.Options.AllowUnknownFields = true;

			var data1 =
				"20 01 00 " + XS(typeof(SampleUnknown)) + " 02 00 " +
				XS("X", RoughType.Int) + " " + XS("F", RoughType.Mapping) +
				" 0A 21 20 01 00 0C 00 00 00 02 00 01 00 00 00 61 01 00 00 00 02 00 " +
				XS(typeof(SampleBool)) +
				" 01 00 " + XS("B", RoughType.Bool) + " 01 00 01 00 00 00 00";
			var w1 = bd.FromBytes<SampleUnknown>(SX(data1));
			Assert.AreEqual(12, w1.X);
			bd.ClearClassIds();
			var data2 = bs.ToBytes(w1);
			Assert.AreEqual(data1, XS(data2));
			var w2 = bd.FromBytes<SampleUnknown>(data2);
			var dict = (Dictionary<char, object>)w2.Storage.Fields[0].Value;
			Assert.AreEqual(1, dict.Count);
			var lst = (List<object>)dict['a'];
			Assert.AreEqual(1, lst.Count);
			Assert.IsTrue(((SampleBool)lst[0]).B);
		}

		[TestMethod]
		public void TestUnknownStorageClear()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			bd.Options.AllowUnknownFields = true;

			var data1 =
				"20 01 00 " + XS(typeof(SampleUnknown)) + " 02 00 " +
				XS("X", RoughType.Int) + " " + XS("Y", RoughType.Int) +
				" 01 00 04 00 00 00 02 00 05 00 00 00 00 00";
			var w1 = bd.FromBytes<SampleUnknown>(SX(data1));
			Assert.AreEqual(4, w1.X);

			bd.ClearClassIds();
			var data2 =
				"20 01 00 " + XS(typeof(SampleUnknown)) + " 02 00 " +
				XS("X", RoughType.Int) + " " + XS("Z", RoughType.Int) +
				" 01 00 07 00 00 00 02 00 08 00 00 00 00 00";
			var w2 = bd.FromBytes<SampleUnknown>(SX(data2));
			Assert.AreEqual(7, w2.X);

			var lst = new List<SampleUnknown> { w1, w2 };

			XAssert.Throws<YuzuException>(() => bs.ToBytes(lst), "Conflict");

			bs.ClearClassIds();
			w1.Storage.Clear(clearMetadata: true);
			w2.Storage.Clear(clearMetadata: true);
			var result1 = bs.ToBytes(lst);
			Assert.AreEqual(
				"21 20 02 00 00 00 01 00 " + XS(typeof(SampleUnknown)) + " 01 00 " +
				XS("X", RoughType.Int) +
				" 01 00 04 00 00 00 00 00 01 00 01 00 07 00 00 00 00 00",
				XS(result1));
		}

		[TestMethod]
		public void TestEscape()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			{
				var s = "\"/{\u0001}\n\t\"\"";
				var v = new Sample1 { Y = s };
				var result = bs.ToBytes(v);
				Assert.AreEqual(
					"20 01 00 " + XS(typeof(Sample1)) + " 02 00 " +
					XS("X", RoughType.Int, "Y", RoughType.String) +
					" 01 00 00 00 00 00 02 00 " + XS(s) + " 00 00",
					XS(result));

				var w = new Sample1();
				bd.FromBytes(w, result);
				Assert.AreEqual(s, w.Y);

				v.Y = "привет";
				var result2 = bs.ToBytes(v);
				Assert.AreEqual(
					"20 01 00 01 00 00 00 00 00 02 00 0C " +
					XS(Encoding.UTF8.GetBytes("привет")) + " 00 00",
					XS(result2));
				bd.FromBytes(w, result2);
				Assert.AreEqual(v.Y, w.Y);

				var ms = new MemoryStream(result2.Length);
				ms.Write(result2, 0, result2.Length);
				ms.Position = 0;
				bd.FromReader(w, new UnsafeBinaryReader(ms));
				Assert.AreEqual(v.Y, w.Y);
			}
			{
				var a = "\ud801\udc37";
				var result = bs.ToBytes(a);
				var b = new byte[] { 16, 4 }.Concat(Encoding.UTF8.GetBytes(a)).ToArray();
				CollectionAssert.AreEqual(b, result);
				Assert.AreEqual(a, bd.FromBytes(b));
			}
		}

		[TestMethod]
		public void TestDate()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var d = new DateTime(2011, 3, 25);
			var v1 = new SampleDate {
				D = d, DOfs = new DateTimeOffset(d, TimeSpan.FromHours(1)), T = TimeSpan.FromMinutes(5) };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleDate)) + " 03 00 " +
				XS("D", RoughType.DateTime, "DOfs", RoughType.DateTimeOffset, "T", RoughType.TimeSpan) +
				" 01 00 00 00 F5 B7 96 B8 CD 08 " +
				"02 00 00 00 F5 B7 96 B8 CD 08 00 68 C4 61 08 00 00 00 " +
				"03 00 00 5E D0 B2 00 00 00 00 00 00",
				XS(result1));

			var w1 = new SampleDate();
			bd.FromBytes(w1, result1);
			Assert.AreEqual(v1.D, w1.D);
			Assert.AreEqual(v1.DOfs, w1.DOfs);
			Assert.AreEqual(v1.T, w1.T);
			var w1g = (new BinaryDeserializerGen()).FromBytes<SampleDate>(result1);
			Assert.AreEqual(v1.D, w1g.D);
			Assert.AreEqual(v1.DOfs, w1g.DOfs);
			Assert.AreEqual(v1.T, w1g.T);

			var v2 = new DateTime(2011, 3, 25, 1, 2, 3, DateTimeKind.Utc);
			var result2 = bs.ToBytes(v2);
			var w2 = bd.FromBytes<DateTime>(result2);
			Assert.AreEqual(v2, w2);
			Assert.AreEqual(v2.Kind, w2.Kind);

			var v3 = new DateTime(2011, 3, 25, 1, 2, 3, DateTimeKind.Local);
			var result3 = bs.ToBytes(v3);
			var w3 = bd.FromBytes<DateTime>(result3);
			Assert.AreEqual(v3, w3);
			Assert.AreEqual(v3.Kind, w3.Kind);

			var v4 = new DateTimeOffset(2013, 4, 25, 3, 2, 1, TimeSpan.FromHours(10));
			var result4 = bs.ToBytes(v4);
			var w4 = bd.FromBytes<DateTimeOffset>(result4);
			Assert.AreEqual(v4, w4);
		}

		[TestMethod]
		public void TestGuid()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v = new SampleGuid { G = new Guid("5727b607-dcee-445f-856c-fd8ebb4b4573") };
			var result = bs.ToBytes(v);
			Assert.AreEqual(
				"\n20 01 00 " + XS(typeof(SampleGuid)) + " 01 00 " +
				XS("G", RoughType.Guid) +
				" 01 00 07 B6 27 57 EE DC 5F 44 85 6C FD 8E BB 4B 45 73 00 00",
				"\n" + XS(result));

			var w = bd.FromBytes<SampleGuid>(result);
			Assert.AreEqual(v.G, w.G);

			var wg = (new BinaryDeserializerGen()).FromBytes<SampleGuid>(result);
			Assert.AreEqual(v.G, wg.G);
			{
				var g = Guid.NewGuid();
				var v1 = new SampleObj { F = g };
				var result1 = bs.ToBytes(v1);
				Assert.AreEqual(
					"\n20 02 00 " + XS(typeof(SampleObj)) + " 01 00 " + XS("F", RoughType.Any) +
					" 01 00 14 " + XS(g.ToByteArray()) + " 00 00",
					"\n" + XS(result1));
				var w1 = bd.FromBytes<SampleObj>(result1);
				Assert.AreEqual(v1.F, w1.F);
			}
		}

		[TestMethod]
		public void TestDelegate()
		{
			var bs = new BinarySerializer();

			var v1 = new SampleSelfDelegate { x = 77 };
			v1.OnSomething = v1.Handler1;
			var result = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleSelfDelegate)) + " 02 00 " +
				XS("OnSomething", RoughType.Record, "x", RoughType.Int) +
				" 01 00 " + XS("Handler1") + " 02 00 4D 00 00 00 00 00",
				XS(result));

			var w1 = new SampleSelfDelegate();
			var bd = new BinaryDeserializer();
			bd.FromBytes(w1, result);
			Assert.AreEqual(v1.x, w1.x);
			w1.OnSomething(10);
			Assert.AreEqual(87, w1.x);

			result[69]++; // Replace("Handler1", "Handler2")
			(new BinaryDeserializer()).FromBytes(w1, result);
			w1.OnSomething(10);
			Assert.AreEqual(770, w1.x);
		}

		[TestMethod]
		public void TestNullField()
		{
			var bs = new BinarySerializer();
			var sample = new SampleWithNullField();
			var result = bs.ToBytes(sample);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleWithNullField)) + " 01 00 " + XS("About", RoughType.String) +
				" 01 00 00 01 00 00",
				XS(result));
			var bd = new BinaryDeserializer();
			var bdg = new BinaryDeserializerGen();
			var w = new SampleWithNullField { About = "zzz" };
			bd.FromBytes(w, result);
			Assert.AreEqual(sample.About, w.About);

			bd.FromBytes(w, new byte[] { 0x20, 01, 00, 01, 00, 00, 00, 00, 00 });
			Assert.AreEqual("", w.About);

			var wg = (SampleWithNullFieldCompact)bdg.FromBytes(SX(
				"20 01 00 " + XS(typeof(SampleWithNullFieldCompact)) + " 01 00 " + XS("N", RoughType.Record) +
				" 00 00 00 00"));
			Assert.AreEqual(null, wg.N);

			var v2 = new SampleObj();
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"20 02 00 " + XS(typeof(SampleObj)) + " 01 00 " + XS("F", RoughType.Any) +
				" 01 00 11 00 00",
				XS(result2));
			var w2 = bd.FromBytes<SampleObj>(result2);
			Assert.IsNull(w2.F);

			Assert.AreEqual("11", XS(bs.ToBytes(null)));
			Assert.IsNull(bd.FromBytes(new byte[] { 0x11 }));
		}

		[TestMethod]
		public void TestBeforeSerialization()
		{
			var bs = new BinarySerializer();
			var v0 = new SampleBeforeSerialization { X = "m" };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleBeforeSerialization)) + " 01 00 " + XS("X", RoughType.String) +
				" 01 00 " + XS("m1") + " 00 00",
				XS(result0));
			var result1 = bs.ToBytes(new SampleBefore2 { X = "m" });
			Assert.AreEqual(
				"20 02 00 " + XS(typeof(SampleBefore2)) + " 01 00 " + XS("X", RoughType.String) +
				" 01 00 " + XS("m231") + " 00 00",
				XS(result1));
		}

		[TestMethod]
		public void TestAfterSerialization()
		{
			var bs = new BinarySerializer();
			var v0 = new SampleAfterSerialization { X = "m" };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleAfterSerialization)) + " 01 00 " + XS("X", RoughType.String) +
				" 01 00 " + XS("m") + " 00 00",
				XS(result0));
			Assert.AreEqual("m1", v0.X);
		}

		[TestMethod]
		public void TestBeforeDeserialization()
		{
			var bs = new BinarySerializer();
			var v0 = new SampleBeforeDeserialization();
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleBeforeDeserialization)) + " 01 00 " + XS("X", RoughType.String) +
				" 01 00 " + XS("X") + " 00 00",
				XS(result0));

			CheckDeserializers(bd => {
				Assert.AreEqual("2X", bd.FromBytes<SampleBeforeDeserialization>(result0).X);
			});
		}

		[TestMethod]
		public void TestAfterDeserialization()
		{
			var bs = new BinarySerializer();
			var v0 = new SampleAfterDeserialization { X = "m" };
			var result0 = bs.ToBytes(v0);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleAfterDeserialization)) + " 01 00 " + XS("X", RoughType.String) +
				" 01 00 " + XS("m") + " 00 00",
				XS(result0));
			var result1 = bs.ToBytes(new SampleAfter2 { X = "m" });

			CheckDeserializers(bd => {
				Assert.AreEqual("m1", bd.FromBytes<SampleAfterDeserialization>(result0).X);
				Assert.AreEqual("m231", bd.FromBytes<SampleAfter2>(result1).X);
			});
		}

		[TestMethod]
		public void TestMerge()
		{
			var bs = new BinarySerializer();

			var v1 = new SampleMerge();
			v1.DI.Add(3, 4);
			v1.LI.Add(33);
			v1.M = new Sample1 { X = 768, Y = "ttt" };

			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleMerge)) + " 03 00 " +
				XS("DI") + " 22 05 05 " +
				XS("LI") + " 21 05 " + XS("M", RoughType.Record) +
				" 01 00 01 00 00 00 03 00 00 00 04 00 00 00 02 00 01 00 00 00 21 00 00 00 03 00 02 00 " +
				XS(typeof(Sample1)) + " 02 00 " + XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 00 03 00 00 00 00 00 00",
				XS(result1));

			CheckDeserializers(bd => {
				var w1 = new SampleMerge();
				w1.DI.Add(5, 6);
				w1.LI.Add(44);
				w1.M = new Sample1 { X = 999, Y = "qqq" };
				bd.FromBytes(w1, result1);
				CollectionAssert.AreEqual(new Dictionary<int, int> { { 5, 6 }, { 3, 4 } }, w1.DI);
				CollectionAssert.AreEqual(new[] { 44, 33 }, w1.LI);
				Assert.AreEqual(768, w1.M.X);
				Assert.AreEqual("qqq", w1.M.Y);
			});
		}

		[TestMethod]
		public void TestNamespaces()
		{
			var bs = new BinarySerializer();

			var v1 = new YuzuTest2.SampleNamespace { B = new SampleBase { FBase = 3 } };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS("YuzuTest2.SampleNamespace, YuzuTest") + " 01 00 " + XS("B") +
				" 20 01 00 02 00 " + XS(typeof(SampleBase)) + " 01 00 " + XS("0_FBase") +
				" 05 01 00 03 00 00 00 00 00 00 00",
				XS(result1));

			CheckDeserializers(bd => {
				var w1 = bd.FromBytes(result1);
				Assert.AreEqual(3, (w1 as YuzuTest2.SampleNamespace).B.FBase);
			});
		}

		[TestMethod]
		public void TestNestedTypes()
		{
			var bs = new BinarySerializer();

			var v1 = new SampleNested { E = SampleNested.NestedEnum.One, C = new SampleNested.NestedClass() };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleNested)) + " 03 00 " +
				XS("C", RoughType.Record, "E", RoughType.Int, "Z", RoughType.Sequence) + " 05" +
				" 01 00 02 00 " + XS(typeof(SampleNested.NestedClass)) +
				" 01 00 " + XS("Z", RoughType.Int) +
				" 01 00 00 00 00 00 00 00 " +
				"02 00 00 00 00 00 00 00",
				XS(result1));

			CheckDeserializers(bd => {
				var w1 = (SampleNested)bd.FromBytes(result1);
				Assert.AreEqual(v1.E, w1.E);
				Assert.AreEqual(v1.C.Z, w1.C.Z);
			});
		}

		[TestMethod]
		public void TestMemberOfInterface()
		{
			var bs = new BinarySerializer();

			var v1 = new List<ISampleMember>();
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual("21 20 00 00 00 00", XS(result1));

			var bd = new BinaryDeserializer();
			var w1 = new List<ISampleMember>();
			bd.FromBytes(w1, result1);
			Assert.AreEqual(0, w1.Count);

			v1.Add(new SampleMemberI());
			var result2 = bs.ToBytes(v1);
			Assert.AreEqual(
				"21 20 01 00 00 00 01 00 " +
				XS(typeof(SampleMemberI)) + " 01 00 " + XS("X", RoughType.Int) + " 00 00",
				XS(result2));
			bd.FromBytes(w1, result2);
			Assert.AreEqual(71, w1[0].X);

			Assert.AreEqual("21 20 00 00 00 00", XS(bs.ToBytes(new List<SampleMemberAbstract>())));

			var v3 = new List<SampleMemberAbstract> { new SampleMemberConcrete() };
			var result3 = bs.ToBytes(v3);
			Assert.AreEqual(
				"21 20 01 00 00 00 02 00 " +
				XS(typeof(SampleMemberConcrete)) + " 01 00 " + XS("X", RoughType.Int) + " 00 00",
				XS(result3));
			var w3 = new List<SampleMemberAbstract>();
			bd.FromBytes(w3, result3);
			Assert.AreEqual(72, w3[0].X);
		}

		[TestMethod]
		public void TestTopLevelContainerOfNonPrimitiveTypes()
		{
			var bs = new BinarySerializer();
			bs.Options.TagMode = TagMode.Names;
			var bd = new BinaryDeserializer();
			bd.Options.TagMode = TagMode.Names;

			var v1 = new List<SampleDerivedB> { new SampleDerivedB { FB = 10 }, new SampleDerivedB { FB = 20 } };

			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"21 20 02 00 00 00 01 00 " + XS(typeof(SampleDerivedB)) + " 02 00 " +
				XS("FBase", RoughType.Int, "FB", RoughType.Int) +
				" 01 00 00 00 00 00 02 00 0A 00 00 00 00 00 " +
				"01 00" +
				" 01 00 00 00 00 00 02 00 14 00 00 00 00 00",
				XS(result1));
			var w1 = (List<object>)bd.FromBytes(result1);
			for (int i = 0; i < v1.Count; i++)
				Assert.AreEqual(v1[i].FB, (w1[i] as SampleDerivedB).FB);

			var v2 = new Dictionary<string, object> {
				{ "3", new SampleDerivedB { FB = 10 } },
				{ "7", new SampleDerivedB { FB = 20 } } };

			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"\n22 10 11 02 00 00 00" +
				" 01 33 20 01 00 01 00 00 00 00 00 02 00 0A 00 00 00 00 00" +
				" 01 37 20 01 00 01 00 00 00 00 00 02 00 14 00 00 00 00 00",
				"\n"+XS(result2));
			var w2 = (Dictionary<string, object>)bd.FromBytes(result2);
			foreach (var i in v2)
				Assert.AreEqual((i.Value as SampleDerivedB).FB, (w2[i.Key] as SampleDerivedB).FB);
		}

		[TestMethod]
		public void TestAssemblies()
		{
			var bs = new BinarySerializer();

			var v1 = new List<SampleAssemblyBase> {
				new SampleAssemblyDerivedQ { Q = 10 },
				new SampleAssemblyDerivedR { R = "R1" } };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"21 20 02 00 00 00 01 00 " + XS("YuzuTestAssembly.SampleAssemblyDerivedQ, AssemblyTest") + " 02 00 " +
				XS("P", RoughType.Short, "Q", RoughType.Short) +
				" 02 00 0A 00 00 00" +
				" 02 00 " + XS(typeof(SampleAssemblyDerivedR)) + " 02 00 " +
				XS("P", RoughType.Short, "R", RoughType.String) +
				" 02 00 " + XS("R1") + " 00 00",
				XS(result1));

			CheckDeserializers(bd => {
				var w1 = new List<SampleAssemblyBase>();
				bd.FromBytes(w1, result1);
				Assert.AreEqual((v1[0] as SampleAssemblyDerivedQ).Q, (w1[0] as SampleAssemblyDerivedQ).Q);
				Assert.AreEqual((v1[1] as SampleAssemblyDerivedR).R, (w1[1] as SampleAssemblyDerivedR).R);
			});
		}

		[TestMethod]
		public void TestUnknown()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var bd1 = new BinaryDeserializer();
			var data1 = SX(
				"20 01 00 " + XS("NewType1") + " 02 00 " + XS("a", RoughType.Int, "b", RoughType.String) +
				" 01 00 07 07 00 00 00 00");
			var w1 = (YuzuUnknown)bd.FromBytes<object>(data1);
			Assert.AreEqual("NewType1", w1.ClassTag);
			Assert.AreEqual(1, w1.Fields.Count);
			Assert.AreEqual(7 * 256 + 7, w1.Fields["a"]);
			CollectionAssert.AreEqual(data1, bs.ToBytes(w1));
			dynamic d1 = bd1.FromBytes(data1);
			Assert.AreEqual("NewType1", d1.ClassTag);
			Assert.AreEqual(1, d1.Fields.Count);
			Assert.AreEqual(7 * 256 + 7, d1.a);

			var data2 = SX("20 01 00 02 00 " + XS("qwe") + " 00 00");
			var w2 = (YuzuUnknown)bd.FromBytes(data2);
			Assert.AreEqual("NewType1", w2.ClassTag);
			Assert.AreEqual(1, w2.Fields.Count);
			Assert.AreEqual("qwe", w2.Fields["b"]);
			CollectionAssert.AreEqual(data2, bs.ToBytes(w2));

			var data3 = SX(
				"20 02 00 " + XS(typeof(SampleBool)) + " 02 00 " + XS("B", RoughType.Bool, "a", RoughType.Record) +
				" 01 00 01 02 00 03 00 " + XS("NewType2") + " 00 00 00 00 00 00");
			bd.Options.AllowUnknownFields = true;
			var w3 = bd.FromBytes<SampleBool>(data3);
			Assert.AreEqual(true, w3.B);

			var data4 = SX(
				"22 10 20 01 00 00 00 " + XS("zz") +
				" 04 00 " + XS("NewType3") + " 01 00 " + XS("Fld", RoughType.SByte) +
				" 01 00 70 00 00");
			var w4 = (Dictionary<string, object>)bd.FromBytes(data4);

			Assert.AreEqual(1, w4.Count);
			var w4i = w4.First();
			Assert.AreEqual("zz", w4i.Key);
			Assert.IsInstanceOfType(w4i.Value, typeof(YuzuUnknown));
			CollectionAssert.AreEqual(
				new SortedDictionary<string, object> { { "Fld", (sbyte)(7 * 16) } },
				((YuzuUnknown)w4i.Value).Fields);
		}

		[TestMethod]
		public void TestUnknownDictOfLists()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			{
				bs.Options.AllowEmptyTypes = true;
				bs.Options.Meta = SampleUnknownDictOfLists.Override();
				var actual = bs.ToBytes(SampleUnknownDictOfLists.Sample);
				var expected = SX(
					"20 01 00 " + XS("Something") + " 01 00 " + XS("F", RoughType.Mapping) + " 05 21 20" +
					" 01 00 01 00 00 00 07 00 00 00 01 00 00 00 02 00 " +
					XS(typeof(SampleBool)) + " 01 00 " + XS("B", RoughType.Bool) +
					" 01 00 01 00 00 00 00");
				Assert.AreEqual("\n" + XS(expected), "\n" + XS(actual));
				var w1 = (YuzuUnknown)bd.FromBytes<object>(expected);
				var w1e = (List<object>)((Dictionary<int, object>)w1.Fields["F"])[7];
				Assert.IsTrue(((SampleBool)w1e[0]).B);
			}
		}

		[TestMethod]
		public void TestAllowReadingFromAncestor()
		{
			var bs = new BinarySerializer();
			var v1 = new Sample2 { X = 83, Y = "83" };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(Sample2)) + " 02 00 " + XS("X", RoughType.Int, "Y", RoughType.String) +
				" 01 00 53 00 00 00 00 00", XS(result1));

			var w1 = new Sample2Allow();
			var bd = new BinaryDeserializer();
			bd.FromBytes(w1, result1);
			Assert.AreEqual(v1.X, w1.X);
		}

		[TestMethod]
		public void TestMultithreading()
		{
			var threadTestData = new byte[50][];
			var threads = new Task[threadTestData.Length];
			for (int i = 0; i < threads.Length; ++i) {
				var j = i;
				threads[i] = new Task(() => {
					var bs = new BinarySerializer();
					threadTestData[j] = bs.ToBytes(new object[] {
						new Sample1 { X = j },
						new Sample2(),
						new Sample3(),
						new Sample4(),
					});
				});
				threads[i].Start();
			}
			foreach (var t in threads)
				t.Wait(1000);
			for (int i = 0; i < threads.Length; ++i) {
				var j = i;
				threads[i] = new Task(() => {
					var bd = new BinaryDeserializer();
					var w = bd.FromBytes(threadTestData[j]);
					Assert.AreEqual(j, ((Sample1)((List<object>)w)[0]).X);
				});
				threads[i].Start();
			}
			foreach (var t in threads)
				t.Wait(1000);
		}

		[TestMethod]
		public void TestSurrogateStr()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v1 = new SampleSurrogateColor { R = 255, G = 0, B = 161 };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(XS(RoughType.String) + " " + XS("#FF00A1"), XS(result1));

			var w1 = bd.FromBytes<SampleSurrogateColor>(result1);
			Assert.AreEqual(v1.R, w1.R);
			Assert.AreEqual(v1.G, w1.G);
			Assert.AreEqual(v1.B, w1.B);
			return;

			/*
			var v2 = new SampleSurrogateColorIf { R = 77, G = 88, B = 99 };
			Assert.AreEqual("[\n99,\n88,\n77\n]", js.ToString(v2));

			var v3 = new SampleSurrogateColorIfDerived { R = 55, G = 66, B = 77 };
			Assert.AreEqual("556677", js.ToString(v3));

			SampleSurrogateColorIf.S = true;
			Assert.AreEqual("778899", js.ToString(v2));
			// RequireInt fails at EOF, so add trailing space.
			var w2 = jd.FromString<SampleSurrogateColorIfDerived>("778899 ");
			Assert.AreEqual(v2.R, w2.R);
			Assert.AreEqual(v2.G, w2.G);
			Assert.AreEqual(v2.B, w2.B);
			*/
		}

		[TestMethod]
		public void TestSurrogateClass()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v1 = new SampleSurrogateClass { FB = true };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual(
				"20 01 00 " + XS(typeof(SampleBool)) + " 01 00 " +
				XS("B", RoughType.Bool) + " 01 00 01 00 00",
				XS(result1));
			Assert.AreEqual(
				"21 11 01 00 00 00 20 01 00 01 00 01 00 00", XS(bs.ToBytes(new List<object> { v1 })));
			var w1 = bd.FromBytes<SampleSurrogateClass>(result1);
			Assert.IsTrue(w1.FB);

			var v2 = new SampleCompactSurrogate { X = 24, Y = 25 };
			var result2 = bs.ToBytes(v2);
			Assert.AreEqual(
				"20 02 00 " + XS(typeof(SamplePoint)) + " 02 00 " +
				XS("X", RoughType.Int, "Y", RoughType.Int) + " 18 00 00 00 19 00 00 00",
				XS(result2));
			Assert.AreEqual(
				"21 11 01 00 00 00 20 02 00 18 00 00 00 19 00 00 00",
				XS(bs.ToBytes(new List<object> { v2 })));
			var w2 = bd.FromBytes<SampleCompactSurrogate>(result2);
			Assert.AreEqual(v2.X, w2.X);
			Assert.AreEqual(v2.Y, w2.Y);
		}

		[TestMethod]
		public void TestSurrogateHash()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();

			var v1 = new SampleSurrogateHashSet { 'a', 'z', 'x' };
			var result1 = bs.ToBytes(v1);
			Assert.AreEqual("10 " + XS("axz"), XS(result1));

			var w1 = bd.FromBytes<SampleSurrogateHashSet>(result1);
			CollectionAssert.AreEqual(v1.OrderBy(ch => ch).ToList(), w1.OrderBy(ch => ch).ToList());
		}

		[TestMethod]
		public void TestSignature()
		{
			var bs = new BinarySerializer();
			bs.BinaryOptions.AutoSignature = true;
			var result1 = bs.ToBytes(17);
			Assert.AreEqual("59 42 30 31 05 11 00 00 00", XS(result1));
			var bd = new BinaryDeserializer();
			bd.BinaryOptions.AutoSignature = true;
			Assert.AreEqual(17, bd.FromBytes<int>(result1));

			XAssert.Throws<YuzuException>(() => bd.FromBytes(new byte[] { 0x05, 0x11, 0, 0, 0 }), "ignature");

			var ms = new MemoryStream(new byte[] { 0x05, 0x12, 0, 0, 0 });
			bd.Reader = new BinaryReader(ms);
			Assert.IsFalse(bd.IsValidSignature());
			bd.BinaryOptions.AutoSignature = false;
			Assert.AreEqual(18, bd.FromReader<int>(bd.Reader));
		}

		[TestMethod]
		public void TestClassAlias()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var bdg = new BinaryDeserializerGen();
			{
				var v1 = new SampleAlias { X = 77 };
				var result1 = bs.ToBytes(v1);
				Assert.AreEqual(
					"20 01 00 " + XS("DifferentName") + " 01 00 " + XS("X", RoughType.Int) +
					" 01 00 4D 00 00 00 00 00",
					XS(result1));
				var w1 = bd.FromBytes<SampleAlias>(result1);
				Assert.AreEqual(v1.X, w1.X);
			}
			{
				var v2 = new SampleAliasMany { X = 76 };
				var result2 = bs.ToBytes(v2);
				Assert.AreEqual(
					"\n20 02 00 " + XS(typeof(SampleAliasMany)) + " 01 00 " + XS("X", RoughType.Int) +
					" 01 00 4C 00 00 00 00 00",
					"\n" + XS(result2));
				var w2 = bd.FromBytes<SampleAliasMany>(result2);
				Assert.AreEqual(v2.X, w2.X);

				var w2n1 = bd.FromBytes<SampleAliasMany>(SX(
					"20 03 00 " + XS("Name1") + " 01 00 " + XS("X", RoughType.Int) +
					" 01 00 4C 00 00 00 00 00"
				));
				Assert.AreEqual(v2.X, w2n1.X);

				var w2n2 = bdg.FromBytes<SampleAliasMany>(SX(
					"20 01 00 " + XS("Name2") + " 01 00 " + XS("X", RoughType.Int) +
					" 01 00 4C 00 00 00 00 00"
				));
				Assert.AreEqual(v2.X, w2n2.X);
			}
		}

		[TestMethod]
		public void TestClassAliasNested()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			{
				var v1 = new SampleWithAliasedField { F = new SampleAliasField { X = 99 } };
				var expected =
					"20 01 00 " + XS(typeof(SampleWithAliasedField)) + " 01 00 " + XS("F", RoughType.Record) +
					" 01 00 02 00 " + XS("NewNameForAliasField") + " 01 00 " + XS("X", RoughType.Int) +
					" 01 00 63 00 00 00 00 00 00 00";
				var w1 = bd.FromBytes<SampleWithAliasedField>(SX(expected));
				Assert.AreEqual(v1.F.X, w1.F.X);
				var result1 = bs.ToBytes(v1);
				Assert.AreEqual("\n" + expected, "\n" + XS(result1));
			}

		}

		[TestMethod]
		public void TestAliasForNestedClassWhenEnclosingClassRenamed()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			var v1 = EnclosingClassForEnclosingClass.Sample;
			var result1 = bs.ToBytes(v1);
			var expected =
				"20 01 00 " + XS(typeof(EnclosingClassForEnclosingClass)) +
				" 01 00 01 46 20 01 00 02 00 " +
				XS("YuzuTest.SampleAliasForNestedClassWhenEnclosingClassRenamed, YuzuTest") +
				" 01 00 " +
				XS(nameof(SampleAliasForNestedClassWhenEnclosingClassRenamed_Renamed.NestedClassField)) +
				" 20 01 00 03 00 " +
				XS("YuzuTest.SampleAliasForNestedClassWhenEnclosingClassRenamed+NestedClass, YuzuTest") +
				" 01 00 01 46 05 01 00 9A 02 00 00 00 00 00 00 00 00";
			Assert.AreEqual(expected, XS(result1));
			var w1 = bd.FromBytes<EnclosingClassForEnclosingClass>(SX(expected));
			Assert.AreEqual(v1.F.NestedClassField.F, w1.F.NestedClassField.F);
		}

		[TestMethod]
		public void TestAliasForGenericArguments()
		{
			var bs = new BinarySerializer();
			var bd = new BinaryDeserializer();
			{
				var result1 = bs.ToBytes(RenameDictionaryValue.Data);
				var expected =
					"20 01 00 " + XS(typeof(RenameDictionaryValue)) +
					" 01 00 " + XS(nameof(RenameDictionaryValue.Samples)) +
					" 22 05 20 01 00 01 00 00 00 01 00 00 00 02 00 " +
					XS("YuzuTest.RenameDictionaryValue+Sample, YuzuTest") +
					" 01 00 01 46 05 01 00 01 00 00 00 00 00 00 00";
				Assert.AreEqual(expected, XS(result1));
				var w1 = bd.FromBytes<RenameDictionaryValue>(result1);
				Assert.AreEqual(RenameDictionaryValue.Data.Samples[1].F, w1.Samples[1].F);
			}
			{
				var result1 = bs.ToBytes(RenameDictionaryKey.Data);
				var expected =
					"20 03 00 " + XS(typeof(RenameDictionaryKey)) + " 01 00 " +
					XS(nameof(RenameDictionaryKey.Samples)) +
					" 22 20 05 01 00 01 00 00 00 04 00 " +
					XS("YuzuTest.RenameDictionaryKey+Sample, YuzuTest") +
					" 01 00 01 46 05 01 00 02 00 00 00 00 00 02 00 00 00 00 00";
				Assert.AreEqual(expected, XS(result1));
				var w1 = bd.FromBytes<RenameDictionaryKey>(result1);
				var k = new RenameDictionaryKey.Sample_Renamed { F = 2 };
				Assert.AreEqual(RenameDictionaryKey.Data.Samples[k], w1.Samples[k]);
			}
			{
				var result1 = bs.ToBytes(RenameListType.Data);
				var expected =
					"20 05 00 " + XS(typeof(RenameListType)) + " 01 00 " +
					XS(nameof(RenameListType.Samples)) + " 21 20 01 00 01 00 00 00 06 00 " +
					XS("YuzuTest.RenameListType+Sample, YuzuTest") +
					" 01 00 01 46 05 01 00 03 00 00 00 00 00 00 00";
				Assert.AreEqual(expected, XS(result1));
				var w1 = bd.FromBytes<RenameListType>(result1);
				Assert.AreEqual(RenameListType.Data.Samples[0].F, w1.Samples[0].F);
			}
			{
				var result1 = bs.ToBytes(RenameHashSetType.Data);
				var expected =
					"20 07 00 " + XS(typeof(RenameHashSetType)) + " 01 00 " +
					XS(nameof(RenameHashSetType.Samples)) + " 21 20 01 00 01 00 00 00 08 00 " +
					XS("YuzuTest.RenameHashSetType+Sample, YuzuTest") +
					" 01 00 01 46 05 01 00 04 00 00 00 00 00 00 00";
				Assert.AreEqual(expected, XS(result1));
				var w1 = bd.FromBytes<RenameHashSetType>(result1);
				Assert.AreEqual(RenameHashSetType.Data.Samples.First().F, w1.Samples.First().F);
			}

			{
				var result1 = bs.ToBytes(RenameCustomGenericType.Data);
				var alias = "YuzuTest.RenameCustomGenericType+GenericSample`1[[YuzuTest.RenameCustomGenericType+Sample, YuzuTest]], YuzuTest";
				var expected =
					"20 09 00 " + XS(typeof(RenameCustomGenericType)) + " 01 00 " +
					XS(nameof(RenameCustomGenericType.Samples)) + " 20" +
					" 01 00 0A 00 " + XS(alias) +
					" 01 00 " + XS("Type") + " 20 01 00 0B 00 " +
					XS(typeof(RenameCustomGenericType.Sample)) +
					" 01 00 01 46 05 01 00 05 00 00 00 00 00 00 00 00 00";
				Assert.AreEqual("\n" + expected, "\n" + XS(result1));
				var w1 = bd.FromBytes<RenameCustomGenericType>(result1);
				Assert.AreEqual(RenameCustomGenericType.Data.Samples.Type.F, w1.Samples.Type.F);
			}
			{
				var result1 = bs.ToBytes(RenameCustomGenericTypeGenericArgumentType.Data);
				var expected = 
					"20 0C 00 " + XS(typeof(RenameCustomGenericTypeGenericArgumentType)) + " 01 00 " +
					XS(nameof(RenameCustomGenericTypeGenericArgumentType.Samples)) + " 20 01 00 0D 00 " +
					XS("YuzuTest.RenameCustomGenericTypeGenericArgumentType+GenericSample`1[[YuzuTest.RenameCustomGenericTypeGenericArgumentType+Sample, YuzuTest]], YuzuTest") +
					" 01 00 " + XS("Type") + " 20 01 00 0E 00 " +
					XS("YuzuTest.RenameCustomGenericTypeGenericArgumentType+Sample, YuzuTest") + " 01 00 01 46 05 01 00 06 00 00 00 00 00 00 00 00 00";
				Assert.AreEqual("\n" + expected, "\n" + XS(result1));
				var w1 = bd.FromBytes<RenameCustomGenericTypeGenericArgumentType>(result1);
				Assert.AreEqual(RenameCustomGenericTypeGenericArgumentType.Data.Samples.Type.F, w1.Samples.Type.F);
			}
		}

		[TestMethod]
		public void TestAliasWithinUnknown()
		{
			var bs = new BinarySerializer();
			var bytes = SX("20 01 00 " + XS(typeof(SampleAliasWithinUnknownContainer)) + " 01 00 " +
				XS("Foo") + " 20 01 00 02 00 " +
				XS("YuzuTest.SampleAliasClassToBeRenamed, YuzuTest") + " 02 00 " +
				XS("Bar") + " 20 " + XS("Foo") +
				" 05 01 00 02 00 02 00 BC 01 00 00 00 00 02 00 DE 00 00 00 00 00 00 00");
			var bd = new BinaryDeserializer();
			bd.Options.AllowUnknownFields = true;
			Meta.Get(typeof(SampleAliasClassToBeRenamed_Renamed), bd.Options);
			var r = bd.FromBytes<SampleAliasWithinUnknownContainer>(bytes);
			Assert.AreEqual(1, r.UnknownStorage.Fields.Count);
			Assert.IsInstanceOfType(
				r.UnknownStorage.Fields[0].Value, typeof(SampleAliasClassToBeRenamed_Renamed));
			r.Foo_Renamed = (SampleAliasClassToBeRenamed_Renamed)r.UnknownStorage.Fields[0].Value;
			Assert.AreEqual(222, r.Foo_Renamed.Foo);
			Assert.AreEqual(1, r.Foo_Renamed.UnknownStorage.Fields.Count);
			Assert.IsInstanceOfType(
				r.Foo_Renamed.UnknownStorage.Fields[0].Value, typeof(SampleAliasClassToBeRenamed_Renamed));
			r.Foo_Renamed.Bar_Renamed =
				(SampleAliasClassToBeRenamed_Renamed)r.Foo_Renamed.UnknownStorage.Fields[0].Value;
			Assert.AreEqual(444, r.Foo_Renamed.Bar_Renamed.Foo);
		}

		[TestMethod]
		public void TestFactory()
		{
			var bd = new BinaryDeserializer();
			{
				var result1 = SX(
					"20 01 00 " + XS(typeof(SamplePrivateConstructor)) + " 01 00 " + XS("X", RoughType.Int) +
					" 01 00 47 00 00 00 00 00");
				var w1 = bd.FromBytes<SamplePrivateConstructor>(result1);
				Assert.AreEqual(71, w1.X);
				var w2 = (new BinaryDeserializerGen()).FromBytes<SamplePrivateConstructor>(result1);
				Assert.AreEqual(71, w1.X);
			}
			{
				var w1 = bd.FromBytes<SampleConstructorParam>(SX(
					"20 02 00 " + XS(typeof(SampleConstructorParam)) + " 01 00 " + XS("X", RoughType.Int) +
					" 00 00"));
				Assert.AreEqual(72, w1.X);
			}
		}

		[TestMethod]
		public void TestErrors()
		{
			var bs = new BinarySerializer();
			XAssert.Throws<YuzuException>(() => bs.ToBytes(new object()), "unknown");

			var bd = new BinaryDeserializer();
			bd.Options.AllowEmptyTypes = true;
			var bdg = new BinaryDeserializerGenerator();

			XAssert.Throws<YuzuException>(() => bdg.Generate<ISample>(), "ISample");
			XAssert.Throws<YuzuException>(() => bdg.Generate<SampleAbstract>(), "SampleAbstract");
			XAssert.Throws<YuzuException>(() => bd.FromBytes<int>(new byte[] { 0xFF }), "255");

			XAssert.Throws<YuzuException>(() => bd.FromBytes(new byte[] { 0xFF }), "255");
			XAssert.Throws<YuzuException>(() => bd.FromBytes<int>(new byte[] { 0xFF }), "255");
			XAssert.Throws<YuzuException>(() => bd.FromBytes<int>(new byte[] { 07 }), "Int32");

			XAssert.Throws<YuzuException>(() => bd.FromBytes<Sample1>(SX(
				"20 01 00 " + XS("notype") + " 00 00 00 00"
			)), "notype");

			var w = new Sample1();
			XAssert.Throws<YuzuException>(() => bd.FromBytes(w, SX(
				"20 02 00 " + XS(typeof(Empty)) + " 00 00 00 00"
			)), "Sample1");

			var w2 = new Sample2Allow();
			XAssert.Throws<YuzuException>(() => bd.FromBytes(w2, SX(
				"20 02 00 00 00"
			)), "Sample2Allow");

			XAssert.Throws<YuzuException>(() => bd.FromBytes(w, SX("20 05 00")), "5");

			XAssert.Throws<YuzuException>(() => bd.FromBytes(w, SX(
				"20 03 00 " + XS(typeof(Sample1)) + " 00 00 00 00"
			)), " X ");

			XAssert.Throws<YuzuException>(() => bd.FromBytes(w, SX(
				"20 03 00 " + XS(typeof(Empty)) + " 00 01 " + XS("New", RoughType.Int) + " 00 00"
			)), "New");

			XAssert.Throws<YuzuException>(() => bd.FromBytes(w, SX(
				"20 03 00 " + XS(typeof(Sample1)) + " 00 01 " + XS("X", RoughType.String) + " 00 00"
			)), "Int32");

			XAssert.Throws<YuzuException>(() => bd.FromBytes(SX(
				"20 03 00 " + XS(typeof(SampleList)) + " 01 00 " + XS("E", RoughType.String) + " 01 00" +
				" 21 05 00 00 00 00 00 00"
			)), "List");
			XAssert.Throws<YuzuException>(() => bd.FromBytes(SX(
				"20 03 00 " + XS(typeof(SampleList)) + " 01 00 " + XS("E", RoughType.Sequence) + " 05 01 00" +
				" 00 00 00 00 00 00"
			)), "List");
			XAssert.Throws<YuzuException>(() => bd.FromBytes<Sample1>(SX(
				"20 03 00 " + XS(typeof(YuzuTest.Sample2)) + " 02 00 " + XS("X") + " 05 " + XS("Y") +  " 10"
			)), "YuzuTest.Sample2");

		}

		[TestMethod]
		public void TestSurrogatesWithGenericsSimple()
		{
			var b1Etalon = SX("20 01 00 " + XS(typeof(SurrogateDictionaryKey)) + " 01 00 " +
				XS(nameof(SurrogateDictionaryKey.F)) +
				" 22 05 05 01 00 03 00 00 00 01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00 05 00 00 00 06 00 00 00 00 00");

			var b2Etalon = SX("20 02 00 " + XS(typeof(SurrogateDictionaryValue)) + " 01 00 " +
				XS(nameof(SurrogateDictionaryValue.F)) +
				" 22 05 05 01 00 03 00 00 00 07 00 00 00 08 00 00 00 09 00 00 00 0A 00 00 00 0B 00 00 00 0C 00 00 00 00 00");

			var b3Etalon = SX("20 03 00 " + XS(typeof(SurrogateListElement)) + " 01 00 " +
				XS(nameof(SurrogateListElement.F)) +
				" 21 05 01 00 04 00 00 00 01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00 00 00");

			var b4Etalon = SX("20 04 00 " + XS(typeof(SurrogateHashSetElement)) + " 01 00 " +
				XS(nameof(SurrogateHashSetElement.F)) +
				" 21 05 01 00 04 00 00 00 06 00 00 00 07 00 00 00 08 00 00 00 09 00 00 00 00 00");

			var b5Etalon = SX("20 05 00 " + XS(typeof(SurrogateCustomGenericArgument)) + " 01 00 " +
				XS(nameof(SurrogateCustomGenericArgument.F)) + " 20 01 00 06 00 " +
				XS(Yuzu.Util.TypeSerializer.Serialize(typeof(SurrogateCustomGenericArgument.Generic<string>))) + " 01 00 " +
				XS(nameof(SurrogateCustomGenericArgument.Generic<string>.F)) + " 10 01 00 02 34 37 00 00 00 00");

			var b6Etalon = SX("20 07 00 " + XS(typeof(SurrogateCustomGeneric)) + " 01 00 " +
				XS(nameof(SurrogateCustomGeneric.F)) + " 10 01 00 02 33 31 00 00");

			var bd = new BinaryDeserializer();
			var s1 = bd.FromBytes<SurrogateDictionaryKey>(b1Etalon);
			Assert.IsTrue(s1.F[new TypedNumberSample(1)] == 2);
			Assert.IsTrue(s1.F[new TypedNumberSample(3)] == 4);
			Assert.IsTrue(s1.F[new TypedNumberSample(5)] == 6);
			var s2 = bd.FromBytes<SurrogateDictionaryValue>(b2Etalon);
			Assert.IsTrue(s2.F[7] == new TypedNumberSample(8));
			Assert.IsTrue(s2.F[9] == new TypedNumberSample(10));
			Assert.IsTrue(s2.F[11] == new TypedNumberSample(12));
			var s3 = bd.FromBytes<SurrogateListElement>(b3Etalon);
			Assert.IsTrue(s3.F.SequenceEqual(new List<TypedNumberSample> {
				new TypedNumberSample(1), new TypedNumberSample(2),
				new TypedNumberSample(3), new TypedNumberSample(4)
			}));
			var s4 = bd.FromBytes<SurrogateHashSetElement>(b4Etalon);
			Assert.IsTrue(s4.F.Contains(new TypedNumberSample(6)));
			Assert.IsTrue(s4.F.Contains(new TypedNumberSample(7)));
			Assert.IsTrue(s4.F.Contains(new TypedNumberSample(8)));
			Assert.IsTrue(s4.F.Contains(new TypedNumberSample(9)));
			Assert.IsTrue(s4.F.Count == 4);
			var s5 = bd.FromBytes<SurrogateCustomGenericArgument>(b5Etalon);
			Assert.IsTrue(s5.F.F.Number == 47);
			var s6 = bd.FromBytes<SurrogateCustomGeneric>(b6Etalon);
			Assert.IsTrue(s6.F.F == 31);
		}

	}
}
