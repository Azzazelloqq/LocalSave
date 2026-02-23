using System.Collections.Generic;
using System.IO;

namespace LocalSaveSystem
{
internal sealed class ArraySaveSerializer<T> : SaveSerializer<T[]>
{
	public override int Version => 1;

	public override void Write(BinaryWriter writer, T[] value, SaveRegistry registry)
	{
		if (value == null)
		{
			writer.Write(-1);
			return;
		}

		writer.Write(value.Length);
		for (var i = 0; i < value.Length; i++)
		{
			registry.Write(writer, value[i]);
		}
	}

	public override T[] Read(BinaryReader reader, SaveRegistry registry)
	{
		var length = reader.ReadInt32();
		if (length < 0)
		{
			return null;
		}

		var array = new T[length];
		for (var i = 0; i < length; i++)
		{
			array[i] = registry.Read<T>(reader);
		}

		return array;
	}
}

internal sealed class ListSaveSerializer<T> : SaveSerializer<List<T>>
{
	public override int Version => 1;

	public override void Write(BinaryWriter writer, List<T> value, SaveRegistry registry)
	{
		if (value == null)
		{
			writer.Write(-1);
			return;
		}

		writer.Write(value.Count);
		for (var i = 0; i < value.Count; i++)
		{
			registry.Write(writer, value[i]);
		}
	}

	public override List<T> Read(BinaryReader reader, SaveRegistry registry)
	{
		var count = reader.ReadInt32();
		if (count < 0)
		{
			return null;
		}

		var list = new List<T>(count);
		for (var i = 0; i < count; i++)
		{
			list.Add(registry.Read<T>(reader));
		}

		return list;
	}
}

internal sealed class DictionarySaveSerializer<TKey, TValue> : SaveSerializer<Dictionary<TKey, TValue>>
{
	public override int Version => 1;

	public override void Write(BinaryWriter writer, Dictionary<TKey, TValue> value, SaveRegistry registry)
	{
		if (value == null)
		{
			writer.Write(-1);
			return;
		}

		writer.Write(value.Count);
		foreach (var pair in value)
		{
			registry.Write(writer, pair.Key);
			registry.Write(writer, pair.Value);
		}
	}

	public override Dictionary<TKey, TValue> Read(BinaryReader reader, SaveRegistry registry)
	{
		var count = reader.ReadInt32();
		if (count < 0)
		{
			return null;
		}

		var dict = new Dictionary<TKey, TValue>(count);
		for (var i = 0; i < count; i++)
		{
			var key = registry.Read<TKey>(reader);
			var value = registry.Read<TValue>(reader);
			dict[key] = value;
		}

		return dict;
	}
}

internal sealed class NullableSaveSerializer<T> : SaveSerializer<T?> where T : struct
{
	public override int Version => 1;

	public override void Write(BinaryWriter writer, T? value, SaveRegistry registry)
	{
		writer.Write(value.HasValue);
		if (value.HasValue)
		{
			registry.Write(writer, value.Value);
		}
	}

	public override T? Read(BinaryReader reader, SaveRegistry registry)
	{
		var hasValue = reader.ReadBoolean();
		if (!hasValue)
		{
			return null;
		}

		return registry.Read<T>(reader);
	}
}

}
