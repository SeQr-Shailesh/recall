namespace SeQrRecall.Application.Dtos.System;

public sealed class SystemVersionDto
{
    public required string ApiVersion { get; init; }

    public required string ApplicationVersion { get; init; }

    public required string Environment { get; init; }
}
