using System;

namespace LocalSaveSystem
{
public readonly struct SaveHandle<T> : IDisposable
{
	private readonly SaveStore _store;
	private readonly SaveRecord<T> _record;

	internal SaveHandle(SaveStore store, SaveRecord<T> record)
	{
		_store = store;
		_record = record;
	}

	public ref T Value => ref _record.Value;

	public void Dispose()
	{
		if (_record == null)
		{
			return;
		}

		_record.MarkDirty();
		_store.MarkDirty();
	}
}
}
