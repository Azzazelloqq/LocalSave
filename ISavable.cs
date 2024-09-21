namespace LocalSaveSystem
{
public interface ISavable
{
    public string SaveId { get; }
    public void InitializeAsNewSave();
    public void CopyFrom(ISavable loadedSavable);
}
}