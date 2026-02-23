using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalSaveSystem
{
public sealed class SaveRegistry
{
	private readonly Dictionary<Type, ISaveSerializer> _serializers =
		new Dictionary<Type, ISaveSerializer>();

	/// <summary>
	/// Serialization behavior for payloads produced by this registry.
	/// </summary>
	public SaveSerializationOptions Options { get; }

	public SaveRegistry(SaveSerializationOptions options = null)
	{
		Options = options ?? new SaveSerializationOptions();
	}

	public static SaveRegistry CreateDefault(SaveSerializationOptions options = null)
	{
		var registry = new SaveRegistry(options);
		registry.RegisterDefaults();
		Generated.SaveGeneratedRegistry.RegisterAll(registry);
		SaveRegistryContributorLoader.RegisterAll(registry);
		return registry;
	}

	public void Register<T>(ISaveSerializer<T> serializer)
	{
		if (serializer == null)
		{
			throw new ArgumentNullException(nameof(serializer));
		}

		_serializers[typeof(T)] = serializer;
	}

	public ISaveSerializer<T> GetOrCreateSerializer<T>()
	{
		return (ISaveSerializer<T>)GetOrCreateSerializer(typeof(T));
	}

	public ISaveSerializer GetOrCreateSerializer(Type type)
	{
		if (_serializers.TryGetValue(type, out var serializer))
		{
			return serializer;
		}

		serializer = CreateSerializer(type);
		_serializers[type] = serializer;
		return serializer;
	}

	public int GetVersion(Type type)
	{
		return GetOrCreateSerializer(type).Version;
	}

	public byte[] Serialize<T>(T value)
	{
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		GetOrCreateSerializer<T>().Write(writer, value, this);
		return ms.ToArray();
	}

	public byte[] Serialize(Type type, object value)
	{
		using var ms = new MemoryStream();
		using var writer = new BinaryWriter(ms);
		GetOrCreateSerializer(type).Write(writer, value, this);
		return ms.ToArray();
	}

	public T Deserialize<T>(byte[] payload)
	{
		using var ms = new MemoryStream(payload);
		using var reader = new BinaryReader(ms);
		return GetOrCreateSerializer<T>().Read(reader, this);
	}

	public object Deserialize(Type type, byte[] payload)
	{
		using var ms = new MemoryStream(payload);
		using var reader = new BinaryReader(ms);
		return GetOrCreateSerializer(type).Read(reader, this);
	}

	public void Write<T>(BinaryWriter writer, T value)
	{
		GetOrCreateSerializer<T>().Write(writer, value, this);
	}

	public void Write(BinaryWriter writer, object value, Type type)
	{
		GetOrCreateSerializer(type).Write(writer, value, this);
	}

	public T Read<T>(BinaryReader reader)
	{
		return GetOrCreateSerializer<T>().Read(reader, this);
	}

	public object Read(Type type, BinaryReader reader)
	{
		return GetOrCreateSerializer(type).Read(reader, this);
	}

	private void RegisterDefaults()
	{
		Register(new BoolSerializer());
		Register(new ByteSerializer());
		Register(new ShortSerializer());
		Register(new IntSerializer());
		Register(new LongSerializer());
		Register(new UShortSerializer());
		Register(new UIntSerializer());
		Register(new ULongSerializer());
		Register(new FloatSerializer());
		Register(new DoubleSerializer());
		Register(new DecimalSerializer());
		Register(new CharSerializer());
		Register(new StringSerializer());
		Register(new GuidSerializer());
		Register(new ByteArraySerializer());

		Register(new Vector2Serializer());
		Register(new Vector3Serializer());
		Register(new Vector4Serializer());
		Register(new QuaternionSerializer());
		Register(new ColorSerializer());
		Register(new Color32Serializer());
		Register(new Vector2IntSerializer());
		Register(new Vector3IntSerializer());
	}

	private ISaveSerializer CreateSerializer(Type type)
	{
		if (type.IsEnum)
		{
			var underlying = Enum.GetUnderlyingType(type);
			var serializer = GetOrCreateSerializer(underlying);
			return new EnumWrapperSerializer(type, underlying, serializer);
		}

		if (type.IsArray)
		{
			var elementType = type.GetElementType();
			var serializerType = typeof(ArraySaveSerializer<>).MakeGenericType(elementType);
			return (ISaveSerializer)Activator.CreateInstance(serializerType);
		}

		if (type.IsGenericType)
		{
			var definition = type.GetGenericTypeDefinition();
			if (definition == typeof(List<>))
			{
				var elementType = type.GetGenericArguments()[0];
				var serializerType = typeof(ListSaveSerializer<>).MakeGenericType(elementType);
				return (ISaveSerializer)Activator.CreateInstance(serializerType);
			}

			if (definition == typeof(Dictionary<,>))
			{
				var args = type.GetGenericArguments();
				var serializerType = typeof(DictionarySaveSerializer<,>).MakeGenericType(args);
				return (ISaveSerializer)Activator.CreateInstance(serializerType);
			}

			if (definition == typeof(Nullable<>))
			{
				var elementType = type.GetGenericArguments()[0];
				var serializerType = typeof(NullableSaveSerializer<>).MakeGenericType(elementType);
				return (ISaveSerializer)Activator.CreateInstance(serializerType);
			}
		}

		if (!type.IsDefined(typeof(SaveModelAttribute), true) &&
			!type.IsDefined(typeof(SerializableAttribute), true))
		{
			throw new InvalidOperationException(
				$"No serializer registered for {type.FullName}. " +
				"Add [SaveModel] or register a custom serializer.");
		}

		var reflectionSerializerType = typeof(ReflectionSaveSerializer<>).MakeGenericType(type);
		return (ISaveSerializer)Activator.CreateInstance(reflectionSerializerType);
	}

	private sealed class EnumWrapperSerializer : ISaveSerializer
	{
		private readonly Type _enumType;
		private readonly Type _underlyingType;
		private readonly ISaveSerializer _underlyingSerializer;

		public EnumWrapperSerializer(Type enumType, Type underlyingType, ISaveSerializer underlyingSerializer)
		{
			_enumType = enumType;
			_underlyingType = underlyingType;
			_underlyingSerializer = underlyingSerializer;
		}

		public Type TargetType => _enumType;
		public int Version => 1;

		public void Write(BinaryWriter writer, object value, SaveRegistry registry)
		{
			var underlying = Convert.ChangeType(value, _underlyingType);
			_underlyingSerializer.Write(writer, underlying, registry);
		}

		public object Read(BinaryReader reader, SaveRegistry registry)
		{
			var underlying = _underlyingSerializer.Read(reader, registry);
			return Enum.ToObject(_enumType, underlying);
		}
	}
}
}
