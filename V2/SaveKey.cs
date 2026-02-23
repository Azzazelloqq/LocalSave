using System;

namespace LocalSaveSystem
{
public interface ISaveKey
{
	string Id { get; }
	Type ValueType { get; }
	object CreateDefaultValue();
}

public readonly struct SaveKey<T> : ISaveKey
{
	public string Id { get; }
	private readonly Func<T> _defaultFactory;

	public SaveKey(string id, Func<T> defaultFactory = null)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new ArgumentException("SaveKey id must be a non-empty string.", nameof(id));
		}

		Id = id;
		_defaultFactory = defaultFactory;
	}

	public T CreateDefaultValue()
	{
		return _defaultFactory != null ? _defaultFactory() : default;
	}

	object ISaveKey.CreateDefaultValue()
	{
		return CreateDefaultValue();
	}

	Type ISaveKey.ValueType => typeof(T);
}
}
