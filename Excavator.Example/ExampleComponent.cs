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
        /// The person assigned to do the import
        /// </summary>
        private PersonAlias ImportPersonAlias;

        #endregion

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        public override int TransformData( string importUser = null )
        {
            // Report progress to the main thread so it can update the UI
            ReportProgress( 0, "Starting import..." );

            // Connects to the source database (already loaded in memory by the UI)
            var scanner = new DataScanner( database );

            // List of tables the user would like to import
            var tableList = TableNodes.Where( n => n.Checked != false ).Select( n => n.Name ).ToList();

            // Supplies a lazy-loaded database queryable
            var tableData = scanner.ScanTable( "TableName" ).AsQueryable();

            // Iterate and do something with the data
            foreach ( var row in tableData )
            {
                // Get a value from the row. This has to be a nullable type.
                string columnValue = row["ColumnName"] as string;

                // Create a Rock model and assign data to it
                Person person = new Person();

                // Standard process to save data in Rock
                RockTransactionScope.WrapTransaction( () =>
                {
                    // Instantiate the object model service
                    var personService = new PersonService();

                    // If it's a new model, add it to the database first
                    personService.Add( person, ImportPersonAlias );

                    // Save the data to the database
                    personService.Save( person, ImportPersonAlias );
                } );
            }

            int numberImported = tableData.Count();
            ReportProgress( 0, Environment.NewLine + string.Format( "Completed import: {0:N0} records imported.", numberImported ) );
            return numberImported;
        }

        #endregion
    }
}
