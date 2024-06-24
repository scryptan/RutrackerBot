namespace RutrackerBot;

public class TorrentFileDescription
{
    public readonly string FilePath;
    public readonly FileType Type;

    public TorrentFileDescription(string filePath, FileType type)
    {
        FilePath = filePath;
        Type = type;
    }

    public override string ToString()
    {
        switch (Type)
        {
            case FileType.File:
                return FilePath;
                break;
            case FileType.Folder:
                return $"{FilePath}/";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Type), Type, null);
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is TorrentFileDescription description) return description.FilePath == FilePath;
        
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return FilePath.GetHashCode();
    }
}

public enum FileType
{
    File,
    Folder
}