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
    public delegate void ProgressUpdate( int value );

    /// <summary>
    /// Excavator class holds the base methods and properties needed to convert data to Rock
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
        /// The full name.
        /// </value>
        public abstract string FullName
        {
            get;
        }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        /// <value>
        /// The error message.
        /// </value>
        public virtual string errorMessage
        {
            get { return string.Empty; }
            set { errorMessage = value; }
        }

        /// <summary>
        /// Holds a reference to the loaded nodes
        /// </summary>
        public List<DatabaseNode> loadedNodes;

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
            // currently only handles orca framework
            database = (Database)db;
            // TODO: implement option to read from SQL //
            loadedNodes = new List<DatabaseNode>();
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

                loadedNodes.Add( tableItem );
            }

            return loadedNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Previews the data.
        /// </summary>
        /// <param name="tableName">Name of the table to preview.</param>
        /// <returns></returns>
        public DataTable PreviewData( string nodeId )
        {
            var node = loadedNodes.Where( n => n.Id.Equals( nodeId ) ).FirstOrDefault();

            // if the current node has a parent, preview the parent's data
            if ( node.Table.Any() )
            {
                node = loadedNodes.Where( n => n.Id.Equals( node.Table.Select( t => t.Id ) ) ).FirstOrDefault();
            }

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

            return null;
        }

        /// <summary>
        /// Transforms and saves the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public abstract bool TransformData();

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

            // too many other assemblies in current directory to use this
            //var currentDirectory = Path.GetDirectoryName( Application.ExecutablePath );
            //catalog.Catalogs.Add( new DirectoryCatalog( currentDirectory ) );

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
