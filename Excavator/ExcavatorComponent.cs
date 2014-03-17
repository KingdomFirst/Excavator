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
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OrcaMDF.Core.Engine;
using OrcaMDF.Core.MetaData;

namespace Excavator
{
    /// <summary>
    /// Provides a type-safe reference to report progress to the UI
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="status">The status.</param>
    public delegate void ReportProgress( int value, string status );

    /// <summary>
    /// Excavator holds the base methods and properties needed to convert data to Rock
    /// </summary>
    public abstract class ExcavatorComponent
    {
        #region Fields

        /// <summary>
        /// The local database
        /// </summary>
        public Database database;

        /// <summary>
        /// Gets the full name of the excavator type.
        /// </summary>
        /// <value>
        /// The name of the database being imported.
        /// </value>
        public abstract string FullName
        {
            get;
        }

        /// <summary>
        /// Holds a reference to the loaded nodes
        /// </summary>
        public List<DatabaseNode> TableNodes;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the full name of this excavator type.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return FullName;
        }

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <returns></returns>
        public bool LoadSchema( object db )
        {
            // Currently only imports MDF's (using OrcaMDF framework)
            database = (Database)db;
            TableNodes = new List<DatabaseNode>();
            var scanner = new DataScanner( database );
            var tables = database.Dmvs.Tables;

            foreach ( var table in tables.Where( t => !t.IsMSShipped ).OrderBy( t => t.Name ) )
            {
                var rows = scanner.ScanTable( table.Name );
                var tableItem = new DatabaseNode();
                tableItem.Name = table.Name;
                tableItem.NodeType = typeof( object );

                var rowSchema = rows.FirstOrDefault();
                if ( rowSchema != null )
                {
                    foreach ( var column in rowSchema.Columns )
                    {
                        var childItem = new DatabaseNode();
                        childItem.Name = column.Name;
                        childItem.NodeType = Extensions.GetSQLType( column.Type );
                        childItem.Table.Add( tableItem );
                        tableItem.Columns.Add( childItem );
                    }
                }

                TableNodes.Add( tableItem );
            }

            return TableNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Previews the data.
        /// </summary>
        /// <param name="tableName">Name of the table to preview.</param>
        /// <returns></returns>
        public DataTable PreviewData( string nodeId )
        {
            var node = TableNodes.Where( n => n.Id.Equals( nodeId ) || n.Columns.Any( c => c.Id == nodeId ) ).FirstOrDefault();

            if ( node != null )
            {
                var scanner = new DataScanner( database );
                var rows = scanner.ScanTable( node.Name );
                var dataTable = new DataTable();
                foreach ( var column in node.Columns )
                {
                    dataTable.Columns.Add( column.Name, column.NodeType );
                }

                var rowData = rows.FirstOrDefault();
                if ( rowData != null )
                {
                    var rowPreview = dataTable.NewRow();
                    foreach ( var column in rowData.Columns )
                    {
                        rowPreview[column.Name] = rowData[column] ?? DBNull.Value;
                    }

                    dataTable.Rows.Add( rowPreview );
                    return dataTable;
                }
            }

            return null;
        }

        /// <summary>
        /// Transforms and saves the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public abstract int TransformData( string importUser = null );

        #endregion

        #region Events

        /// <summary>
        /// Occurs when progress updated.
        /// </summary>
        public event ReportProgress ProgressUpdated;

        /// <summary>
        /// Reports the progress with a custom status.
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="status">The status.</param>
        public void ReportProgress( int progress, string status )
        {
            if ( ProgressUpdated != null )
            {
                ProgressUpdated( progress, Environment.NewLine + DateTime.Now.ToLongTimeString() + "  " + status );
            }
        }

        /// <summary>
        /// Reports a partial progress with extra ellipses
        /// </summary>
        /// <param name="progress">The progress.</param>
        /// <param name="status">The status.</param>
        public void ReportPartialProgress()
        {
            if ( ProgressUpdated != null )
            {
                ProgressUpdated( 0, "." );
            }
        }

        #endregion
    }

    /// <summary>
    /// Loads all the excavator components
    /// </summary>
    public class FrontEndLoader
    {
        /// <summary>
        /// Holds a list of all the excavator types
        /// </summary>
        [ImportMany( typeof( ExcavatorComponent ) )]
        public List<ExcavatorComponent> excavatorTypes = new List<ExcavatorComponent>();

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontEndLoader"/> class.
        /// </summary>
        public FrontEndLoader()
        {
            var extensionFolder = ConfigurationManager.AppSettings["ExtensionPath"];
            var catalog = new AggregateCatalog();
            if ( Directory.Exists( extensionFolder ) )
            {
                catalog.Catalogs.Add( new DirectoryCatalog( extensionFolder ) );
            }

            var currentDirectory = Path.GetDirectoryName( Application.ExecutablePath );
            catalog.Catalogs.Add( new DirectoryCatalog( currentDirectory ) );

            try
            {
                var container = new CompositionContainer( catalog );
                container.ComposeParts( this );
            }
            catch
            {
                // no extensions in this folder
            }
        }
    }
}
