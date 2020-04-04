using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BackupToAzureBlob
{
    class Program
    {
        private static void PrintUsage()
        {
            Console.WriteLine("Usage: BackupToAzureBlob <SourceDirectory> <BlobStorageDirectory> [appsettings.json]");
        }
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            var sourceDirectory = args[0];
            if (!Directory.Exists(sourceDirectory))
            {
                Console.WriteLine("Source directory does not exist: " + sourceDirectory);
                return;
            }
            var blobDirectory = args[1];
            if (!blobDirectory.EndsWith('/'))
            {
                blobDirectory += "/";
            }
            blobDirectory = blobDirectory.TrimStart('/');

            AppSettings config;
            var appSettingsFilename = "appsettings.json";
            if (args.Length > 2)
            {
                appSettingsFilename = args[2];
            }
            if (!File.Exists(appSettingsFilename))
            {
                Console.WriteLine("App settings does not exist: " + appSettingsFilename);
                return;
            }
            config = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(appSettingsFilename));
            var accessTier = new AccessTier(config.AccessTier);

            var container = new BlobContainerClient(config.StorageAccountConnectionString, config.ContainerName);

            // Find what's there now
            var blobs = new List<BlobItem>();
            if (config.VerboseLogging) Console.Write("Loading existing blobs");
            await foreach (var blob in container.GetBlobsAsync())
            {
                if (blob.Name.StartsWith(blobDirectory))
                {
                    if (config.VerboseLogging) Console.Write($"\rLoading existing blobs ({blobs.Count} done)");
                    blobs.Add(blob);
                }
            }
            var blobsByName = blobs.ToDictionary(b => b.Name, b => b);
            if (config.VerboseLogging) Console.WriteLine($"{blobs.Count} blobs loaded.");

            // Find out what to upload & upload it
            var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                bool upload;
                var blobName = blobDirectory + Path.GetRelativePath(sourceDirectory, file).Replace(@"\", "/");
                if (config.VerboseLogging) Console.Write("Checking " + blobName + ". ");
                if (blobsByName.TryGetValue(blobName, out var blob))
                {
                    if (config.VerboseLogging) Console.Write("Exists. ");
                    var fileInfo = new FileInfo(file);
                    if (blob.Properties.ContentLength.HasValue)
                    {
                        if (blob.Properties.ContentLength.Value == fileInfo.Length)
                        {
                            if (config.VerboseLogging) Console.Write("File size is the same. Skipping. ");
                            upload = false;
                        }
                        else
                        {
                            if (config.VerboseLogging) Console.Write("File size not equal. Updating. ");
                            upload = true;
                        }
                    }
                    else
                    {
                        if (config.VerboseLogging) Console.Write("Can't get size. Skipping. ");
                        upload = false;
                    }
                }
                else
                {
                    if (config.VerboseLogging) Console.Write("Blob doesn't exist. ");
                    upload = true;
                }

                var blobClient = new BlockBlobClient(config.StorageAccountConnectionString, config.ContainerName, blobName);
                if (upload)
                {
                    await blobClient.DeleteIfExistsAsync();

                    if (!config.VerboseLogging)
                    {
                        Console.WriteLine("Uploading " + blobName);
                    }

                    using var fileStream = File.OpenRead(file);
                    await blobClient.UploadAsync(fileStream);
                    if (config.VerboseLogging) Console.Write("Uploaded. ");
                }
                var accessTierResult = await blobClient.SetAccessTierAsync(accessTier);
                if (config.VerboseLogging) Console.Write($"Access tier set to {accessTier} with result {accessTierResult.Status}. ");

                if (config.VerboseLogging) Console.WriteLine();
            }
        }
    }

    class AppSettings
    {
        public string StorageAccountConnectionString { get; set; }
        public string ContainerName { get; set; }
        public string AccessTier { get; set; }
        public bool VerboseLogging { get; set; }
    }
}
