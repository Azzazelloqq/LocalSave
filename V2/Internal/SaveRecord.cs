using System;

namespace LocalSaveSystem
{
internal abstract class SaveRecord
{
	public string Id { get; }
	public bool Dirty { get; private set; }

	protected SaveRecord(string id)
	{
		Id = id;
	}

	public void MarkDirty()
	{
		Dirty = true;
	}

	public void ClearDirty()
	{
		Dirty = false;
	}

	public abstract Type ValueType { get; }
	public abstract object BoxedValue { get; set; }
	public abstract SaveEntryPayload ToPayload(SaveRegistry registry);
}

internal sealed class SaveRecord<T> : SaveRecord
{
	public SaveKey<T> Key { get; }
	public T Value;

	public SaveRecord(SaveKey<T> key, T value) : base(key.Id)
	{
		Key = key;
		Value = value;
	}

	public override Type ValueType => typeof(T);

	public override object BoxedValue
	{
		get => Value;
		set => Value = (T)value;
	}

	public override SaveEntryPayload ToPayload(SaveRegistry registry)
	{
		var serializer = registry.GetOrCreateSerializer<T>();
		var payload = registry.Serialize(Value);
		var typeName = serializer.TargetType.AssemblyQualifiedName
			?? serializer.TargetType.FullName
			?? serializer.TargetType.Name;
		var typeId = SaveTypeId.Compute(typeName);
		return new SaveEntryPayload(Key.Id, typeName, typeId, serializer.Version, payload);
	}
}
}
