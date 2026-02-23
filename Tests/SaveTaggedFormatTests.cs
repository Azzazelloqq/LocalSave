using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LocalSaveSystem.Tests
{
	[TestFixture]
	public class SaveTaggedFormatTests
	{
		[SaveModel]
		private struct TaggedFieldModel
		{
			[SaveFieldId("custom_id")] public int Value;
		}

		[Serializable]
		private struct RenamedFieldModel
		{
			[SaveFieldId("legacy_name")] public int NewValue;
		}

		[Serializable]
		private struct RenamedFieldNoIdModel
		{
			public int NewValue;
		}

		private enum Mode
		{
			Easy,
			Hard
		}

		[Serializable]
		private struct SerializableDuplicateFieldModel
		{
			[SaveFieldId("dup")] public int A;
			[SaveFieldId("dup")] public int B;
		}

		[Test]
		public void SaveFieldId_UsedInPayload()
		{
			var registry = CreateRegistry(true);
			var bytes = registry.Serialize(new TaggedFieldModel { Value = 42 });

			using var stream = new MemoryStream(bytes);
			using var reader = new BinaryReader(stream);
			Assert.IsTrue(SaveTaggedFormat.TryReadHeader(reader, out var fieldCount, out var isNull));
			Assert.IsFalse(isNull);
			Assert.AreEqual(1, fieldCount);

			var field = SaveTaggedFormat.ReadField(reader);
			Assert.AreEqual("custom_id", field.FieldId);
		}

		[Test]
		public void CompactFormat_DoesNotWriteTaggedHeader()
		{
			var registry = CreateRegistry(false);
			var bytes = registry.Serialize(new TaggedFieldModel { Value = 0 });

			using var stream = new MemoryStream(bytes);
			using var reader = new BinaryReader(stream);
			Assert.IsFalse(SaveTaggedFormat.TryReadHeader(reader, out _, out _));
		}

		[Test]
		public void UnknownField_LogsWarning()
		{
			var registry = CreateRegistry(true);
			var payload = BuildTaggedPayload(registry);

			LogAssert.Expect(LogType.Warning,
				new Regex("Unknown fields.*TaggedFieldModel.*extra", RegexOptions.IgnoreCase));

			var value = registry.Deserialize<TaggedFieldModel>(payload);
			Assert.AreEqual(5, value.Value);
		}

		[Test]
		public void SaveFieldId_AllowsRename()
		{
			var registry = CreateRegistry(true);
			var payload = BuildTaggedPayload(registry, "legacy_name", registry.Serialize(11));

			var value = registry.Deserialize<RenamedFieldModel>(payload);
			Assert.AreEqual(11, value.NewValue);
		}

		[Test]
		public void SaveFieldId_Missing_LogsUnknownAndDefaults()
		{
			var registry = CreateRegistry(true);
			var payload = BuildTaggedPayload(registry, "legacy_name", registry.Serialize(11));

			LogAssert.Expect(LogType.Warning,
				new Regex("Unknown fields.*RenamedFieldNoIdModel.*legacy_name", RegexOptions.IgnoreCase));

			var value = registry.Deserialize<RenamedFieldNoIdModel>(payload);
			Assert.AreEqual(0, value.NewValue);
		}

		[Test]
		public void ValidateFieldIds_LogsDuplicates()
		{
			LogAssert.Expect(LogType.Warning,
				new Regex("Duplicate field ids.*SerializableDuplicateFieldModel.*dup", RegexOptions.IgnoreCase));

			SaveTaggedFormat.ValidateFieldIds(typeof(SerializableDuplicateFieldModel), new[] { "dup", "dup" });
		}

		[Test]
		public void TryReadPayload_ConvertsIntToLong()
		{
			var registry = CreateRegistry(true);
			var raw = registry.Serialize(7);
			var payload = new SaveTaggedFieldPayload("x", SaveTypeResolver.GetTypeName(typeof(int)), raw);

			Assert.IsTrue(SaveTaggedFormat.TryReadPayload(payload, typeof(long), registry, out var value, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(7L, (long)value);
		}

		[Test]
		public void TryReadPayload_ConvertsStringToEnum()
		{
			var registry = CreateRegistry(true);
			var raw = registry.Serialize("Hard");
			var payload = new SaveTaggedFieldPayload("mode", SaveTypeResolver.GetTypeName(typeof(string)), raw);

			Assert.IsTrue(SaveTaggedFormat.TryReadPayload(payload, typeof(Mode), registry, out var value, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(Mode.Hard, (Mode)value);
		}

		[Test]
		public void SaveStoreOptions_PropagatesTaggedSetting()
		{
			var path = Path.Combine(Path.GetTempPath(), "LocalSaveSystemTests", Guid.NewGuid().ToString("N"));
			var store = new SaveStore(new SaveStoreOptions(path) { UseTaggedFormat = false });
			try
			{
				Assert.IsFalse(store.Registry.Options.UseTaggedFormat);
			}
			finally
			{
				store.DeleteAll();
				store.Dispose();
			}
		}

		private static SaveRegistry CreateRegistry(bool useTaggedFormat)
		{
			return SaveRegistry.CreateDefault(new SaveSerializationOptions { UseTaggedFormat = useTaggedFormat });
		}

		private static byte[] BuildTaggedPayload(SaveRegistry registry)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			SaveTaggedFormat.WriteHeader(writer, 2, false);
			var knownPayload = registry.Serialize(5);
			var extraPayload = registry.Serialize(10);
			SaveTaggedFormat.WriteField(writer, "custom_id", typeof(int), knownPayload);
			SaveTaggedFormat.WriteField(writer, "extra", typeof(int), extraPayload);
			return stream.ToArray();
		}

		private static byte[] BuildTaggedPayload(SaveRegistry registry, string fieldId, byte[] payload)
		{
			using var stream = new MemoryStream();
			using var writer = new BinaryWriter(stream);
			SaveTaggedFormat.WriteHeader(writer, 1, false);
			SaveTaggedFormat.WriteField(writer, fieldId, typeof(int), payload);
			return stream.ToArray();
		}
	}
}
