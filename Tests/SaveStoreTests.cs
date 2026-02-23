using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LocalSaveSystem.Tests
{
	[TestFixture]
	public class SaveStoreTests
	{
		[Serializable]
		private struct SimpleState
		{
			[SaveFieldId("level")] public int Level;
			[SaveFieldId("coins")] public long Coins;
			public string Name { get; set; }
		}

		[Serializable]
		private sealed class LifecycleState : ISaveInitializable, ISaveAfterLoad
		{
			public int Value;

			public void InitializeAsNew()
			{
				Value = 5;
			}

			public void OnAfterLoad()
			{
				Value = Mathf.Max(Value, 10);
			}
		}

		[Test]
		public void SaveStore_Roundtrip_Tagged()
		{
			var path = TestUtilities.CreateTempPath();
			var key = new SaveKey<SimpleState>("simple_state");
			try
			{
				var options = new SaveStoreOptions(path) { UseTaggedFormat = true };
				var store = new SaveStore(options);
				store.RegisterKey(key);

				store.Update(key, (ref SimpleState state) =>
				{
					state.Level = 7;
					state.Coins = 120;
					state.Name = "Alice";
				});

				using (var handle = store.Edit(key))
				{
					handle.Value.Coins += 30;
				}

				store.ForceSave();
				store.Dispose();

				var storeReloaded = new SaveStore(options);
				storeReloaded.RegisterKey(key);
				var loaded = storeReloaded.Get(key);
				Assert.AreEqual(7, loaded.Level);
				Assert.AreEqual(150, loaded.Coins);
				Assert.AreEqual("Alice", loaded.Name);
				storeReloaded.Dispose();
			}
			finally
			{
				TestUtilities.Cleanup(path);
			}
		}

		[Test]
		public void SaveStore_Roundtrip_Compact()
		{
			var path = TestUtilities.CreateTempPath();
			var key = new SaveKey<SimpleState>("simple_state_compact");
			try
			{
				var options = new SaveStoreOptions(path) { UseTaggedFormat = false };
				var store = new SaveStore(options);
				store.RegisterKey(key);
				store.Set(key, new SimpleState { Level = 3, Coins = 5, Name = "Bob" });
				store.ForceSave();
				store.Dispose();

				var storeReloaded = new SaveStore(options);
				storeReloaded.RegisterKey(key);
				var loaded = storeReloaded.Get(key);
				Assert.AreEqual(3, loaded.Level);
				Assert.AreEqual(5, loaded.Coins);
				Assert.AreEqual("Bob", loaded.Name);
				storeReloaded.Dispose();
			}
			finally
			{
				TestUtilities.Cleanup(path);
			}
		}

		[Test]
		public void SaveStore_DeleteAll_ResetsData()
		{
			var path = TestUtilities.CreateTempPath();
			var key = new SaveKey<LifecycleState>("lifecycle_state");
			try
			{
				var options = new SaveStoreOptions(path) { UseTaggedFormat = true };
				var store = new SaveStore(options);
				store.RegisterKey(key);
				store.Set(key, new LifecycleState { Value = 42 });
				store.ForceSave();

				store.DeleteAll();
				var afterDelete = store.Get(key);
				Assert.AreEqual(5, afterDelete.Value);
				store.Dispose();
			}
			finally
			{
				TestUtilities.Cleanup(path);
			}
		}

		[Test]
		public void SaveStore_FileExtensionAndAtomicWrite()
		{
			var path = TestUtilities.CreateTempPath();
			var key = new SaveKey<SimpleState>("file_extension_test");
			try
			{
				var options = new SaveStoreOptions(path)
				{
					FileExtension = ".foo",
					UseAtomicWrite = true
				};

				var store = new SaveStore(options);
				store.RegisterKey(key);
				store.Set(key, new SimpleState { Level = 1 });
				store.ForceSave();
				store.Dispose();

				var files = Directory.GetFiles(path, "*.foo", SearchOption.TopDirectoryOnly);
				Assert.AreEqual(1, files.Length);
				Assert.AreEqual(0, Directory.GetFiles(path, "*.tmp").Length);
				Assert.AreEqual(0, Directory.GetFiles(path, "*.bak").Length);
			}
			finally
			{
				TestUtilities.Cleanup(path);
			}
		}

		[Test]
		public void SaveStore_InitializableAndAfterLoad()
		{
			var path = TestUtilities.CreateTempPath();
			var key = new SaveKey<LifecycleState>("lifecycle_hooks");
			try
			{
				var options = new SaveStoreOptions(path);
				var store = new SaveStore(options);
				store.RegisterKey(key);

				var initial = store.Get(key);
				Assert.AreEqual(5, initial.Value);

				initial.Value = -2;
				store.Set(key, initial);
				store.ForceSave();
				store.Dispose();

				var storeReloaded = new SaveStore(options);
				storeReloaded.RegisterKey(key);
				var loaded = storeReloaded.Get(key);
				Assert.AreEqual(10, loaded.Value);
				storeReloaded.Dispose();
			}
			finally
			{
				TestUtilities.Cleanup(path);
			}
		}

		[UnityTest]
		public IEnumerator SaveStore_AutoSave_WritesToDisk()
		{
			var path = TestUtilities.CreateTempPath();
			var key = new SaveKey<SimpleState>("autosave_state");
			var options = new SaveStoreOptions(path)
			{
				UseTaggedFormat = true,
				AutoSavePeriodSeconds = 1
			};

			var store = new SaveStore(options);
			store.RegisterKey(key);
			store.Update(key, (ref SimpleState state) => { state.Level = 9; });
			store.StartAutoSave();

			yield return new WaitForSeconds(1.3f);

			store.StopAutoSave();
			store.Dispose();

			var storeReloaded = new SaveStore(options);
			storeReloaded.RegisterKey(key);
			var loaded = storeReloaded.Get(key);
			Assert.AreEqual(9, loaded.Level);
			storeReloaded.Dispose();

			TestUtilities.Cleanup(path);
		}
	}
}
