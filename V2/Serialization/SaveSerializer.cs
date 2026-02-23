using System;
using System.IO;

namespace LocalSaveSystem
{
public interface ISaveSerializer
{
	Type TargetType { get; }
	int Version { get; }
	void Write(BinaryWriter writer, object value, SaveRegistry registry);
	object Read(BinaryReader reader, SaveRegistry registry);
}

public interface ISaveSerializer<T> : ISaveSerializer
{
	void Write(BinaryWriter writer, T value, SaveRegistry registry);
	new T Read(BinaryReader reader, SaveRegistry registry);
}

public abstract class SaveSerializer<T> : ISaveSerializer<T>
{
	public abstract int Version { get; }
	public Type TargetType => typeof(T);

	public abstract void Write(BinaryWriter writer, T value, SaveRegistry registry);
	public abstract T Read(BinaryReader reader, SaveRegistry registry);

	void ISaveSerializer.Write(BinaryWriter writer, object value, SaveRegistry registry)
	{
		Write(writer, (T)value, registry);
	}

	object ISaveSerializer.Read(BinaryReader reader, SaveRegistry registry)
	{
		return Read(reader, registry);
	}
}
}
