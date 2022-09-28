using System.Globalization;
using System.Net;
using System.Text;
using MediaToolkit.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using MovieCastIdentifier.Filters;
using MovieCastIdentifier.Helpers;
using MovieCastIdentifier.Services;
using MovieCastIdentifier.SignalRHubs;

namespace MovieCastIdentifier.Controllers
{
    public class StreamingController : Controller
    {
        private readonly long _fileSizeLimit;
        private readonly ILogger<StreamingController> _logger;
        private readonly string[] _permittedExtensions = { ".txt", ".mp4", ".mkv" };
        private readonly string _targetFilePath;
        private readonly IHubContext<FileStreamHub, FileStreamClient> _hubContext;
        private readonly IBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly string ffmpegFilePath;
        private readonly IMediaToolkitService _mediaToolkitService;
        private readonly IWebHostEnvironment _env;

        // Get the default form options so that we can use them to set the default 
        // limits for request body data.
        private static readonly FormOptions _defaultFormOptions = new FormOptions();

        public StreamingController(ILogger<StreamingController> logger,
            IConfiguration config,
            IHubContext<FileStreamHub, FileStreamClient> hubContext,
            IBackgroundTaskQueue backgroundTaskQueue,
            IServiceScopeFactory serviceScopeFactory,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _fileSizeLimit = config.GetValue<long>("FileSizeLimit");

            // To save physical files to a path provided by configuration:
            _targetFilePath = config.GetValue<string>("StoredFilesPath");
            _hubContext = hubContext;
            _backgroundTaskQueue = backgroundTaskQueue;
            _serviceScopeFactory = serviceScopeFactory;
            _env = env;
            ffmpegFilePath = Path.Combine(_env.WebRootPath, "ffmpeg", "ffmpeg.exe");
            _mediaToolkitService = MediaToolkitService.CreateInstance(ffmpegFilePath);

            // To save physical files to the temporary files folder, use:
            //_targetFilePath = Path.GetTempPath();
        }

        // The following upload methods:
        //
        // 1. Disable the form value model binding to take control of handling 
        //    potentially large files.
        //
        // 2. Typically, antiforgery tokens are sent in request body. Since we 
        //    don't want to read the request body early, the tokens are sent via 
        //    headers. The antiforgery token filter first looks for tokens in 
        //    the request header and then falls back to reading the body.

