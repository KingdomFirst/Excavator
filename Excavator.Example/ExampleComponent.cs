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
using System.Linq;
using OrcaMDF.Core.Engine;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.Example
{
    /// <summary>
    /// This example extends the base Excavator class to consume a database model.
    /// Currently does nothing, this is just an example.
    /// Data models and mapping methods can be in other partial classes.
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    partial class ExampleComponent : ExcavatorComponent
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
            get { return "Example"; }
        }

        /// <summary>
        /// Gets the supported file extension type(s).
        /// </summary>
        /// <value>
        /// The supported extension type(s).
        /// </value>
        public override string ExtensionType
        {
            get { return ".example"; }
        }

        /// <summary>
        /// The local database
        /// </summary>
        public Database Database;

        // Disable compiler warning: value never assigned
#pragma warning disable 414
#pragma warning disable 169

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        private PersonAlias ImportPersonAlias;

        // Flag to set postprocessing audits on save
        private static bool DisableAudit = true;

        // Report progress when a multiple of this number has been imported
        private static int ReportingNumber = 100;

#pragma warning restore

        #endregion

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override bool LoadSchema( string fileName )
        {
            Database = new Database( fileName );
            TableNodes = new List<DatabaseNode>();
            var scanner = new DataScanner( Database );
            var tables = Database.Dmvs.Tables;

            foreach ( var table in tables.Where( t => !t.IsMSShipped ).OrderBy( t => t.Name ) )
            {
                var rows = scanner.ScanTable( table.Name );
                var tableItem = new DatabaseNode();
                tableItem.Name = table.Name;
                tableItem.NodeType = typeof( object );

                var rowData = rows.FirstOrDefault();
                if ( rowData != null )
                {
                    foreach ( var column in rowData.Columns )
                    {
                        var childItem = new DatabaseNode();
                        childItem.Name = column.Name;
                        childItem.NodeType = Extensions.GetSQLType( column.Type );
                        childItem.Table.Add( tableItem );
                        tableItem.Columns.Add( childItem );
                        tableItem.Value = rowData[column] ?? DBNull.Value;
                    }
                }

                TableNodes.Add( tableItem );
            }

            return TableNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        public override int TransformData( string importUser = null )
        {
            // Report progress to the main thread so it can update the UI
            ReportProgress( 0, "Starting import..." );

            // Instantiate the object model service
            var rockContext = new RockContext();

            // Connects to the source database (already loaded in memory by the UI)
            var scanner = new DataScanner( Database );

            // List of tables the user would like to import
            var tableList = TableNodes.Where( n => n.Checked != false ).Select( n => n.Name ).ToList();

            // Supplies a lazy-loaded database queryable
            var tableData = scanner.ScanTable( "TableName" ).AsQueryable();

            // Hold a count of how many records have been imported
            int completed = 0;

            // Pick a method to save data to Rock: #1 (simple) or #2 (fast)

            // Option #1. Standard way to put data in Rock
            foreach ( var dataRow in tableData )
            {
                // Get a value from the row. This has to be a nullable type.
                string columnValue = dataRow["ColumnName"] as string;

                // Create a Rock model and assign data to it
                Person person = new Person();
                person.LastName = columnValue;

                rockContext.WrapTransaction( () =>
                {
                    // If it's a new model, add it to the database first
                    rockContext.People.Add( person );

                    // Save the data to the database
                    rockContext.SaveChanges( DisableAudit );
                } );

                completed++;
            }

            // end option #1

            // Option #2. More efficient way to import large data sets
            var newPersonList = new List<Person>();

            foreach ( var dataRow in tableData )
            {
                // Get a value from the row. This has to be a nullable type.
                string columnValue = dataRow["ColumnName"] as string;

                // Create a Rock model and assign data to it
                Person person = new Person();

                newPersonList.Add( new Person() );
                completed++;

                // Save 100 people at a time
                if ( completed % ReportingNumber < 1 )
                {
                    SaveModel( newPersonList );
                }
            }

            // Outside foreach, save any that haven't been saved yet
            if ( newPersonList.Any() )
            {
                SaveModel( newPersonList );
            }

            // end option #2

            // Report the final imported count
            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", completed ) );
            return completed;
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
                rockContext.SaveChanges( DisableAudit );
            } );
        }

        #endregion
    }
}
