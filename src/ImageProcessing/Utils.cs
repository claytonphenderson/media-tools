using System.Security.Cryptography;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using TagLib.Image;

namespace ImageProcessing;

public static class Utils
{
    public static DateTime GetExifDate(string path)
    {
        // Read exif data
        var directories = ImageMetadataReader.ReadMetadata(path);
        var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

        // Read original image capture date
        var imageDate = DateTime.Now;
        if (subIfdDirectory != null && subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime dateTime))
        {
            imageDate = dateTime;
        }

        return imageDate;
    }

    public static ImageOrientation GetExifOrientation(string path)
    {
        // Read exif data
        var directories = ImageMetadataReader.ReadMetadata(path);
        var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

        // Read orientation of image
        var imageOrientation = ImageOrientation.None;
        if (subIfdDirectory != null && subIfdDirectory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int orientation))
        {
            imageOrientation = (ImageOrientation)orientation;
        }

        return imageOrientation;
    }

    /// <summary>
    /// Uses MD5 to hash the image bytes and returns a hex string representation.
    /// Useful for deduplication and id.
    /// </summary>
    /// <param name="fs"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string GetImageHash(FileStream fs)
    {

        using (var md5 = MD5.Create())
        {
            var hashedResult = md5.ComputeHash(fs) ?? throw new Exception("Could not hash image");
            fs.Position = 0; //rewind the stream for future use
            return BitConverter.ToString(hashedResult).Replace("-", "").ToLowerInvariant();
        }


    }

    /// <summary>
    /// Returns "jpeg" if file is "myprofile.jpeg"
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string GetFileExtension(string path)
    {
        return path.Split(".").Last().ToLower().Trim() ?? throw new Exception("No file path found " + path);
    }
}