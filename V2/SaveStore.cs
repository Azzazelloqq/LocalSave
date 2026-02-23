using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LocalSaveSystem
{
public sealed class SaveStore : ISaveStore
{
	private readonly SaveStoreOptions _options;
	private readonly SaveRegistry _registry;
	private readonly SaveMigratorRegistry _migrators;
	private readonly BinarySaveStore _binaryStore;
	private readonly Dictionary<string, SaveRecord> _records;
	private readonly Dictionary<string, SaveEntryPayload> _loadedEntries;
	private readonly Dictionary<string, ISaveKey> _registeredKeys;
	private readonly object _lock = new();
	private CancellationTokenSource _autoSaveCts;
	private bool _needSave;
	private bool _isDisposed;

	public SaveRegistry Registry => _registry;
	public SaveMigratorRegistry Migrators => _migrators;

	public SaveStore(SaveStoreOptions options, SaveRegistry registry = null, SaveMigratorRegistry migrators = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		if (string.IsNullOrWhiteSpace(_options.StoragePath))
		{
			throw new ArgumentException("StoragePath must be set in SaveStoreOptions.", nameof(options));
		}

		_registry = registry ?? SaveRegistry.CreateDefault(new SaveSerializationOptions
		{
			UseTaggedFormat = _options.UseTaggedFormat
		});
		_registry.Options.UseTaggedFormat = _options.UseTaggedFormat;
		_migrators = migrators ?? new SaveMigratorRegistry();
		_binaryStore = new BinarySaveStore(_options);
		_records = new Dictionary<string, SaveRecord>(StringComparer.Ordinal);
		_loadedEntries = _binaryStore.LoadAll();
		_registeredKeys = new Dictionary<string, ISaveKey>(StringComparer.Ordinal);

		SubscribeOnEvents();
	}

	public void RegisterKey<T>(SaveKey<T> key)
	{
		EnsureNotDisposed();

		lock (_lock)
		{
			_registeredKeys[key.Id] = key;
			if (_records.ContainsKey(key.Id))
			{
				return;
			}

			var record = CreateRecordFromLoadedOrDefault(key);
			_records[key.Id] = record;
		}
	}

	public void RegisterKeys(IEnumerable<ISaveKey> keys)
	{
		EnsureNotDisposed();
		if (keys == null)
		{
			return;
		}

		var method = GetType().GetMethod(nameof(RegisterKey));
		foreach (var key in keys)
		{
			if (key == null)
			{
				continue;
			}

			var genericMethod = method?.MakeGenericMethod(key.ValueType);
			genericMethod?.Invoke(this, new object[] { key });
		}
	}

	public bool TryGetRegisteredKey(string id, out ISaveKey key)
	{
		lock (_lock)
		{
			return _registeredKeys.TryGetValue(id, out key);
		}
	}

	public T Get<T>(SaveKey<T> key)
	{
		EnsureNotDisposed();
		lock (_lock)
		{
			var record = GetOrCreateRecord(key);
			return record.Value;
		}
	}

	public bool TryGet<T>(SaveKey<T> key, out T value)
	{
		EnsureNotDisposed();
		lock (_lock)
		{
			_registeredKeys[key.Id] = key;
			if (_records.TryGetValue(key.Id, out var existing))
			{
				if (existing is SaveRecord<T> typed)
				{
					value = typed.Value;
					return true;
				}
			}

			if (_loadedEntries.TryGetValue(key.Id, out var entry))
			{
				var record = CreateRecordFromEntry(key, entry);
				_records[key.Id] = record;
				value = record.Value;
				return true;
			}
		}

		value = default;
		return false;
	}

	public void Set<T>(SaveKey<T> key, T value)
	{
		EnsureNotDisposed();
		lock (_lock)
		{
			var record = GetOrCreateRecord(key);
			record.Value = value;
			record.MarkDirty();
			MarkDirty();
		}
	}

	public void Update<T>(SaveKey<T> key, SaveUpdate<T> updater)
	{
		if (updater == null)
		{
			return;
		}

		EnsureNotDisposed();
		lock (_lock)
		{
			var record = GetOrCreateRecord(key);
			updater(ref record.Value);
			record.MarkDirty();
			MarkDirty();
		}
	}

	public SaveHandle<T> Edit<T>(SaveKey<T> key)
	{
		EnsureNotDisposed();
		lock (_lock)
		{
			var record = GetOrCreateRecord(key);
			return new SaveHandle<T>(this, record);
		}
	}

	public void Save()
	{
		EnsureNotDisposed();
		lock (_lock)
		{
			_needSave = true;
		}
	}

	public void ForceSave()
	{
		EnsureNotDisposed();
		lock (_lock)
		{
			SaveDirtyEntriesLocked();
		}
	}

	public void StartAutoSave()
	{
		EnsureNotDisposed();

		lock (_lock)
		{
			if (_autoSaveCts != null && !_autoSaveCts.IsCancellationRequested)
			{
				return;
			}

			_autoSaveCts = new CancellationTokenSource();
			AutoSaveLoop(_options.AutoSavePeriodSeconds, _autoSaveCts.Token);
		}
	}

	public void StopAutoSave()
	{
		lock (_lock)
		{
			if (_autoSaveCts != null && !_autoSaveCts.IsCancellationRequested)
			{
				_autoSaveCts.Cancel();
			}
		}
	}

	public void DeleteAll()
	{
		EnsureNotDisposed();
		List<ISaveKey> keys;
		lock (_lock)
		{
			_binaryStore.DeleteAll();
			_loadedEntries.Clear();
			_records.Clear();
			keys = _registeredKeys.Values.ToList();
		}

		RegisterKeys(keys);
	}

	public void Dispose()
	{
		lock (_lock)
		{
			if (_isDisposed)
			{
				return;
			}

			StopAutoSave();
			_autoSaveCts?.Dispose();
			_autoSaveCts = null;
			_isDisposed = true;
			UnsubscribeOnEvents();
		}
	}

	internal void MarkDirty()
	{
		_needSave = true;
	}

	internal void SetBoxed(ISaveKey key, object value)
	{
		if (key == null || value == null)
		{
			return;
		}

		if (!key.ValueType.IsAssignableFrom(value.GetType()))
		{
			throw new InvalidOperationException(
				$"Value type {value.GetType().Name} does not match key type {key.ValueType.Name}.");
		}

		lock (_lock)
		{
			_registeredKeys[key.Id] = key;
			var record = GetOrCreateRecordInternal(key);
			record.BoxedValue = value;
			record.MarkDirty();
			MarkDirty();
		}
	}

	private SaveRecord<T> GetOrCreateRecord<T>(SaveKey<T> key)
	{
		_registeredKeys[key.Id] = key;

		if (_records.TryGetValue(key.Id, out var existing))
		{
			if (existing is SaveRecord<T> typed)
			{
				return typed;
			}

			throw new InvalidOperationException($"Save key '{key.Id}' is already used with type {existing.ValueType.Name}.");
		}

		var record = CreateRecordFromLoadedOrDefault(key);
		_records[key.Id] = record;
		return record;
	}

	private SaveRecord GetOrCreateRecordInternal(ISaveKey key)
	{
		if (_records.TryGetValue(key.Id, out var existing))
		{
			return existing;
		}

		var method = GetType().GetMethod(nameof(CreateRecordFromLoadedOrDefault),
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		var genericMethod = method?.MakeGenericMethod(key.ValueType);
		var record = (SaveRecord)genericMethod?.Invoke(this, new object[] { key });
		_records[key.Id] = record;
		return record;
	}

	private SaveRecord<T> CreateRecordFromLoadedOrDefault<T>(SaveKey<T> key)
	{
		if (_loadedEntries.TryGetValue(key.Id, out var entry))
		{
			return CreateRecordFromEntry(key, entry);
		}

		var value = CreateDefaultValue(key);
		var record = new SaveRecord<T>(key, value);
		record.MarkDirty();
		MarkDirty();
		return record;
	}

	private SaveRecord<T> CreateRecordFromEntry<T>(SaveKey<T> key, SaveEntryPayload entry)
	{
		if (!IsCompatibleType(typeof(T), entry.TypeName))
		{
			Debug.LogWarning(
				$"[Save system] Type mismatch for key '{key.Id}'. Expected {typeof(T).Name}, got {entry.TypeName}. " +
				"Using default value.");
			var fallback = new SaveRecord<T>(key, CreateDefaultValue(key));
			fallback.MarkDirty();
			MarkDirty();
			return fallback;
		}

		try
		{
			var value = DeserializeEntry(typeof(T), entry);
			var typedValue = value is T cast ? cast : default;
			return new SaveRecord<T>(key, typedValue);
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[Save system] Failed to load key '{key.Id}': {e.Message}");
			var fallback = new SaveRecord<T>(key, CreateDefaultValue(key));
			fallback.MarkDirty();
			MarkDirty();
			return fallback;
		}
	}

	private object DeserializeEntry(Type type, SaveEntryPayload entry)
	{
		var value = _registry.Deserialize(type, entry.Payload);
		var currentVersion = _registry.GetVersion(type);
		if (entry.DataVersion < currentVersion)
		{
			var boxed = value;
			if (!_migrators.TryMigrate(type, entry.DataVersion, currentVersion, ref boxed))
			{
				Debug.LogWarning(
					$"[Save system] Missing migrator for {type.Name} ({entry.DataVersion} -> {currentVersion}).");
			}
			value = boxed;
		}

		if (value is ISaveAfterLoad afterLoad)
		{
			afterLoad.OnAfterLoad();
		}

		return value;
	}

	private static bool IsCompatibleType(Type requested, string storedTypeName)
	{
		if (string.IsNullOrWhiteSpace(storedTypeName))
		{
			return false;
		}

		var storedType = Type.GetType(storedTypeName);
		return storedType != null && requested.IsAssignableFrom(storedType);
	}

	private static T CreateDefaultValue<T>(SaveKey<T> key)
	{
		var value = key.CreateDefaultValue();
		if (value == null && typeof(T).IsClass)
		{
			var ctor = typeof(T).GetConstructor(Type.EmptyTypes);
			if (ctor != null)
			{
				value = (T)Activator.CreateInstance(typeof(T));
			}
		}

		if (value is ISaveInitializable initializable)
		{
			initializable.InitializeAsNew();
		}

		return value;
	}

	private void SaveDirtyEntriesLocked()
	{
		if (!_needSave)
		{
			return;
		}

		var dirtyRecords = _records.Values.Where(record => record.Dirty).ToList();
		if (dirtyRecords.Count == 0)
		{
			_needSave = false;
			return;
		}

		try
		{
			var entries = dirtyRecords.Select(record => record.ToPayload(_registry)).ToList();
			_binaryStore.SaveEntries(entries);
			foreach (var record in dirtyRecords)
			{
				record.ClearDirty();
			}

			_needSave = false;
		}
		catch (Exception e)
		{
			Debug.LogError($"[Save system] Failed to save entries: {e.Message}");
		}
	}

	private async void AutoSaveLoop(int periodPerSeconds, CancellationToken token)
	{
		var periodMs = periodPerSeconds * 1000;
		while (!token.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(periodMs, token);
				lock (_lock)
				{
					if (!_needSave)
					{
						continue;
					}

					SaveDirtyEntriesLocked();
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception e)
			{
				Debug.LogError($"[Save system] AutoSaveLoop failed: {e.Message}");
				break;
			}
		}
	}

	private void SubscribeOnEvents()
	{
		if (_options.SaveOnQuit)
		{
			Application.quitting += OnApplicationQuitting;
		}
	}

	private void UnsubscribeOnEvents()
	{
		Application.quitting -= OnApplicationQuitting;
	}

	private void OnApplicationQuitting()
	{
		lock (_lock)
		{
			StopAutoSave();
			SaveDirtyEntriesLocked();
		}
	}

	private void EnsureNotDisposed()
	{
		if (_isDisposed)
		{
			throw new ObjectDisposedException(nameof(SaveStore));
		}
	}
}
}
