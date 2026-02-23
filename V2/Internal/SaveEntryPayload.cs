namespace LocalSaveSystem
{
internal readonly struct SaveEntryPayload
{
	public string Key { get; }
	public string TypeName { get; }
	public int TypeId { get; }
	public int DataVersion { get; }
	public byte[] Payload { get; }

	public SaveEntryPayload(string key, string typeName, int typeId, int dataVersion, byte[] payload)
	{
		Key = key;
		TypeName = typeName;
		TypeId = typeId;
		DataVersion = dataVersion;
		Payload = payload;
	}
}
}
