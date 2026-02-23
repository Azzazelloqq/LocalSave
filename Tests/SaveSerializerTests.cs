using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using LocalSaveSystem.Legacy;
using NUnit.Framework;

namespace LocalSaveSystem.Tests
{
	[TestFixture]
	public class SaveSerializerTests
	{
		[Serializable]
		private sealed class CustomModel
		{
			public int Value;
		}

		private sealed class CustomModelSerializer : SaveSerializer<CustomModel>
		{
			public static int WriteCount;
			public static int ReadCount;

			public override int Version => 1;

			public override void Write(BinaryWriter writer, CustomModel value, SaveRegistry registry)
			{
				WriteCount++;
				if (value == null)
				{
					writer.Write(false);
					return;
				}

				writer.Write(true);
				writer.Write(value.Value + 1);
			}

			public override CustomModel Read(BinaryReader reader, SaveRegistry registry)
			{
				ReadCount++;
				if (!reader.ReadBoolean())
				{
					return null;
				}

				return new CustomModel { Value = reader.ReadInt32() - 1 };
			}
		}

		#pragma warning disable CS0618
		[Serializable]
		private sealed class LegacySavable : ISavable
		{
			public string SaveId => "legacy_state";
			public int Value;

			public void InitializeAsNewSave()
			{
				Value = 1;
			}

			public void CopyFrom(ISavable loadedSavable)
			{
				if (loadedSavable is LegacySavable typed)
				{
					Value = typed.Value;
				}
			}
		}
		#pragma warning restore CS0618

		[Test]
		public void SaveRegistry_UsesCustomSerializer()
		{
			CustomModelSerializer.WriteCount = 0;
			CustomModelSerializer.ReadCount = 0;

			var registry = SaveRegistry.CreateDefault(new SaveSerializationOptions { UseTaggedFormat = true });
			registry.Register(new CustomModelSerializer());

			var bytes = registry.Serialize(new CustomModel { Value = 5 });
			var loaded = registry.Deserialize<CustomModel>(bytes);

			Assert.AreEqual(5, loaded.Value);
			Assert.AreEqual(1, CustomModelSerializer.WriteCount);
			Assert.AreEqual(1, CustomModelSerializer.ReadCount);
		}

		[Test]
		public void LegacyImporter_ImportsSavable()
		{
			var path = TestUtilities.CreateTempPath();
			try
			{
				var legacyFile = Path.Combine(path, "Saves.dat");
				var legacy = new LegacySavable { Value = 42 };

				#pragma warning disable SYSLIB0011
				using (var fs = new FileStream(legacyFile, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					var formatter = new BinaryFormatter();
					formatter.Serialize(fs, new ISavable[] { legacy });
				}
				#pragma warning restore SYSLIB0011

				var options = new SaveStoreOptions(path);
				var store = new SaveStore(options);
				var key = new SaveKey<LegacySavable>(legacy.SaveId);
				store.RegisterKey(key);

				var summary = LegacyBinaryFormatterImporter.ImportInto(store, legacyFile);
				Assert.AreEqual(1, summary.Imported);

				var loaded = store.Get(key);
				Assert.AreEqual(42, loaded.Value);
				store.Dispose();

				var storeReloaded = new SaveStore(options);
				storeReloaded.RegisterKey(key);
				var reloaded = storeReloaded.Get(key);
				Assert.AreEqual(42, reloaded.Value);
				storeReloaded.Dispose();
			}
			finally
			{
				TestUtilities.Cleanup(path);
			}
		}
	}
}
