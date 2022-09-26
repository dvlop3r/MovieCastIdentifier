namespace MovieCastIdentifier.Helpers;

public static class StreamToByteArrayExtension
    {
        public static byte[] ToByteArray(this Stream stream)
        {
            using (stream)
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                     stream.CopyTo(memStream);
                     return memStream.ToArray();
                }
            }
        }
    }