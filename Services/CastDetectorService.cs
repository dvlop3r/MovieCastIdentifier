using IronOcr;
using MediaToolkit.Services;
using MediaToolkit.Tasks;
using Microsoft.AspNetCore.SignalR;
using MovieCastIdentifier.Models;
using MovieCastIdentifier.SignalRHubs;

namespace MovieCastIdentifier;

public class CastDetectorService : ICastDetectorService
{
    public async Task ExecuteAsync(
        string path,
        IMediaToolkitService _mediaToolkitService,
        string untrustedFileNameForStorage,
        string trustedFileNameForDisplay,
        IImdbApi _imdbApi,
        IHubContext<FileStreamHub,FileStreamClient> hubContext)
    {
        // Process the file with background task
        var metadataTask = new FfTaskGetMetadata(path);
        var metadata = await _mediaToolkitService.ExecuteAsync(metadataTask);

        if(Directory.Exists(@"c:\frames"))
            {
                Directory.Delete(@"c:\frames", true);
                Directory.CreateDirectory(@"c:\frames");
            }
        else
            Directory.CreateDirectory(@"c:\frames");

        // Increase speed by cutting out last 3 minutes
        var i = Double.Parse(metadata.Metadata.Format.Duration) - 180;

        // Stop looking for cast after ~8 minutes
        var stopper = Double.Parse(metadata.Metadata.Format.Duration) - 180 - 340;
        while(true)
        {
            // Start at the end of the video and go backwards capturing a frame every 5 seconds
            var outputFile = string.Format("{0}\\frame{1}.jpeg", @"c:\frames", (int)i);
            var task = new FfTaskSaveThumbnail(path, outputFile, TimeSpan.FromSeconds(i));
            await _mediaToolkitService.ExecuteAsync(task);
            i-=5;
            if(i < stopper)
            {
                await hubContext.Clients.All.ReceiveMessage("", "Couldn't find any cast in the movie.!");
                break;
            }

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
            OcrResult result = null;
            using(var input = new OcrInput(outputFile))
            {
                result = ocr.Read(input);
            }

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


                // Fetch memer images from IMDB using realMembers, we can optionally use castMembers
                var members = new List<Member>();
                foreach(var member in realMembers)
                {
                    var response = await _imdbApi.GetCastMember(member);
                    members.Add(new Member{
                        Name = member,
                        ImageUrl = response.D.Where(x => x.L.ToLower().Contains(member.ToLower())).FirstOrDefault()?.I?.ImageUrl
                    });
                }
                await hubContext.Clients.All.ReceiveImdbData("", members);
                await hubContext.Clients.All.ReceiveMessage("", "Successfully fetched cast member images from IMDB!");

                // Alternatively call the JS FetchImdbApi function via SignalR to get the IMDB data
                // await hubContext.Clients.All.FetchImdbApi("", "call IMDB api");                            


                break;
            }
        }
    }
}