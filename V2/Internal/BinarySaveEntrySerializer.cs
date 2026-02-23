using System;
using System.IO;
using System.Text;

namespace LocalSaveSystem
{
internal static class BinarySaveEntrySerializer
{
	private const uint Magic = 0x3253534C; // "LSS2"
	private const int FormatVersion = 1;
	private static readonly Encoding Utf8 = Encoding.UTF8;

	public static void Write(Stream stream, SaveEntryPayload entry)
	{
		using var writer = new BinaryWriter(stream, Utf8, true);
		writer.Write(Magic);
		writer.Write(FormatVersion);
		WriteString(writer, entry.Key);
		WriteString(writer, entry.TypeName);
		writer.Write(entry.TypeId);
		writer.Write(entry.DataVersion);
		writer.Write(entry.Payload?.Length ?? 0);
		if (entry.Payload != null && entry.Payload.Length > 0)
		{
			writer.Write(entry.Payload);
		}
	}

	public static SaveEntryPayload Read(Stream stream)
	{
		using var reader = new BinaryReader(stream, Utf8, true);
		var magic = reader.ReadUInt32();
		if (magic != Magic)
		{
			throw new InvalidDataException("Invalid save entry header.");
		}

		var format = reader.ReadInt32();
		if (format != FormatVersion)
		{
			throw new InvalidDataException($"Unsupported save format version: {format}");
		}

		var key = ReadString(reader);
		var typeName = ReadString(reader);
		var typeId = reader.ReadInt32();
		var dataVersion = reader.ReadInt32();
		var payloadLength = reader.ReadInt32();
		var payload = payloadLength > 0 ? reader.ReadBytes(payloadLength) : Array.Empty<byte>();
		return new SaveEntryPayload(key, typeName, typeId, dataVersion, payload);
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
