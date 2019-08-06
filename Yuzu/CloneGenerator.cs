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
			foreach (var kv in generatedCloners)
				ActuallyGenerate(kv.Key, kv.Value);
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

		private string GenerateClonerSimple(Type t, string itemName)
		{
			if (Cloner.IsCopyable(t))
				return itemName;
			string itemClonerName;
			if (generatedCloners.TryGetValue(t, out itemClonerName))
				return string.Format("{0}(cl, {1})", itemClonerName, itemName);
			return null;
		}

		private string GenerateClonerInit(Type t, string itemName)
		{
			var result = GenerateClonerSimple(t, itemName);
			if (result != null)
				return result;
			var clonerName = cw.GetTempName();
			cw.Put("var {0} = cl.GetCloner<{1}>();\n", clonerName, Utils.GetTypeSpec(t));
			return string.Format("({0}){1}({2})", Utils.GetTypeSpec(t), clonerName, itemName);
		}

		private void GenerateCloneItem(Meta meta, Meta.Item yi)
		{
			var simpleCloner = GenerateClonerSimple(yi.Type, "s." + yi.Name);
			if (simpleCloner != null) {
				cw.Put("result.{0} = {1};\n", yi.Name, simpleCloner);
				return;
			}
			if (yi.Type.IsArray) {
				var e = yi.Type.GetElementType();
				cw.Put("if (s.{0} != null) {{\n", yi.Name);
				cw.Put("result.{0} = new {1}[s.{0}.Length];\n", yi.Name, Utils.GetTypeSpec(e));
				if (Cloner.IsCopyable(e))
					cw.Put("Array.Copy(s.{0}, result.{0}, s.{0}.Length);\n", yi.Name);
				else {
					var indexName = cw.GetTempName();
					var clonerCall = GenerateClonerInit(e, string.Format("s.{0}[{1}]", yi.Name, indexName));
					cw.Put("for(int {0} = 0; {0} < s.{1}.Length; ++{0})\n", indexName, yi.Name);
					cw.PutInd("result.{0}[{1}] = {2};\n", yi.Name, indexName, clonerCall);
				}
				cw.Put("}\n");
				return;
			}
			{
				var idict = Utils.GetIDictionary(yi.Type);
				if (idict != null) {
					var a = idict.GetGenericArguments();
					cw.Put("if (s.{0} != null) {{\n", yi.Name);
					cw.Put("result.{0} = new {1}();\n", yi.Name, Utils.GetTypeSpec(yi.Type));
					var itemName = cw.GetTempName();
					var clonerCallK = GenerateClonerInit(a[0], string.Format("{0}.Key", itemName));
					var clonerCallV = GenerateClonerInit(a[1], string.Format("{0}.Value", itemName));
					cw.Put("foreach (var {0} in s.{1})\n", itemName, yi.Name);
					cw.PutInd("result.{0}.Add({1}, {2});\n", yi.Name, clonerCallK, clonerCallV);
					cw.Put("}\n");
					return;
				}
			}
			{
				var icoll = Utils.GetICollection(yi.Type);
				if (icoll != null) {
					var a = icoll.GetGenericArguments();
					cw.Put("if (s.{0} != null) {{\n", yi.Name);
					cw.Put("result.{0} = new {1}();\n", yi.Name, Utils.GetTypeSpec(yi.Type));
					var itemName = cw.GetTempName();
					var clonerCall = GenerateClonerInit(a[0], itemName);
					cw.Put("foreach (var {0} in s.{1})\n", itemName, yi.Name);
					cw.PutInd("result.{0}.Add({1});\n", yi.Name, clonerCall);
					cw.Put("}\n");
					return;
				}
			}
			cw.Put("result.{0} = cl.Deep(s.{0});\n", yi.Name);
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
			var clonerName = "Clone_" + Utils.GetMangledTypeNameNS(t);
			generatedCloners[t] = clonerName;
		}

		private void ActuallyGenerate(Type t, string clonerName)
		{
			if (t.IsInterface)
				throw new YuzuException("Useless ClonerGenerator for interface " + t.FullName);
			if (t.IsAbstract)
				throw new YuzuException("Useless ClonerGenerator for abstract class " + t.FullName);

			var meta = Meta.Get(t, options);

			cw.Put("private static {0} {1}(Cloner cl, object src)\n", Utils.GetTypeSpec(t), clonerName);
			cw.Put("{\n");
			if (!Utils.IsStruct(meta.Type))
				cw.Put("if (src == null) return null;\n");
			cw.Put("var result = {0};\n", GenerateFactoryCall(meta));
			cw.Put("var s = ({0})src;\n", Utils.GetTypeSpec(t));
			GenerateClonerBody(meta);
			cw.Put("return result;\n");
			cw.Put("}\n");
			cw.Put("\n");
		}
	}
}
