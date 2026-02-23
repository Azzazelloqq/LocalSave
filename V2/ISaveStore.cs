using System;
using System.Collections.Generic;

namespace LocalSaveSystem
{
public interface ISaveStore : IDisposable
{
	void RegisterKey<T>(SaveKey<T> key);
	void RegisterKeys(IEnumerable<ISaveKey> keys);
	bool TryGetRegisteredKey(string id, out ISaveKey key);

	T Get<T>(SaveKey<T> key);
	bool TryGet<T>(SaveKey<T> key, out T value);
	void Set<T>(SaveKey<T> key, T value);
	void Update<T>(SaveKey<T> key, SaveUpdate<T> updater);
	SaveHandle<T> Edit<T>(SaveKey<T> key);

	void Save();
	void ForceSave();
	void StartAutoSave();
	void StopAutoSave();
	void DeleteAll();
}
}
