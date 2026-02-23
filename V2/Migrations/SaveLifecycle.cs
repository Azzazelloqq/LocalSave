namespace LocalSaveSystem
{
public interface ISaveInitializable
{
	void InitializeAsNew();
}

public interface ISaveAfterLoad
{
	void OnAfterLoad();
}
}
