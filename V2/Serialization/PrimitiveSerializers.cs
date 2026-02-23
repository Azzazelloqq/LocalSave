using System;
using System.IO;
using UnityEngine;

namespace LocalSaveSystem
{
internal sealed class BoolSerializer : SaveSerializer<bool>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, bool value, SaveRegistry registry) => writer.Write(value);
	public override bool Read(BinaryReader reader, SaveRegistry registry) => reader.ReadBoolean();
}

internal sealed class ByteSerializer : SaveSerializer<byte>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, byte value, SaveRegistry registry) => writer.Write(value);
	public override byte Read(BinaryReader reader, SaveRegistry registry) => reader.ReadByte();
}

internal sealed class ShortSerializer : SaveSerializer<short>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, short value, SaveRegistry registry) => writer.Write(value);
	public override short Read(BinaryReader reader, SaveRegistry registry) => reader.ReadInt16();
}

internal sealed class IntSerializer : SaveSerializer<int>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, int value, SaveRegistry registry) => writer.Write(value);
	public override int Read(BinaryReader reader, SaveRegistry registry) => reader.ReadInt32();
}

internal sealed class LongSerializer : SaveSerializer<long>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, long value, SaveRegistry registry) => writer.Write(value);
	public override long Read(BinaryReader reader, SaveRegistry registry) => reader.ReadInt64();
}

internal sealed class UShortSerializer : SaveSerializer<ushort>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, ushort value, SaveRegistry registry) => writer.Write(value);
	public override ushort Read(BinaryReader reader, SaveRegistry registry) => reader.ReadUInt16();
}

internal sealed class UIntSerializer : SaveSerializer<uint>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, uint value, SaveRegistry registry) => writer.Write(value);
	public override uint Read(BinaryReader reader, SaveRegistry registry) => reader.ReadUInt32();
}

internal sealed class ULongSerializer : SaveSerializer<ulong>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, ulong value, SaveRegistry registry) => writer.Write(value);
	public override ulong Read(BinaryReader reader, SaveRegistry registry) => reader.ReadUInt64();
}

internal sealed class FloatSerializer : SaveSerializer<float>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, float value, SaveRegistry registry) => writer.Write(value);
	public override float Read(BinaryReader reader, SaveRegistry registry) => reader.ReadSingle();
}

internal sealed class DoubleSerializer : SaveSerializer<double>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, double value, SaveRegistry registry) => writer.Write(value);
	public override double Read(BinaryReader reader, SaveRegistry registry) => reader.ReadDouble();
}

internal sealed class DecimalSerializer : SaveSerializer<decimal>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, decimal value, SaveRegistry registry) => writer.Write(value);
	public override decimal Read(BinaryReader reader, SaveRegistry registry) => reader.ReadDecimal();
}

internal sealed class CharSerializer : SaveSerializer<char>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, char value, SaveRegistry registry) => writer.Write(value);
	public override char Read(BinaryReader reader, SaveRegistry registry) => reader.ReadChar();
}

internal sealed class StringSerializer : SaveSerializer<string>
{
	public override int Version => 1;

	public override void Write(BinaryWriter writer, string value, SaveRegistry registry)
	{
		if (value == null)
		{
			writer.Write(false);
			return;
		}

		writer.Write(true);
		writer.Write(value);
	}

	public override string Read(BinaryReader reader, SaveRegistry registry)
	{
		var hasValue = reader.ReadBoolean();
		return hasValue ? reader.ReadString() : null;
	}
}

internal sealed class GuidSerializer : SaveSerializer<Guid>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Guid value, SaveRegistry registry) => writer.Write(value.ToByteArray());
	public override Guid Read(BinaryReader reader, SaveRegistry registry) => new Guid(reader.ReadBytes(16));
}

internal sealed class ByteArraySerializer : SaveSerializer<byte[]>
{
	public override int Version => 1;

	public override void Write(BinaryWriter writer, byte[] value, SaveRegistry registry)
	{
		if (value == null)
		{
			writer.Write(-1);
			return;
		}

		writer.Write(value.Length);
		writer.Write(value);
	}

	public override byte[] Read(BinaryReader reader, SaveRegistry registry)
	{
		var length = reader.ReadInt32();
		return length < 0 ? null : reader.ReadBytes(length);
	}
}

internal sealed class Vector2Serializer : SaveSerializer<Vector2>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Vector2 value, SaveRegistry registry)
	{
		writer.Write(value.x);
		writer.Write(value.y);
	}

	public override Vector2 Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Vector2(reader.ReadSingle(), reader.ReadSingle());
	}
}

internal sealed class Vector3Serializer : SaveSerializer<Vector3>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Vector3 value, SaveRegistry registry)
	{
		writer.Write(value.x);
		writer.Write(value.y);
		writer.Write(value.z);
	}

	public override Vector3 Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}
}

internal sealed class Vector4Serializer : SaveSerializer<Vector4>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Vector4 value, SaveRegistry registry)
	{
		writer.Write(value.x);
		writer.Write(value.y);
		writer.Write(value.z);
		writer.Write(value.w);
	}

	public override Vector4 Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}
}

internal sealed class QuaternionSerializer : SaveSerializer<Quaternion>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Quaternion value, SaveRegistry registry)
	{
		writer.Write(value.x);
		writer.Write(value.y);
		writer.Write(value.z);
		writer.Write(value.w);
	}

	public override Quaternion Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}
}

internal sealed class ColorSerializer : SaveSerializer<Color>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Color value, SaveRegistry registry)
	{
		writer.Write(value.r);
		writer.Write(value.g);
		writer.Write(value.b);
		writer.Write(value.a);
	}

	public override Color Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}
}

internal sealed class Color32Serializer : SaveSerializer<Color32>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Color32 value, SaveRegistry registry)
	{
		writer.Write(value.r);
		writer.Write(value.g);
		writer.Write(value.b);
		writer.Write(value.a);
	}

	public override Color32 Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
	}
}

internal sealed class Vector2IntSerializer : SaveSerializer<Vector2Int>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Vector2Int value, SaveRegistry registry)
	{
		writer.Write(value.x);
		writer.Write(value.y);
	}

	public override Vector2Int Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Vector2Int(reader.ReadInt32(), reader.ReadInt32());
	}
}

internal sealed class Vector3IntSerializer : SaveSerializer<Vector3Int>
{
	public override int Version => 1;
	public override void Write(BinaryWriter writer, Vector3Int value, SaveRegistry registry)
	{
		writer.Write(value.x);
		writer.Write(value.y);
		writer.Write(value.z);
	}

	public override Vector3Int Read(BinaryReader reader, SaveRegistry registry)
	{
		return new Vector3Int(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
	}
}
}
