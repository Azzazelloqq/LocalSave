using System;

namespace LocalSaveSystem
{
internal static class SaveVersionResolver
{
	public static int GetVersion(Type type)
	{
		var attribute = (SaveVersionAttribute)Attribute.GetCustomAttribute(type, typeof(SaveVersionAttribute), true);
		return attribute?.Version ?? 1;
	}
}
}
