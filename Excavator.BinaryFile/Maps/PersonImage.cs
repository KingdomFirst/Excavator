using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using BinaryFile = Rock.Model.BinaryFile;

namespace Excavator.BinaryFile
{
    /// <summary>
    /// Partial of BinaryFile import that holds a Person map
    /// </summary>
    public class PersonImage : BinaryFileComponent, IMap
    {
        public void Map( ZipArchive folder )
        {
            var lookupContext = new RockContext();
            var settings = ConfigurationManager.AppSettings;
            //var archiveFolder = zipFile.Value as ZipArchive;

            var fileSystemProvider = EntityTypeCache.Read( Rock.SystemGuid.EntityType.STORAGE_PROVIDER_FILESYSTEM.AsGuid(), lookupContext );

            var fileType = FileTypes.FirstOrDefault( f => f.Name == "Person Image" );
            if ( fileType == null )
            {
                var fileTypeService = new BinaryFileTypeService( lookupContext );

                fileType = new BinaryFileType();
                fileType.Name = "Person Image";

                fileType.Attributes = new Dictionary<string, AttributeCache>();
                fileType.AttributeValues = new Dictionary<string, AttributeValue>();

                // add default file location to attribute values here

                fileTypeService.Add( fileType );
                lookupContext.SaveChanges();

                FileTypes.Add( fileType );
            }

            int completed = 0;
            var newFileList = new List<Rock.Model.BinaryFile>();

            int totalRows = folder.Entries.Count;
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying files import ({0:N0} found.", totalRows ) );

            foreach ( var file in folder.Entries )
            {
                var foreignId = file.Name.AsType<int?>();
                var personKeys = GetPersonKeys( foreignId );
                if ( personKeys != null )
                {
                    var rockFile = new Rock.Model.BinaryFile();
                    rockFile.IsSystem = false;
                    rockFile.IsTemporary = false;
                    rockFile.FileName = file.Name;
                    rockFile.Description = string.Format( "Imported as {0}", file.Name );
                    rockFile.BinaryFileTypeId = fileType.Id;
                    //rockFile.StorageEntityTypeId = fileSystemProvider.Id;

                    rockFile.DatabaseData = new BinaryFileData();
                    string content = new StreamReader( file.Open() ).ReadToEnd();

                    byte[] m_Bytes = System.Text.Encoding.UTF8.GetBytes( content );
                    rockFile.DatabaseData.Content = m_Bytes;
                    rockFile.MimeType = "image/jpeg";

                    newFileList.Add( rockFile );
                    completed++;

                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} files imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFiles( newFileList );

                        // Reset context
                        newFileList.Clear();
                        lookupContext = new RockContext();

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
        private static void SaveFiles( List<Rock.Model.BinaryFile> newFileList )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.BinaryFiles.AddRange( newFileList.AsEnumerable() );
                rockContext.SaveChanges( DisableAuditing );
            } );
        }
    }
}