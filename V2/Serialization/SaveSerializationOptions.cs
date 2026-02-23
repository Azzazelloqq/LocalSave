namespace LocalSaveSystem
{
/// <summary>
/// Controls how save payloads are serialized.
/// </summary>
public sealed class SaveSerializationOptions
{
	/// <summary>
	/// When enabled, saves use a tagged format (field id + type + payload).
	/// When disabled, saves use a compact sequential format.
	/// </summary>
	public bool UseTaggedFormat { get; set; } = true;
}
}
