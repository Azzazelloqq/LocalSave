using Unity.Plastic.Newtonsoft.Json.Linq;

namespace LocalSaveSystem
{
public interface ISavable
{
    public string SaveId { get; }
    public void InitializeAsNewSave();
    public void Parse(JObject jObject);
}
}