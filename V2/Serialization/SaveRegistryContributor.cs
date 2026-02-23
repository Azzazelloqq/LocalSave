using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LocalSaveSystem
{
public interface ISaveRegistryContributor
{
	void Register(SaveRegistry registry);
}

internal static class SaveRegistryContributorLoader
{
	private static readonly object LockObject = new object();
	private static bool _loaded;
	private static List<ISaveRegistryContributor> _contributors;

	public static void RegisterAll(SaveRegistry registry)
	{
		EnsureLoaded();
		if (_contributors == null)
		{
			return;
		}

		foreach (var contributor in _contributors)
		{
			try
			{
				contributor.Register(registry);
			}
			catch
			{
				// Ignore contributor errors to avoid breaking save setup.
			}
		}
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		lock (LockObject)
		{
			if (_loaded)
			{
				return;
			}

			_contributors = new List<ISaveRegistryContributor>();
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in assemblies)
			{
				foreach (var type in GetTypesSafe(assembly))
				{
					if (type == null || type.IsAbstract || type.IsInterface)
					{
						continue;
					}

					if (!typeof(ISaveRegistryContributor).IsAssignableFrom(type))
					{
						continue;
					}

					if (type.GetConstructor(Type.EmptyTypes) == null)
					{
						continue;
					}

					try
					{
						var instance = (ISaveRegistryContributor)Activator.CreateInstance(type);
						_contributors.Add(instance);
					}
					catch
					{
						// Skip invalid contributors.
					}
				}
			}

			_loaded = true;
		}
	}

	private static IEnumerable<Type> GetTypesSafe(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException e)
		{
			return e.Types.Where(type => type != null);
		}
		catch
		{
			return Array.Empty<Type>();
		}
	}
}
}
