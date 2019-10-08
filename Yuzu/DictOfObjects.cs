using System.Collections.Generic;
using System.Linq;

using Yuzu.Metadata;

namespace Yuzu.DictOfObjects
{
	public static class DictOfObjects
	{
		public static CommonOptions Options = new CommonOptions();

		public static Dictionary<string, object> Pack<T>(T obj) =>
			Meta.Get(typeof(T), Options).Items.ToDictionary(yi => yi.Name, yi => yi.GetValue(obj));

		public static T Unpack<T>(Dictionary<string, object> d)
		{
			var meta = Meta.Get(typeof(T), Options);
			var result = meta.Factory();
			foreach (var yi in meta.Items) {
				if (d.TryGetValue(yi.Name, out object itemValue))
					yi.SetValue(result, itemValue);
			}
			return (T)result;
		}
	}
}