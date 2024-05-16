using System;

namespace LocalSaveSystem.Factory
{
public interface ISaveFactory : IDisposable
{
    public ISavable[] CreateSaves();
}
}