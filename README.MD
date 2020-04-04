# BackupToAzureBlob

Simple utility to backup a directory to Azure Blob Storage.

## Dependencies

To use this, you must have the following:
- An Azure Storage account and container
- [.Net Core 3.1 runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)

## Usage

1. Edit appsettings.json to add a storage account connection string and blob container name. Optionally choose an access tier other than Archive depending on your needs.
2. Run `dotnet BackupToAzureBlob.dll <SourceDirectory> <BlobStorageDirectory>`
3. Wait for it to finish
