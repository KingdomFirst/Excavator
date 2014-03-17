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
using System.Linq;
using OrcaMDF.Core.Engine;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// This extends the base Excavator class to consume FellowshipOne's database model.
    /// Data models and mapping methods are in the Models and Maps directories, respectively.
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    partial class F1Component : ExcavatorComponent
    {
        #region Fields

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

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        private PersonAlias ImportPersonAlias;

        /// <summary>
        /// All the people who've been imported
        /// </summary>
        private List<ImportedPerson> ImportedPeople;

        /// <summary>
        /// All imported batches. Used in Batches & Contributions
        /// </summary>
        private Dictionary<int?, int?> ImportedBatches;

        /// <summary>
        /// The list of current campuses
        /// </summary>
        private List<Campus> CampusList;

        // Existing entity types

        private int IntegerFieldTypeId;
        private int TextFieldTypeId;
        private int PersonEntityTypeId;
        private int BatchEntityTypeId;

        // Custom entity types

        private int IndividualAttributeId;
        private int HouseholdAttributeId;
        private int BatchAttributeId;

        // Report progress when a multiple of this number has been imported
        private static int ReportingNumber = 30;

        #endregion

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override int TransformData( string importUser = null )
        {
            ReportProgress( 0, "Starting import..." );
            var personService = new PersonService();
            var importPerson = personService.GetByFullName( importUser, includeDeceased: false, allowFirstNameOnly: true ).FirstOrDefault();
            ImportPersonAlias = new PersonAliasService().Get( importPerson.Id );
            var tableList = TableNodes.Where( n => n.Checked != false ).ToList();

            ReportProgress( 0, Environment.NewLine + "Checking for existing attributes..." );
            LoadExistingRockData();

            ReportProgress( 0, Environment.NewLine + "Checking for table dependencies..." );
            bool isValidImport = ImportedPeople.Any() || tableList.Any( n => n.Name.Equals( "Individual_Household" ) );

            var tableDependencies = new List<string>();
            tableDependencies.Add( "Batch" );                // needed to attribute contributions properly
            tableDependencies.Add( "Company" );              // needed to attribute any company items
            tableDependencies.Add( "Individual_Household" ); // needed for just about everything

            if ( isValidImport )
            {
                // Order tables so non-dependents are imported first
                if ( tableList.Any( n => tableDependencies.Contains( n.Name ) ) )
                {
                    tableList = tableList.OrderByDescending( n => tableDependencies.IndexOf( n.Name ) ).ToList();
                }

                var scanner = new DataScanner( database );
                foreach ( var table in tableList )
                {
                    if ( !tableDependencies.Contains( table.Name ) )
                    {
                        switch ( table.Name )
                        {
                            case "Account":
                                MapAccount( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Communication":
                                MapCommunication( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Contribution":
                                MapContribution( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Household_Address":
                                MapFamilyAddress( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Pledge":
                                MapPledge( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            default:
                                break;
                        }

                        // Don't use additional background workers (for now)
                        //BackgroundWorker bwSpawnProcess = new BackgroundWorker();
                        //bwSpawnProcess.DoWork += bwSpawnProcess_DoWork;
                        //bwSpawnProcess.RunWorkerCompleted += bwSpawnProcess_RunWorkerCompleted;
                        //bwSpawnProcess.RunWorkerAsync( table.Name );
                        //bgWorkers.Add( bwSpawnProcess );
                    }
                    else
                    {
                        if ( table.Name == "Individual_Household" )
                        {
                            MapPerson( scanner.ScanTable( table.Name ).AsQueryable() );
                        }
                        else if ( table.Name == "Batch" )
                        {
                            MapBatch( scanner.ScanTable( table.Name ).AsQueryable() );
                        }
                        else if ( table.Name == "Company" )
                        {
                            MapCompany( scanner.ScanTable( table.Name ).AsQueryable() );
                        }
                    }
                }

                ReportProgress( 100, Environment.NewLine + "Import completed.  " );
            }
            else
            {
                ReportProgress( 0, "No imported people exist. Please include the Individual_Household table during the import." );
            }

            return 0; // return total number of rows imported?
        }

        /// <summary>
        /// Loads Rock data that's used globally by the transform
        /// </summary>
        private void LoadExistingRockData()
        {
            var attributeValueService = new AttributeValueService();
            var attributeService = new AttributeService();

            IntegerFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;
            TextFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;
            PersonEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            BatchEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialBatch" ).Id;
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();

            var householdAttribute = personAttributes.FirstOrDefault( a => a.Key == "F1HouseholdId" );
            if ( householdAttribute == null )
            {
                householdAttribute = new Rock.Model.Attribute();
                householdAttribute.Key = "F1HouseholdId";
                householdAttribute.Name = "F1 Household Id";
                householdAttribute.FieldTypeId = IntegerFieldTypeId;
                householdAttribute.EntityTypeId = PersonEntityTypeId;
                householdAttribute.EntityTypeQualifierValue = string.Empty;
                householdAttribute.EntityTypeQualifierColumn = string.Empty;
                householdAttribute.Description = "The FellowshipOne household identifier for the person that was imported";
                householdAttribute.DefaultValue = string.Empty;
                householdAttribute.IsMultiValue = false;
                householdAttribute.IsRequired = false;
                householdAttribute.Order = 0;

                attributeService.Add( householdAttribute, ImportPersonAlias );
                attributeService.Save( householdAttribute, ImportPersonAlias );
                personAttributes.Add( householdAttribute );
            }

            var individualAttribute = personAttributes.FirstOrDefault( a => a.Key == "F1IndividualId" );
            if ( individualAttribute == null )
            {
                individualAttribute = new Rock.Model.Attribute();
                individualAttribute.Key = "F1IndividualId";
                individualAttribute.Name = "F1 Individual Id";
                individualAttribute.FieldTypeId = IntegerFieldTypeId;
                individualAttribute.EntityTypeId = PersonEntityTypeId;
                individualAttribute.EntityTypeQualifierValue = string.Empty;
                individualAttribute.EntityTypeQualifierColumn = string.Empty;
                individualAttribute.Description = "The FellowshipOne individual identifier for the person that was imported";
                individualAttribute.DefaultValue = string.Empty;
                individualAttribute.IsMultiValue = false;
                individualAttribute.IsRequired = false;
                individualAttribute.Order = 0;

                attributeService.Add( individualAttribute, ImportPersonAlias );
                attributeService.Save( individualAttribute, ImportPersonAlias );
                personAttributes.Add( individualAttribute );
            }

            IndividualAttributeId = individualAttribute.Id;
            HouseholdAttributeId = householdAttribute.Id;

            ReportProgress( 0, Environment.NewLine + "Checking for existing people..." );
            var listHouseholdId = attributeValueService.GetByAttributeId( householdAttribute.Id ).Select( av => new { PersonId = av.EntityId, HouseholdId = av.Value } ).ToList();
            var listIndividualId = attributeValueService.GetByAttributeId( individualAttribute.Id ).Select( av => new { PersonId = av.EntityId, IndividualId = av.Value } ).ToList();

            ImportedPeople = listHouseholdId.GroupJoin( listIndividualId, household => household.PersonId,
                individual => individual.PersonId, ( household, individual ) => new ImportedPerson
                {
                    PersonId = household.PersonId,
                    HouseholdId = household.HouseholdId.AsType<int?>(),
                    IndividualId = individual.Select( i => i.IndividualId.AsType<int?>() ).FirstOrDefault()
                } ).ToList();

            var batchAttribute = attributeService.Queryable().FirstOrDefault( a => a.EntityTypeId == BatchEntityTypeId
                && a.Key == "F1BatchId" );
            if ( batchAttribute == null )
            {
                batchAttribute = new Rock.Model.Attribute();
                batchAttribute.Key = "F1BatchId";
                batchAttribute.Name = "F1 Batch Id";
                batchAttribute.FieldTypeId = IntegerFieldTypeId;
                batchAttribute.EntityTypeId = BatchEntityTypeId;
                batchAttribute.EntityTypeQualifierValue = string.Empty;
                batchAttribute.EntityTypeQualifierColumn = string.Empty;
                batchAttribute.Description = "The FellowshipOne identifier for the batch that was imported";
                batchAttribute.DefaultValue = string.Empty;
                batchAttribute.IsMultiValue = false;
                batchAttribute.IsRequired = false;
                batchAttribute.Order = 0;

                attributeService.Add( batchAttribute, ImportPersonAlias );
                attributeService.Save( batchAttribute, ImportPersonAlias );
            }

            BatchAttributeId = batchAttribute.Id;

            ReportProgress( 0, Environment.NewLine + "Checking for existing contributions..." );
            ImportedBatches = new AttributeValueService().GetByAttributeId( batchAttribute.Id )
                .Select( av => new { F1BatchId = av.Value.AsType<int?>(), RockBatchId = av.EntityId } )
                .ToDictionary( t => t.F1BatchId, t => t.RockBatchId );

            CampusList = new CampusService().Queryable().ToList();
        }

        /// <summary>
        /// Checks if this person or business has been imported and returns the Rock.Person Id
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <param name="householdId">The household identifier.</param>
        /// <returns></returns>
        private int? GetPersonId( int? individualId = null, int? householdId = null )
        {
            var existingPerson = ImportedPeople.FirstOrDefault( p => p.IndividualId == individualId && p.HouseholdId == householdId );
            if ( existingPerson != null )
            {
                return existingPerson.PersonId;
            }
            else
            {
                var lookup = new AttributeValueService().Queryable();
                string lookupId = individualId.HasValue ? individualId.ToString() : householdId.ToString();
                if ( individualId != null )
                {
                    lookup = lookup.Where( av => av.AttributeId == IndividualAttributeId && av.Value == lookupId );
                }
                else
                {
                    lookup = lookup.Where( av => av.AttributeId == HouseholdAttributeId && av.Value == lookupId );
                }

                var lookupAttribute = lookup.FirstOrDefault();
                if ( lookupAttribute != null )
                {
                    ImportedPeople.Add( new ImportedPerson() { PersonId = lookupAttribute.EntityId, HouseholdId = householdId, IndividualId = individualId } );
                    return lookupAttribute.EntityId;
                }
            }

            return null;
        }

        #endregion

        #region Async Tasks

        /// <summary>
        /// Runs the background worker method that matches the selected table name
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        //private void bwSpawnProcess_DoWork( object sender, DoWorkEventArgs e )
        //{
        //    var scanner = new DataScanner( database );
        //    var nodeName = (string)e.Argument;
        //    if ( nodeName != null )
        //    {
        //        switch statement
        //    }
        //}

        /// <summary>
        /// Runs when the background process for each method completes
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        //private void bwSpawnProcess_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        //{
        //    if ( e.Cancelled != true )
        //    {
        //        BackgroundWorker bwSpawnProcess = sender as BackgroundWorker;
        //        bwSpawnProcess.RunWorkerCompleted -= new RunWorkerCompletedEventHandler( bwSpawnProcess_RunWorkerCompleted );
        //        bwSpawnProcess.ProgressChanged -= new ProgressChangedEventHandler( bwSpawnProcess_ProgressChanged );
        //        bwSpawnProcess.DoWork -= new DoWorkEventHandler( bwSpawnProcess_DoWork );
        //        bwSpawnProcess.Dispose();
        //        bgWorkers.Remove( (BackgroundWorker)sender )
        //    }
        //}

        #endregion
    }

    /// <summary>
    /// Helper class to store references to people that've been imported
    /// </summary>
    public class ImportedPerson
    {
        /// <summary>
        /// Stores the Rock.Person Id
        /// </summary>
        public int? PersonId;

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
