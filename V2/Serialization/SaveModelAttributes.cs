using System;

namespace LocalSaveSystem
{
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SaveModelAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SaveMemberAttribute : Attribute
{
	/// <summary>
	/// Optional ordering for members when using sequential serialization.
	/// </summary>
	public int Order { get; }

	public SaveMemberAttribute(int order = -1)
	{
		Order = order;
	}
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SaveIgnoreAttribute : Attribute
{
}

/// <summary>
/// Overrides the field identifier used by tagged serialization.
/// Useful for keeping compatibility when renaming members.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SaveFieldIdAttribute : Attribute
{
	/// <summary>
	/// Stable identifier of the field in tagged payloads.
	/// </summary>
	public string Id { get; }

	public SaveFieldIdAttribute(string id)
	{
		Id = id;
	}
}
}
