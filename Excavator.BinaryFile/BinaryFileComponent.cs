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
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Excavator;
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
            if ( DataNodes == null )
            {
                DataNodes = new List<DataNode>();
            }

            var folderItem = new DataNode();
            var previewFolder = new ZipArchive( new FileStream( fileName, FileMode.Open ) );
            folderItem.Name = Path.GetFileNameWithoutExtension( fileName );

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

                    //var extension = document.FullName.Substring( document.FullName.Length - 3, 3 );
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
            var selectedFiles = DataNodes.Where( n => n.Checked != false ).ToList();

            foreach ( var file in selectedFiles )
            {
                var actualFileName = Path.GetFileNameWithoutExtension( file.Name );

                IBinaryFile worker = IMapAdapterFactory.GetAdapter( actualFileName );
                if ( worker != null )
                {
                    var folder = new ZipArchive( new FileStream( file.Name, FileMode.Open ) );
                    worker.Map( folder );
                }
                else
                {
                    LogException( "Binary File", string.Format( "Unknown File: {0} does not start with the name of a known data map.", actualFileName ) );
                }
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

    #region Helper Classes

    /// <summary>
    /// Generic map interface
    /// </summary>
    public interface IBinaryFile
    {
        void Map( ZipArchive zipData );
    }

    /// <summary>
    /// Adapter helper method to call the write object map
    /// </summary>
    public static class IMapAdapterFactory
    {
        public static IBinaryFile GetAdapter( string fileName )
        {
            IBinaryFile adapter = null;

            var configFileTypes = ConfigurationManager.GetSection( "binaryFileTypes" ) as NameValueCollection;

            // ensure we have a file matching a config type
            //if ( configFileTypes != null && configFileTypes.AllKeys.Any( k => fileName.StartsWith( k.RemoveWhitespace() ) ) )
            //{
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

            //}

            return adapter;
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

    #endregion
}