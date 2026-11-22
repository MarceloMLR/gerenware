namespace GerenWare.Files.Contracts;

/// <summary>The limits of the conversion of a stored file into PDF (ADR-0165).</summary>
public static class FileConversion
{
    /// <summary>The largest file Microsoft Graph converts: 2 MB.</summary>
    public const long MaxBytes = 2 * 1024 * 1024;
}

/// <summary>Why a stored file was not converted.</summary>
public enum FileConversionFailure
{
    /// <summary>The file is larger than <see cref="FileConversion.MaxBytes"/>.</summary>
    TooLarge = 1,

    /// <summary>The service that converts refused or failed.</summary>
    Refused = 2,
}

/// <summary>
/// A stored file was not converted: too large, or refused by the service, with the HTTP status and its code, never the
/// name of the file.
/// </summary>
public sealed class FileConversionException(FileConversionFailure failure, int? status = null, string? code = null)
    : Exception(failure == FileConversionFailure.TooLarge
        ? $"The file is larger than the {FileConversion.MaxBytes} bytes the conversion into PDF accepts."
        : $"The conversion into PDF was refused: {status?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "no status"}{(code is null ? string.Empty : $" {code}")}.")
{
    public FileConversionFailure Failure { get; } = failure;

    public int? Status { get; } = status;

    public string? Code { get; } = code;
}

/// <summary>
/// Converts a stored file into PDF (ADR-0165): SharePoint does it through Microsoft Graph; the local storage does not,
/// and says so. Nothing is stored: the PDF comes back to the module, which keeps it where it wants.
/// </summary>
public interface IFileConversion
{
    /// <summary>Whether the storage of the installation converts files into PDF.</summary>
    bool ConvertsToPdf { get; }

    /// <summary>
    /// The PDF of the current version of a file, named as the file with ".pdf"; null when the file does not exist.
    /// Throws <see cref="FileConversionException"/> when the file is too large or the conversion is refused, and
    /// <see cref="NotSupportedException"/> when the storage does not convert.
    /// </summary>
    Task<FileDownload?> ToPdfAsync(string fileId, CancellationToken cancellationToken);
}
