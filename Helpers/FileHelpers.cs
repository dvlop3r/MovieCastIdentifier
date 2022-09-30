using IronOcr;
using MediaToolkit.Services;
using MediaToolkit.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using MovieCastIdentifier.Services;
using MovieCastIdentifier.SignalRHubs;
using Patagames.Ocr;

namespace MovieCastIdentifier.Helpers
{
    public static class FileHelpers
    {
        // If you require a check on specific characters in the IsValidFileExtensionAndSignature
        // method, supply the characters in the _allowedChars field.
        private static readonly byte[] _allowedChars = { };
        // For more file signatures, see the File Signatures Database (https://www.filesignatures.net/)
        // and the official specifications for the file types you wish to add.
        private static readonly Dictionary<string, List<byte[]>> _fileSignature = new Dictionary<string, List<byte[]>>
        {
            { ".gif", new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } } },
            { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
            { ".jpeg", new List<byte[]>
                {
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 },
                }
            },
            { ".jpg", new List<byte[]>
                {
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 },
                    new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 },
                }
            },
            { ".zip", new List<byte[]> 
                {
                    new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 
                    new byte[] { 0x50, 0x4B, 0x4C, 0x49, 0x54, 0x45 },
                    new byte[] { 0x50, 0x4B, 0x53, 0x70, 0x58 },
                    new byte[] { 0x50, 0x4B, 0x05, 0x06 },
                    new byte[] { 0x50, 0x4B, 0x07, 0x08 },
                    new byte[] { 0x57, 0x69, 0x6E, 0x5A, 0x69, 0x70 },
                }
            },
        };

        // **WARNING!**
        // In the following file processing methods, the file's content isn't scanned.
        // In most production scenarios, an anti-virus/anti-malware scanner API is
        // used on the file before making the file available to users or other
        // systems. For more information, see the topic that accompanies this sample
        // app.

        
        public static async Task<MyHugeMemoryStream> ProcessFileStreaming(
            MultipartSection section, ContentDispositionHeaderValue contentDisposition, 
            ModelStateDictionary modelState, string[] permittedExtensions, long sizeLimit,
            IHubContext<FileStreamHub,FileStreamClient> hubContext, string rootPath,
            IBackgroundTaskQueue queue, IServiceScopeFactory scopeFactory,
            IMediaToolkitService _mediaToolkitService, string untrustedFileNameForStorage,
            string trustedFileNameForDisplay, IImdbApi _imdbApi)
        {
            try
            {
                using (var memoryStream = new MyHugeMemoryStream())
                {
                    // Stream file to memory
                    await section.Body.CopyToAsync(memoryStream);
                    await hubContext.Clients.All.ReceiveMessage("", $"File \"{trustedFileNameForDisplay}\" uploaded and streamed to memory.");

                    // Save file to disk, direct reading from section body is possible but writing to
                    // and then reading from memory stream is faster and more efficient
                    var filePath = Path.Combine(rootPath , contentDisposition.FileName.ToString().Trim('"'));
                    using(var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        await memoryStream.CopyToAsync(fileStream);
                        var hubMessage = $"File \"{trustedFileNameForDisplay}\" stored on disk."+
                        $"{Environment.NewLine}Now sit back until we process the movie. This should only take a couple of minutes!";
                        await hubContext.Clients.All.ReceiveMessage("", hubMessage);
                    }

                    // Process the file with background task
                    var metadataTask = new FfTaskGetMetadata(filePath);
                    var metadata = await _mediaToolkitService.ExecuteAsync(metadataTask);

                    var i = Double.Parse(metadata.Metadata.Format.Duration) - 300;
                    while(true)
                    {
                        // Start at the end of the video and go backwards capturing a frame every 5 seconds
                        var outputFile = string.Format("{0}\\frame{1}.jpeg", @"c:\frames", (int)i);
                        var task = new FfTaskSaveThumbnail(filePath, outputFile, TimeSpan.FromSeconds(i));
                        await _mediaToolkitService.ExecuteAsync(task);
                        i-=5;

                        // Use Tesseract.Net.Sdk OCR to extract text from the frame
                        // var ocrTask = OcrApi.Create();
                        // ocrTask.Init(Patagames.Ocr.Enums.Languages.English);
                        // var result = ocrTask.GetTextFromImage(outputFile);

                        // Tesseract package, Tesseract.Net.Sdk is fater and more accurate
                        // var ocr = new TesseractEngine("./tessdata", "eng", EngineMode.Default);
                        // var page = ocr.Process(Pix.LoadFromFile(outputFile));
                        // var result = page.GetText();

                        // Using IronOcr, most efficient and accurate OCR package
                        var ocr = new IronTesseract();
                        var input = new OcrInput(outputFile);
                        var result = ocr.Read(input);

                        if(result.Text.ToLower().Contains("cast"))
                        {
                            // Get the cast list
                            // get index of cast
                            var castIndex = result.Text.ToLower().IndexOf("cast");
                            var text = result.Text.Substring(castIndex, result.Text.Length - castIndex);
                            var castList = text.Split("\n");
                            // Remove empty, \r and \n from the cast list
                            var cleanList = new List<string>();
                            castList.ToList().ForEach(x => 
                            {
                                var clean = x.Replace("\r", "").Replace("\n", "");
                                if(!string.IsNullOrEmpty(clean))
                                    cleanList.Add(clean);
                            });
                            // Ensure we have the right frame
                            if(!string.Equals(cleanList.First().ToLower(), "cast"))
                                continue;

                            // Get the first 5 cast members
                            var castMembers = cleanList.SkipWhile(x => x.ToLower() == "cast").Take(5).ToList();
                            // Get real member names (not accurate)
                            var realMembers = cleanList.Skip((cleanList.Count/2)+1).Take(5).ToList();


                            // Fetch memer images from IMDB
                            await _imdbApi.GetCastMember(realMembers.First());

                            // Alternatively call the JS FetchImdbApi function via SignalR to get the IMDB data
                            // await hubContext.Clients.All.FetchImdbApi("", "call IMDB api");                            


                            break;
                        }
                    }


                    // Check if the file is empty or exceeds the size limit.
                    if (memoryStream.Length == 0)
                    {
                        modelState.AddModelError("File", "The file is empty.");
                    }
                    else if (memoryStream.Length > sizeLimit)
                    {
                        var megabyteSizeLimit = sizeLimit / 1048576;
                        modelState.AddModelError("File",
                        $"The file exceeds {megabyteSizeLimit:N1} MB.");
                    }
                    else if (!IsValidFileExtensionAndSignature(contentDisposition.FileName.Value, memoryStream,permittedExtensions))
                    {
                        modelState.AddModelError("File",
                            "The file type isn't permitted or the file's " +
                            "signature doesn't match the file's extension.");
                    }
                    else
                    {
                        var byteArray = await memoryStream.MyByteArrayAsync();
                        return memoryStream;
                    }
                }
            }
            catch (Exception ex)
            {
                modelState.AddModelError("File process",
                    $"Failed to process the file: {ex.Message}");
                // Log the exception
            }

            return new MyHugeMemoryStream();
        }

        private static bool IsValidFileExtensionAndSignature(string fileName, Stream data, string[] permittedExtensions)
        {
            if (string.IsNullOrEmpty(fileName) || data == null || data.Length == 0)
            {
                return false;
            }

            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
            {
                return false;
            }

            // Ignore the rest
            return true;

            data.Position = 0;

            using (var reader = new BinaryReader(data))
            {
                if (ext.Equals(".txt") || ext.Equals(".csv") || ext.Equals(".prn"))
                {
                    if (_allowedChars.Length == 0)
                    {
                        // Limits characters to ASCII encoding.
                        for (var i = 0; i < data.Length; i++)
                        {
                            if (reader.ReadByte() > sbyte.MaxValue)
                            {
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // Limits characters to ASCII encoding and
                        // values of the _allowedChars array.
                        for (var i = 0; i < data.Length; i++)
                        {
                            var b = reader.ReadByte();
                            if (b > sbyte.MaxValue ||
                                !_allowedChars.Contains(b))
                            {
                                return false;
                            }
                        }
                    }

                    return true;
                }

                // Uncomment the following code block if you must permit
                // files whose signature isn't provided in the _fileSignature
                // dictionary. We recommend that you add file signatures
                // for files (when possible) for all file types you intend
                // to allow on the system and perform the file signature
                // check.
                
                // if (!_fileSignature.ContainsKey(ext))
                // {
                //     return true;
                // }
                

                // File signature check
                // --------------------
                // With the file signatures provided in the _fileSignature
                // dictionary, the following code tests the input content's
                // file signature.
                var signatures = _fileSignature[ext];
                var headerBytes = reader.ReadBytes(signatures.Max(m => m.Length));

                return signatures.Any(signature => 
                    headerBytes.Take(signature.Length).SequenceEqual(signature));
            }
        }
    }
}