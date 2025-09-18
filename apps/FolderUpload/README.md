## Folder Upload
Given a directory with photos and subdirectories, this script will crawl all the directories and upload the media to a specified blob (w/ HNS) storage account. Uses 5 concurrent workers to upload files to storage.

You need to specify the following env variables
```
PHOTO_LIB_URL -> points to your blob storage
PHOTO_LIB_CONTAINER -> the name of the container you would like to upload to
```

### Running locally
You can run it with
```
dotnet run -- ~/path/to/root/directory
```

Uploaded photos are automatically partitioned into a date-based folder structure for ease of use.  Additionally, the parsed date will be added as file metadata.
```
originals/YEAR/MONTH/DAY/guid.EXT
```

### Standalone Binary
Building a standalone macos binary.  Produces an executable in the ../output directory that can be run.
```
./tools/build-binaries

run the binary with 

./FolderUpload ./path/to/dir
```
