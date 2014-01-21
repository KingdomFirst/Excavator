//
// THIS WORK IS LICENSED UNDER A CREATIVE COMMONS ATTRIBUTION-NONCOMMERCIAL-
// SHAREALIKE 3.0 UNPORTED LICENSE:
// http://creativecommons.org/licenses/by-nc-sa/3.0/
//

using System;
using System.Data;
using System.Linq;
using System.Reflection;
using OrcaMDF.Core.Engine;

namespace Excavator
{
    /// <summary>
    /// This extends the base Excavator class to account for FellowshipOne's database model
    /// </summary>    
    class F1 : ExcavatorComponent
    {
        /// <summary>
        /// The local dataset
        /// </summary>
        private DataSet dataset;

        /// <summary>
        /// List of the valid types in this namespace
        /// </summary>
        private Type[] validTypes;

        /// <summary>
        /// Gets the full name of the excavator type.
        /// </summary>
        /// <value>
        /// The full name.
        /// </value>
        public override string FullName
        {
            get { return "FellowshipOne"; }
        }

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <returns></returns>
        public override bool Load( object database )
        {
            var db = (Database)database;
            var scanner = new DataScanner( db );
            var tables = db.Dmvs.Tables;
            dataset = new DataSet();

            validTypes = Assembly.GetExecutingAssembly().GetTypes().ToArray();

            foreach ( var table in tables.Where( t => !t.IsMSShipped ) )
            {
                
                var rows = scanner.ScanTable( table.Name );
                var scannedTable = new DataTable();
                dataset.Tables.Add( scannedTable );

                var rowSchema = rows.FirstOrDefault();
                if ( rowSchema != null )
                {
                    foreach ( var column in rowSchema.Columns )
                    {
                        Type a = Extensions.GetSQLType( column.Type );
                        scannedTable.Columns.Add( column.Name, a );
                    }
                }                

                //foreach( var row in rows )
                //{
                //    var scannedRow = scannedTable.NewRow();                    

                //    foreach ( var column in row.Columns )
                //    {
                //        scannedRow[column.Name] = row[column] ?? DBNull.Value;
                //    }

                //    scannedTable.Rows.Add( scannedRow );
                //}                
            }

            return dataset.Tables.Count > 0 ? true : false;
        }

        /// <summary>
        /// Saves the data for this instance.
        /// </summary>
        /// <returns></returns>
        public override bool Save()
        {
            return false;
        }


        #endregion
    }
}
