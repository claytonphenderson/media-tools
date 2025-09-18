using System.ComponentModel;
using Azure.Identity;
using Azure.Storage.Files.DataLake;

namespace ImageProcessing;

public static class CloudStorage
{
    public static async Task UploadImage(DataLakeFileSystemClient fileSystemClient, string fileName, FileStream fs, Dictionary<string, string>? metadata)
    {
        // create the new file or skip if it already exists
        var fileClient = fileSystemClient.GetFileClient(fileName);
        if (Environment.GetEnvironmentVariable("OVERWRITE_ON") != "true")
        {
            if (await fileClient.ExistsAsync())
            {
                Console.WriteLine($"File {fileName} exists already. Skipping.");
                return;
            }
        }

        // Upload bytes and set metadata fields
        await fileClient.UploadAsync(fs);

        if (metadata is not null)
        {
            await fileClient.SetMetadataAsync(metadata);
        }
    }

}
