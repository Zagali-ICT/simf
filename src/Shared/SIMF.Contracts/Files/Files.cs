using SIMF.Common.Enums;

namespace SIMF.Contracts.Files;

/// <summary>D-568 — the result of a successful upload to the centralized file
/// store. <see cref="Url"/> is the download-by-GUID path the caller renders /
/// persists (<c>/api/v1/files/{id}</c>).</summary>
public sealed record UploadedFileResponse(
    Guid Id,
    string Url,
    FileService Service,
    FileType FileType,
    bool IsEncrypted,
    long SizeBytes);
