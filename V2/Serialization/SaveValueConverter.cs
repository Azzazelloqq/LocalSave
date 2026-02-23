using System;

namespace LocalSaveSystem
{
/// <summary>
/// Attempts safe conversions between stored values and target types.
/// </summary>
public static class SaveValueConverter
{
	public static bool TryConvert(object value, Type targetType, out object converted)
	{
		converted = null;
		if (targetType == null)
		{
			return false;
		}

		if (value == null)
		{
			if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
			{
				return true;
			}

			return false;
		}

		var sourceType = value.GetType();
		if (targetType.IsAssignableFrom(sourceType))
		{
			converted = value;
			return true;
		}

		var targetUnderlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
		var sourceUnderlying = Nullable.GetUnderlyingType(sourceType) ?? sourceType;

		if (targetUnderlying.IsEnum)
		{
			if (value is string str)
			{
				try
				{
					converted = Enum.Parse(targetUnderlying, str, true);
					return true;
				}
				catch
				{
					return false;
				}
			}

			try
			{
				var enumValue = Convert.ChangeType(value, Enum.GetUnderlyingType(targetUnderlying));
				converted = Enum.ToObject(targetUnderlying, enumValue);
				return true;
			}
			catch
			{
				return false;
			}
		}

		if (sourceUnderlying.IsEnum && IsConvertible(targetUnderlying))
		{
			try
			{
				converted = Convert.ChangeType(value, targetUnderlying);
				return true;
			}
			catch
			{
				return false;
			}
		}

		if (targetUnderlying == typeof(Guid))
		{
			if (value is byte[] bytes && bytes.Length == 16)
			{
				converted = new Guid(bytes);
				return true;
			}

			if (value is string guidString && Guid.TryParse(guidString, out var guid))
			{
				converted = guid;
				return true;
			}
		}

		if (targetUnderlying == typeof(string))
		{
			converted = value.ToString();
			return true;
		}

		if (IsConvertible(targetUnderlying) && value is IConvertible)
		{
			try
			{
				converted = Convert.ChangeType(value, targetUnderlying);
				return true;
			}
			catch
			{
				return false;
			}
		}

		return false;
	}

	private static bool IsConvertible(Type type)
	{
		return typeof(IConvertible).IsAssignableFrom(type);
	}
}
}