        #region snippet_StreamFile
        [HttpPost]
        [DisableFormValueModelBinding]
        [ValidateAntiForgeryToken]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> StreamFile()
        {
            if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
            {
                ModelState.AddModelError("File", 
                    $"The request couldn't be processed (Error 1).");
                // Log error

                return BadRequest(ModelState);
            }

            // Accumulate the form data key-value pairs in the request (formAccumulator).
            var formAccumulator = new KeyValueAccumulator();
            var trustedFileNameForDisplay = string.Empty;
            var untrustedFileNameForStorage = string.Empty;
            var streamedFileContent = new MyHugeMemoryStream();

            var boundary = MultipartRequestHelper.GetBoundary(
                MediaTypeHeaderValue.Parse(Request.ContentType),
                _defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, HttpContext.Request.Body);

            var section = await reader.ReadNextSectionAsync();

            while (section != null)
            {
                var hasContentDispositionHeader = 
                    ContentDispositionHeaderValue.TryParse(
                        section.ContentDisposition, out var contentDisposition);

                if (hasContentDispositionHeader)
                {
                    if (MultipartRequestHelper
                        .HasFileContentDisposition(contentDisposition))
                    {
                        untrustedFileNameForStorage = contentDisposition.FileName.Value;
                        var trustedFileNameForFileStorage = Path.GetRandomFileName();
                        // Don't trust the file name sent by the client. To display
                        // the file name, HTML-encode the value.
                        trustedFileNameForDisplay = WebUtility.HtmlEncode(
                                contentDisposition.FileName.Value);

                        // Notify the client that the upload is starting
                        string message = "Please wait while we upload and "+
                            "process your file. You will receive a response shortly.";
                        await _hubContext.Clients.All.ReceiveMessage("", message);
                        
                        // Upload and process the file
                        await FileHelpers.ProcessFileStreaming(section, contentDisposition, 
                            ModelState, _permittedExtensions, _fileSizeLimit, _hubContext, _targetFilePath,
                            _backgroundTaskQueue, _serviceScopeFactory, _mediaToolkitService,
                            untrustedFileNameForStorage, trustedFileNameForDisplay);
                        

                        if (!ModelState.IsValid)
                        {
                            await _hubContext.Clients.All.ReceiveMessage("", "");
                            return BadRequest(ModelState);
                        }

                        // var filePath = Path.Combine(_targetFilePath, untrustedFileNameForStorage);
                        // try{
                        //     using (var targetStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        //     {
                        //         await streamedFileContent.CopyToAsync(targetStream);

                        //         // Notify the client that the file was uploaded successfully
                        //         await _hubContext.Clients.All.ReceiveMessage("", 
                        //             $"File {trustedFileNameForDisplay} uploaded successfully.");

                        //         _logger.LogInformation(
                        //             $"Uploaded file '{untrustedFileNameForStorage}' saved to " +
                        //             $"'{filePath}' with length {streamedFileContent.Length}.");
                        //     }
                        // }
                        // catch(Exception e){
                        //     ModelState.AddModelError("File save", 
                        //         $"Failed to save file {trustedFileNameForDisplay}. Error: {e.Message}");
                        // }
                    }
                    else if (MultipartRequestHelper
                        .HasFormDataContentDisposition(contentDisposition))
                    {
                        // Don't limit the key name length because the 
                        // multipart headers length limit is already in effect.
                        var key = HeaderUtilities
                            .RemoveQuotes(contentDisposition.Name).Value;
                        var encoding = GetEncoding(section);

                        if (encoding == null)
                        {
                            ModelState.AddModelError("File", 
                                $"The request couldn't be processed (Error 2).");
                            // Log error

                            return BadRequest(ModelState);
                        }

                        using (var streamReader = new StreamReader(
                            section.Body,
                            encoding,
                            detectEncodingFromByteOrderMarks: true,
                            bufferSize: 1024,
                            leaveOpen: true))
                        {
                            // The value length limit is enforced by 
                            // MultipartBodyLengthLimit
                            var value = await streamReader.ReadToEndAsync();

                            if (string.Equals(value, "undefined", 
                                StringComparison.OrdinalIgnoreCase))
                            {
                                value = string.Empty;
                            }

                            formAccumulator.Append(key, value);

                            if (formAccumulator.ValueCount > 
                                _defaultFormOptions.ValueCountLimit)
                            {
                                // Form key count limit of 
                                // _defaultFormOptions.ValueCountLimit 
                                // is exceeded.
                                ModelState.AddModelError("File", 
                                    $"The request couldn't be processed (Error 3).");
                                // Log error

                                return BadRequest(ModelState);
                            }
                        }
                    }
                }

                // Drain any remaining section body that hasn't been consumed and
                // read the headers for the next section.
                section = await reader.ReadNextSectionAsync();
            }

            // Bind form data to our model
            var formData = new FormData();
            var formValueProvider = new FormValueProvider(
                BindingSource.Form,
                new FormCollection(formAccumulator.GetResults()),
                CultureInfo.CurrentCulture);
            var bindingSuccessful = await TryUpdateModelAsync(formData, prefix: "",
                valueProvider: formValueProvider);

            if (!bindingSuccessful)
            {
                ModelState.AddModelError("File", 
                    "The request couldn't be processed (Error 5).");
                // Log error

                return BadRequest(ModelState);
            }

            // **WARNING!**
            // In the following example, the file is saved without
            // scanning the file's contents. In most production
            // scenarios, an anti-virus/anti-malware scanner API
            // is used on the file before making the file available
            // for download or for use by other systems. 
            // For more information, see the topic that accompanies 
            // this sample app.


            // Save file to database
            // var file = new AppFile()
            // {
            //     Content = streamedFileContent,
            //     UntrustedName = untrustedFileNameForStorage,
            //     Note = formData.Note,
            //     Size = streamedFileContent.Length, 
            //     UploadDT = DateTime.UtcNow
            // };

            // _context.File.Add(file);
            // await _context.SaveChangesAsync();

            return Created(nameof(StreamingController), null);
        }
        #endregion

        private static Encoding GetEncoding(MultipartSection section)
        {
            var hasMediaTypeHeader = 
                MediaTypeHeaderValue.TryParse(section.ContentType, out var mediaType);

            // UTF-7 is insecure and shouldn't be honored. UTF-8 succeeds in 
            // most cases.
            if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
            {
                return Encoding.UTF8;
            }

            return mediaType.Encoding;
        }
    }

    public class FormData
    {
        public string Title { get; set; }
    }
}