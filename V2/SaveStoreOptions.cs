namespace LocalSaveSystem
{
public sealed class SaveStoreOptions
{
	public string StoragePath { get; }
	public int AutoSavePeriodSeconds { get; set; } = 3;
	public bool SaveOnQuit { get; set; } = true;
	public bool UseAtomicWrite { get; set; } = true;
	/// <summary>
	/// When enabled, writes tagged payloads (field id + type + payload).
	/// When disabled, writes compact sequential payloads.
	/// </summary>
	public bool UseTaggedFormat { get; set; } = true;
	public string FileExtension { get; set; } = ".lss2";

	public SaveStoreOptions(string storagePath)
	{
		StoragePath = storagePath;
	}
}
}
