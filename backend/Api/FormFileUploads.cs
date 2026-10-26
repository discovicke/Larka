using Backend.Application.Dtos;

namespace Backend.Api;

public static class FormFileUploads
{
    /// <summary>
    /// Öppnar filerna från ett formulär som strömmar för servicen. Strömmarna stängs först när anropet är klart,
    /// eftersom servicen läser dem då.
    /// </summary>
    public static async Task<TResult> WithUploadsAsync<TResult>(
        IEnumerable<IFormFile>? files, Func<List<FileUpload>, Task<TResult>> action)
    {
        var streams = new List<Stream>();
        try
        {
            var uploads = new List<FileUpload>();
            foreach (var file in files ?? [])
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);
                uploads.Add(new FileUpload(file.FileName, file.Length, stream));
            }

            return await action(uploads);
        }
        finally
        {
            foreach (var stream in streams)
            {
                stream.Dispose();
            }
        }
    }
}
