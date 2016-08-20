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
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using Excavator.Utility;
using LumenWorks.Framework.IO.Csv;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.CSV
{
    /// <summary>
    /// This example extends the base Excavator class to consume a CSV model.
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    partial class CSVComponent : ExcavatorComponent
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
            get { return "CSV File"; }
        }

        /// <summary>
        /// Gets the supported file extension type(s).
        /// </summary>
        /// <value>
        /// The supported extension type.
        /// </value>
        public override string ExtensionType
        {
            get { return ".csv"; }
        }

        /// <summary>
        /// The local data store, contains Database and TableNode list
        /// because multiple files can be uploaded
        /// </summary>
        private List<CSVInstance> CsvDataToImport { get; set; }

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        protected static int? ImportPersonAliasId;

        /// <summary>
        /// The anonymous giver record
        /// </summary>
        protected static int? AnonymousGiverAliasId;

        /// <summary>
        /// The person entity type identifier
        /// </summary>
        private int PersonEntityTypeId;

        /// <summary>
        /// The family group type identifier
        /// </summary>
        private int FamilyGroupTypeId;

        /// <summary>
        /// All the family groups who've been imported
        /// </summary>
        private List<Group> ImportedFamilies;

        /// <summary>
        /// The list of current campuses
        /// </summary>
        private List<Campus> CampusList;

        /// <summary>
        /// All imported batches. Used in Batches & Contributions
        /// </summary>
        protected static Dictionary<int, int?> ImportedBatches;

        /// <summary>
        /// All the people keys who've been imported
        /// </summary>
        protected static List<PersonKeys> ImportedPeopleKeys;

        // Existing entity types

        protected static int TextFieldTypeId;
        protected static int IntegerFieldTypeId;

        // Custom attribute types

        protected static AttributeCache IndividualIdAttribute;
        protected static AttributeCache HouseholdIdAttribute;

        #endregion Fields

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// may be called multiple times, if uploading multiple CSV files.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override bool LoadSchema( string fileName )
        {
            //enforce that the filename must be a known configuration.
            if ( !FileIsKnown( fileName ) )
                return false;

            var dbPreview = new CsvReader( new StreamReader( fileName ), true );

            if ( CsvDataToImport == null )
            {
                CsvDataToImport = new List<CSVInstance>();
                DataNodes = new List<DataNode>();
            }

            //a local tableNode object, which will track this one of multiple CSV files that may be imported
            var tableNodes = new List<DataNode>();
            CsvDataToImport.Add( new CSVInstance( fileName ) { TableNodes = tableNodes, RecordType = GetRecordTypeFromFilename( fileName ) } );

            var tableItem = new DataNode();
            tableItem.Name = Path.GetFileNameWithoutExtension( fileName );
            int currentIndex = 0;

            var firstRow = dbPreview.ElementAtOrDefault( 0 );
            if ( firstRow != null )
            {
                foreach ( var columnName in dbPreview.GetFieldHeaders() )
                {
                    var childItem = new DataNode();
                    childItem.Name = columnName;
                    childItem.NodeType = typeof( string );
                    childItem.Value = firstRow[currentIndex] ?? string.Empty;
                    childItem.Parent.Add( tableItem );
                    tableItem.Children.Add( childItem );
                    currentIndex++;
                }

                tableNodes.Add( tableItem );
                DataNodes.Add( tableItem ); //this is to maintain compatibility with the base Excavator object.
            }

            return tableNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Previews the data. Overrides base class because we have potential for more than one imported file
        /// </summary>
        /// <param name="tableName">Name of the table to preview.</param>
        /// <returns></returns>
        //public override DataTable PreviewData( string nodeId )
        //{
        //    foreach ( var dataNode in CsvDataToImport )
        //    {
        //        var node = dataNode.TableNodes.Where( n => n.Id.Equals( nodeId ) || n.Children.Any( c => c.Id == nodeId ) ).FirstOrDefault();
        //        if ( node != null && node.Children.Any() )
        //        {
        //            var dataTable = new DataTable();
        //            dataTable.Columns.Add( "File", typeof( string ) );
        //            foreach ( var column in node.Children )
        //            {
        //                dataTable.Columns.Add( column.Name, column.NodeType );
        //            }

        //            var rowPreview = dataTable.NewRow();
        //            foreach ( var column in node.Children )
        //            {
        //                rowPreview[column.Name] = column.Value ?? DBNull.Value;
        //            }

        //            dataTable.Rows.Add( rowPreview );
        //            return dataTable;
        //        }
        //    }
        //    return null;
        //}

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        public override int TransformData( Dictionary<string, string> settings )
        {
            var importUser = settings["ImportUser"];

            int completed = 0;
            ReportProgress( 0, "Starting health checks..." );
            if ( !LoadExistingData( importUser ) )
            {
                return -1;
            }

            // only import things that the user checked
            List<CSVInstance> selectedCsvData = CsvDataToImport.Where( c => c.TableNodes.Any( n => n.Checked != false ) ).ToList();

            ReportProgress( 0, "Starting data import..." );

            // Person data is important, so load it first
            if ( selectedCsvData.Any( d => d.RecordType == CSVInstance.RockDataType.INDIVIDUAL ) )
            {
                selectedCsvData = selectedCsvData.OrderByDescending( d => d.RecordType == CSVInstance.RockDataType.INDIVIDUAL ).ToList();
            }

            foreach ( var csvData in selectedCsvData )
            {
                if ( csvData.RecordType == CSVInstance.RockDataType.INDIVIDUAL )
                {
                    completed += LoadIndividuals( csvData );

                    //
                    // Refresh the list of imported people for other record types to use.
                    //
                    LoadPersonKeys( new RockContext() );
                }
                else if ( csvData.RecordType == CSVInstance.RockDataType.FAMILY )
                {
                    completed += LoadFamily( csvData );
                }
                else if ( csvData.RecordType == CSVInstance.RockDataType.METRICS )
                {
                    completed += LoadMetrics( csvData );
                }
                else if ( csvData.RecordType == CSVInstance.RockDataType.PLEDGE )
                {
                    completed += MapPledge( csvData );
                }
                else if ( csvData.RecordType == CSVInstance.RockDataType.BATCH )
                {
                    completed += MapBatch( csvData );
                }
                else if ( csvData.RecordType == CSVInstance.RockDataType.CONTRIBUTION )
                {
                    completed += MapContribution( csvData );
                }
            } //read all files

            ReportProgress( 100, string.Format( "Completed import: {0:N0} rows processed.", completed ) );
            return completed;
        }

        /// <summary>
        /// Checks the database for existing import data.
        /// returns false if an error occurred
        /// </summary>
        private bool LoadExistingData( string importUser )
        {
            var lookupContext = new RockContext();
            var personService = new PersonService( lookupContext );
            var importPerson = personService.GetByFullName( importUser, includeDeceased: false, allowFirstNameOnly: true ).FirstOrDefault();
            if ( importPerson == null )
            {
                importPerson = personService.Queryable().FirstOrDefault();
                if ( importPerson == null )
                {
                    LogException( "CheckExistingImport", "The named import user was not found, and none could be created." );
                    return false;
                }
            }

            ImportPersonAliasId = importPerson.PrimaryAliasId;

            var anonymousGiver = personService.GetByFullName( "Anonymous, Giver", includeDeceased: false, allowFirstNameOnly: true ).FirstOrDefault();
            if ( anonymousGiver == null )
            {
                anonymousGiver = personService.Queryable().FirstOrDefault( p => p.Guid.ToString().ToUpper() == "802235DC-3CA5-94B0-4326-AACE71180F48" );
                if ( anonymousGiver == null )
                {
                    LogException( "CheckExistingImport", "The named Anonymous Giving user was not found, and none could be created." );
                    return false;
                }
            }

            AnonymousGiverAliasId = anonymousGiver.PrimaryAliasId;

            PersonEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            FamilyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            ReportProgress( 0, "Checking for existing people..." );

            // Don't track groups in this context, just use it as a static reference
            ImportedFamilies = lookupContext.Groups.AsNoTracking()
                .Where( g => g.GroupTypeId == FamilyGroupTypeId && g.ForeignKey != null ).ToList();

            CampusList = new CampusService( lookupContext ).Queryable().ToList();

            LoadPersonKeys( lookupContext );

            ImportedBatches = new FinancialBatchService( lookupContext ).Queryable().AsNoTracking()
                .Where( b => b.ForeignId != null )
                .ToDictionary( t => ( int )t.ForeignId, t => ( int? )t.Id );
            
            return true;
        }

        /// <summary>
        /// Gets the person keys.
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <returns></returns>
        protected static PersonKeys GetPersonKeys( int? individualId = null )
        {
            if ( individualId != null )
            {
                return ImportedPeopleKeys.FirstOrDefault( p => p.IndividualId == individualId );
            }
            else
            {
                return null;
            }
        }

        protected static void LoadPersonKeys( RockContext lookupContext )
        {
            ImportedPeopleKeys = new PersonAliasService( lookupContext ).Queryable().AsNoTracking()
                .Where( pa => pa.ForeignKey != null )
                .Select( pa => new PersonKeys
                {
                    PersonAliasId = pa.Id,
                    PersonId = pa.PersonId,
                    IndividualId = pa.ForeignId
                } ).ToList();
        }

        #endregion Methods

        #region Convenience Methods

        /// <summary>
        /// Detach all entity objects from the change tracker that is associated with
        /// the given RockContext. Any entity that is stored (for example in an Imported...
        /// variable) should be detached before the function ends. If it is explicitely
        /// attached and then not detached then when it is attached to another context
        /// an exception "An entity object cannot be referenced by multiple instances
        /// of IEntityChangeTracker" occurs.
        /// 
        /// Taken from: http://stackoverflow.com/questions/2465933/how-to-clean-up-an-entity-framework-object-context
        /// </summary>
        public static void DetachAllInContext( RockContext context )
        {
            foreach ( var dbEntityEntry in context.ChangeTracker.Entries() )
            {
                if ( dbEntityEntry.Entity != null )
                {
                    dbEntityEntry.State = EntityState.Detached;
                }
            }
        }
                
        #endregion

        #region File Processing Methods

        /// <summary>
        /// Gets the name of the file without the extension.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        private string GetFileRootName( string fileName )
        {
            var root = Path.GetFileName( fileName ).ToLower().Replace( ".csv", string.Empty );
            return root;
        }

        /// <summary>
        /// Checks if the file matches a known format.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        private bool FileTypeMatches( CSVInstance.RockDataType filetype, string name )
        {
            if ( name.ToUpper().StartsWith( filetype.ToString() ) )
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if the file matches a known format.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        private bool FileIsKnown( string fileName )
        {
            string name = GetFileRootName( fileName );
            foreach ( var filetype in Extensions.Get<CSVInstance.RockDataType>() )
            {
                if ( FileTypeMatches( filetype, name ) )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the record type based on the filename.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns></returns>
        private CSVInstance.RockDataType GetRecordTypeFromFilename( string filename )
        {
            string name = GetFileRootName( filename );
            foreach ( var filetype in Extensions.Get<CSVInstance.RockDataType>() )
            {
                if ( FileTypeMatches( filetype, name ) )
                {
                    return filetype;
                }
            }

            return CSVInstance.RockDataType.NONE;
        }

        #endregion File Processing Methods

        #region Individual Constants

        /*
         * This is the definition of the csv format for the Individual.csv file
         */

        private const int FamilyId = 0;
        private const int FamilyName = 1;
        private const int CreatedDate = 2;
        private const int PersonId = 3;
        private const int Prefix = 4;
        private const int FirstName = 5;
        private const int NickName = 6;
        private const int MiddleName = 7;
        private const int LastName = 8;
        private const int Suffix = 9;
        private const int FamilyRole = 10;
        private const int MaritalStatus = 11;
        private const int ConnectionStatus = 12;
        private const int RecordStatus = 13;
        private const int IsDeceased = 14;
        private const int HomePhone = 15;
        private const int MobilePhone = 16;
        private const int WorkPhone = 17;
        private const int AllowSMS = 18;
        private const int Email = 19;
        private const int IsEmailActive = 20;
        private const int AllowBulkEmail = 21;
        private const int Gender = 22;
        private const int DateOfBirth = 23;
        private const int School = 24;
        private const int GraduationDate = 25;
        private const int Anniversary = 26;
        private const int GeneralNote = 27;
        private const int MedicalNote = 28;
        private const int SecurityNote = 29;

        #endregion Individual Constants

        #region Family Constants

        /*
         * This is the definition for the Family.csv import file:
         * Columns already numbered from Individuals file:
         * private const int FamilyId = 0;
         * private const int FamilyName = 1;
         * private const in CreatedDate = 2;
         */

        private const int Campus = 3;
        private const int Address = 4;
        private const int Address2 = 5;
        private const int City = 6;
        private const int State = 7;
        private const int Zip = 8;
        private const int Country = 9;
        private const int SecondaryAddress = 10;
        private const int SecondaryAddress2 = 11;
        private const int SecondaryCity = 12;
        private const int SecondaryState = 13;
        private const int SecondaryZip = 14;
        private const int SecondaryCountry = 15;

        #endregion Family Constants

        #region Metrics Constants

        /*
         * Definition for the Metrics.csv import file:
         */

        private const int MetricCampus = 0;
        private const int MetricName = 1;
        private const int MetricValue = 2;
        private const int MetricService = 3;
        private const int MetricCategory = 4;

        #endregion Family Constants

        #region Batch Constants

        /*
         * Definition for the Contribution.csv import file:
         */

        private const int BatchID = 0;
        private const int BatchName = 1;
        private const int BatchDate = 2;
        private const int BatchAmount = 3;

        #endregion Batch Constants

        #region Contribution Constants

        /*
         * Definition for the Contribution.csv import file:
         */

        private const int IndividualID = 0;
        private const int FundName = 1;
        private const int SubFundName = 2;
        private const int FundGLAccount = 3;
        private const int SubFundGLAccount = 4;
        private const int FundIsActive = 5;
        private const int SubFundIsActive = 6;
        private const int ReceivedDate = 7;
        private const int CheckNumber = 8;
        private const int Memo = 9;
        private const int ContributionTypeName = 10;
        private const int Amount = 11;
        private const int StatedValue = 12;
        private const int ContributionID = 13;
        private const int ContributionBatchID = 14;

        #endregion Contribution Constants

        #region Pledge Constants

        /*
         * Definition for the Pledge.csv import file:
         * Columns already numbered from Individuals file:
         * private const int IndividualID = 0;
         * private const int FundName = 1;
         * private const int SubFundName = 2;
         * private const int FundGLAccount = 3;
         * private const int SubFundGLAccount = 4;
         * private const int FundIsActive = 5;
         * private const int SubFundIsActive = 6
         */

        private const int PledgeFrequencyName = 7;
        private const int TotalPledge = 8;
        private const int StartDate = 9;
        private const int EndDate = 10;
        private const int PledgeId = 11;

        #endregion Pledge Constants
    }
}