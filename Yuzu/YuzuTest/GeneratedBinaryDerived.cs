using System;

using Yuzu.Binary;

namespace YuzuGenBin
{
	public class BinaryDeserializerGenDerived: BinaryDeserializerGen
	{
		private static void Read_YuzuTest__SampleMergeNonPrimitive(BinaryDeserializer d, ReaderClassDef def, object obj)
		{
			var result = (global::YuzuTest.SampleMergeNonPrimitive)obj;
			var dg = (BinaryDeserializerGenDerived)d;
			ReaderClassDef.FieldDef fd;
			fd = def.Fields[d.Reader.ReadInt16()];
			if (1 != fd.OurIndex) throw dg.Error("1!=" + fd.OurIndex);
			var tmp1 = d.Reader.ReadInt32();
			if (tmp1 >= 0) {
				while (--tmp1 >= 0) {
					var tmp2 = d.Reader.ReadInt32();
					var tmp3 = (global::YuzuTest.Sample1)dg.ReadObject<global::YuzuTest.Sample1>();
					result.DI.Add(tmp2, tmp3);
				}
			}
			fd = def.Fields[d.Reader.ReadInt16()];
			if (2 != fd.OurIndex) throw dg.Error("2!=" + fd.OurIndex);
			var tmp4 = d.Reader.ReadInt32();
			if (tmp4 >= 0) {
				while (--tmp4 >= 0) {
					var tmp5 = (global::YuzuTest.Sample1)dg.ReadObject<global::YuzuTest.Sample1>();
					result.LI.Add(tmp5);
				}
			}
			fd = def.Fields[d.Reader.ReadInt16()];
			if (3 == fd.OurIndex) {
				dg.ReadIntoObject<global::YuzuTest.Sample1>(result.M);
				fd = def.Fields[d.Reader.ReadInt16()];
			}
			if (fd.OurIndex != ReaderClassDef.EOF) throw dg.Error("Unfinished object");
		}

		private static object Make_YuzuTest__SampleMergeNonPrimitive(BinaryDeserializer d, ReaderClassDef def)
		{
			var result = new global::YuzuTest.SampleMergeNonPrimitive();
			Read_YuzuTest__SampleMergeNonPrimitive(d, def, result);
			return result;
		}

		static BinaryDeserializerGenDerived()
		{
			readCache[typeof(global::YuzuTest.SampleMergeNonPrimitive)] = Read_YuzuTest__SampleMergeNonPrimitive;
			makeCache[typeof(global::YuzuTest.SampleMergeNonPrimitive)] = Make_YuzuTest__SampleMergeNonPrimitive;
		}
	}
}
