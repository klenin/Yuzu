using System;
using System.Collections.Generic;
using System.IO;

using Yuzu.Metadata;
using Yuzu.Util;

namespace Yuzu.Clone
{

	public class ClonerGenBase : Cloner
	{
		protected static Dictionary<Type, Func<Cloner, object, object>> clonerCache =
			new Dictionary<Type, Func<Cloner, object, object>>();
		public ClonerGenBase(): base(clonerCache) { }
	}

	public class ClonerGenerator : IGenerator
	{
		private CodeWriter cw = new CodeWriter();
		private string wrapperNameSpace;
		private CommonOptions options;
		private Dictionary<Type, string> generatedCloners = new Dictionary<Type, string>();

		public string LineSeparator { get { return cw.LineSeparator; } set { cw.LineSeparator = value; } }

		public StreamWriter GenWriter
		{
			get { return cw.Output; }
			set { cw.Output = value; }
		}

		public ClonerGenerator(string wrapperNameSpace = "YuzuGenClone", CommonOptions? options = null)
		{
			this.wrapperNameSpace = wrapperNameSpace;
			this.options = options ?? new CommonOptions();
		}

		public void GenerateHeader()
		{
			cw.Put("using System;\n");
			cw.Put("using System.Reflection;\n");
			cw.Put("\n");
			cw.Put("using Yuzu;\n");
			cw.Put("using Yuzu.Clone;\n");
			cw.Put("\n");
			cw.Put("namespace {0}\n", wrapperNameSpace);
			cw.Put("{\n");
			cw.Put("public class ClonerGen: ClonerGenBase\n");
			cw.Put("{\n");
		}

		public void GenerateFooter()
		{
			cw.Put("static ClonerGen()\n");
			cw.Put("{\n");
			foreach (var r in generatedCloners)
				cw.Put("clonerCache[typeof({0})] = {1};\n", Utils.GetTypeSpec(r.Key), r.Value);
			cw.Put("}\n");
			cw.Put("}\n"); // Close class.
			cw.Put("}\n"); // Close namespace.
		}

		private string GenerateFactoryCall(Meta meta) =>
			meta.FactoryMethod == null ?
				String.Format("new {0}()", Utils.GetTypeSpec(meta.Type)) :
				String.Format("{0}.{1}()", Utils.GetTypeSpec(meta.Type), meta.FactoryMethod.Name);

		private void GenerateCloneItem(Meta meta, Meta.Item yi)
		{
			if (Cloner.IsCopyable(yi.Type)) {
				cw.Put("result.{0} = s.{0};\n", yi.Name);
				return;
			}
			string generatedClonerName;
			if (generatedCloners.TryGetValue(yi.Type, out generatedClonerName)) {
				cw.Put("result.{0} = ({1}){2}(cl, s.{0});\n",
					yi.Name, Utils.GetTypeSpec(yi.Type), generatedClonerName);
				return;
			}
			if (yi.Type.IsArray) {
				var e = yi.Type.GetElementType();
				if (Cloner.IsCopyable(e)) {
					cw.Put("result.{0} = cl.CloneArrayPrimitive<{1}>(s.{0});\n",
						yi.Name, Utils.GetTypeSpec(e));
				}
				else {
					cw.Put("result.{0} = cl.CloneArray(s.{0});\n", yi.Name, Utils.GetTypeSpec(e));
				}
				return;
			}
			{
				var idict = Utils.GetIDictionary(yi.Type);
				if (idict != null) {
					var a = idict.GetGenericArguments();
					if (!Cloner.IsCopyable(a[0])) {
						var tempKeyClonerName = cw.GetTempName();
						var tempValueClonerName = cw.GetTempName();
						cw.Put("var {0} = cl.GetCloner(typeof({1}));\n",
							tempKeyClonerName, Utils.GetTypeSpec(a[0]));
						cw.Put("var {0} = cl.GetCloner(typeof({1}));\n",
							tempValueClonerName, Utils.GetTypeSpec(a[1]));
						cw.Put("result.{0} = cl.CloneIDictionary<{1}, {2}, {3}>(s.{0}, {4}, {5});\n",
							yi.Name, Utils.GetTypeSpec(yi.Type),
							Utils.GetTypeSpec(a[0]), Utils.GetTypeSpec(a[1]),
							tempKeyClonerName, tempValueClonerName);
					}
					else if (!Cloner.IsCopyable(a[1])) {
						var tempValueClonerName = cw.GetTempName();
						cw.Put("var {0} = cl.GetCloner(typeof({1}));\n",
							tempValueClonerName, Utils.GetTypeSpec(a[1]));
						cw.Put("result.{0} = cl.CloneIDictionaryPrimiviteKey<{1}, {2}, {3}>(s.{0}, {4});\n",
							yi.Name, Utils.GetTypeSpec(yi.Type),
							Utils.GetTypeSpec(a[0]), Utils.GetTypeSpec(a[1]),
							tempValueClonerName);
					}
					else
						cw.Put("result.{0} = cl.CloneIDictionaryPrimitive(s.{0});\n", yi.Name);
					return;
				}
			}
			{
				var icoll = Utils.GetICollection(yi.Type);
				if (icoll != null) {
					var a = icoll.GetGenericArguments();
					if (!Cloner.IsCopyable(a[0])) {
						var tempClonerName = cw.GetTempName();
						cw.Put("var {0} = cl.GetCloner(typeof({1}));\n",
							tempClonerName, Utils.GetTypeSpec(a[0]));
						cw.Put("result.{0} = cl.CloneCollection<{1}, {2}>(s.{0}, {3});\n",
							yi.Name, Utils.GetTypeSpec(yi.Type), Utils.GetTypeSpec(a[0]), tempClonerName);
					}
					else
						cw.Put("result.{0} = cl.CloneCollectionPrimitive(s.{0});\n", yi.Name);
				}
				return;
			}

			cw.Put("result.{0} = ({1})cl.GetCloner(typeof({1}))(s.{0});\n",
				yi.Name, Utils.GetTypeSpec(yi.Type));
		}

		private void GenerateClonerBody(Meta meta)
		{
			cw.ResetTempNames();
			foreach (var yi in meta.Items)
				GenerateCloneItem(meta, yi);
		}

		public void Generate<T>() { Generate(typeof(T)); }

		public void Generate(Type t)
		{
			if (t.IsInterface)
				throw new YuzuException("Useless ClonerGenerator for interface " + t.FullName);
			if (t.IsAbstract)
				throw new YuzuException("Useless ClonerGenerator for abstract class " + t.FullName);

			var meta = Meta.Get(t, options);

			var clonerName = "Clone_" + Utils.GetMangledTypeNameNS(t);
			cw.Put("private static object {0}(Cloner cl, object src)\n", clonerName);
			cw.Put("{\n");
			cw.Put("var result = {0};\n", GenerateFactoryCall(meta));
			cw.Put("var s = ({0})src;\n", Utils.GetTypeSpec(t));
			GenerateClonerBody(meta);
			cw.Put("return result;\n");
			cw.Put("}\n");
			cw.Put("\n");
			generatedCloners[t] = clonerName;
		}
	}
}
