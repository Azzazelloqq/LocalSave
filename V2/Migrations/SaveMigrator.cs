using System;

namespace LocalSaveSystem
{
public interface ISaveMigrator
{
	Type TargetType { get; }
	int FromVersion { get; }
	int ToVersion { get; }
	object Migrate(object value);
}

public abstract class SaveMigrator<T> : ISaveMigrator
{
	public Type TargetType => typeof(T);
	public abstract int FromVersion { get; }
	public abstract int ToVersion { get; }
	public abstract T Migrate(T value);

	object ISaveMigrator.Migrate(object value)
	{
		return Migrate((T)value);
	}
}
}
