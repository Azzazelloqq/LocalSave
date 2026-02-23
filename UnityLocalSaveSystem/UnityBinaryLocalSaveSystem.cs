using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using LocalSaveSystem.Factory;
using UnityEditor;
using UnityEngine;

namespace LocalSaveSystem
{
/// <summary>
/// A Unity-specific implementation of ILocalSaveSystem that uses BinaryFormatter
/// to serialize and deserialize an array of ISavable objects to/from a file on disk.
/// It also integrates with Unity events (such as application quitting) and provides
/// auto-save functionality.
/// </summary>
[Obsolete("Legacy BinaryFormatter implementation. Use SaveStore with binary entries instead.")]
public class UnityBinaryLocalSaveSystem : ILocalSaveSystem
{
	/// <summary>
	/// The default file name for storing binary data.
	/// </summary>
	private const string FileName = "Saves.dat"; // Можно поменять расширение на .bin или любое другое
	
	private ISavable[] _savesCache;
	private Dictionary<string, ISavable> _loadedSaves;
	private bool _needSaveToStorage;
	private readonly string _storagePath;
	private readonly string _filePath;
	private CancellationTokenSource _cancellationTokenSource;
	private readonly int _autoSavePeriodPerSeconds;
	private readonly object _lock = new();
	private bool _isDisposed;

	#if UNITY_EDITOR || DEVELOPMENT_BUILD
	// Fields for development or editor mode usage.
	private static ISavable[] _savesCacheDev;

	/// <summary>
	/// A sample path for storing data in development or editor mode.
	/// </summary>
	private static string SaveDirectoryPathDev =>
		Path.Combine(Application.persistentDataPath, "YourGameName", "SaveData");

	private static string SaveFilePathDev =>
		Path.Combine(SaveDirectoryPathDev, FileName);
	#endif

	/// <summary>
	/// Creates a new instance of UnityBinaryLocalSaveSystem with a given storage path and auto-save period.
	/// </summary>
	/// <param name="storagePath">The path where save files will be stored.</param>
	/// <param name="autoSavePeriodPerSeconds">The frequency, in seconds, for auto-saving the data.</param>
	public UnityBinaryLocalSaveSystem(string storagePath, int autoSavePeriodPerSeconds = 3)
	{
		_storagePath = storagePath;
		_filePath = Path.Combine(_storagePath, FileName);
		_autoSavePeriodPerSeconds = autoSavePeriodPerSeconds;
		SubscribeOnEvents();
	}

	public void InitializeSaves(ISaveFactory saveFactory)
	{
		var saves = saveFactory.CreateSaves();
		_savesCache = saves ?? throw new ArgumentNullException(nameof(saves));
		_loadedSaves = LoadSaves();

		ParseSavesFromStorage();
	}

	/// <inheritdoc />
	public async Task InitializeSavesAsync(ISaveFactory saveFactory, CancellationToken cancellationToken)
	{
		var saves = saveFactory.CreateSaves();
		_savesCache = saves ?? throw new ArgumentNullException(nameof(saves));
		_loadedSaves = await LoadSavesAsync(cancellationToken);

		ParseSavesFromStorage();
	}

	/// <inheritdoc />
	public void Dispose()
	{
		lock (_lock)
		{
			if (_isDisposed)
			{
				return;
			}

			StopAutoSave();
			_cancellationTokenSource?.Dispose();
			_isDisposed = true;
		}
	}

