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
        /// Holds a list of all the people who've been imported
        /// </summary>
        private List<ImportedPerson> ImportedPersonList;

        /// <summary>
        /// The list of current campuses
        /// </summary>
        private List<Campus> CampusList;

        private int IntegerFieldTypeId;
        private int PersonEntityTypeId;
        private int IndividualAttributeId;
        private int HouseholdAttributeId;

        #endregion

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override bool TransformData( string importUser = null )
        {
            var personService = new PersonService();
            var importPerson = personService.GetByFullName( importUser, includeDeceased: false, allowFirstNameOnly: true ).FirstOrDefault();
            ImportPersonAlias = new PersonAliasService().Get( importPerson.Id );

            LoadExistingRockData();

            var scanner = new DataScanner( database );
            var primaryTables = new List<string>();
            primaryTables.Add( "Contribution" );
            primaryTables.Add( "Batch" );
            primaryTables.Add( "Company" );
            primaryTables.Add( "Individual_Household" );

            // Orders the nodes so the primary tables get imported first
            var orderedNodes = loadedNodes.Where( n => n.Checked != false ).ToList();
            if ( orderedNodes.Any( n => primaryTables.Contains( n.Name ) ) )
            {
                orderedNodes = orderedNodes.OrderByDescending( n => primaryTables.IndexOf( n.Name ) ).ToList();
            }

            int workerCount = 0;

            foreach ( var node in orderedNodes )
            {
                if ( !primaryTables.Contains( node.Name ) )
                {
                    BackgroundWorker bwSpawnProcess = new BackgroundWorker();
                    bwSpawnProcess.DoWork += bwSpawnProcess_DoWork;
                    bwSpawnProcess.ProgressChanged += bwSpawnProcess_ProgressChanged;
                    bwSpawnProcess.RunWorkerCompleted += bwSpawnProcess_RunWorkerCompleted;
                    bwSpawnProcess.RunWorkerAsync( node.Name );
                    workerCount++;
                }
                else
                {
                    if ( node.Name == "Individual_Household" )
                    {
                        MapPerson( scanner.ScanTable( node.Name ).AsQueryable() );
                    }
                    else if ( node.Name == "Batch" )
                    {
                        MapBatch( scanner.ScanTable( node.Name ).AsQueryable() );
                    }
                    else if ( node.Name == "Company" )
                    {
                        MapCompany( scanner.ScanTable( node.Name ).AsQueryable() );
                    }
                    else if ( node.Name == "Contribution" )
                    {
                        MapContribution( scanner.ScanTable( node.Name ).AsQueryable() );
                    }
                }
            }

            return workerCount > 0 ? true : false;
        }

        /// <summary>
        /// Loads Rock data that's used globally by the transform
        /// </summary>
        public void LoadExistingRockData()
        {
            var attributeValueService = new AttributeValueService();
            var attributeService = new AttributeService();

            IntegerFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;
            PersonEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
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

            // Get all imported people with their F1 Id's
            var listHouseholdId = attributeValueService.GetByAttributeId( householdAttribute.Id ).Select( av => new { PersonId = av.EntityId, HouseholdId = av.Value } ).ToList();
            var listIndividualId = attributeValueService.GetByAttributeId( individualAttribute.Id ).Select( av => new { PersonId = av.EntityId, IndividualId = av.Value } ).ToList();

            ImportedPersonList = listHouseholdId.GroupJoin( listIndividualId, household => household.PersonId,
                individual => individual.PersonId, ( household, individual ) => new ImportedPerson
                {
                    PersonId = household.PersonId,
                    HouseholdId = household.HouseholdId.AsType<int?>(),
                    IndividualId = individual.Select( i => i.IndividualId.AsType<int?>() ).FirstOrDefault()
                } ).ToList();

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
            var existingPerson = ImportedPersonList.FirstOrDefault( p => p.IndividualId == individualId && p.HouseholdId == householdId );
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
                    ImportedPersonList.Add( new ImportedPerson() { PersonId = lookupAttribute.EntityId, HouseholdId = householdId, IndividualId = individualId } );
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
        private void bwSpawnProcess_DoWork( object sender, DoWorkEventArgs e )
        {
            var scanner = new DataScanner( database );
            var nodeName = (string)e.Argument;
            if ( nodeName != null )
            {
                switch ( nodeName )
                {
                    case "Account":
                        MapAccount( scanner.ScanTable( nodeName ).AsQueryable() );
                        break;

                    case "Attendance":
                        //Not run because attendance/locations/groups data is so custom
                        //MapAttendance( scanner.ScanTable( nodeName ).AsQueryable() );
                        break;

                    case "ActivityMinistry":
                        //Not run because attendance/locations/groups data is so custom
                        //MapActivityMinistry( scanner.ScanTable( nodeName ).AsQueryable() );
                        break;

                    case "Household_Address":
                        MapFamilyAddress( scanner.ScanTable( nodeName ).AsQueryable() );
                        break;

                    case "Pledge":
                        MapPledge( scanner.ScanTable( nodeName ).AsQueryable() );
                        break;

                    case "RLC":
                        //Not run because attendance/locations/groups data is so custom
                        //MapRLC( scanner.ScanTable( nodeName ).AsQueryable() );
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Runs when the background process for each method completes
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwSpawnProcess_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            if ( e.Cancelled != true )
            {
                BackgroundWorker bwSpawnProcess = sender as BackgroundWorker;
                bwSpawnProcess.RunWorkerCompleted -= new RunWorkerCompletedEventHandler( bwSpawnProcess_RunWorkerCompleted );
                bwSpawnProcess.ProgressChanged -= new ProgressChangedEventHandler( bwSpawnProcess_ProgressChanged );
                bwSpawnProcess.DoWork -= new DoWorkEventHandler( bwSpawnProcess_DoWork );
                bwSpawnProcess.Dispose();
            }
        }

        /// <summary>
        /// Reports the progress for each background worker that was started
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwSpawnProcess_ProgressChanged( object sender, ProgressChangedEventArgs e )
        {
            //throw new NotImplementedException();
        }

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
