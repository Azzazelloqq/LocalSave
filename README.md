# LocalSaveSystem v2

Binary local save system with typed keys, struct-friendly updates, and optional code generation.

## Quick start

```csharp
using System.IO;
using LocalSaveSystem;
using UnityEngine;

[SaveModel]
[SaveVersion(1)]
public struct PlayerState
{
	public int Level;
	public int Gold;
}

public static class SaveKeys
{
	public static readonly SaveKey<PlayerState> PlayerState =
		new SaveKey<PlayerState>("player_state");
}

public sealed class SaveBootstrap
{
	private SaveStore _store;

	public void Initialize()
	{
		var path = Path.Combine(Application.persistentDataPath, "SaveData");
		_store = new SaveStore(new SaveStoreOptions(path));
		_store.RegisterKey(SaveKeys.PlayerState);
		_store.StartAutoSave();
	}

	public void AddGold(int amount)
	{
		_store.Update(SaveKeys.PlayerState, (ref PlayerState state) =>
		{
			state.Gold += amount;
		});
	}
}
```

## How to use

1) **Create keys**
- One `SaveKey<T>` per data type you want to store.
- Keep keys in a static class for reuse.

2) **Initialize store**
- Create `SaveStore` with `SaveStoreOptions` and a storage path.
- Register all keys once at startup.
- Start auto-save if you want periodic saving.

3) **Read & update**
- Use `Get`/`TryGet` for reads.
- Use `Set`, `Update` or `Edit` to modify values.
- `Update` and `Edit` are struct-friendly (work by ref).

4) **Save control**
- `Save()` marks dirty records; auto-save will persist them.
- `ForceSave()` writes immediately.

## Struct-friendly edits

- `Update<T>(SaveKey<T>, SaveUpdate<T>)` gives a `ref` to struct data
- `Edit<T>(SaveKey<T>)` returns a `SaveHandle<T>` for scoped edits

## Field identity

- By default, fields are matched by their **name** in tagged saves.
- If you rename a field, old data will be ignored and a warning is logged.
- To keep data across renames, pin a stable id:
- Duplicate `SaveFieldId` values log a warning once per type.

```csharp
[SaveModel]
public struct PlayerState
{
	[SaveFieldId("gold")] public int Gold;
}
```

## Serialization and versioning

- Register custom serializers via `SaveRegistry.Register`
- `SaveVersionAttribute` defines the current data version
- `SaveMigrator<T>` handles upgrades between versions

## Code generation (Roslyn)

- Add `[SaveModel]` to your data types.
- Build the generator project (`LocalSaveSystem.SourceGen`).
- The DLL is copied to `Assets/LocalSaveSystem/analyzers/` and Unity picks it up.
- Generated serializers are compiled into your assembly and registered automatically.

Notes:
- Only public fields/properties are emitted in source-generated serializers.
- If a type is not supported by the generator, reflection fallback will be used.

## Save formats

- **Tagged (default)**: field id + type + payload. Safe for renames/reorders.
- **Compact**: sequential fields without metadata. Smaller but schema-sensitive.
- `SaveFieldId` is only used in tagged format.

Toggle via `SaveStoreOptions.UseTaggedFormat`:

```csharp
var options = new SaveStoreOptions(path)
{
    UseTaggedFormat = false
};
```

If you create a registry manually, keep it in sync:

```csharp
var registry = SaveRegistry.CreateDefault(new SaveSerializationOptions
{
    UseTaggedFormat = false
});
var store = new SaveStore(options, registry: registry);
```

## Custom serializers

Custom `ISaveSerializer<T>` is responsible for the on-disk format. If you want to
respect the tagged/compact toggle, check `registry.Options.UseTaggedFormat`.

## Warnings

- Unknown field ids in tagged payloads are logged.
- Field type conversion failures are logged with expected/actual types.

## Tests

EditMode tests live in `Assets/LocalSaveSystem/Tests` and can be run via Unity Test Runner.

## Legacy import

Use `LocalSaveSystem.Legacy.LegacyBinaryFormatterImporter.ImportInto` to migrate
from the old `Saves.dat` format into v2 keys.