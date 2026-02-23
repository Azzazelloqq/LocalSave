using System;

namespace LocalSaveSystem
{
internal static class SaveModelActivator
{
	public static T Create<T>()
	{
		var type = typeof(T);
		if (type.IsValueType)
		{
			return default;
		}

		return (T)Activator.CreateInstance(type, true);
	}
}
}
