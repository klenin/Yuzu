using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Yuzu.Metadata;
using Yuzu.Util;

namespace Yuzu.Clone
{

	public class ClonerGenBase : Cloner
	{
		protected static Dictionary<Type, Func<Cloner, object, object>> clonerCache =
			new Dictionary<Type, Func<Cloner, object, object>>();
		public ClonerGenBase(): base(clonerCache) { }

		public static object ValueCopyCloner(Cloner cl, object src) => src;
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
			foreach (var kv in generatedCloners.OrderBy(kv => kv.Key.Name))
				ActuallyGenerate(kv.Key, kv.Value);
			cw.Put("static ClonerGen()\n");
			cw.Put("{\n");
			foreach (var r in generatedCloners.OrderBy(kv => kv.Key.Name))
				cw.Put("clonerCache[typeof({0})] = {1};\n",
					Utils.GetTypeSpec(r.Key),
					Cloner.IsCopyable(r.Key, options) ? "ValueCopyCloner" :
					Utils.IsStruct(r.Key) ? r.Value + "_obj" : r.Value);
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
			if (Cloner.IsCopyable(t, options))
				return itemName;
			if (t == typeof(object))
				return string.Format("cl.DeepObject({0})", itemName);
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
			if (yi.SetValue != null) {
				var n = "s." + yi.Name;
				var simpleCloner = yi.IsCopyable ? n : GenerateClonerSimple(yi.Type, n);
				if (simpleCloner != null) {
					cw.Put("result.{0} = {1};\n", yi.Name, simpleCloner);
					return;
				}
			}
			if (yi.Type.IsArray) {
				var e = yi.Type.GetElementType();
				cw.Put("if (s.{0} != null) {{\n", yi.Name);
				cw.Put("result.{0} = new {1}[s.{0}.Length];\n", yi.Name, Utils.GetTypeSpec(e));
				if (Cloner.IsCopyable(e, options))
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
					if (yi.SetValue != null)
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
					if (yi.SetValue != null)
						cw.Put("result.{0} = new {1}();\n", yi.Name, Utils.GetTypeSpec(yi.Type));
					var itemName = cw.GetTempName();
					var clonerCall = GenerateClonerInit(a[0], itemName);
					cw.Put("foreach (var {0} in s.{1})\n", itemName, yi.Name);
					cw.PutInd("result.{0}.Add({1});\n", yi.Name, clonerCall);
					cw.Put("}\n");
					return;
				}
			}
			if (yi.SetValue != null)
				cw.Put("result.{0} = ({1})cl.DeepObject(s.{0});\n", yi.Name, Utils.GetTypeSpec(yi.Type));
			else
				cw.Put("cl.GetMerger<{1}>()(result.{0}, s.{0});\n", yi.Name, Utils.GetTypeSpec(yi.Type));
		}

		private void GenerateClonerBody(Meta meta)
		{
			cw.ResetTempNames();
			foreach (var yi in meta.Items) {
				if (yi.SerializeCond != null) {
					if (yi.SerializeIfMethod != null)
						cw.Put("if (s.{0}()) {{\n", yi.SerializeIfMethod.Name);
					else if (!yi.DefaultValue.Equals(YuzuNoDefault.NoDefault))
						cw.Put("if (s.{0} != {1}) {{\n", yi.Name, Utils.CodeValueFormat(yi.DefaultValue));
					else if (!yi.IsMember)
						throw new NotImplementedException("Custom SerializeCondition is not supported");
					GenerateCloneItem(meta, yi);
					cw.Put("}\n");
				}
				else
					GenerateCloneItem(meta, yi);
			}
		}

		public void Generate<T>() { Generate(typeof(T)); }

		public void Generate(Type t)
		{
			var clonerName = "Clone_" + Utils.GetMangledTypeNameNS(t);
			generatedCloners[t] = clonerName;
		}

		private bool GenerateSurrogateCloner(Meta meta)
		{
			var sg = meta.Surrogate;
			if (sg.SurrogateType == null)
				return false;
			if (sg.FuncFrom == null || sg.FuncTo == null)
				throw new YuzuException("Both FromSurrogate and ToSurrogate must be defined for cloning");
			var surrogateTempName = cw.GetTempName();
			var surrogateClone = GenerateClonerInit(
				sg.SurrogateType, string.Format("s.{0}()", sg.MethodTo.Name));
			cw.Put("var {0} = {1};\n", surrogateTempName, surrogateClone);
			cw.Put("var result = {0}.{1}({2});\n",
				Utils.GetTypeSpec(meta.Type), sg.MethodFrom.Name, surrogateTempName);
			return true;
		}

		private void ActuallyGenerate(Type t, string clonerName)
		{
			if (t.IsInterface)
				throw new YuzuException("Useless ClonerGenerator for interface " + t.FullName);
			if (t.IsAbstract)
				throw new YuzuException("Useless ClonerGenerator for abstract class " + t.FullName);

			var meta = Meta.Get(t, options);

			if (Cloner.IsCopyable(meta.Type, options)) {
				cw.Put("private static {0} {1}(Cloner cl, object src) =>\n",
					Utils.GetTypeSpec(t), clonerName);
				cw.PutInd("({0})src;\n", Utils.GetTypeSpec(t));
				cw.Put("\n");
				return;
			}

			cw.Put("private static {0} {1}(Cloner cl, object src)\n", Utils.GetTypeSpec(t), clonerName);
			cw.Put("{\n");
			if (!Utils.IsStruct(t)) {
				cw.Put("if (src == null) return null;\n");
				if (!t.IsSealed) {
					cw.Put("if (src.GetType() != typeof({0}))\n", Utils.GetTypeSpec(t));
					cw.PutInd("return ({0})cl.DeepObject(src);\n", Utils.GetTypeSpec(t));
				}
			}
			cw.Put("var s = ({0})src;\n", Utils.GetTypeSpec(t));
			cw.GenerateActionList(meta.BeforeSerialization, "s");
			if (!GenerateSurrogateCloner(meta)) {
				cw.Put("var result = {0};\n", GenerateFactoryCall(meta));
				cw.GenerateActionList(meta.BeforeDeserialization);
				GenerateClonerBody(meta);
			}
			cw.GenerateActionList(meta.AfterSerialization, "s");
			cw.GenerateActionList(meta.AfterDeserialization);
			cw.Put("return result;\n");
			cw.Put("}\n");
			cw.Put("\n");
			if (Utils.IsStruct(t)) {
				cw.Put("private static object {0}_obj(Cloner cl, object src) =>\n", clonerName);
				cw.PutInd("{0}(cl, src);\n", clonerName);
				cw.Put("\n");
			}
		}
	}
}
