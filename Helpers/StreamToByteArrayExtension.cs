namespace MovieCastIdentifier.Helpers;

public static class StreamToByteArrayExtension
    {
        public async static Task<byte[]> MyByteArrayAsync(this MyHugeMemoryStream memoryStream)
        {
            using (memoryStream)
            {
                memoryStream.Position = (memoryStream.Length / 4) * 3;
                byte[] byteArray = new byte[memoryStream.Length - memoryStream.Position];
                await memoryStream.ReadAsync(byteArray, 0, byteArray.Length);
                memoryStream.Position = 0;
                return byteArray;
            }
        }
    }