using GerenWare.Files.Contracts;

namespace GerenWare.Files.Infrastructure;

/// <summary>
/// The conversion of a storage that does not convert, as the local folder (ADR-0165): only Microsoft Graph makes the
/// PDF of a file stored in SharePoint, so the modules offer only the original format.
/// </summary>
internal sealed class NoFileConversion : IFileConversion
{
    public static readonly NoFileConversion Instance = new();

    private NoFileConversion()
    {
    }

    public bool ConvertsToPdf => false;

    public Task<FileDownload?> ToPdfAsync(string fileId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The file storage of the installation does not convert files into PDF; only SharePoint does.");
}
