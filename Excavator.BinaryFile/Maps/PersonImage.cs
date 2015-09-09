using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Storage;

namespace Excavator.BinaryFile.PersonImage
{
    /// <summary>
    /// Maps Person Images
    /// </summary>
    public class PersonImage : BinaryFileComponent, IBinaryFile
    {
        /// <summary>
        /// Maps the specified folder.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <param name="fileType">Type of the person image file.</param>
        public void Map( ZipArchive folder, BinaryFileType fileType )
        {
            // check for existing images
            var lookupContext = new RockContext();
            var existingImageList = new PersonService( lookupContext ).Queryable().AsNoTracking()
                .Where( p => p.Photo != null )
                .ToDictionary( p => p.Id, p => p.Photo.CreatedDateTime );

            var emptyJsonObject = "{}";
            var newFileList = new Dictionary<int, Rock.Model.BinaryFile>();

            var storageProvider = fileType.StorageEntityTypeId == DatabaseProvider.EntityType.Id
                ? (ProviderComponent)DatabaseProvider
                : (ProviderComponent)FileSystemProvider;

            int completed = 0;
            int totalRows = folder.Entries.Count;
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying files import ({0:N0} found.", totalRows ) );

            foreach ( var file in folder.Entries )
            {
                var fileExtension = Path.GetExtension( file.Name );
                if ( BinaryFileComponent.FileTypeBlackList.Contains( fileExtension ) )
                {
                    LogException( "Binary File Import", string.Format( "{0} filetype not allowed ({1})", fileExtension, file.Name ) );
                    continue;
                }

                var personForeignId = Path.GetFileNameWithoutExtension( file.Name ).AsType<int?>();
                var personKeys = BinaryFileComponent.ImportedPeople.FirstOrDefault( p => p.IndividualId == personForeignId );
                if ( personKeys != null )
                {
                    // only import the most recent profile photo
                    if ( !existingImageList.ContainsKey( personKeys.PersonId ) || existingImageList[personKeys.PersonId].Value < file.LastWriteTime.DateTime )
                    {
                        var rockFile = new Rock.Model.BinaryFile();
                        rockFile.IsSystem = false;
                        rockFile.IsTemporary = false;
                        rockFile.FileName = file.Name;
                        rockFile.BinaryFileTypeId = fileType.Id;
                        rockFile.MimeType = Extensions.GetMIMEType( file.Name );
                        rockFile.CreatedDateTime = file.LastWriteTime.DateTime;
                        rockFile.Description = string.Format( "Imported as {0}", file.Name );
                        rockFile.SetStorageEntityTypeId( fileType.StorageEntityTypeId );
                        rockFile.StorageEntitySettings = fileType.AttributeValues
                            .ToDictionary( a => a.Key, v => v.Value.Value ).ToJson() ?? emptyJsonObject;

                        // use base stream instead of file stream to keep the byte[]
                        // NOTE: if byte[] converts to a string it will corrupt the stream
                        using ( var fileContent = new StreamReader( file.Open() ) )
                        {
                            var baseStream = fileContent.BaseStream.ReadBytesToEnd();
                            rockFile.ContentStream = new MemoryStream( baseStream );
                        }

                        newFileList.Add( personKeys.PersonId, rockFile );
                    }

                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} files imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFiles( newFileList, storageProvider );

                        // add image keys to master list
                        foreach ( var newFile in newFileList )
                        {
                            existingImageList.AddOrReplace( newFile.Key, newFile.Value.CreatedDateTime );
                        }

                        // Reset batch list
                        newFileList.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            if ( newFileList.Any() )
            {
                SaveFiles( newFileList, storageProvider );
            }

            ReportProgress( 100, string.Format( "Finished files import: {0:N0} addresses imported.", completed ) );
        }

        /// <summary>
        /// Saves the files.
        /// </summary>
        /// <param name="newFileList">The new file list.</param>
        private static void SaveFiles( Dictionary<int, Rock.Model.BinaryFile> newFileList, ProviderComponent storageProvider )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.BinaryFiles.AddRange( newFileList.Values );
                rockContext.SaveChanges();

                foreach ( var file in newFileList )
                {
                    if ( storageProvider != null )
                    {
                        storageProvider.SaveContent( file.Value );
                        file.Value.Path = storageProvider.GetPath( file.Value );
                    }
                    else
                    {
                        LogException( "Binary File Import", string.Format( "Could not load provider {0}.", storageProvider.ToString() ) );
                    }

                    // associate the person with this photo
                    rockContext.People.FirstOrDefault( p => p.Id == file.Key ).PhotoId = file.Value.Id;
                }

                rockContext.SaveChanges( DisableAuditing );
            } );
        }
    }
}