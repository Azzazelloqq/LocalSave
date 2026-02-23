using System.Collections.Generic;
using System.IO;
using LocalSaveSystem;
using LocalSaveSystem.Legacy;
using UnityEngine;

namespace LocalSaveSystem.Example
{
[SaveModel]
[SaveVersion(2)]
public struct PlayerState
{
	public int Level;
	[SaveFieldId("gold")] public long Coins;
	public Vector3 Position;
	[SaveIgnore] public int SessionCache;
	public float CritChance { get; set; }
}

[SaveModel]
public struct GameSettings : ISaveAfterLoad
{
	[SaveFieldId("music")] public float MusicVolume { get; set; }
	[SaveFieldId("sfx")] public float SfxVolume { get; set; }

	[SaveIgnore] public bool IsDirty { get; set; }

	public void OnAfterLoad()
	{
		MusicVolume = Mathf.Clamp01(MusicVolume);
		SfxVolume = Mathf.Clamp01(SfxVolume);
	}
}

public static class SaveKeys
{
	public static readonly SaveKey<PlayerState> PlayerState = new SaveKey<PlayerState>("player_state");
	public static readonly SaveKey<int> SessionCount = new SaveKey<int>("session_count");
	public static readonly SaveKey<GameSettings> GameSettings =
		new SaveKey<GameSettings>("game_settings", () => new GameSettings { MusicVolume = 0.8f, SfxVolume = 0.8f });
	public static readonly SaveKey<InventorySnapshot> Inventory =
		new SaveKey<InventorySnapshot>("inventory", () => new InventorySnapshot());
}

public sealed class PlayerStateMigratorV1ToV2 : SaveMigrator<PlayerState>
{
	public override int FromVersion => 1;
	public override int ToVersion => 2;

	public override PlayerState Migrate(PlayerState value)
	{
		// Example migration: clamp level and give a small gold bonus.
		if (value.Level < 1)
		{
			value.Level = 1;
		}

		value.Coins += 100;
		return value;
	}
}

public sealed class InventorySnapshot
{
	public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
}

public sealed class InventorySnapshotSerializer : SaveSerializer<InventorySnapshot>
{
	public override int Version => 1;

	public override void Write(BinaryWriter writer, InventorySnapshot value, SaveRegistry registry)
	{
		if (value == null)
		{
			writer.Write(false);
			return;
		}

		writer.Write(true);
		registry.Write(writer, value.Items);
	}

	public override InventorySnapshot Read(BinaryReader reader, SaveRegistry registry)
	{
		var hasValue = reader.ReadBoolean();
		if (!hasValue)
		{
			return null;
		}

		var snapshot = new InventorySnapshot();
		snapshot.Items = registry.Read<Dictionary<string, int>>(reader) ?? new Dictionary<string, int>();
		return snapshot;
	}
}

public sealed class LocalSaveSystemExample : MonoBehaviour
{
	[SerializeField] private bool _importLegacyOnStart = true;
	[SerializeField] private string _legacyFileName = "Saves.dat";
	[SerializeField] private bool _useTaggedFormat = true;

	private SaveStore _store;

	private void Awake()
	{
		var savePath = Path.Combine(Application.persistentDataPath, "SaveData");
		var migrators = new SaveMigratorRegistry();
		migrators.Register(new PlayerStateMigratorV1ToV2());

		var options = new SaveStoreOptions(savePath)
		{
			AutoSavePeriodSeconds = 5,
			FileExtension = ".lss2",
			SaveOnQuit = true,
			UseTaggedFormat = _useTaggedFormat
		};

		_store = new SaveStore(options, migrators: migrators);
		_store.Registry.Register(new InventorySnapshotSerializer());
		_store.RegisterKey(SaveKeys.PlayerState);
		_store.RegisterKey(SaveKeys.SessionCount);
		_store.RegisterKey(SaveKeys.GameSettings);
		_store.RegisterKey(SaveKeys.Inventory);

		if (_importLegacyOnStart)
		{
			var legacyPath = Path.Combine(savePath, _legacyFileName);
			LegacyBinaryFormatterImporter.ImportInto(_store, legacyPath);
		}

		_store.StartAutoSave();
	}

	public void AddGold(int amount)
	{
		_store.Update(SaveKeys.PlayerState, (ref PlayerState state) =>
		{
			state.Coins += amount;
		});
	}

	public void Teleport(Vector3 position)
	{
		using (var handle = _store.Edit(SaveKeys.PlayerState))
		{
			handle.Value.Position = position;
		}
	}

	public void IncrementSession()
	{
		_store.Update(SaveKeys.SessionCount, (ref int count) => { count++; });
	}

	public void SetMusicVolume(float volume)
	{
		_store.Update(SaveKeys.GameSettings, (ref GameSettings settings) =>
		{
			settings.MusicVolume = Mathf.Clamp01(volume);
			settings.IsDirty = true;
		});
	}

	public void AddItem(string itemId, int amount)
	{
		using (var handle = _store.Edit(SaveKeys.Inventory))
		{
			if (handle.Value.Items == null)
			{
				handle.Value.Items = new Dictionary<string, int>();
			}

			handle.Value.Items.TryGetValue(itemId, out var count);
			handle.Value.Items[itemId] = count + amount;
		}
	}

	public bool TryGetPlayerState(out PlayerState state)
	{
		return _store.TryGet(SaveKeys.PlayerState, out state);
	}

	public void SaveNow()
	{
		_store.ForceSave();
	}

	public void MarkDirty()
	{
		_store.Save();
	}

	public void ResetAll()
	{
		_store.DeleteAll();
	}

	private void OnDestroy()
	{
		_store?.Dispose();
	}
}
}
