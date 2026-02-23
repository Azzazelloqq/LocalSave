using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LocalSaveSystem
{
internal sealed class ReflectionSaveSerializer<T> : SaveSerializer<T>
{
	private readonly ReflectionSaveMember[] _members;
	private readonly bool _isReferenceType;

	public ReflectionSaveSerializer()
	{
		var type = typeof(T);
		_members = ReflectionSaveMember.Collect(type);
		_isReferenceType = !type.IsValueType;
		SaveTaggedFormat.ValidateFieldIds(type, _members.Select(member => member.Id));
	}

	public override int Version => SaveVersionResolver.GetVersion(typeof(T));

	public override void Write(BinaryWriter writer, T value, SaveRegistry registry)
	{
		if (registry.Options.UseTaggedFormat)
		{
			if (_isReferenceType)
			{
				if (value == null)
				{
					SaveTaggedFormat.WriteHeader(writer, _members.Length, true);
					return;
				}
			}

			SaveTaggedFormat.WriteHeader(writer, _members.Length, false);
			var boxedTagged = (object)value;
			foreach (var member in _members)
			{
				var memberValue = member.GetValue(boxedTagged);
				var payload = registry.Serialize(member.MemberType, memberValue);
				SaveTaggedFormat.WriteField(writer, member.Id, member.MemberType, payload);
			}

			return;
		}

		if (_isReferenceType)
		{
			if (value == null)
			{
				writer.Write(false);
				return;
			}

			writer.Write(true);
		}

		var boxed = (object)value;
		foreach (var member in _members)
		{
			var memberValue = member.GetValue(boxed);
			registry.Write(writer, memberValue, member.MemberType);
		}
	}

	public override T Read(BinaryReader reader, SaveRegistry registry)
	{
		var start = reader.BaseStream.Position;
		if (SaveTaggedFormat.TryReadHeader(reader, out var fieldCount, out var isNull))
		{
			if (isNull)
			{
				return default;
			}

			var fields = new Dictionary<string, SaveTaggedFieldPayload>(StringComparer.Ordinal);
			for (var i = 0; i < fieldCount; i++)
			{
				var field = SaveTaggedFormat.ReadField(reader);
				if (!string.IsNullOrWhiteSpace(field.FieldId))
				{
					fields[field.FieldId] = field;
				}
			}

			var boxedTagged = (object)SaveModelActivator.Create<T>();
			foreach (var member in _members)
			{
				if (!fields.TryGetValue(member.Id, out var field))
				{
					continue;
				}

				fields.Remove(member.Id);
				if (SaveTaggedFormat.TryReadPayload(field, member.MemberType, registry, out var value, out var error))
				{
					member.SetValue(boxedTagged, value);
				}
				else
				{
					SaveTaggedFormat.LogFieldReadError(typeof(T), member.Id, error);
				}
			}

			if (fields.Count > 0)
			{
				SaveTaggedFormat.LogUnknownFields(typeof(T), fields.Keys);
			}

			return (T)boxedTagged;
		}

		reader.BaseStream.Position = start;
		if (_isReferenceType)
		{
			var hasValue = reader.ReadBoolean();
			if (!hasValue)
			{
				return default;
			}
		}

		var boxed = (object)SaveModelActivator.Create<T>();
		foreach (var member in _members)
		{
			var value = registry.Read(member.MemberType, reader);
			member.SetValue(boxed, value);
		}

		return (T)boxed;
	}

	private sealed class ReflectionSaveMember
	{
		public Type MemberType { get; }
		public string Id { get; private set; }
		private readonly Func<object, object> _getter;
		private readonly Action<object, object> _setter;

		private ReflectionSaveMember(string id, Type memberType, Func<object, object> getter, Action<object, object> setter)
		{
			Id = id;
			MemberType = memberType;
			_getter = getter;
			_setter = setter;
		}

		public object GetValue(object target) => _getter(target);
		public void SetValue(object target, object value) => _setter(target, value);

		public static ReflectionSaveMember[] Collect(Type type)
		{
			var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(field => !field.IsStatic)
				.Where(field => !field.IsInitOnly)
				.Where(field => !field.IsDefined(typeof(SaveIgnoreAttribute), true))
				.Where(field => field.IsPublic || field.IsDefined(typeof(SaveMemberAttribute), true))
				.Select(field => new
				{
					Id = field.GetCustomAttribute<SaveFieldIdAttribute>()?.Id ?? field.Name,
					Order = field.GetCustomAttribute<SaveMemberAttribute>()?.Order ?? int.MaxValue,
					Name = field.Name,
					Member = CreateFieldMember(field)
				});

			var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Where(property => property.CanRead && property.CanWrite)
				.Where(property => property.GetIndexParameters().Length == 0)
				.Where(property => !property.IsDefined(typeof(SaveIgnoreAttribute), true))
				.Where(property => property.GetMethod != null && property.SetMethod != null)
				.Where(property => property.GetMethod.IsPublic || property.IsDefined(typeof(SaveMemberAttribute), true))
				.Select(property => new
				{
					Id = property.GetCustomAttribute<SaveFieldIdAttribute>()?.Id ?? property.Name,
					Order = property.GetCustomAttribute<SaveMemberAttribute>()?.Order ?? int.MaxValue,
					Name = property.Name,
					Member = CreatePropertyMember(property)
				});

			return fields.Concat(properties)
				.OrderBy(item => item.Order)
				.ThenBy(item => item.Name, StringComparer.Ordinal)
				.Select(item =>
				{
					item.Member.SetId(item.Id);
					return item.Member;
				})
				.ToArray();
		}

		private static ReflectionSaveMember CreateFieldMember(FieldInfo field)
		{
			return new ReflectionSaveMember(field.Name, field.FieldType, field.GetValue, field.SetValue);
		}

		private static ReflectionSaveMember CreatePropertyMember(PropertyInfo property)
		{
			return new ReflectionSaveMember(property.Name, property.PropertyType, property.GetValue, property.SetValue);
		}

		public void SetId(string id)
		{
			if (!string.IsNullOrWhiteSpace(id))
			{
				Id = id;
			}
		}
	}
}
}
