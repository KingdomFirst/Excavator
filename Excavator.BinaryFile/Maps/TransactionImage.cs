using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Storage;
using Rock.Web.Cache;

namespace Excavator.BinaryFile
{
    /// <summary>
    /// Maps Transaction Images
    /// </summary>
    public class TransactionImage : BinaryFileComponent, IBinaryFile
    {
        /// <summary>
        /// Maps the specified folder.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <param name="transactionImageType">Type of the transaction image file.</param>
        public void Map( ZipArchive folder, BinaryFileType transactionImageType )
        {
            var lookupContext = new RockContext();

            var emptyJsonObject = "{}";
            var newFileList = new Dictionary<int, Rock.Model.BinaryFile>();
            var transactionIdList = new FinancialTransactionService( lookupContext )
                .Queryable().AsNoTracking().Where( t => t.ForeignId != null )
                .ToDictionary( t => t.ForeignId, t => t.Id );

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

                var transactionId = Path.GetFileNameWithoutExtension( file.Name );
                if ( transactionIdList.ContainsKey( transactionId ) )
                {
                    var rockFile = new Rock.Model.BinaryFile();
                    rockFile.IsSystem = false;
                    rockFile.IsTemporary = false;
                    rockFile.FileName = file.Name;
                    rockFile.BinaryFileTypeId = transactionImageType.Id;
                    rockFile.CreatedDateTime = file.LastWriteTime.DateTime;
                    rockFile.Description = string.Format( "Imported as {0}", file.Name );
                    rockFile.SetStorageEntityTypeId( transactionImageType.StorageEntityTypeId );
                    rockFile.StorageEntitySettings = transactionImageType.AttributeValues
                        .ToDictionary( a => a.Key, v => v.Value.Value ).ToJson() ?? emptyJsonObject;

                    rockFile.DatabaseData = new BinaryFileData();
                    string content = new StreamReader( file.Open() ).ReadToEnd();

                    byte[] m_Bytes = System.Text.Encoding.UTF8.GetBytes( content );
                    rockFile.DatabaseData.Content = m_Bytes;
                    rockFile.MimeType = Extensions.GetMIMEType( file.Name );

                    newFileList.Add( transactionIdList[transactionId], rockFile );

                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} files imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFiles( newFileList );

                        // Reset list
                        newFileList.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            if ( newFileList.Any() )
            {
                SaveFiles( newFileList );
            }

            ReportProgress( 100, string.Format( "Finished files import: {0:N0} addresses imported.", completed ) );
        }

        /// <summary>
        /// Saves the files.
        /// </summary>
        /// <param name="newFileList">The new file list.</param>
        private static void SaveFiles( Dictionary<int, Rock.Model.BinaryFile> newFileList )
        {
            ProviderComponent storageProvider = null;

            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.BinaryFiles.AddRange( newFileList.Values );
                rockContext.SaveChanges();

                foreach ( var entry in newFileList )
                {
                    var entityType = EntityTypeCache.Read( entry.Value.StorageEntityTypeId.Value );
                    if ( storageProvider == null && entry.Value.StorageEntityTypeId != null )
                    {
                        storageProvider = ProviderContainer.GetComponent( entityType.Name );
                    }

                    storageProvider.SaveContent( entry.Value );

                    // set the path now that we have a guid -- this is normally set
                    // by the MEF storage component (which we don't have access to)
                    var accessType = entry.Value.MimeType.StartsWith( "image" ) ? "Image" : "File";
                    entry.Value.Path = string.Format( "~/Get{0}.ashx?guid={1}", accessType, entry.Value.Guid );

                    // associate the image with the right transaction
                    var transactionImage = new FinancialTransactionImage();
                    transactionImage.TransactionId = entry.Key;
                    transactionImage.BinaryFileId = entry.Value.Id;
                    transactionImage.Order = 0;
                    rockContext.FinancialTransactions.FirstOrDefault( t => t.Id == entry.Key )
                        .Images.Add( transactionImage );
                }

                rockContext.SaveChanges( DisableAuditing );
            } );
        }
    }
}