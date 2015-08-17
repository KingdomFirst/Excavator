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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Excavator.BinaryFile;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.BinaryFile
{
    /// <summary>
    /// This example extends the base Excavator class to consume a database model.
    /// Currently does nothing, this is just an example.
    /// Data models and mapping methods can be in other partial classes.
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

        /// <summary>
        /// Gets or sets the files to import.
        /// </summary>
        /// <value>
        /// The files to import.
        /// </value>
        private List<BinaryInstance> BinaryInstances { get; set; }

        /// <summary>
        /// All the people who've been imported
        /// </summary>
        protected static List<PersonKeys> ImportedPeople;

        // StorageEntity attribute
        //protected static AttributeCache ;

        /// <summary>
        /// The file types
        /// </summary>
        protected List<BinaryFileType> FileTypes;

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
            if ( BinaryInstances == null )
            {
                BinaryInstances = new List<BinaryInstance>();
                DataNodes = new List<DataNode>();
            }

            var folderItem = new DataNode();
            var previewInstance = new BinaryInstance( fileName );
            folderItem.Name = Path.GetFileNameWithoutExtension( fileName );

            foreach ( var document in previewInstance.ArchiveFolder.Entries.Take( 50 ) )
            {
                if ( document != null )
                {
                    var entryItem = new DataNode();
                    entryItem.Name = document.Name;
                    string content = new StreamReader( document.Open() ).ReadToEnd();
                    entryItem.Value = Encoding.UTF8.GetBytes( content ) ?? null;
                    entryItem.NodeType = typeof( byte[] );
                    entryItem.Parent.Add( folderItem );
                    folderItem.Children.Add( entryItem );

                    //var extension = document.FullName.Substring( document.FullName.Length - 3, 3 );
                }
            }

            previewInstance.FileNodes.Add( folderItem );
            DataNodes.Add( folderItem );
            BinaryInstances.Add( previewInstance );

            return DataNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Previews the data. Overrides base class because we have potential for more than one imported file
        /// </summary>
        /// <param name="tableName">Name of the table to preview.</param>
        /// <returns></returns>
        public override DataTable PreviewData( string nodeId )
        {
            foreach ( var instance in BinaryInstances )
            {
                var node = instance.FileNodes.Where( n => n.Id.Equals( nodeId ) || n.Children.Any( c => c.Id == nodeId ) ).FirstOrDefault();
                if ( node != null && node.Children.Any() )
                {
                    var dataTable = new DataTable();
                    dataTable.Columns.Add( "File", typeof( string ) );
                    foreach ( var column in node.Children )
                    {
                        dataTable.Columns.Add( column.Name, column.NodeType );
                    }

                    var rowPreview = dataTable.NewRow();
                    foreach ( var column in node.Children )
                    {
                        rowPreview[column.Name] = column.Value ?? DBNull.Value;
                    }

                    dataTable.Rows.Add( rowPreview );
                    return dataTable;
                }
            }
            return null;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        public override int TransformData( Dictionary<string, string> settings )
        {
            var importUser = settings["ImportUser"];

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
            LoadExistingRockData( rockContext );

            // only import things that the user checked
            var selectedFiles = BinaryInstances.Where( c => c.FileNodes.Any( n => n.Checked != false ) ).ToList();

            foreach ( var file in selectedFiles )
            {
                //IMap adapter = IMapAdapterFactory.GetAdapter( file );
                //if ( adapter != null )
                //{
                //    adapter.Map( file.Value as ZipArchive );
                //}
            }

            // Report the final imported count
            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", 100 ) );
            return 0;
        }

        /// <summary>
        /// Loads Rock data that's used globally by the transform
        /// </summary>
        private void LoadExistingRockData( RockContext lookupContext = null )
        {
            lookupContext = lookupContext ?? new RockContext();

            FileTypes = new BinaryFileTypeService( lookupContext ).Queryable().AsNoTracking().ToList();

            // load attributes to get the default storage location
            foreach ( var type in FileTypes )
            {
                type.LoadAttributes( lookupContext );
            }

            var personAliasIds = new PersonAliasService( lookupContext ).Queryable().AsNoTracking().ToList();
            var ImportedPeople = personAliasIds.Select( pa => new PersonKeys()
                {
                    PersonAliasId = pa.Id,
                    PersonId = pa.PersonId,
                    IndividualId = pa.ForeignId.AsType<int?>(),
                } ).ToList();
        }

        /// <summary>
        /// Gets the person keys.
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <param name="householdId">The household identifier.</param>
        /// <param name="includeVisitors">if set to <c>true</c> [include visitors].</param>
        /// <returns></returns>
        protected static PersonKeys GetPersonKeys( int? individualId = null )
        {
            if ( individualId != null )
            {
                return ImportedPeople.FirstOrDefault( p => p.IndividualId == individualId );
            }
            else
            {
                return null;
            }
        }

        #endregion Methods
    }

    public interface IMap
    {
        void Map( ZipArchive zipData );
    }

    public static class IMapAdapterFactory
    {
        public static IMap GetAdapter( string fileName )
        {
            IMap adapter = null;

            var fileTypes = ConfigurationManager.GetSection( "binaryFileTypes" ) as NameValueConfigurationCollection;

            // ensure we have a file matching an adapter type
            if ( fileTypes != null && fileTypes.AllKeys.Any( k => fileName.StartsWith( k ) ) )
            {
                var interfaceType = typeof( IMap );
                var typeInstances = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany( s => s.GetTypes() )
                    .Where( p => interfaceType.IsAssignableFrom( p ) );

                if ( fileName.StartsWith( "Person" ) )
                {
                    adapter = new PersonImage();
                }
                else if ( fileName.StartsWith( "Transaction" ) )
                {
                    adapter = new TransactionImage();
                }
                else
                {
                    adapter = new MinistryDocument();
                }
            }

            return adapter;
        }
    }

    /// <summary>
    ///
    /// </summary>
    public class BinaryInstance
    {
        /// <summary>
        /// Holds a reference to the loaded nodes
        /// </summary>
        public List<DataNode> FileNodes;

        /// <summary>
        /// The local database
        /// </summary>
        public ZipArchive ArchiveFolder;

        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        /// <value>
        /// The name of the file.
        /// </value>
        public string FileName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CSVInstance"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        public BinaryInstance( string fileName )
        {
            FileName = fileName;
            FileNodes = new List<DataNode>();
            ArchiveFolder = new ZipArchive( new FileStream( fileName, FileMode.Open ) );
        }
    }

    /// <summary>
    /// Helper class to store references to people that've been imported
    /// </summary>
    public class PersonKeys
    {
        /// <summary>
        /// Stores the Rock PersonAliasId
        /// </summary>
        public int PersonAliasId;

        /// <summary>
        /// Stores the Rock PersonId
        /// </summary>
        public int PersonId;

        /// <summary>
        /// Stores the F1 Individual Id
        /// </summary>
        public int? IndividualId;

        /// <summary>
        /// Stores the F1 Household Id
        /// </summary>
        public int? HouseholdId;
    }
}