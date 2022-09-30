using MediaToolkit.Services;
using Microsoft.AspNetCore.SignalR;
using MovieCastIdentifier.SignalRHubs;

namespace MovieCastIdentifier;


public interface ICastDetectorService
{
    Task ExecuteAsync(string path,
                      IMediaToolkitService _mediaToolkitService,
                      string untrustedFileNameForStorage,
                      string trustedFileNameForDisplay,
                      IImdbApi _imdbApi,
                      IHubContext<FileStreamHub,FileStreamClient> hubContext);
}