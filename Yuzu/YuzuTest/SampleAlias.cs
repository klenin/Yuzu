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

}