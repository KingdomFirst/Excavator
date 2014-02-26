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
using OrcaMDF.Core.MetaData;
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
        /// The list of current campuses
        /// </summary>
        private List<Campus> CampusList;

        // Custom Attributes added for unique F1 Id's
        private int BatchAttributeId;

        private int ContributionAttributeId;

        private int IndividualAttributeId;

        private int HouseholdAttributeId;

        #endregion

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override bool TransformData()
        {
            VerifyRockAttributes();
            var orderedNodes = SetImportOrder( loadedNodes );

            var scanner = new DataScanner( database );
            int workerCount = 0;

            foreach ( var node in orderedNodes )
            {
                switch ( node.Name )
                {
                    case "Batch":
                        MapBatch( scanner.ScanTable( node.Name ).AsQueryable() );
                        break;

                    case "Contribution":
                        MapContribution( scanner.ScanTable( node.Name ).AsQueryable() );
                        break;

                    case "Individual_Household":
                        //MapPerson( scanner.ScanTable( node.Name ).AsQueryable() );
                        break;

                    case "Pledge":
                        MapPledge( scanner.ScanTable( node.Name ).AsQueryable() );
                        break;

                    default:
                        break;
                }

                //BackgroundWorker bwSpawnWorker = new BackgroundWorker();
                //bwSpawnWorker.DoWork += bwSpawnWorker_DoWork;
                //bwSpawnWorker.ProgressChanged += bwSpawnWorker_ProgressChanged;
                //bwSpawnWorker.RunWorkerCompleted += bwSpawnWorker_RunWorkerCompleted;
                //bwSpawnWorker.RunWorkerAsync( node.Name );
                //workerCount++;
            }

            return workerCount > 0 ? true : false;
        }

        /// <summary>
        /// Verifies all Rock attributes exist that are used globally by the transform.
        /// </summary>
        public void VerifyRockAttributes()
        {
            var attributeValueService = new AttributeValueService();
            var attributeService = new AttributeService();

            // change this to user-defined person
            var aliasService = new PersonAliasService();
            ImportPersonAlias = aliasService.Get( 1 );
            // end change

            int personEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            int batchEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialBatch" ).Id;
            int transactionEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialTransaction" ).Id;
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;
            int integerFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;

            CampusList = new CampusService().Queryable().ToList();

            PersonAttributeList = attributeService.Queryable().Where( a => a.EntityTypeId == personEntityTypeId ).ToList();

            var householdAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "F1HouseholdId" );
            if ( householdAttribute == null )
            {
                householdAttribute = new Rock.Model.Attribute();
                householdAttribute.Key = "F1HouseholdId";
                householdAttribute.Name = "F1 Household Id";
                householdAttribute.FieldTypeId = integerFieldTypeId;
                householdAttribute.EntityTypeId = personEntityTypeId;
                householdAttribute.EntityTypeQualifierValue = string.Empty;
                householdAttribute.EntityTypeQualifierColumn = string.Empty;
                householdAttribute.Description = "The FellowshipOne household identifier for the person that was imported";
                householdAttribute.DefaultValue = string.Empty;
                householdAttribute.IsMultiValue = false;
                householdAttribute.IsRequired = false;
                householdAttribute.Order = 0;

                attributeService.Add( householdAttribute, ImportPersonAlias );
                attributeService.Save( householdAttribute, ImportPersonAlias );
                PersonAttributeList.Add( householdAttribute );
            }

            var individualAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "F1IndividualId" );
            if ( individualAttribute == null )
            {
                individualAttribute = new Rock.Model.Attribute();
                individualAttribute.Key = "F1IndividualId";
                individualAttribute.Name = "F1 Individual Id";
                individualAttribute.FieldTypeId = integerFieldTypeId;
                individualAttribute.EntityTypeId = personEntityTypeId;
                individualAttribute.EntityTypeQualifierValue = string.Empty;
                individualAttribute.EntityTypeQualifierColumn = string.Empty;
                individualAttribute.Description = "The FellowshipOne individual identifier for the person that was imported";
                individualAttribute.DefaultValue = string.Empty;
                individualAttribute.IsMultiValue = false;
                individualAttribute.IsRequired = false;
                individualAttribute.Order = 0;

                attributeService.Add( individualAttribute, ImportPersonAlias );
                attributeService.Save( individualAttribute, ImportPersonAlias );
                PersonAttributeList.Add( individualAttribute );
            }

            HouseholdAttributeId = householdAttribute.Id;
            IndividualAttributeId = individualAttribute.Id;

            // Get all imported people with their F1 Id's
            var listHouseholdId = attributeValueService.GetByAttributeId( householdAttribute.Id ).Select( av => new { PersonId = av.EntityId, HouseholdId = av.Value } ).ToList();
            var listIndividualId = attributeValueService.GetByAttributeId( individualAttribute.Id ).Select( av => new { PersonId = av.EntityId, IndividualId = av.Value } ).ToList();

            ImportedPersonList = listHouseholdId.Join( listIndividualId, household => household.PersonId
                , individual => individual.PersonId
                , ( household, individual ) => new ImportedPerson
                {
                    PersonId = household.PersonId,
                    HouseholdId = household.HouseholdId.AsType<int?>(),
                    IndividualId = individual.IndividualId.AsType<int?>()
                } ).ToList();

            // Get all imported batches
            var batchAttribute = attributeService.Queryable().Where( a => a.EntityTypeId == batchEntityTypeId ).FirstOrDefault( a => a.Key == "F1BatchId" );
            if ( batchAttribute == null )
            {
                batchAttribute = new Rock.Model.Attribute();
                batchAttribute.Key = "F1BatchId";
                batchAttribute.Name = "F1 Batch Id";
                batchAttribute.FieldTypeId = integerFieldTypeId;
                batchAttribute.EntityTypeId = batchEntityTypeId;
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
            ImportedBatches = attributeValueService.GetByAttributeId( batchAttribute.Id )
                .Select( av => new { F1BatchId = av.Value.AsType<int?>(), RockBatchId = av.EntityId } )
                .ToDictionary( t => t.F1BatchId, t => t.RockBatchId );

            // Get all imported contributions
            var contributionAttribute = attributeService.Queryable().Where( a => a.EntityTypeId == transactionEntityTypeId ).FirstOrDefault( a => a.Key == "F1ContributionId" );
            if ( contributionAttribute == null )
            {
                contributionAttribute = new Rock.Model.Attribute();
                contributionAttribute.Key = "F1ContributionId";
                contributionAttribute.Name = "F1 Contribution Id";
                contributionAttribute.FieldTypeId = integerFieldTypeId;
                contributionAttribute.EntityTypeId = transactionEntityTypeId;
                contributionAttribute.EntityTypeQualifierValue = string.Empty;
                contributionAttribute.EntityTypeQualifierColumn = string.Empty;
                contributionAttribute.Description = "The FellowshipOne identifier for the contribution that was imported";
                contributionAttribute.DefaultValue = string.Empty;
                contributionAttribute.IsMultiValue = false;
                contributionAttribute.IsRequired = false;
                contributionAttribute.Order = 0;

                attributeService.Add( contributionAttribute, ImportPersonAlias );
                attributeService.Save( contributionAttribute, ImportPersonAlias );
            }

            ContributionAttributeId = contributionAttribute.Id;
            ImportedContributions = attributeValueService.GetByAttributeId( contributionAttribute.Id )
               .Select( av => new { ContributionId = av.Value.AsType<int?>(), TransactionId = av.EntityId } )
               .ToDictionary( t => t.ContributionId, t => t.TransactionId );
        }

        /// <summary>
        /// Orders the nodes so the primary tables get imported first
        /// </summary>
        /// <param name="nodeList">The node list.</param>
        private List<DatabaseNode> SetImportOrder( List<DatabaseNode> nodeList )
        {
            nodeList = nodeList.Where( n => n.Checked != false ).ToList();
            if ( nodeList.Any() )
            {
                var household = nodeList.Where( node => node.Name.Equals( "Individual_Household" ) ).FirstOrDefault();
                var batch = nodeList.Where( node => node.Name.Equals( "Batch" ) ).FirstOrDefault();
                var rlc = nodeList.Where( node => node.Name.Equals( "RLC" ) ).FirstOrDefault();
                var contributions = nodeList.Where( node => node.Name.Equals( "Contribution " ) ).FirstOrDefault();

                nodeList.Remove( household );
                nodeList.Remove( batch );
                nodeList.Remove( rlc );
                nodeList.Remove( contributions );
                var primaryTables = new List<DatabaseNode>() { household, batch, rlc, contributions };
                primaryTables.RemoveAll( n => n == null );
                nodeList.InsertRange( 0, primaryTables );
            }

            return nodeList;
        }

        /// <summary>
        /// Checks if this person has been imported and returns the Rock.Person Id
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <param name="householdId">The household identifier.</param>
        /// <returns></returns>
        private int? GetPersonId( int? individualId = null, int? householdId = null )
        {
            var existingPerson = ImportedPersonList.FirstOrDefault( p => p.IndividualId == individualId && p.HouseholdId == householdId );
            if ( existingPerson != null )
            {
                return existingPerson.PersonId;
            }
            else
            {
                string f1IndividualId = individualId.ToString();
                var lookupAttribute = new AttributeValueService().Queryable().FirstOrDefault( av => av.AttributeId == IndividualAttributeId && av.Value == f1IndividualId );
                if ( lookupAttribute != null )
                {
                    ImportedPersonList.Add( new ImportedPerson() { PersonId = lookupAttribute.EntityId, HouseholdId = householdId, IndividualId = individualId } );
                    return lookupAttribute.Id;
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
        private void bwSpawnWorker_DoWork( object sender, DoWorkEventArgs e )
        {
            var nodeName = (string)e.Argument;
            if ( nodeName != null )
            {
                var scanner = new DataScanner( database );
                IQueryable<Row> tableData = scanner.ScanTable( nodeName ).AsQueryable();
            }
        }

        /// <summary>
        /// Runs when the background process for each method completes
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwSpawnWorker_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            //return completed to original thread;
        }

        /// <summary>
        /// Reports the progress for each background worker that was started
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwSpawnWorker_ProgressChanged( object sender, ProgressChangedEventArgs e )
        {
            //throw new NotImplementedException();
        }

        #endregion
    }
}
