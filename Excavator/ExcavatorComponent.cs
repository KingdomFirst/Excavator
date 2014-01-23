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
using System.Configuration;
using System.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Data;
using System.IO;
using System.Linq;
using OrcaMDF.Core.Engine;

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
        /// The percentage complete
        /// </summary>
        private int percentComplete;

        /// <summary>
        /// The local dataset
        /// </summary>
        public Database database;

        /// <summary>
        /// The local dataset
        /// </summary>
        public DataSet dataset;

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
        /// Occurs on progress update.
        /// </summary>
        public event ProgressUpdate OnProgressUpdate;

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
            var scanner = new DataScanner( database );
            var tables = database.Dmvs.Tables;
            dataset = new DataSet();

            foreach ( var table in tables.Where( t => !t.IsMSShipped && t.Name == "Individual_Household" ) )
            {
                var rows = scanner.ScanTable( table.Name );
                var scannedTable = new DataTable();
                scannedTable.TableName = table.Name;
                dataset.Tables.Add( scannedTable );

                var rowSchema = rows.FirstOrDefault();
                if ( rowSchema != null )
                {
                    foreach ( var column in rowSchema.Columns )
                    {
                        Type systemType = Extensions.GetSQLType( column.Type );
                        scannedTable.Columns.Add( column.Name, systemType );
                    }
                }
            }

            return dataset.Tables.Count > 0 ? true : false;
        }

        /// <summary>
        /// Fills the data set.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <returns></returns>
        public DataSet LoadData( object database )
        {
            BackgroundWorker bwLoadDatabase = new BackgroundWorker();
            bwLoadDatabase.DoWork += bwLoadDatabase_DoWork;
            //bwLoadDatabase.ProgressChanged += bwLoadDatabase_ProgressChanged;
            //bwLoadDatabase.RunWorkerCompleted += bwLoadDatabase_RunWorkerCompleted;
            bwLoadDatabase.WorkerReportsProgress = true;
            bwLoadDatabase.RunWorkerAsync();

            return dataset;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public abstract bool TransformData();

        /// <summary>
        /// Saves the data for this instance.
        /// </summary>
        /// <returns></returns>
        public abstract bool SaveData();

        #endregion

        #region Background Tasks

        /// <summary>
        /// Handles the DoWork event of the bwLoadDatabase control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwLoadDatabase_DoWork( object sender, DoWorkEventArgs e )
        {
            BackgroundWorker bw = sender as BackgroundWorker;
            var scanner = new DataScanner( database );
            int totalCount = dataset.Tables.Count;
            int processed = 0;

            foreach ( DataTable table in dataset.Tables )
            {
                var rows = scanner.ScanTable( table.ToString() );
                foreach ( var row in rows )
                {
                    var rowData = table.NewRow();
                    foreach ( var column in row.Columns )
                    {
                        rowData[column.Name] = row[column] ?? DBNull.Value;
                    }

                    table.Rows.Add( rowData );
                }

                percentComplete = processed++ * 100 / totalCount;
                //bw.ReportProgress( percentComplete );
                if ( OnProgressUpdate != null )
                {
                    OnProgressUpdate( percentComplete );
                }
            }
        }


        /// <summary>
        /// Handles the RunWorkerCompleted event of the bwLoadDatabase control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        //private void bwLoadDatabase_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        //{
        //    if ( e.Error == null )
        //    {
        //        // complete
        //    }
        //    else
        //    {
        //        MessageBox.Show( string.Format( "Load Error: {0}", e.Error.Message ) );
        //    }
        //}

        /// <summary>
        /// Handles the ProgressChanged event of the bwLoadDatabase control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        //private void bwLoadDatabase_ProgressChanged( object sender, ProgressChangedEventArgs e )
        //{
        //    throw new NotImplementedException();
        //}

        #endregion
    }

    /// <summary>
    /// Loads all the excavator components
    /// </summary>
    class FrontEndLoader
    {
        [ImportMany( typeof( ExcavatorComponent ) )]
        public List<ExcavatorComponent> excavatorTypes = new List<ExcavatorComponent>();

        public FrontEndLoader()
        {
            var extensionFolder = ConfigurationManager.AppSettings["ExtensionPath"];
            var solutionPath = Directory.GetParent( System.IO.Directory.GetCurrentDirectory() ).Parent.FullName;
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add( new DirectoryCatalog( solutionPath + extensionFolder ) );
            var container = new CompositionContainer( catalog );
            container.ComposeParts( this );
        }
    }
}
