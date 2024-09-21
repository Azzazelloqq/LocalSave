using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalSaveSystem.Factory;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace LocalSaveSystem.UnityLocalSaveSystem
{
/// <summary>
/// A Unity-specific implementation of the local save system using JSON serialization.
/// </summary>
public class UnityJsonLocalSaveSystem : ILocalSaveSystem
{
	private const string FileName = "Saves.json";
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
	private static ISavable[] _savesCacheDev;

	private static string SaveDirectoryPathDev =>
		Path.Combine(Application.persistentDataPath, "YourGameName", "SaveData");

	private static string SaveFilePath =>
		Path.Combine(SaveDirectoryPathDev, FileName);
	#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="UnityJsonLocalSaveSystem"/> class.
	/// </summary>
	/// <param name="storagePath">The path where save files will be stored.</param>
	/// <param name="autoSavePeriodPerSeconds">The period for auto-saving in seconds.</param>
	public UnityJsonLocalSaveSystem(string storagePath, int autoSavePeriodPerSeconds = 3)
	{
		_storagePath = storagePath;
		_filePath = Path.Combine(_storagePath, FileName);
		_autoSavePeriodPerSeconds = autoSavePeriodPerSeconds;
		SubscribeOnEvents();
	}

	/// <summary>
	/// Initializes the saves using a save factory.
	/// </summary>
	/// <param name="saveFactory">The factory that creates savable objects.</param>
	public void InitializeSaves(ISaveFactory saveFactory)
	{
		var saves = saveFactory.CreateSaves();
		_savesCache = saves ?? throw new ArgumentNullException(nameof(saves));
		_loadedSaves = LoadSaves();

		ParseSavesFromStorage();
	}

	/// <summary>
	/// Asynchronously initializes the saves using a save factory.
	/// </summary>
	/// <param name="saveFactory">The factory that creates savable objects.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	public async Task InitializeSavesAsync(ISaveFactory saveFactory, CancellationToken cancellationToken)
	{
		var saves = saveFactory.CreateSaves();

		_savesCache = saves ?? throw new ArgumentNullException(nameof(saves));
		_loadedSaves = await LoadSavesAsync(cancellationToken);

		ParseSavesFromStorage();
	}
	
	/// <summary>
	/// Releases all resources used by the <see cref="UnityJsonLocalSaveSystem"/>.
	/// </summary>
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

	/// <summary>
	/// Starts the auto-save process.
	/// </summary>
	public void StartAutoSave()
	{
		lock (_lock)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UnityJsonLocalSaveSystem));
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

	/// <summary>
	/// Stops the auto-save process.
	/// </summary>
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

	public bool IsHaveSaveInt(string id)
	{
		EnsureInitialized();
		return PlayerPrefs.HasKey(id);
	}

	public bool IsHaveSaveFloat(string id)
	{
		EnsureInitialized();
		return PlayerPrefs.HasKey(id);
	}

	public bool IsHaveSaveString(string id)
	{
		EnsureInitialized();
		return PlayerPrefs.HasKey(id);
	}

	public void SaveInt(string id, int data)
	{
		EnsureInitialized();
		PlayerPrefs.SetInt(id, data);
		_needSaveToStorage = true;
	}

	public void SaveFloat(string id, float data)
	{
		EnsureInitialized();
		PlayerPrefs.SetFloat(id, data);
		_needSaveToStorage = true;
	}

	public void SaveString(string id, string data)
	{
		EnsureInitialized();
		PlayerPrefs.SetString(id, data);
		_needSaveToStorage = true;
	}

	public void Save()
	{
		EnsureInitialized();
		_needSaveToStorage = true;
	}

	public int LoadInt(string id)
	{
		EnsureInitialized();

		if (PlayerPrefs.HasKey(id))
		{
			return PlayerPrefs.GetInt(id);
		}

		Debug.LogError($"Cannot find data by id {id}");
		return default;
	}

	public float LoadFloat(string id)
	{
		EnsureInitialized();

		if (PlayerPrefs.HasKey(id))
		{
			return PlayerPrefs.GetFloat(id);
		}

		Debug.LogError($"Cannot find data by id {id}");
		return default;
	}

	public string LoadString(string id)
	{
		EnsureInitialized();

		if (PlayerPrefs.HasKey(id))
		{
			return PlayerPrefs.GetString(id);
		}

		Debug.LogError($"Cannot find data by id {id}");
		return default;
	}

	public T Load<T>() where T : ISavable
	{
		EnsureInitialized();

		foreach (var savable in _savesCache)
		{
			if (savable is T savedData)
			{
				return savedData;
			}
		}

		Debug.LogError(
			$"Cannot find {typeof(T)}. Ensure the savable is added to {nameof(ISavable)}[] in the {nameof(InitializeSaves)} method.");
		return default;
	}

	/// <summary>
	/// Forces an immediate save to storage.
	/// </summary>
	public void ForceUpdateStorageSaves()
	{
		StopAutoSave();
		SaveAll();
	}

	#if DEVELOPMENT_BUILD || UNITY_EDITOR
	
	/// <summary>
	/// Deletes all saves during development or in the Unity Editor.
	/// </summary>
	public void DeleteAllSavesDev()
	{
		UnsubscribeOnEvents();
		StopAutoSave();
		PlayerPrefs.DeleteAll();

		if (!Directory.Exists(SaveDirectoryPathDev))
		{
			return;
		}

		if (File.Exists(SaveFilePath))
		{
			File.Delete(SaveFilePath);
		}

		Directory.Delete(SaveDirectoryPathDev, true);
	}
	#endif

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
	/// Ensures that the save system has been initialized.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown if the saves have not been initialized.</exception>
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
	/// Parses saves from the storage and initializes the savable objects.
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
	/// Subscribes to necessary application events.
	/// </summary>
	private void SubscribeOnEvents()
	{
		Application.quitting += OnApplicationQuitting;
	}

	/// <summary>
	/// Unsubscribes from application events.
	/// </summary>
	private void UnsubscribeOnEvents()
	{
		Application.quitting -= OnApplicationQuitting;
	}

	/// <summary>
	/// Handler for the application quitting event.
	/// </summary>
	private void OnApplicationQuitting()
	{
		StopAutoSave();
		SaveAll();
		UnsubscribeOnEvents();
	}

	/// <summary>
	/// Starts the auto-save loop.
	/// </summary>
	/// <param name="periodPerSeconds">The period between saves in seconds.</param>
	/// <param name="token">Cancellation token to cancel the operation.</param>
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
	/// Saves all data asynchronously.
	/// </summary>
	/// <param name="token">Cancellation token to cancel the operation.</param>
	private void SaveAll()
	{
		try
		{
			CreateDirectoryIfNeeded();
			var settings = new JsonSerializerSettings
			{
				TypeNameHandling = TypeNameHandling.Auto
			};

			var json = JsonConvert.SerializeObject(_savesCache, settings);
			File.WriteAllText(_filePath, json);
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to save data: {e.Message}");
		}
	}

	private async Task SaveAllAsync(CancellationToken token)
	{
		try
		{
			CreateDirectoryIfNeeded();
			var settings = new JsonSerializerSettings
			{
				TypeNameHandling = TypeNameHandling.Auto
			};

			var json = JsonConvert.SerializeObject(_savesCache, settings);
			await File.WriteAllTextAsync(_filePath, json, token);

			Debug.Log("[Save system] All data saved asynchronously");
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to save data asynchronously: {e.Message}");
		}
	}

	/// <summary>
	/// Loads saves from the storage.
	/// </summary>
	/// <returns>A dictionary of loaded savable objects.</returns>
	private Dictionary<string, ISavable> LoadSaves()
	{
		try
		{
			if (!File.Exists(_filePath))
			{
				return new Dictionary<string, ISavable>();
			}

			var settings = new JsonSerializerSettings
			{
				TypeNameHandling = TypeNameHandling.Auto
			};

			var json = File.ReadAllText(_filePath);
			var savesList = JsonConvert.DeserializeObject<ISavable[]>(json, settings);

			return savesList?.ToDictionary(s => s.SaveId) ?? new Dictionary<string, ISavable>();
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to load saves: {e.Message}");
			return new Dictionary<string, ISavable>();
		}
	}

	/// <summary>
	/// Asynchronously loads saves from the storage.
	/// </summary>
	/// <param name="token">Cancellation token to cancel the operation.</param>
	/// <returns>A dictionary of loaded savable objects.</returns>
	private async Task<Dictionary<string, ISavable>> LoadSavesAsync(CancellationToken token)
	{
		try
		{
			if (!File.Exists(_filePath))
			{
				return new Dictionary<string, ISavable>();
			}

			var settings = new JsonSerializerSettings
			{
				TypeNameHandling = TypeNameHandling.Auto
			};

			var json = await File.ReadAllTextAsync(_filePath, token);
			var savesList = JsonConvert.DeserializeObject<ISavable[]>(json, settings);

			return savesList?.ToDictionary(s => s.SaveId) ?? new Dictionary<string, ISavable>();
		}
		catch (OperationCanceledException)
		{
			// Операция отменена, возвращаем пустой словарь
			return new Dictionary<string, ISavable>();
		}
		catch (Exception e)
		{
			Debug.LogError($"Failed to load saves asynchronously: {e.Message}");
			return new Dictionary<string, ISavable>();
		}
	}

	/// <summary>
	/// Creates the storage directory if it does not exist.
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
	/// Deletes all saves in development mode or Unity Editor.
	/// </summary>
	[MenuItem("SaveSystem/DeleteAllSaves")]
	private static void DeleteSavesDev()
	{
		PlayerPrefs.DeleteAll();

		if (Directory.Exists(SaveDirectoryPathDev))
		{
			if (File.Exists(SaveFilePath))
			{
				File.Delete(SaveFilePath);
			}

			Directory.Delete(SaveDirectoryPathDev, true);
		}
	}

	/// <summary>
	/// Saves data for development purposes.
	/// </summary>
	public static void SaveDev()
	{
		if (!Directory.Exists(SaveDirectoryPathDev))
		{
			Directory.CreateDirectory(SaveDirectoryPathDev);
		}

		var settings = new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.Auto
		};

		var json = JsonConvert.SerializeObject(_savesCacheDev, settings);
		File.WriteAllText(SaveFilePath, json);
	}

	/// <summary>
	/// Gets a specific savable object for development purposes.
	/// </summary>
	/// <typeparam name="TSavable">The type of the savable object.</typeparam>
	/// <param name="allSavables">Array of all savable objects.</param>
	/// <returns>The requested savable object.</returns>
	public static TSavable GetSave<TSavable>(ISavable[] allSavables) where TSavable : ISavable
	{
		_savesCacheDev = allSavables;
		var loadedSaves = new Dictionary<string, ISavable>();

		if (File.Exists(SaveFilePath))
		{
			var settings = new JsonSerializerSettings
			{
				TypeNameHandling = TypeNameHandling.Auto
			};

			var json = File.ReadAllText(SaveFilePath);
			var savesList = JsonConvert.DeserializeObject<ISavable[]>(json, settings);

			if (savesList != null)
			{
				loadedSaves = savesList.ToDictionary(s => s.SaveId);
			}
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
			if (savable is TSavable foundSave)
			{
				return foundSave;
			}
		}

		return default;
	}
	#endif
}
}