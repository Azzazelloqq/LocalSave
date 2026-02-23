using System;

namespace LocalSaveSystem
{
/// <summary>
/// Represents an entity that can be saved and loaded by the local save system.
/// Each savable object must have a unique identifier (SaveId) and 
/// provide methods to initialize new data and copy from loaded data.
/// </summary>
[Obsolete("Legacy API. Use SaveKey<T> + SaveStore instead.")]
public interface ISavable
{
	/// <summary>
	/// A unique string identifier that the save system uses to store and load this object.
	/// </summary>
	string SaveId { get; }

	/// <summary>
	/// Initializes this savable object with default or "new" data values.
	/// Typically called when there's no existing data on disk.
	/// </summary>
	void InitializeAsNewSave();

	/// <summary>
	/// Copies over fields from a loaded savable object to restore the previously saved state.
	/// </summary>
	/// <param name="loadedSavable">The loaded savable object from which to copy data.</param>
	void CopyFrom(ISavable loadedSavable);
}
}