// <copyright>
// Copyright 2013 by the Spark Development Network
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Storage;
using Rock.Storage.Provider;
using Rock.Web.Cache;
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
        /// Gets the full name of the excavator type.
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

        /// All the people who've been imported
        protected static List<PersonKeys> ImportedPeople;

        /// <summary>
        /// The available storage provider types
        /// </summary>
        [ImportMany]
        protected List<ProviderComponent> StorageProviders = new List<ProviderComponent>();

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

            DataNodes.Add( folderItem );

            return DataNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        public override int TransformData( Dictionary<string, string> settings )
        {
            var importUser = settings["ImportUser"];
            int totalCount = 0;

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
                var selectedFileType = FileTypes.FirstOrDefault( t => selectedFile.Name.RemoveWhitespace().StartsWith( t.Name.RemoveWhitespace(), StringComparison.InvariantCultureIgnoreCase ) );
                if ( selectedFileType == null )
                {
                    selectedFileType = FileTypes.FirstOrDefault( f => f.Name == "Default" );
                }

                var archiveFolder = new ZipArchive( new FileStream( selectedFile.Path, FileMode.Open ) );
                IBinaryFile worker = IMapAdapterFactory.GetAdapter( selectedFile.Name );
                if ( worker != null && selectedFileType != null )
                {
                    ReportProgress( 0, string.Format( "Starting {0} file import", selectedFileType.Name ) );
                    var selectedProvider = StorageProviders.FirstOrDefault( p => selectedFileType.StorageEntityTypeId == p.EntityType.Id );
                    worker.Map( archiveFolder, selectedFileType, selectedProvider );
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
        private void LoadRockData( RockContext lookupContext = null )
        {
            lookupContext = lookupContext ?? new RockContext();

            // initialize storage providers and file types
            LoadStorageProviders();

            FileTypes = new BinaryFileTypeService( lookupContext ).Queryable().AsNoTracking().ToList();

            // core-specified blacklist files
            FileTypeBlackList = ( GlobalAttributesCache.Read().GetValue( "ContentFiletypeBlacklist" )
                ?? string.Empty ).Split( new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries );

            // clean up blacklist
            FileTypeBlackList = FileTypeBlackList.Select( a => a.ToLower().TrimStart( new char[] { '.', ' ' } ) );

            // get all the file types we'll be importing
            var binaryTypeSettings = ConfigurationManager.GetSection( "binaryFileTypes" ) as NameValueCollection;

            // create any custom types that don't exist yet
            foreach ( var typeKey in binaryTypeSettings.AllKeys )
            {
                var fileType = FileTypes.FirstOrDefault( f => f.Name == typeKey );

                // create new binary file type if it doesn't exist
                if ( fileType == null )
                {
                    fileType = new BinaryFileType();
                    fileType.Name = typeKey;
                    fileType.Description = typeKey;
                    fileType.AllowCaching = true;

                    var typeValue = binaryTypeSettings[typeKey];
                    if ( typeValue != null )
                    {
                        var storageProvider = StorageProviders.FirstOrDefault( p => p.TypeName.RemoveWhitespace().EndsWith( typeValue.RemoveWhitespace() ) );
                        if ( storageProvider != null )
                        {
                            // ensure the storage provider is active
                            fileType.StorageEntityTypeId = storageProvider.EntityType.Id;
                            lookupContext.BinaryFileTypes.Add( fileType );
                            lookupContext.SaveChanges();
                            FileTypes.Add( fileType );
                        }
                        else
                        {
                            LogException( "Binary File Import", string.Format( "{0} must use the name of a configured storage provider.", typeKey ) );
                        }
                    }
                    else
                    {
                        LogException( "Binary File Import", string.Format( "{0} must specify the storage provider type.", typeKey ) );
                    }
                }
            }

            // load attributes on file types
            foreach ( var type in FileTypes )
            {
                type.LoadAttributes( lookupContext );
            }

            // get a list of all the imported people keys
            var personAliasList = new PersonAliasService( lookupContext ).Queryable().AsNoTracking().ToList();
            ImportedPeople = personAliasList.Select( pa =>
                new PersonKeys()
                {
                    PersonAliasId = pa.Id,
                    PersonId = pa.PersonId,
                    IndividualId = pa.ForeignId,
                } ).ToList();
        }

        /// <summary>
        /// Loads the storage providers.
        /// </summary>
        public void LoadStorageProviders()
        {
            // check the current directory for other storage providers
            var catalog = new AggregateCatalog();
            var dllUri = new UriBuilder( Assembly.GetExecutingAssembly().CodeBase );
            var currentDirectory = Path.GetDirectoryName( Uri.UnescapeDataString( dllUri.Path ) );
            catalog.Catalogs.Add( new DirectoryCatalog( currentDirectory, "*.dll" ) );

            try
            {
                // make sure we don't crash/get permission errors while loading
                var container = new CompositionContainer( catalog, true );
                container.ComposeParts( this );
            }
            catch ( Exception ex )
            {
                // permissions error or other
                var exception = ex.ToString();
                if ( ex.InnerException != null )
                {
                    exception = ex.InnerException.ToString();
                }

                LogException( "Components", string.Format( "{0}. Please check your permissions or run as Administrator.", exception ) );
            }

            // add the two core storage providers
            StorageProviders.Add( new Database() );
            StorageProviders.Add( new FileSystem() );
        }

        #endregion Methods
    }

    #region Helper Classes

    /// <summary>
    /// Generic map interface
    /// </summary>
    public interface IBinaryFile
    {
        void Map( ZipArchive zipData, BinaryFileType fileType, ProviderComponent storageProvider );
    }

    /// <summary>
    /// Adapter helper method to call the right object Map()
    /// </summary>
    public static class IMapAdapterFactory
    {
        public static IBinaryFile GetAdapter( string fileName )
        {
            // create the adapter so we can instantiate it later
            IBinaryFile adapter = null;

            // declare the component class we're looking for
            var iBinaryFileType = typeof( IBinaryFile );

            // get the available binary file maps
            var binaryFileMaps = iBinaryFileType.Assembly.ExportedTypes.Where( p => iBinaryFileType.IsAssignableFrom( p ) && !p.IsInterface );

            // pick the one that starts with the name of this .zip
            var selectedType = binaryFileMaps.FirstOrDefault( t => fileName.StartsWith( t.Name.RemoveWhitespace() ) );

            // assume Ministry Document (generic data type) by default
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