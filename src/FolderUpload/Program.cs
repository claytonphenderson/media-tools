using System.IO.Hashing;
using System.Security.Cryptography;
using System.Threading.Channels;
using Azure.Identity;
using Azure.Storage.Files.DataLake;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using TagLib.Image;

string photoLibraryUrl = Environment.GetEnvironmentVariable("PHOTO_LIB_URL") ?? throw new Exception("PHOTO_LIB_URL env variable not set");
string container = Environment.GetEnvironmentVariable("PHOTO_LIB_CONTAINER") ?? throw new Exception("PHOTO_LIB_CONTAINER env variable not set");
Channel<string> filesChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions());

// Recursively get all media files from all subdirectories within the specified
// directory at the provided path.  All detected files matching the 
async Task CrawlDirectory(string directoryPath)
{
    var directories = System.IO.Directory.GetDirectories(directoryPath);
    foreach (var sub in directories)
    {
        await CrawlDirectory(sub);
    }

    var unfiltered = System.IO.Directory.GetFiles(directoryPath);
    foreach (var file in unfiltered)
    {
        try
        {
            string[] supportedExtensions = ["jpeg", "jpg", "mov", "heic", "png", "mp4", "mpeg"];
            var extension = file.ToLower().Split(".").Last();
            if (supportedExtensions.Contains(extension))
            {
                await filesChannel.Writer.WriteAsync(file);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not parse file {file} - {e.Message}");
        }
    }
}

// Start several workers that pull file paths from the filesChannel and upload their
// contents to blob storage.  Returns once all files are processed.
void InitializeUploadWorkers()
{
    var taskGroup = new List<Task>();
    var serviceClient = new DataLakeServiceClient(
        new Uri(photoLibraryUrl),
        new DefaultAzureCredential());

    var fileSystemClient = serviceClient.GetFileSystemClient(container);

    for (int i = 0; i < 5; i++)
    {
        taskGroup.Add(Task.Run(async () =>
        {
            var workerId = (char)('A' + Random.Shared.Next(0, 26));
            await foreach (var item in filesChannel.Reader.ReadAllAsync())
            {
                try
                {
                    // Read exif data
                    var directories = ImageMetadataReader.ReadMetadata(item);
                    var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

                    // Read original image capture date
                    var imageDate = DateTime.Now;
                    if (subIfdDirectory != null && subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dateTime))
                    {
                        imageDate = dateTime;
                    }

                    // Read orientation of image
                    var imageOrientation = ImageOrientation.None;
                    if (subIfdDirectory != null && subIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation))
                    {
                        imageOrientation = (ImageOrientation)orientation;
                    }

                    using (var fs = System.IO.File.OpenRead(item))
                    using (var md5 = MD5.Create())
                    {
                        // Hash the image and use that as the file name
                        // set the stream position to 0 for uploading
                        var hashedImg = md5.ComputeHash(fs);
                        fs.Position = 0;

                        // create the new file or skip if it already exists
                        var fileName = $"/{imageDate.Year}/{imageDate.ToString("MM")}/{imageDate.ToString("dd")}/{Convert.ToBase64String(hashedImg)}.{item.Split(".").Last().ToLower().Trim()}";
                        var fileClient = fileSystemClient.GetFileClient(fileName);
                        if (Environment.GetEnvironmentVariable("OVERWRITE_ON") != "true")
                        {
                            if (await fileClient.ExistsAsync())
                            {
                                Console.WriteLine($"File {fileName} exists already. Skipping.");
                                continue;
                            }
                        }

                        // Upload bytes and set metadata fields
                        await fileClient.UploadAsync(fs);
                        await fileClient.SetMetadataAsync(new Dictionary<string, string>
                        {
                            {"date", imageDate.ToString("o")},
                            {"orientation", imageOrientation.ToString()}
                        });

                        Console.WriteLine($"worker {workerId} uploaded {fileName} | {fs.Length} bytes");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error when processing {item} - {e.StackTrace}");
                }
            }
        }));
    }

    // wait for all workers to be done
    Task.WaitAll(taskGroup);
}


Console.WriteLine("Starting directory crawl");
await CrawlDirectory(args[0]);

Console.WriteLine($"Crawl done, found {filesChannel.Reader.Count} files");
InitializeUploadWorkers();

Console.WriteLine("Done");
