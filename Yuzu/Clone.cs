using Yuzu.Metadata;

namespace Yuzu.Clone
{
	public class Cloner
	{
		public CommonOptions Options;

		public T ShallowClone <T>(T obj) => (T)ShallowCloneObject(obj);

		public object ShallowCloneObject(object obj)
		{
			var meta = Meta.Get(obj.GetType(), Options);
			var result = meta.Factory();
			foreach (var item in meta.Items) {
				item.SetValue(result, item.GetValue(obj));
			}
			return result;
		}
	}
}