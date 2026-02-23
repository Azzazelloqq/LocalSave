using System;

namespace LocalSaveSystem
{
/// <summary>
/// Resolves type names stored in tagged payloads.
/// </summary>
public static class SaveTypeResolver
{
	public static string GetTypeName(Type type)
	{
		if (type == null)
		{
			return string.Empty;
		}

		return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
	}

	public static Type Resolve(string typeName)
	{
		if (string.IsNullOrWhiteSpace(typeName))
		{
			return null;
		}

		var type = Type.GetType(typeName, false);
		if (type != null)
		{
			return type;
		}

		var trimmed = typeName;
		var comma = typeName.IndexOf(',');
		if (comma > 0)
		{
			trimmed = typeName.Substring(0, comma);
		}

		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		foreach (var assembly in assemblies)
		{
			type = assembly.GetType(trimmed, false);
			if (type != null)
			{
				return type;
			}
		}

		return null;
	}
}
}
