using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Storage.Provider;
using Rock.Web.Cache;
using static Excavator.Utility.CachedTypes;
using static Excavator.Utility.Extensions;
using Database = Rock.Storage.Provider.Database;

namespace Excavator.BinaryFile
{
    /// <summary>
    /// Data models and mapping methods to import binary files
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    public class BinaryFileComponent : ExcavatorComponent
    {
        #region Fields

        /// <summary>
        /// Gets the full name of the Excavator type.
        /// </summary>
        /// <value>
        /// The name of the database being imported.
        /// </value>
        public override string FullName
        {
            get { return "Binary File"; }
        }

        /// <summary>
        /// Gets the supported file extension type(s).
        /// </summary>
        /// <value>
        /// The supported extension type(s).
        /// </value>
        public override string ExtensionType
        {
            get { return ".zip"; }
        }

        /// <summary>
        /// All the people who've been imported
        /// </summary>
        protected static List<PersonKeys> ImportedPeople;

        /// <summary>
        /// The database provider
        /// </summary>
        protected static Database DatabaseProvider;

        /// <summary>
        /// The filesystem provider
        /// </summary>
        protected static FileSystem FileSystemProvider;

        // Binary File RootPath Attribute
        protected static AttributeCache RootPathAttribute;

        // Maintains compatibility with core blacklist
        protected static IEnumerable<string> FileTypeBlackList;

        /// <summary>
        /// The file types
        /// </summary>
        protected static List<BinaryFileType> FileTypes;

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        protected static int? ImportPersonAliasId;

        #endregion Fields

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override bool LoadSchema( string fileName )
        {
            if ( DataNodes == null )
            {
                DataNodes = new List<DataNode>();
            }

            var folderItem = new DataNode();
            var previewFolder = new ZipArchive( new FileStream( fileName, FileMode.Open ) );
            folderItem.Name = Path.GetFileNameWithoutExtension( fileName );
            folderItem.Path = fileName;

            foreach ( var document in previewFolder.Entries.Take( 50 ) )
            {
                if ( document != null )
                {
                    var entryItem = new DataNode();
                    entryItem.Name = document.FullName;
                    string content = new StreamReader( document.Open() ).ReadToEnd();
                    entryItem.Value = Encoding.UTF8.GetBytes( content ) ?? null;
                    entryItem.NodeType = typeof( byte[] );
                    entryItem.Parent.Add( folderItem );
                    folderItem.Children.Add( entryItem );
                }
            }

            previewFolder.Dispose();
            DataNodes.Add( folderItem );
            return DataNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <returns></returns>
        public override int TransformData( Dictionary<string, string> settings )
        {
            var importUser = settings["ImportUser"];
            var totalCount = 0;

            ReportProgress( 0, "Starting health checks..." );
            var rockContext = new RockContext();
            var personService = new PersonService( rockContext );
            var importPerson = personService.GetByFullName( importUser, allowFirstNameOnly: true ).FirstOrDefault();

            if ( importPerson == null )
            {
                importPerson = personService.Queryable().AsNoTracking().FirstOrDefault();
            }

            ImportPersonAliasId = importPerson.PrimaryAliasId;
            ReportProgress( 0, "Checking for existing attributes..." );
            LoadRockData( rockContext );

            // only import things that the user checked
            foreach ( var selectedFile in DataNodes.Where( n => n.Checked != false ) )
            {
                var defaultFileType = FileTypes.FirstOrDefault( f => f.Name == "Ministry Document" );
                var specificFileType = FileTypes.FirstOrDefault( t => selectedFile.Name.RemoveWhitespace().StartsWith( t.Name.RemoveWhitespace() ) );

                var archiveFolder = new ZipArchive( new FileStream( selectedFile.Path, FileMode.Open ) );
                var worker = IMapAdapterFactory.GetAdapter( selectedFile.Name.RemoveWhitespace() );
                if ( worker != null )
                {
                    worker.Map( archiveFolder, specificFileType ?? defaultFileType );
                    totalCount += archiveFolder.Entries.Count;
                }
                else
                {
                    LogException( "Binary File", string.Format( "Unknown File: {0} does not start with the name of a known data map.", selectedFile.Name ) );
                }
            }

            // Report the final imported count
            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", totalCount ) );
            return totalCount;
        }

        /// <summary>
        /// Loads Rock data that's used globally by the transform
        /// </summary>
        /// <param name="lookupContext">The lookup context.</param>
        private static void LoadRockData( RockContext lookupContext = null )
        {
            lookupContext = lookupContext ?? new RockContext();

            // initialize file providers
            DatabaseProvider = new Database();
            FileSystemProvider = new FileSystem();

            // core-specified attribute guid for setting file root path
            RootPathAttribute = AttributeCache.Read( new Guid( "3CAFA34D-9208-439B-A046-CB727FB729DE" ) );

            // core-specified blacklist files
            FileTypeBlackList = ( GlobalAttributesCache.Read().GetValue( "ContentFiletypeBlacklist" )
                ?? string.Empty ).Split( new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries );

            // clean up blacklist
            FileTypeBlackList = FileTypeBlackList.Select( a => a.ToLower().TrimStart( new char[] { '.', ' ' } ) );
            FileTypes = new BinaryFileTypeService( lookupContext ).Queryable().AsNoTracking().ToList();

            // get all the types we'll be importing
            var binaryTypeSettings = ConfigurationManager.GetSection( "binaryFileTypes" ) as NameValueCollection;

            // create any custom types defined in settings that don't exist yet
            foreach ( var typeKey in binaryTypeSettings.AllKeys )
            {
                if ( !FileTypes.Any( f => f.Name == typeKey ) )
                {
                    var newFileType = new BinaryFileType();
                    lookupContext.BinaryFileTypes.Add( newFileType );
                    newFileType.Name = typeKey;
                    newFileType.Description = typeKey;
                    newFileType.AllowCaching = true;

                    var typeValue = binaryTypeSettings[typeKey];
                    if ( typeValue != null )
                    {
                        // #TODO: support additional storage types (like AWS?)
                        newFileType.StorageEntityTypeId = typeValue.Equals( "Database" ) ? DatabaseStorageTypeId : FileSystemStorageTypeId;
                        newFileType.Attributes = new Dictionary<string, AttributeCache>();
                        newFileType.AttributeValues = new Dictionary<string, AttributeValueCache>();

                        // save changes to binary type to get an ID
                        lookupContext.SaveChanges();

                        var newRootPath = new AttributeValue()
                        {
                            AttributeId = RootPathAttribute.Id,
                            EntityId = newFileType.Id,
                            Value = typeValue
                        };

                        newFileType.Attributes.Add( RootPathAttribute.Key, RootPathAttribute );
                        newFileType.AttributeValues.Add( RootPathAttribute.Key, new AttributeValueCache( newRootPath ) );

                        // save attribute values with the current type ID
                        lookupContext.AttributeValues.Add( newRootPath );
                    }

                    lookupContext.SaveChanges();
                    FileTypes.Add( newFileType );
                }
            }

            // load attributes on file system types to get the default storage location
            foreach ( var type in FileTypes )
            {
                type.LoadAttributes( lookupContext );

                if ( type.StorageEntityTypeId == FileSystemStorageTypeId && binaryTypeSettings.AllKeys.Any( k => type.Name.Equals( k ) ) )
                {
                    // override the configured storage location since we can't handle relative paths
                    type.AttributeValues["RootPath"].Value = binaryTypeSettings[type.Name];
                }
            }

            // get a list of all the imported people keys
            var personAliasList = new PersonAliasService( lookupContext ).Queryable().AsNoTracking().ToList();
            ImportedPeople = personAliasList.Select( pa =>
                new PersonKeys()
                {
                    PersonAliasId = pa.Id,
                    PersonId = pa.PersonId,
                    PersonForeignId = pa.ForeignId,
                    PersonForeignKey = pa.ForeignKey
                } ).ToList();
        }

        #endregion Methods
    }

    #region Helper Classes

    /// <summary>
    /// Generic map interface
    /// </summary>
    public interface IBinaryFile
    {
        void Map( ZipArchive zipData, BinaryFileType fileType );
    }

    /// <summary>
    /// Adapter helper method to call the right object Map()
    /// </summary>
    public static class IMapAdapterFactory
    {
        public static IBinaryFile GetAdapter( string fileName )
        {
            IBinaryFile adapter = null;

            var configFileTypes = ConfigurationManager.GetSection( "binaryFileTypes" ) as NameValueCollection;

            // by default will assume a ministry document
            var iBinaryFileType = typeof( IBinaryFile );
            var mappedFileTypes = iBinaryFileType.Assembly.ExportedTypes
                .Where( p => iBinaryFileType.IsAssignableFrom( p ) && !p.IsInterface );
            var selectedType = mappedFileTypes.FirstOrDefault( t => fileName.StartsWith( t.Name.RemoveWhitespace() ) );
            if ( selectedType != null )
            {
                adapter = (IBinaryFile)Activator.CreateInstance( selectedType );
            }
            else
            {
                adapter = new MinistryDocument();
            }

            return adapter;
        }
    }

    #endregion
}
