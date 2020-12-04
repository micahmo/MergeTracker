namespace MergeTracker
{
    /// <summary>
    /// Defines a class which supplies a readonly text property for a given key
    /// </summary>
    public interface ITextData
    {
        string GetTextData(string key);
    }
}
