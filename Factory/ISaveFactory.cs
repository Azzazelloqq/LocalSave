using System;

namespace LocalSaveSystem.Factory
{
/// <summary>
/// A factory interface responsible for creating an array of ISavable objects 
/// that will be managed (saved, loaded) by the local save system.
/// </summary>
[Obsolete("Legacy API. Use SaveKey<T> + SaveStore instead.")]
public interface ISaveFactory : IDisposable
{
	/// <summary>
	/// Creates an array of savable objects. Each object in the array must implement ISavable.
	/// The local save system will later store and retrieve these objects.
	/// </summary>
	/// <returns>An array of ISavable objects to be managed by the save system.</returns>
	public ISavable[] CreateSaves();
}
}