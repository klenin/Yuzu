using System.Collections.Generic;

using Yuzu;

namespace YuzuTest
{
	[YuzuAlias("SampleOrder")]
	class SampleOrder
	{
		[YuzuMember]
		public int StarterPackOfferEndTime;
		[YuzuMember]
		public bool StartGoldInitialized;
	}

	[YuzuAlias("SampleOrder")]
	class SampleOrderExt : SampleOrder
	{
		[YuzuMember]
		public bool StarterPackOffered;
	}

	[YuzuAlias("NewNameForAliasField")]
	class SampleAliasField
	{
		[YuzuMember]
		public int X;
	}

	class SampleWithAliasedField
	{
		[YuzuMember]
		public SampleAliasField F;
	}

	[YuzuAlias("DifferentName")]
	public class SampleAlias
	{
		[YuzuRequired]
		public int X;
	}

	[YuzuAlias(read: new string[] { "Name1", "Name2" })]
	public class SampleAliasMany
	{
		[YuzuRequired]
		public int X;
	}

	public class RenameDictionaryValue
	{
		[YuzuAlias("YuzuTest.RenameDictionaryValue+Sample, YuzuTest")]
		public class Sample_Renamed
		{
			[YuzuMember]
			public int F;
		}
		[YuzuMember]
		public Dictionary<int, Sample_Renamed> Samples = new Dictionary<int, Sample_Renamed>();

		public static RenameDictionaryValue Sample = new RenameDictionaryValue {
			Samples = new Dictionary<int, Sample_Renamed> { { 1, new Sample_Renamed { F = 1 } } }
		};
	}

	public class RenameDictionaryKey
	{
		[YuzuAlias("YuzuTest.RenameDictionaryKey+Sample, YuzuTest")]
		public struct Sample_Renamed
		{
			[YuzuMember]
			public int F;
		}
		[YuzuMember]
		public Dictionary<Sample_Renamed, int> Samples = new Dictionary<Sample_Renamed, int>();

		public static RenameDictionaryKey Sample = new RenameDictionaryKey {
			Samples = new Dictionary<Sample_Renamed, int> { { new Sample_Renamed { F = 2 }, 2 } }
		};
	}

	public class RenameListType
	{
		[YuzuAlias("YuzuTest.RenameListType+Sample, YuzuTest")]
		public class Sample_Renamed
		{
			[YuzuMember]
			public int F;
		}
		[YuzuMember]
		public List<Sample_Renamed> Samples = new List<Sample_Renamed>();

		public static RenameListType Sample = new RenameListType {
			Samples = new List<Sample_Renamed> { new Sample_Renamed { F = 3 } }
		};
	}

	public class RenameHashSetType
	{
		[YuzuAlias("YuzuTest.RenameHashSetType+Sample, YuzuTest")]
		public class Sample_Renamed
		{
			[YuzuMember]
			public int F;
		}
		[YuzuMember]
		public HashSet<Sample_Renamed> Samples = new HashSet<Sample_Renamed>();

		public static RenameHashSetType Sample = new RenameHashSetType {
			Samples = new HashSet<Sample_Renamed> { new Sample_Renamed { F = 4 } }
		};
	}

	public class RenameCustomGenericType
	{
		public class Sample
		{
			[YuzuMember]
			public int F;
		}

		[YuzuAlias("YuzuTest.RenameCustomGenericType+GenericSample`1, YuzuTest")]
		public class GenericSample_Renamed<T>
		{
			[YuzuMember]
			public T Type;

			public static string TypeFieldName => nameof(Type);
		}
		[YuzuMember]
		public GenericSample_Renamed<Sample> Samples = new GenericSample_Renamed<Sample>();
	}

	public class RenameCustomGenericTypeGenericArgumentType
	{
		[YuzuAlias("YuzuTest.RenameCustomGenericTypeGenericArgumentType+Sample, YuzuTest")]
		public class Sample_Renamed
		{
			[YuzuMember]
			public int F;
		}
		public class GenericSample<T>
		{
			[YuzuMember]
			public T Type;
		}
		[YuzuMember]
		public GenericSample<Sample_Renamed> Samples = new GenericSample<Sample_Renamed>();
	}

	public class EnclosingClassForEnclosingClass
	{
		[YuzuMember]
		public SampleAliasForNestedClassWhenEnclosingClassRenamed_Renamed F;

		public static EnclosingClassForEnclosingClass Sample = new EnclosingClassForEnclosingClass {
			F = new SampleAliasForNestedClassWhenEnclosingClassRenamed_Renamed {
				NestedClassField = new SampleAliasForNestedClassWhenEnclosingClassRenamed_Renamed.NestedClass { F = 666 }
			}
		};
	}

	[YuzuAlias("YuzuTest.SampleAliasForNestedClassWhenEnclosingClassRenamed, YuzuTest")]
	public class SampleAliasForNestedClassWhenEnclosingClassRenamed_Renamed
	{
		[YuzuAlias("YuzuTest.SampleAliasForNestedClassWhenEnclosingClassRenamed+NestedClass, YuzuTest")]
		public class NestedClass
		{
			[YuzuMember]
			public int F;
		}
		[YuzuMember]
		public NestedClass NestedClassField;
	}

}