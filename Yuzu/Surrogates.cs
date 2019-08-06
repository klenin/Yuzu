using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Yuzu.Metadata
{
	public class Surrogate
	{
		[Flags]
		private enum State
		{
			None = 0,
			Is = 1,
			Has = 2,
			Both = 3,
		}

		private static ConcurrentDictionary<Tuple<Type, MetaOptions>, State> surrogateTypes =
			new ConcurrentDictionary<Tuple<Type, MetaOptions>, State>();

		private MethodInfo methodIf;
		public MethodInfo MethodTo;
		public MethodInfo MethodFrom;

		public Func<object, bool> FuncIf;
		public Func<object, object> FuncTo;
		public Func<object, object> FuncFrom;

		public Type SurrogateType;
		private Type ownerType;

		private MetaOptions options;

		public Surrogate(Type ownerType, MetaOptions options)
		{
			this.ownerType = ownerType;
			this.options = options;
		}

		private YuzuException Error(string format, params object[] args)
		{
			return new YuzuException("In type '" + ownerType.FullName + "': " + String.Format(format, args));
		}

		private void MaybeSet(MethodInfo m, MetaItemOverride item, Type attr, string name, ref MethodInfo dest)
		{
			if (attr != null && item.HasAttr(attr)) {
				if (dest != null)
					throw Error("Duplicate {0}: '{1}' and '{2}'", name, dest.Name, m.Name);
				dest = m;
			}
		}

		public void ProcessMethod(MethodInfo m)
		{
			var attrs = options.GetItem(m);
			MaybeSet(m, attrs, options.SurrogateIfAttribute, "SurrogateIf", ref methodIf);
			MaybeSet(m, attrs, options.ToSurrogateAttribute, "ToSurrogate", ref MethodTo);
			MaybeSet(m, attrs, options.FromSurrogateAttribute, "FromSurrogate", ref MethodFrom);
		}

		private void CheckAccepts(MethodInfo m, string name, Type paramType)
		{
			var p = m.GetParameters();
			if (p.Length != 1)
				throw Error(
					"Static {0} '{1}' must have 1 parameter, but has {2}", name, m.Name, p.Length);
			if (paramType != null && !p[0].ParameterType.IsAssignableFrom(paramType))
				throw Error(
					"Static {0} '{1}' must accept parameter of type '{2}', but has '{3}'",
					name, m.Name, paramType.Name, p[0].ParameterType.Name);
		}

		private void SetTypeState(Type t, State newState)
		{
			var k = Tuple.Create(t, options);
			State oldState = surrogateTypes.GetOrAdd(k, newState);
			if ((oldState | newState) == State.Both)
				throw Error("Surrogate chain for type '{0}'", t.Name);
		}

		public void Complete()
		{
			if (methodIf != null) {
				if (methodIf.ReturnType != typeof(bool))
					throw Error(
						"SurrogateIf '{0}' must return bool, but returns '{1}'",
						methodIf.Name, methodIf.ReturnType.Name);
				if (methodIf.IsStatic) {
					CheckAccepts(methodIf, "SurrogateIf", ownerType);
					FuncIf = obj => (bool)methodIf.Invoke(null, new object[] { obj });
				} else {
					var p = methodIf.GetParameters();
					if (p.Length != 0)
						throw Error("SurrogateIf '{0}' must have 0 parameters, but has {1}", methodIf.Name, p.Length);
					FuncIf = obj => (bool)methodIf.Invoke(obj, null);
				}
			}

			if (MethodTo != null) {
				if (MethodTo.ReturnType == typeof(void))
					throw Error("ToSurrogate '{0}' returns void", MethodTo.Name);
				SurrogateType = MethodTo.ReturnType;
				SetTypeState(MethodTo.ReturnType, State.Is);
				if (SurrogateType == ownerType)
					throw Error("ToSurrogate '{0}' returns owner type", MethodTo.Name);
				if (MethodTo.IsStatic) {
					CheckAccepts(MethodTo, "ToSurrogate", ownerType);
					FuncTo = obj => MethodTo.Invoke(null, new object[] { obj });
				} else {
					var p = MethodTo.GetParameters();
					if (p.Length != 0)
						throw Error("ToSurrogate '{0}' must have 0 parameters, but has {1}", MethodTo.Name, p.Length);
					FuncTo = obj => MethodTo.Invoke(obj, null);
				}
			}

			if (MethodFrom != null) {
				if (!MethodFrom.IsStatic)
					throw Error("FromSurrogate '{0}' must be static", MethodFrom.Name);
				if (!ownerType.IsAssignableFrom(MethodFrom.ReturnType))
					throw Error(
						"FromSurrogate '{0}' must return '{1}', but returns '{2}'",
						MethodFrom.Name, ownerType.Name, MethodFrom.ReturnType.Name);
				// FromSurrogate does not make source type a surrogate by itself.
				if (SurrogateType == null) {
					SurrogateType = MethodFrom.GetParameters()[0].ParameterType;
					if (SurrogateType == ownerType)
						throw Error("FromSurrogate '{0}' accepts owner type", MethodFrom.Name);
				}
				CheckAccepts(MethodFrom, "FromSurrogate", SurrogateType);
				FuncFrom = obj => MethodFrom.Invoke(null, new object[] { obj });
			}

			if (SurrogateType != null)
				SetTypeState(ownerType, State.Has);
		}
	}
}
