using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using LocalSaveSystem;
using UnityEngine;

namespace LocalSaveSystem.Legacy
{
public static class LegacyBinaryFormatterImporter
{
	public static LegacyImportSummary ImportInto(SaveStore store, string legacyFilePath)
	{
		if (store == null)
		{
			throw new ArgumentNullException(nameof(store));
		}

		if (string.IsNullOrWhiteSpace(legacyFilePath) || !File.Exists(legacyFilePath))
		{
			return new LegacyImportSummary(0, 0, 0);
		}

		var savables = LoadSavables(legacyFilePath);
		var total = savables.Count;
		var imported = 0;
		var skipped = 0;

		foreach (var savable in savables)
		{
			if (savable == null)
			{
				skipped++;
				continue;
			}

			if (!store.TryGetRegisteredKey(savable.SaveId, out var key))
			{
				skipped++;
				continue;
			}

			if (!key.ValueType.IsAssignableFrom(savable.GetType()))
			{
				skipped++;
				continue;
			}

			store.SetBoxed(key, savable);
			imported++;
		}

		store.ForceSave();
		return new LegacyImportSummary(total, imported, skipped);
	}

	private static List<ISavable> LoadSavables(string legacyFilePath)
	{
		try
		{
			using var fs = new FileStream(legacyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			var formatter = new BinaryFormatter();
			#pragma warning disable SYSLIB0011
			var savesArray = (ISavable[])formatter.Deserialize(fs);
			#pragma warning restore SYSLIB0011
			return savesArray == null ? new List<ISavable>() : new List<ISavable>(savesArray);
		}
		catch (Exception e)
		{
			Debug.LogError($"[Save system] Failed to read legacy saves: {e.Message}");
			return new List<ISavable>();
		}
	}
}

public readonly struct LegacyImportSummary
{
	public int Total { get; }
	public int Imported { get; }
	public int Skipped { get; }

	public LegacyImportSummary(int total, int imported, int skipped)
	{
		Total = total;
		Imported = imported;
		Skipped = skipped;
	}
}
}
