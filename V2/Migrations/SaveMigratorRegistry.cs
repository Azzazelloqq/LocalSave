using System;
using System.Collections.Generic;
using System.Linq;

namespace LocalSaveSystem
{
public sealed class SaveMigratorRegistry
{
	private readonly Dictionary<Type, List<ISaveMigrator>> _migrators =
		new Dictionary<Type, List<ISaveMigrator>>();

	public void Register(ISaveMigrator migrator)
	{
		if (migrator == null)
		{
			return;
		}

		if (!_migrators.TryGetValue(migrator.TargetType, out var list))
		{
			list = new List<ISaveMigrator>();
			_migrators[migrator.TargetType] = list;
		}

		list.RemoveAll(existing => existing.FromVersion == migrator.FromVersion);
		list.Add(migrator);
		list.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
	}

	public void Register<T>(SaveMigrator<T> migrator)
	{
		Register((ISaveMigrator)migrator);
	}

	public bool TryMigrate(Type type, int fromVersion, int toVersion, ref object value)
	{
		if (fromVersion >= toVersion)
		{
			return true;
		}

		if (!_migrators.TryGetValue(type, out var list))
		{
			return false;
		}

		var current = fromVersion;
		while (current < toVersion)
		{
			var migrator = list.FirstOrDefault(item => item.FromVersion == current);
			if (migrator == null)
			{
				return false;
			}

			value = migrator.Migrate(value);
			current = migrator.ToVersion;
		}

		return true;
	}
}
}
