using System;

namespace LocalSaveSystem
{
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SaveVersionAttribute : Attribute
{
	public int Version { get; }

	public SaveVersionAttribute(int version)
	{
		Version = version;
	}
}
}
