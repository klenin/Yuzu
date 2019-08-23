using System;
using System.Collections.Generic;
using System.Linq;

using Yuzu.Metadata;

namespace Yuzu.Clone
{
	internal class ObjectItemsWrapper
	{
		protected Cloner cl;
		protected List<Meta.Item> copyable;
		protected List<Meta.Item> copyableIf;
		protected Action<object, object>[] cloners;
		protected Meta meta;

		private Action<object, object> MakeFieldCloner(Meta.Item yi)
		{
			if (yi.SetValue != null) {
				var cloner = cl.GetCloner(yi.Type);
				if (yi.SerializeCond != null)
					return (dst, src) => {
						var v = yi.GetValue(src);
						if (yi.SerializeCond(src, v))
							yi.SetValue(dst, cloner(v));
					};
				else
					return (dst, src) => yi.SetValue(dst, cloner(yi.GetValue(src)));
			}
			else {
				var merger = cl.GetMerger(yi.Type);
				if (yi.SerializeCond != null)
					return (dst, src) => {
						var v = yi.GetValue(src);
						if (yi.SerializeCond(src, v))
							merger(yi.GetValue(dst), v);
					};
				else
					return (dst, src) => merger(yi.GetValue(dst), yi.GetValue(src));
			}
		}

		protected void MakeFieldCloners()
		{
			int i = 0;
			foreach (var yi in meta.Items)
				if (!cl.IsCopyable(yi))
					cloners[i++] = MakeFieldCloner(yi);
		}

		protected void CopyCopyable(object dst, object src)
		{
			foreach (var yi in copyable)
				yi.SetValue(dst, yi.GetValue(src));
			foreach (var yi in copyableIf) {
				var v = yi.GetValue(src);
				if (yi.SerializeCond(src, v))
					yi.SetValue(dst, v);
			}
		}

		public ObjectItemsWrapper(Cloner cl, Meta meta)
		{
			this.meta = meta;
			this.cl = cl;
			copyable = meta.Items.Where(yi => cl.IsCopyable(yi) && yi.SerializeCond == null).ToList();
			copyableIf = meta.Items.Where(yi => cl.IsCopyable(yi) && yi.SerializeCond != null).ToList();
			// Initialize 'cloners' lazily to prevent infinite recursion.
			cloners = new Action<object, object>[meta.Items.Count - copyable.Count - copyableIf.Count];
		}
	}

	internal class ObjectCloner : ObjectItemsWrapper
	{
		public ObjectCloner(Cloner cl, Meta meta) : base(cl, meta) { }

		internal Func<object, object> Get()
		{
			// Duplicate code to optimize fast path.
			if (!meta.HasAnyTrigger() && cloners.Length == 0)
				return NoTriggersAllCopyable;
			if (meta.HasAnyTrigger())
				return GeneralCase;
			return NoTriggers;
		}

		internal object NoTriggersAllCopyable(object src)
		{
			if (src == null)
				return null;
			if (src.GetType() != meta.Type)
				return cl.DeepObject(src);
			var result = meta.Factory();
			CopyCopyable(result, src);
			return result;
		}

		internal object NoTriggers(object src)
		{
			if (src == null)
				return null;
			if (src.GetType() != meta.Type)
				return cl.DeepObject(src);
			var result = meta.Factory();
			if (cloners[0] == null)
				MakeFieldCloners();
			CopyCopyable(result, src);
			foreach (var cloner in cloners)
				cloner(result, src);
			return result;
		}

		internal object GeneralCase(object src)
		{
			if (src == null)
				return null;
			if (src.GetType() != meta.Type)
				return cl.DeepObject(src);
			meta.BeforeSerialization.Run(src);
			var result = meta.Factory();
			if (cloners.Length > 0 && cloners[0] == null)
				MakeFieldCloners();
			meta.BeforeDeserialization.Run(result);
			CopyCopyable(result, src);
			foreach (var cloner in cloners)
				cloner(result, src);
			meta.AfterSerialization.Run(src);
			meta.AfterDeserialization.Run(result);
			return result;
		}
	}

	internal class ObjectMerger : ObjectItemsWrapper
	{
		public ObjectMerger(Cloner cl, Meta meta) : base(cl, meta) { }

		internal Action<object, object> Get()
		{
			// Duplicate code to optimize fast path.
			if (!meta.HasAnyTrigger() && cloners.Length == 0)
				return NoTriggersAllCopyable;
			if (meta.HasAnyTrigger())
				return GeneralCase;
			return NoTriggers;
		}

		internal void NoTriggersAllCopyable(object dst, object src)
		{
			if (src == null || dst == null)
				return;
			CopyCopyable(dst, src);
		}

		internal void NoTriggers(object dst, object src)
		{
			if (src == null || dst == null)
				return;
			if (cloners[0] == null)
				MakeFieldCloners();
			CopyCopyable(dst, src);
			foreach (var cloner in cloners)
				cloner(dst, src);
		}

		internal void GeneralCase(object dst, object src)
		{
			if (src == null || dst == null)
				return;
			meta.BeforeSerialization.Run(src);
			var result = meta.Factory();
			if (cloners.Length > 0 && cloners[0] == null)
				MakeFieldCloners();
			meta.BeforeDeserialization.Run(result);
			CopyCopyable(dst, src);
			foreach (var cloner in cloners)
				cloner(result, src);
			meta.AfterSerialization.Run(src);
			meta.AfterDeserialization.Run(result);
		}
	}
}
