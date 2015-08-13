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
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;
using Excavator.Utility;
using OrcaMDF.Core.Engine;
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
    partial class BinaryFileComponent : ExcavatorComponent
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
        private List<BinaryFile> FilesToImport { get; set; }

        /// <summary>
        /// The file types
        /// </summary>
        protected List<BinaryFileType> FileTypes;

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        protected static int? ImportPersonAliasId;

        // Flag to run postprocessing audits during save
        protected static bool DisableAuditing = true;

        // Report progress when a multiple of this number has been imported
        private static int ReportingNumber = 100;

        #endregion Fields

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override bool LoadSchema( string fileName )
        {
            if ( FilesToImport == null )
            {
                FilesToImport = new List<BinaryFile>();
                DataNodes = new List<DataNode>();
            }

            var zipFile = new BinaryFile( fileName );
            zipFile.FileNodes = new List<DataNode>();

            var tableItem = new DataNode();
            tableItem.Name = fileName;

            foreach ( var document in zipFile.ArchiveFolder.Entries.Take( 50 ) )
            {
                if ( document != null )
                {
                    var dataItem = new DataNode();
                    dataItem.Name = document.Name;
                    var extension = document.FullName.Substring( document.FullName.Length - 3, 3 );
                    dataItem.NodeType = typeof( string );

                    // not sure how to preview the actual data here?
                    dataItem.Value = document.Archive.Entries.FirstOrDefault();
                    tableItem.Children.Add( dataItem );
                }
            }

            DataNodes.Add( tableItem );
            FilesToImport.Add( zipFile );

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
            var fileList = DataNodes.Where( n => n.Checked != false ).ToList();

            foreach ( var file in fileList )
            {
                if ( file.Name.StartsWith( "People" ) )
                {
                    MapPeople( settings );
                }
            }

            ReportProgress( 0, "Checking for existing attributes..." );
            LoadExistingRockData( rockContext );

            // Report the final imported count
            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", 100 ) );
            return 0;
        }

        /// <summary>
        /// Saves the model.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="newPersonList">The new person list.</param>
        private static void SaveModel( List<Person> newPersonList )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.People.AddRange( newPersonList );
                rockContext.SaveChanges( DisableAuditing );
            } );
        }

        /// <summary>
        /// Loads Rock data that's used globally by the transform
        /// </summary>
        private void LoadExistingRockData( RockContext rockContext = null )
        {
            rockContext = rockContext ?? new RockContext();

            FileTypes = new BinaryFileTypeService( rockContext ).Queryable().AsNoTracking().ToList();

            // load attributes to get the default storage location
            foreach ( var type in FileTypes )
            {
                type.LoadAttributes( rockContext );
            }
        }

        #endregion Methods
    }

    public class BinaryFile
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
        public BinaryFile( string fileName )
        {
            FileName = fileName;
            ArchiveFolder = new ZipArchive( new FileStream( fileName, FileMode.Open ) );
        }
    }
}