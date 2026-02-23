using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LocalSaveSystem
{
internal sealed class BinarySaveStore
{
	private readonly SaveStoreOptions _options;
	private readonly string _storagePath;
	private readonly string _extension;

	public BinarySaveStore(SaveStoreOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_storagePath = options.StoragePath;
		_extension = string.IsNullOrWhiteSpace(options.FileExtension) ? ".lss2" : options.FileExtension;
		if (!_extension.StartsWith(".", StringComparison.Ordinal))
		{
			_extension = "." + _extension;
		}
	}

	public Dictionary<string, SaveEntryPayload> LoadAll()
	{
		var result = new Dictionary<string, SaveEntryPayload>(StringComparer.Ordinal);
		if (string.IsNullOrWhiteSpace(_storagePath) || !Directory.Exists(_storagePath))
		{
			return result;
		}

		var files = Directory.GetFiles(_storagePath, "*" + _extension, SearchOption.TopDirectoryOnly);
		foreach (var file in files)
		{
			try
			{
				using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
				var entry = BinarySaveEntrySerializer.Read(fs);
				if (!string.IsNullOrWhiteSpace(entry.Key))
				{
					result[entry.Key] = entry;
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"[Save system] Failed to read save entry '{file}': {e.Message}");
			}
		}

		return result;
	}

	public void SaveEntries(IEnumerable<SaveEntryPayload> entries)
	{
		CreateDirectoryIfNeeded();
		foreach (var entry in entries)
		{
			SaveEntry(entry);
		}
	}

	public void DeleteAll()
	{
		if (!Directory.Exists(_storagePath))
		{
			return;
		}

		Directory.Delete(_storagePath, true);
	}

	private void SaveEntry(SaveEntryPayload entry)
	{
		var fileName = SaveFileNaming.GetFileName(entry.Key, _extension);
		var filePath = Path.Combine(_storagePath, fileName);
		var tempPath = filePath + ".tmp";

		using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			BinarySaveEntrySerializer.Write(fs, entry);
		}

		if (_options.UseAtomicWrite && File.Exists(filePath))
		{
			var backupPath = filePath + ".bak";
			File.Replace(tempPath, filePath, backupPath, true);
			if (File.Exists(backupPath))
			{
				File.Delete(backupPath);
			}
		}
		else
		{
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}

			File.Move(tempPath, filePath);
		}
	}

	private void CreateDirectoryIfNeeded()
	{
		if (!Directory.Exists(_storagePath))
		{
			Directory.CreateDirectory(_storagePath);
		}
	}
}
}