	/// <inheritdoc />
	public void StartAutoSave()
	{
		lock (_lock)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UnityBinaryLocalSaveSystem));
			}

			var autosaveIsAlreadyStarted =
				_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;

			if (autosaveIsAlreadyStarted)
			{
				return;
			}

			_cancellationTokenSource = new CancellationTokenSource();
			AutoSaveLoop(_autoSavePeriodPerSeconds, _cancellationTokenSource.Token);
		}
	}

	/// <inheritdoc />
	public void StopAutoSave()
	{
		lock (_lock)
		{
			if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
			}
		}
	}

	/// <inheritdoc />
	public bool IsHaveSaveInt(string id)
	{
		EnsureInitialized();
		return PlayerPrefs.HasKey(id);
	}

	/// <inheritdoc />
	public bool IsHaveSaveFloat(string id)
	{
		EnsureInitialized();
		return PlayerPrefs.HasKey(id);
	}

	/// <inheritdoc />
	public bool IsHaveSaveString(string id)
	{
		EnsureInitialized();
		return PlayerPrefs.HasKey(id);
	}

	/// <inheritdoc />
	public void SaveInt(string id, int data)
	{
		EnsureInitialized();
		PlayerPrefs.SetInt(id, data);
		_needSaveToStorage = true;
	}

	/// <inheritdoc />
	public void SaveFloat(string id, float data)
	{
		EnsureInitialized();
		PlayerPrefs.SetFloat(id, data);
		_needSaveToStorage = true;
	}

	/// <inheritdoc />
	public void SaveString(string id, string data)
	{
		EnsureInitialized();
		PlayerPrefs.SetString(id, data);
		_needSaveToStorage = true;
	}

	/// <inheritdoc />
	public void Save()
	{
		EnsureInitialized();
		_needSaveToStorage = true;
	}

	/// <inheritdoc />
	public int LoadInt(string id)
	{
		EnsureInitialized();
		if (PlayerPrefs.HasKey(id))
		{
			return PlayerPrefs.GetInt(id);
		}

		Debug.LogError($"Cannot find data by id {id}");
		return 0;
	}

	/// <inheritdoc />
	public float LoadFloat(string id)
	{
		EnsureInitialized();
		if (PlayerPrefs.HasKey(id))
		{
			return PlayerPrefs.GetFloat(id);
		}

		Debug.LogError($"Cannot find data by id {id}");
		return 0;
	}

	/// <inheritdoc />
	public string LoadString(string id)
	{
		EnsureInitialized();
		if (PlayerPrefs.HasKey(id))
		{
			return PlayerPrefs.GetString(id);
		}

		Debug.LogError($"Cannot find data by id {id}");
		return null;
	}

	/// <inheritdoc />
	public T Load<T>() where T : ISavable
	{
		EnsureInitialized();
		foreach (var savable in _savesCache)
		{
			if (savable is T typed)
			{
				return typed;
			}
		}

		Debug.LogError(
			$"Cannot find {typeof(T)}. Ensure the savable is added to ISavable[] in the InitializeSaves method.");
		return default;
	}

	/// <inheritdoc />
	public void ForceUpdateStorageSaves()
	{
		StopAutoSave();
		SaveAll();
	}

	#if UNITY_EDITOR || DEVELOPMENT_BUILD
	/// <inheritdoc />
	public void DeleteAllSavesDev()
	{
		UnsubscribeOnEvents();
		StopAutoSave();
		PlayerPrefs.DeleteAll();

		if (Directory.Exists(SaveDirectoryPathDev))
		{
			if (File.Exists(SaveFilePathDev))
			{
				File.Delete(SaveFilePathDev);
			}

			Directory.Delete(SaveDirectoryPathDev, true);
		}
	}
	#endif

	/// <inheritdoc />
	public void DeleteAllSaves()
	{
		EnsureInitialized();
		PlayerPrefs.DeleteAll();

		if (Directory.Exists(_storagePath))
		{
			if (File.Exists(_filePath))
			{
				File.Delete(_filePath);
			}

			Directory.Delete(_storagePath, true);
		}

		foreach (var savable in _savesCache)
		{
			savable.InitializeAsNewSave();
		}
	}

	/// <summary>
	/// Ensures the local save system has been initialized with savable objects.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the system is used before initialization.</exception>
	private void EnsureInitialized()
	{
		if (_savesCache != null)
		{
			return;
		}

		Debug.LogError("Initialize the saves before using");
		throw new InvalidOperationException("Saves have not been initialized. Call InitializeSaves() first.");
	}

	/// <summary>
	/// Applies the loaded data to each object in <see cref="_savesCache"/> by matching their <see cref="ISavable.SaveId"/>.
	/// If no data was loaded, initializes each object with default (new) values.
	/// </summary>
	private void ParseSavesFromStorage()
	{
		if (_loadedSaves == null || _loadedSaves.Count == 0)
		{
			foreach (var savable in _savesCache)
			{
				savable.InitializeAsNewSave();
			}

			return;
		}

		foreach (var savable in _savesCache)
		{
			if (_loadedSaves.TryGetValue(savable.SaveId, out var loadedSavable))
			{
				savable.CopyFrom(loadedSavable);
			}
			else
			{
				savable.InitializeAsNewSave();
			}
		}
	}

	/// <summary>
	/// Subscribes to Unity's application quitting event to handle final saving.
	/// </summary>
	private void SubscribeOnEvents()
	{
		Application.quitting += OnApplicationQuitting;
	}

	/// <summary>
	/// Unsubscribes from Unity's application quitting event.
	/// </summary>
	private void UnsubscribeOnEvents()
	{
		Application.quitting -= OnApplicationQuitting;
	}

	/// <summary>
	/// Called when the application is quitting. Stops auto-save and performs a final save.
	/// </summary>
	private void OnApplicationQuitting()
	{
		StopAutoSave();
		SaveAll();
		UnsubscribeOnEvents();
	}

	/// <summary>
	/// Manages periodic saving in a loop until canceled.
	/// </summary>
	/// <param name="periodPerSeconds">Time interval between save attempts in seconds.</param>
	/// <param name="token">Cancellation token to stop auto-saving.</param>
	private async void AutoSaveLoop(int periodPerSeconds, CancellationToken token)
	{
		var periodPerMilliseconds = periodPerSeconds * 1000;

		while (!token.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(periodPerMilliseconds, token);

				if (!_needSaveToStorage)
				{
					continue;
				}

				await SaveAllAsync(token);
				_needSaveToStorage = false;
			}
			catch (OperationCanceledException)
			{
				Debug.Log("[Save system] AutoSaveLoop was canceled");
				break;
			}
			catch (Exception e)
			{
				Debug.LogError(e);
				break;
			}
		}
	}

	/// <summary>
	/// Performs a synchronous save of all cached ISavable objects to disk.
	/// </summary>
	private void SaveAll()
	{
		try
		{
			CreateDirectoryIfNeeded();

			// Сериализуем _savesCache в бинарник
			using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
			var formatter = new BinaryFormatter();
			formatter.Serialize(fs, _savesCache);
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to save data: {e.Message}");
		}
	}

	/// <summary>
	/// Performs an asynchronous save of all cached ISavable objects to disk.
	/// </summary>
	/// <param name="token">Cancellation token for the save operation.</param>
	private async Task SaveAllAsync(CancellationToken token)
	{
		try
		{
			CreateDirectoryIfNeeded();

			byte[] bytes;
			using (var ms = new MemoryStream())
			{
				var formatter = new BinaryFormatter();
				formatter.Serialize(ms, _savesCache);
				bytes = ms.ToArray();
			}

			await File.WriteAllBytesAsync(_filePath, bytes, token);

			Debug.Log("[Save system] All data saved asynchronously (binary)");
		}
		catch (OperationCanceledException)
		{
			Debug.Log("[Save system] Save operation was canceled");
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to save data asynchronously: {e.Message}");
		}
	}

	/// <summary>
	/// Loads any saved ISavable objects from disk in a synchronous manner.
	/// </summary>
	/// <returns>A dictionary mapping SaveId to the deserialized objects.</returns>
	private Dictionary<string, ISavable> LoadSaves()
	{
		try
		{
			if (!File.Exists(_filePath))
			{
				return new Dictionary<string, ISavable>();
			}

			using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			var formatter = new BinaryFormatter();
			var savesArray = (ISavable[])formatter.Deserialize(fs);
			return savesArray?.ToDictionary(s => s.SaveId) ?? new Dictionary<string, ISavable>();
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to load saves: {e.Message}");
			return new Dictionary<string, ISavable>();
		}
	}

	/// <summary>
	/// Loads any saved ISavable objects from disk asynchronously.
	/// </summary>
	/// <param name="token">Cancellation token for the load operation.</param>
	/// <returns>A dictionary mapping SaveId to the deserialized objects.</returns>
	private async Task<Dictionary<string, ISavable>> LoadSavesAsync(CancellationToken token)
	{
		try
		{
			if (!File.Exists(_filePath))
			{
				return new Dictionary<string, ISavable>();
			}

			var bytes = await File.ReadAllBytesAsync(_filePath, token);

			using var ms = new MemoryStream(bytes);
			var formatter = new BinaryFormatter();
			var savesArray = (ISavable[])formatter.Deserialize(ms);
			return savesArray.ToDictionary(s => s.SaveId);
		}
		catch (OperationCanceledException)
		{
			// Операция отменена, вернём пустой словарь
			return new Dictionary<string, ISavable>();
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to load saves asynchronously: {e.Message}");
			return new Dictionary<string, ISavable>();
		}
	}

	/// <summary>
	/// Ensures that the target directory for save files exists, creating it if necessary.
	/// </summary>
	private void CreateDirectoryIfNeeded()
	{
		if (!Directory.Exists(_storagePath))
		{
			Directory.CreateDirectory(_storagePath);
		}
	}

	#if UNITY_EDITOR
	/// <summary>
	/// Deletes all binary save files in development or editor mode via a menu item.
	/// </summary>
	[MenuItem("SaveSystem/DeleteAllBinarySaves")]
	private static void DeleteSavesDevMenu()
	{
		PlayerPrefs.DeleteAll();

		if (Directory.Exists(SaveDirectoryPathDev))
		{
			if (File.Exists(SaveFilePathDev))
			{
				File.Delete(SaveFilePathDev);
			}

			Directory.Delete(SaveDirectoryPathDev, true);
		}
	}

	/// <summary>
	/// Saves data in development or editor mode by serializing the provided ISavable array.
	/// </summary>
	/// <param name="savesCache">An array of ISavable objects to serialize.</param>
	public static void SaveDev(ISavable[] savesCache)
	{
		_savesCacheDev = savesCache;
		if (!Directory.Exists(SaveDirectoryPathDev))
		{
			Directory.CreateDirectory(SaveDirectoryPathDev);
		}

		var path = SaveFilePathDev;
		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
		var formatter = new BinaryFormatter();
		formatter.Serialize(fs, _savesCacheDev);
	}

	/// <summary>
	/// Retrieves a specific savable object of type TSavable from the saved data in development or editor mode.
	/// </summary>
	/// <typeparam name="TSavable">The type of ISavable object to find.</typeparam>
	/// <param name="allSavables">An array of all possible ISavable objects in the project.</param>
	/// <returns>The loaded object if found, otherwise default.</returns>
	public static TSavable GetSaveDev<TSavable>(ISavable[] allSavables) where TSavable : ISavable
	{
		_savesCacheDev = allSavables;
		var loadedSaves = new Dictionary<string, ISavable>();

		var path = SaveFilePathDev;
		if (File.Exists(path))
		{
			using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			var formatter = new BinaryFormatter();
			var savesArray = (ISavable[])formatter.Deserialize(fs);
			loadedSaves = savesArray.ToDictionary(s => s.SaveId);
		}

		if (loadedSaves.Count == 0)
		{
			foreach (var savable in _savesCacheDev)
			{
				savable.InitializeAsNewSave();
			}
		}
		else
		{
			foreach (var savable in _savesCacheDev)
			{
				if (loadedSaves.TryGetValue(savable.SaveId, out var loadedSavable))
				{
					savable.CopyFrom(loadedSavable);
				}
				else
				{
					savable.InitializeAsNewSave();
				}
			}
		}

		foreach (var savable in _savesCacheDev)
		{
			if (savable is TSavable found)
			{
				return found;
			}
		}

		return default;
	}
	#endif
}
}