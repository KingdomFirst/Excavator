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
using Rock.Storage.Provider;
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
                .ToDictionary( t => (int)t.ForeignId, t => t.Id );

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

                int? transactionId = Path.GetFileNameWithoutExtension( file.Name ).AsType<int?>();
                if ( transactionId != null && transactionIdList.ContainsKey( (int)transactionId ) )
                {
                    var rockFile = new Rock.Model.BinaryFile();
                    rockFile.IsSystem = false;
                    rockFile.IsTemporary = false;
                    rockFile.FileName = file.Name;
                    rockFile.BinaryFileTypeId = transactionImageType.Id;
                    rockFile.CreatedDateTime = file.LastWriteTime.DateTime;
                    rockFile.MimeType = Extensions.GetMIMEType( file.Name );
                    rockFile.Description = string.Format( "Imported as {0}", file.Name );
                    rockFile.SetStorageEntityTypeId( transactionImageType.StorageEntityTypeId );
                    rockFile.StorageEntitySettings = transactionImageType.AttributeValues
                        .ToDictionary( a => a.Key, v => v.Value.Value ).ToJson() ?? emptyJsonObject;

                    var fileContent = new StreamReader( file.Open() ).ReadToEnd();
                    rockFile.ContentStream = new MemoryStream( Encoding.UTF8.GetBytes( fileContent ) );
                    newFileList.Add( transactionIdList[(int)transactionId], rockFile );

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
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.BinaryFiles.AddRange( newFileList.Values );
                rockContext.SaveChanges();

                foreach ( var entry in newFileList )
                {
                    if ( entry.Value != null )
                    {
                        var storageProvider = entry.Value.StorageEntityTypeId == DatabaseProvider.EntityType.Id
                            ? (ProviderComponent)DatabaseProvider
                            : (ProviderComponent)FileSystemProvider;

                        if ( storageProvider != null )
                        {
                            storageProvider.SaveContent( entry.Value );
                            entry.Value.Path = storageProvider.GetPath( entry.Value );
                        }
                        else
                        {
                            LogException( "Binary File Import", string.Format( "Could not load provider {0}.", storageProvider.ToString() ) );
                        }
                    }

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