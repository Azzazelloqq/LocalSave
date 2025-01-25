using System;
using System.Threading;
using System.Threading.Tasks;
using LocalSaveSystem.Factory;

namespace LocalSaveSystem
{
/// <summary>
/// Provides an interface for a local save system that stores,
/// loads, and manages different types of game-related data.
/// </summary>
public interface ILocalSaveSystem : IDisposable
{
	/// <summary>
	/// Initializes the savable objects using the specified factory in a synchronous manner.
	/// </summary>
	/// <param name="saveFactory">The factory that creates ISavable objects.</param>
	public void InitializeSaves(ISaveFactory saveFactory);

	/// <summary>
	/// Initializes the savable objects using the specified factory asynchronously.
	/// </summary>
	/// <param name="saveFactory">The factory that creates ISavable objects.</param>
	/// <param name="cancellationToken">A token to signal the operation should be canceled.</param>
	public Task InitializeSavesAsync(ISaveFactory saveFactory, CancellationToken cancellationToken);

	/// <summary>
	/// Starts the automatic save process with a predefined interval.
	/// </summary>
	public void StartAutoSave();

	/// <summary>
	/// Stops the automatic save process.
	/// </summary>
	public void StopAutoSave();

	/// <summary>
	/// Checks if there is a stored integer value under the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <returns>True if the integer data exists; otherwise, false.</returns>
	public bool IsHaveSaveInt(string id);

	/// <summary>
	/// Checks if there is a stored float value under the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <returns>True if the float data exists; otherwise, false.</returns>
	public bool IsHaveSaveFloat(string id);

	/// <summary>
	/// Checks if there is a stored string value under the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <returns>True if the string data exists; otherwise, false.</returns>
	public bool IsHaveSaveString(string id);

	/// <summary>
	/// Saves an integer value under the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <param name="data">The integer data to save.</param>
	public void SaveInt(string id, int data);

	/// <summary>
	/// Saves a float value under the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <param name="data">The float data to save.</param>
	public void SaveFloat(string id, float data);

	/// <summary>
	/// Saves a string value under the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <param name="data">The string data to save.</param>
	public void SaveString(string id, string data);

	/// <summary>
	/// Marks that the system should save all in-memory changes to storage.
	/// (Note that the actual write to disk may happen in an auto-save loop
	/// unless forced.)
	/// </summary>
	public void Save();

	/// <summary>
	/// Loads an integer value by the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <returns>The loaded integer if it exists, otherwise a default value.</returns>
	public int LoadInt(string id);

	/// <summary>
	/// Loads a float value by the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <returns>The loaded float if it exists, otherwise a default value.</returns>
	public float LoadFloat(string id);

	/// <summary>
	/// Loads a string value by the specified key.
	/// </summary>
	/// <param name="id">The key identifier.</param>
	/// <returns>The loaded string if it exists, otherwise a default (null or empty string).</returns>
	public string LoadString(string id);

	/// <summary>
	/// Loads a concrete ISavable object by its interface type.
	/// </summary>
	/// <typeparam name="T">The type of ISavable object to load.</typeparam>
	/// <returns>The loaded object if found, otherwise default.</returns>
	public T Load<T>() where T : ISavable;

	/// <summary>
	/// Forces immediate saving of all changes to storage, bypassing any auto-save schedule.
	/// </summary>
	public void ForceUpdateStorageSaves();

	#if DEVELOPMENT_BUILD || UNITY_EDITOR
	/// <summary>
	/// Deletes all saved data (including PlayerPrefs and files) in development or editor mode.
	/// </summary>
	public void DeleteAllSavesDev();
	#endif

	/// <summary>
	/// Deletes all saved data (including PlayerPrefs and files) in release mode.
	/// </summary>
	public void DeleteAllSaves();
}
}