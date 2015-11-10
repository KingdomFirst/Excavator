﻿using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Storage;
using Rock.Web.Cache;

namespace Excavator.BinaryFile
{
    /// <summary>
    /// Maps Ministry Documents
    /// </summary>
    public class MinistryDocument : BinaryFileComponent, IBinaryFile
    {
        /// <summary>
        /// Maps the specified folder.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <param name="ministryFileType">Type of the ministry file.</param>
        public void Map( ZipArchive folder, BinaryFileType ministryFileType )
        {
            var lookupContext = new RockContext();
            var personEntityTypeId = EntityTypeCache.GetId<Person>();
            var fileFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.FILE.AsGuid(), lookupContext ).Id;

            var existingAttributes = new AttributeService( lookupContext ).GetByFieldTypeId( fileFieldTypeId )
                .Where( a => a.EntityTypeId == personEntityTypeId )
                .ToDictionary( a => a.Key, a => a.Id );

            var storageProvider = ministryFileType.StorageEntityTypeId == DatabaseProvider.EntityType.Id
                ? (ProviderComponent)DatabaseProvider
                : (ProviderComponent)FileSystemProvider;

            var emptyJsonObject = "{}";
            var newFileList = new List<DocumentKeys>();

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

                string[] parsedFileName = file.Name.Split( '_' );
                // Ministry docs should follow this pattern:
                // 0. Firstname
                // 1. Lastname
                // 2. ForeignId
                // 3. Filename

                var personForeignId = parsedFileName[2].AsType<int?>();
                var personKeys = BinaryFileComponent.ImportedPeople.FirstOrDefault( p => p.IndividualId == personForeignId );
                if ( personKeys != null )
                {
                    var rockFile = new Rock.Model.BinaryFile();
                    rockFile.IsSystem = false;
                    rockFile.IsTemporary = false;
                    rockFile.FileName = file.Name;
                    rockFile.BinaryFileTypeId = ministryFileType.Id;
                    rockFile.CreatedDateTime = file.LastWriteTime.DateTime;
                    rockFile.MimeType = Extensions.GetMIMEType( file.Name );
                    rockFile.Description = string.Format( "Imported as {0}", file.Name );
                    rockFile.SetStorageEntityTypeId( ministryFileType.StorageEntityTypeId );
                    rockFile.StorageEntitySettings = emptyJsonObject;

                    if ( ministryFileType.AttributeValues.Any() )
                    {
                        rockFile.StorageEntitySettings = ministryFileType.AttributeValues
                            .ToDictionary( a => a.Key, v => v.Value.Value ).ToJson();
                    }

                    // use base stream instead of file stream to keep the byte[]
                    // NOTE: if byte[] converts to a string it will corrupt the stream
                    using ( var fileContent = new StreamReader( file.Open() ) )
                    {
                        rockFile.ContentStream = new MemoryStream( fileContent.BaseStream.ReadBytesToEnd() );
                    }

                    var attributePattern = "[A-Za-z]+";
                    var attributeName = Regex.Match( parsedFileName[3], attributePattern );
                    var attributeKey = attributeName.Value.RemoveWhitespace();
                    if ( !existingAttributes.ContainsKey( attributeKey ) )
                    {
                        var newAttribute = new Attribute();
                        newAttribute.FieldTypeId = fileFieldTypeId;
                        newAttribute.EntityTypeId = personEntityTypeId;
                        newAttribute.EntityTypeQualifierColumn = string.Empty;
                        newAttribute.EntityTypeQualifierValue = string.Empty;
                        newAttribute.Key = attributeKey;
                        newAttribute.Name = attributeName.Value;
                        newAttribute.Description = attributeName.Value + " created by binary file import";
                        newAttribute.IsGridColumn = false;
                        newAttribute.IsMultiValue = false;
                        newAttribute.IsRequired = false;
                        newAttribute.AllowSearch = false;
                        newAttribute.IsSystem = false;
                        newAttribute.Order = 0;

                        newAttribute.AttributeQualifiers.Add( new AttributeQualifier()
                        {
                            Key = "binaryFileType",
                            Value = ministryFileType.Guid.ToString()
                        } );

                        lookupContext.Attributes.Add( newAttribute );
                        lookupContext.SaveChanges();

                        existingAttributes.Add( newAttribute.Key, newAttribute.Id );
                    }

                    newFileList.Add( new DocumentKeys()
                    {
                        PersonId = personKeys.PersonId,
                        AttributeId = existingAttributes[attributeKey],
                        File = rockFile
                    } );

                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} files imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFiles( newFileList, storageProvider );

                        // Reset list
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
        private static void SaveFiles( List<DocumentKeys> newFileList, ProviderComponent storageProvider )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.BinaryFiles.AddRange( newFileList.Select( f => f.File ) );
                rockContext.SaveChanges();

                foreach ( var entry in newFileList )
                {
                    if ( entry.File != null )
                    {
                        if ( storageProvider != null )
                        {
                            storageProvider.SaveContent( entry.File );
                            entry.File.Path = storageProvider.GetPath( entry.File );
                        }
                        else
                        {
                            LogException( "Binary File Import", string.Format( "Could not load provider {0}.", storageProvider.ToString() ) );
                        }
                    }

                    // set person attribute value to this binary file guid
                    var attributeValue = rockContext.AttributeValues.FirstOrDefault( p => p.AttributeId == entry.AttributeId && p.EntityId == entry.PersonId );
                    if ( attributeValue == null || attributeValue.CreatedDateTime < entry.File.CreatedDateTime )
                    {
                        bool addToContext = attributeValue == null;
                        attributeValue = new AttributeValue();
                        attributeValue.EntityId = entry.PersonId;
                        attributeValue.AttributeId = entry.AttributeId;
                        attributeValue.Value = entry.File.Guid.ToString();
                        attributeValue.IsSystem = false;

                        if ( addToContext )
                        {
                            rockContext.AttributeValues.Add( attributeValue );
                        }
                    }
                }

                rockContext.SaveChanges( DisableAuditing );
            } );
        }
    }
}