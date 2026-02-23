using System;

namespace LocalSaveSystem
{
internal static class SaveTypeId
{
	public static int Compute(Type type)
	{
		var name = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
		return Compute(name);
	}

	public static int Compute(string typeName)
	{
		unchecked
		{
			const int fnvOffsetBasis = unchecked((int)2166136261);
			const int fnvPrime = 16777619;
			var hash = fnvOffsetBasis;

			for (var i = 0; i < typeName.Length; i++)
			{
				hash ^= typeName[i];
				hash *= fnvPrime;
			}

			return hash;
		}
	}
}
}
