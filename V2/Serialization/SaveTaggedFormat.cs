using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LocalSaveSystem
{
/// <summary>
/// Payload entry for tagged field serialization.
/// </summary>
public readonly struct SaveTaggedFieldPayload
{
	public string FieldId { get; }
	public string TypeName { get; }
	public byte[] Payload { get; }

	public SaveTaggedFieldPayload(string fieldId, string typeName, byte[] payload)
	{
		FieldId = fieldId;
		TypeName = typeName;
		Payload = payload;
	}
}

/// <summary>
/// Tagged format utilities for field-id based serialization.
/// </summary>
public static class SaveTaggedFormat
{
	private const uint Magic = 0x4653534C; // "LSSF"
	private const int FormatVersion = 1;
	private static readonly Encoding Utf8 = Encoding.UTF8;
	private static readonly HashSet<string> ValidatedTypes = new HashSet<string>(StringComparer.Ordinal);
	private static readonly object ValidationLock = new object();

	public static void WriteHeader(BinaryWriter writer, int fieldCount, bool isNull)
	{
		writer.Write(Magic);
		writer.Write(FormatVersion);
		writer.Write(isNull ? -1 : fieldCount);
	}

	public static bool TryReadHeader(BinaryReader reader, out int fieldCount, out bool isNull)
	{
		fieldCount = 0;
		isNull = false;

		var start = reader.BaseStream.Position;
		if (reader.BaseStream.Length - start < 12)
		{
			return false;
		}

		var magic = reader.ReadUInt32();
		if (magic != Magic)
		{
			reader.BaseStream.Position = start;
			return false;
		}

		var version = reader.ReadInt32();
		if (version != FormatVersion)
		{
			throw new InvalidDataException($"Unsupported tagged format version: {version}");
		}

		var count = reader.ReadInt32();
		if (count < 0)
		{
			isNull = true;
			return true;
		}

		fieldCount = count;
		return true;
	}

	public static void WriteField(BinaryWriter writer, string fieldId, Type fieldType, byte[] payload)
	{
		WriteString(writer, fieldId);
		WriteString(writer, SaveTypeResolver.GetTypeName(fieldType));
		writer.Write(payload?.Length ?? 0);
		if (payload != null && payload.Length > 0)
		{
			writer.Write(payload);
		}
	}

	public static SaveTaggedFieldPayload ReadField(BinaryReader reader)
	{
		var fieldId = ReadString(reader);
		var typeName = ReadString(reader);
		var length = reader.ReadInt32();
		var payload = length > 0 ? reader.ReadBytes(length) : Array.Empty<byte>();
		return new SaveTaggedFieldPayload(fieldId, typeName, payload);
	}

	public static bool TryReadPayload(SaveTaggedFieldPayload payload, Type targetType, SaveRegistry registry,
		out object value, out string error)
	{
		value = null;
		error = null;

		var storedType = SaveTypeResolver.Resolve(payload.TypeName);
		if (storedType == null)
		{
			error = $"Cannot resolve stored type '{payload.TypeName}'.";
			return false;
		}

		object storedValue;
		try
		{
			storedValue = registry.Deserialize(storedType, payload.Payload ?? Array.Empty<byte>());
		}
		catch (Exception e)
		{
			error = $"Failed to deserialize stored type '{storedType.Name}': {e.Message}";
			return false;
		}

		if (SaveValueConverter.TryConvert(storedValue, targetType, out var converted))
		{
			value = converted;
			return true;
		}

		error = $"Cannot convert '{storedType.Name}' to '{targetType.Name}'.";
		return false;
	}

	public static bool ValidateFieldIds(Type type, IEnumerable<string> fieldIds)
	{
		var typeName = type?.FullName ?? "UnknownType";
		lock (ValidationLock)
		{
			if (ValidatedTypes.Contains(typeName))
			{
				return true;
			}

			ValidatedTypes.Add(typeName);
		}

		if (fieldIds == null)
		{
			return true;
		}

		var duplicates = new HashSet<string>(StringComparer.Ordinal);
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (var id in fieldIds)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				continue;
			}

			if (!seen.Add(id))
			{
				duplicates.Add(id);
			}
		}

		if (duplicates.Count > 0)
		{
			Debug.LogWarning($"[Save system] Duplicate field ids for {typeName}: {string.Join(", ", duplicates)}");
		}

		return true;
	}

	public static void LogUnknownFields(Type type, IEnumerable<string> fieldIds)
	{
		if (type == null || fieldIds == null)
		{
			return;
		}

		var list = new List<string>();
		foreach (var id in fieldIds)
		{
			if (!string.IsNullOrWhiteSpace(id))
			{
				list.Add(id);
			}
		}

		if (list.Count == 0)
		{
			return;
		}

		Debug.LogWarning($"[Save system] Unknown fields for {type.Name}: {string.Join(", ", list)}");
	}

	public static void LogFieldReadError(Type type, string fieldId, string error)
	{
		var typeName = type?.Name ?? "UnknownType";
		Debug.LogWarning($"[Save system] Failed to read field '{fieldId}' on {typeName}: {error}");
	}

	private static void WriteString(BinaryWriter writer, string value)
	{
		if (value == null)
		{
			writer.Write(-1);
			return;
		}

		var bytes = Utf8.GetBytes(value);
		writer.Write(bytes.Length);
		writer.Write(bytes);
	}

	private static string ReadString(BinaryReader reader)
	{
		var length = reader.ReadInt32();
		if (length < 0)
		{
			return null;
		}

		var bytes = reader.ReadBytes(length);
		return Utf8.GetString(bytes);
	}
}
}
