using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Storage;
using static Excavator.Utility.Extensions;

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
        /// <param name="personImageType">Type of the person image file.</param>
        public void Map( ZipArchive folder, BinaryFileType personImageType )
        {
            // check for existing images
            var lookupContext = new RockContext();
            var existingImageList = new PersonService( lookupContext ).Queryable().AsNoTracking()
                .Where( p => p.Photo != null )
                .ToDictionary( p => p.Id, p => p.Photo.CreatedDateTime );

            var emptyJsonObject = "{}";
            var newFileList = new Dictionary<int, Rock.Model.BinaryFile>();

            var storageProvider = personImageType.StorageEntityTypeId == DatabaseProvider.EntityType.Id
                ? (ProviderComponent)DatabaseProvider
                : (ProviderComponent)FileSystemProvider;

            var completedItems = 0;
            var totalEntries = folder.Entries.Count;
            var percentage = ( totalEntries - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying person images import ({0:N0} found.", totalEntries ) );

            foreach ( var file in folder.Entries )
            {
                var fileExtension = Path.GetExtension( file.Name );
                if ( FileTypeBlackList.Contains( fileExtension ) )
                {
                    LogException( "Binary File Import", string.Format( "{0} filetype not allowed ({1})", fileExtension, file.Name ) );
                    continue;
                }

                var personForeignId = Path.GetFileNameWithoutExtension( file.Name ).AsType<int?>();
                var personKeys = ImportedPeople.FirstOrDefault( p => p.PersonForeignId == personForeignId );
                if ( personKeys != null )
                {
                    // only import the most recent profile photo
                    if ( !existingImageList.ContainsKey( personKeys.PersonId ) || existingImageList[personKeys.PersonId].Value < file.LastWriteTime.DateTime )
                    {
                        var rockFile = new Rock.Model.BinaryFile
                        {
                            IsSystem = false,
                            IsTemporary = false,
                            FileName = file.Name,
                            BinaryFileTypeId = personImageType.Id,
                            MimeType = GetMIMEType( file.Name ),
                            CreatedDateTime = file.LastWriteTime.DateTime,
                            Description = string.Format( "Imported as {0}", file.Name )
                        };

                        rockFile.SetStorageEntityTypeId( personImageType.StorageEntityTypeId );
                        rockFile.StorageEntitySettings = emptyJsonObject;

                        if ( personImageType.AttributeValues.Any() )
                        {
                            rockFile.StorageEntitySettings = personImageType.AttributeValues
                                .ToDictionary( a => a.Key, v => v.Value.Value ).ToJson();
                        }

                        // use base stream instead of file stream to keep the byte[]
                        // NOTE: if byte[] converts to a string it will corrupt the stream
                        using ( var fileContent = new StreamReader( file.Open() ) )
                        {
                            rockFile.ContentStream = new MemoryStream( fileContent.BaseStream.ReadBytesToEnd() );
                        }

                        newFileList.Add( personKeys.PersonId, rockFile );
                    }

                    completedItems++;
                    if ( completedItems % percentage < 1 )
                    {
                        var percentComplete = completedItems / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} person image files imported ({1}% complete).", completedItems, percentComplete ) );
                    }
                    else if ( completedItems % ReportingNumber < 1 )
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

            lookupContext.Dispose();
            ReportProgress( 100, string.Format( "Finished files import: {0:N0} person images imported.", completedItems ) );
        }

        /// <summary>
        /// Saves the files.
        /// </summary>
        /// <param name="newFileList">The new file list.</param>
        /// <param name="storageProvider">The storage provider.</param>
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
