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
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// This extends the base Excavator class to account for FellowshipOne's database model
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    class F1 : ExcavatorComponent
    {
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
        /// The key name to use for the FellowshipOne household identifier
        /// </summary>
        private string householdIDKey = "Household_ID";

        /// <summary>
        /// The key name to use for the FellowshipOne individual identifier
        /// </summary>
        private string individualIDKey = "Individual_ID";

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        private PersonAlias ImportPersonAlias;

        /// <summary>
        /// Any attributes associated with Rock Person(s)
        /// </summary>
        private List<Rock.Model.Attribute> PersonAttributeList;

        /// <summary>
        /// Holds a list of all the people who've been imported
        /// </summary>
        private List<ImportedPerson> ImportedPersonList;

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override bool TransformData()
        {
            // Verify we have everything we need
            VerifyRockAttributes();

            int workerCount = 0;
            foreach ( var node in loadedNodes.Where( n => n.Checked != false ) )
            {
                BackgroundWorker bwSpawnWorker = new BackgroundWorker();
                bwSpawnWorker.DoWork += bwSpawnWorker_DoWork;
                bwSpawnWorker.ProgressChanged += bwSpawnWorker_ProgressChanged;
                bwSpawnWorker.RunWorkerCompleted += bwSpawnWorker_RunWorkerCompleted;
                bwSpawnWorker.RunWorkerAsync( node.Name );
                workerCount++;
            }

            return workerCount > 0 ? true : false;
        }

        /// <summary>
        /// Verifies all Rock attributes exist that are used globally by the transform.
        /// </summary>
        public void VerifyRockAttributes()
        {
            var attributeService = new AttributeService();
            var attributeValueService = new AttributeValueService();

            // change this to user-defined person
            var aliasService = new PersonAliasService();
            ImportPersonAlias = aliasService.Get( 1 );

            int personEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;
            int numberFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;

            PersonAttributeList = attributeService.Queryable().Where( a => a.EntityTypeId == personEntityTypeId ).ToList();

            var householdAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == householdIDKey );
            if ( householdAttribute == null )
            {
                householdAttribute = new Rock.Model.Attribute();
                householdAttribute.Key = householdIDKey;
                householdAttribute.Name = "F1 Household ID";
                householdAttribute.FieldTypeId = numberFieldTypeId;
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

            var individualAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == individualIDKey );
            if ( individualAttribute == null )
            {
                individualAttribute = new Rock.Model.Attribute();
                individualAttribute.Key = individualIDKey;
                individualAttribute.Name = "F1 Individual ID";
                individualAttribute.FieldTypeId = numberFieldTypeId;
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

            // Get all current people with household & individual ID's
            var listHouseholdID = attributeValueService.GetByAttributeId( householdAttribute.Id ).Select( av => new { av.EntityId, av.Value } ).ToList();
            var listIndividualID = attributeValueService.GetByAttributeId( individualAttribute.Id ).Select( av => new { av.EntityId, av.Value } ).ToList();

            ImportedPersonList = listHouseholdID.Join( listIndividualID, household => household.EntityId
                , individual => individual.EntityId
                , ( household, individual ) => new ImportedPerson
                {
                    PersonID = household.EntityId,
                    HouseholdID = household.Value.AsType<int?>(),
                    IndividualID = individual.Value.AsType<int?>()
                } ).ToList();
        }

        /// <summary>
        /// Checks if this person has been imported and returns the Rock.Person ID
        /// </summary>
        /// <param name="individualID">The individual identifier.</param>
        /// <param name="householdID">The household identifier.</param>
        /// <returns></returns>
        private int? GetPersonId( int? individualID = null, int? householdID = null )
        {
            var existingPerson = ImportedPersonList.FirstOrDefault( p => p.IndividualID == individualID && p.HouseholdID == householdID );
            if ( existingPerson != null )
            {
                return existingPerson.PersonID;
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

                switch ( nodeName )
                {
                    case "Individual_Household":
                        MapPerson( tableData );
                        break;

                    case "Contribution":
                        MapContribution( tableData );
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

        #region Mapped Data

        /// <summary>
        /// Maps the contribution.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapContribution( IQueryable<Row> tableData, List<string> selectedColumns = null )
        {
            // Individual_ID
            // Household_ID
            // Fund_Name
            // Sub_Fund_Name
            // Received_Date
            // Amount
            // Check_Number
            // Pledge_Drive_Name
            // Memo
            // Contribution_Type_Name
            // Stated_Value
            // True_Value
            // Liquidation_cost
            // ContributionID
            // BatchID
        }

        /// <summary>
        /// Maps the person.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapPerson( IQueryable<Row> tableData, List<string> selectedColumns = null )
        {
            var groupTypeRoleService = new GroupTypeRoleService();
            var dvService = new DefinedValueService();
            var campusService = new CampusService();
            var householdCampusList = new List<string>();

            // Marital statuses: Married, Single, Separated, etc
            List<DefinedValue> maritalStatusTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS ) ).ToList();

            // Connection statuses: Member, Visitor, Attendee, etc
            List<DefinedValue> connectionStatusTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS ) ).ToList();

            // Record status reasons: No Activity, Moved, Deceased, etc
            List<DefinedValue> recordStatusReasons = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS_REASON ) ).ToList();

            // Record statuses: Active, Inactive, Pending
            int? statusActiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ) ).Id;
            int? statusInactiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ) ).Id;
            int? statusPendingId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ) ).Id;

            // Record type: Person
            int? personRecordTypeId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON ) ).Id;

            // Suffix type: Dr., Jr., II, etc
            List<DefinedValue> suffixTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_SUFFIX ) ).ToList();

            // Title type: Mr., Mrs. Dr., etc
            List<DefinedValue> titleTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_TITLE ) ).ToList();

            // Note type: Comment
            int noteCommentTypeId = new NoteTypeService().Get( new Guid( "7E53487C-D650-4D85-97E2-350EB8332763" ) ).Id;

            // Group roles: Adult, Child
            int adultRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;
            int childRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD ) ).Id;

            // Campuses: user-defined, should match F1 Campus designation
            List<Campus> campusList = campusService.Queryable().ToList();

            foreach ( var groupedRows in tableData.GroupBy<Row, int?>( r => r[householdIDKey] as int? ) )
            {
                // only import where selectedColumns.Contains( row.Column )

                var familyMembers = new List<GroupMember>();
                householdCampusList.Clear();

                foreach ( var row in groupedRows )
                {
                    int? individual_id = row["Individual_ID"] as int?;
                    int? household_id = row["Household_ID"] as int?;

                    // Check if person already imported
                    if ( GetPersonId( individual_id, household_id ) == null )
                    {
                        var person = new Person();
                        person.FirstName = row["First_Name"] as string;
                        person.MiddleName = row["Middle_Name"] as string;
                        person.NickName = row["Goes_By"] as string ?? person.FirstName;
                        person.LastName = row["Last_Name"] as string;
                        person.BirthDate = row["Date_Of_Birth"] as DateTime?;
                        person.RecordTypeValueId = personRecordTypeId;
                        int groupRoleId = adultRoleId;

                        var gender = row["Gender"] as string;
                        if ( gender != null )
                        {
                            person.Gender = (Gender)Enum.Parse( typeof( Gender ), gender );
                        }

                        string prefix = row["Prefix"] as string;
                        if ( prefix != null )
                        {
                            person.TitleValueId = titleTypes.Where( s => s.Name == prefix )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string suffix = row["Suffix"] as string;
                        if ( suffix != null )
                        {
                            person.SuffixValueId = suffixTypes.Where( s => s.Name == suffix )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string member_status = row["Status_Name"] as string;
                        if ( member_status == "Member" )
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER ) ).Id;
                            person.RecordStatusValueId = statusActiveId;
                        }
                        else if ( member_status == "Visitor" )
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR ) ).Id;
                            person.RecordStatusValueId = statusActiveId;
                        }
                        else if ( member_status == "Deceased" )
                        {
                            person.IsDeceased = true;
                            person.RecordStatusValueId = statusInactiveId;
                            person.RecordStatusReasonValueId = recordStatusReasons.Where( dv => dv.Name == "Deceased" )
                                .Select( dv => dv.Id ).FirstOrDefault();
                        }
                        else
                        {
                            // F1 defaults are Member & Visitor; all others are user-defined
                            var customConnectionType = connectionStatusTypes.Where( dv => dv.Name == member_status )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();

                            int attendeeId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( "39F491C5-D6AC-4A9B-8AC0-C431CB17D588" ) ).Id;
                            person.ConnectionStatusValueId = customConnectionType ?? attendeeId;
                            person.RecordStatusValueId = statusActiveId;
                        }

                        DateTime? join_date = row["Status_Date"] as DateTime?;
                        if ( join_date != null )
                        {
                            person.CreatedDateTime = (DateTime)join_date;
                        }

                        string marital_status = row["Marital_Status"] as string;
                        if ( marital_status != null )
                        {
                            person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Name == marital_status )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                        }
                        else
                        {
                            person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Name == "Unknown" )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                        }

                        string familyRole = row["Household_Position"] as string;
                        if ( familyRole != null )
                        {
                            if ( familyRole == "Child" || person.Age < 18 )
                            {
                                groupRoleId = childRoleId;
                            }
                            else if ( familyRole == "Visitor" )
                            {
                                // assign person as a known relationship of this family/group
                            }
                        }

                        string campus = row["SubStatus_Name"] as string;
                        if ( campus != null )
                        {
                            householdCampusList.Add( campus );
                        }

                        string status_comment = row["Status_Comment"] as string;
                        if ( status_comment != null )
                        {
                            var comment = new Note();
                            comment.Text = status_comment;
                            comment.NoteTypeId = noteCommentTypeId;
                            RockTransactionScope.WrapTransaction( () =>
                            {
                                var noteService = new NoteService();
                                noteService.Save( comment );
                            } );
                        }

                        person.Attributes = new Dictionary<string, AttributeCache>();
                        person.AttributeValues = new Dictionary<string, List<AttributeValue>>();

                        // individual_id already defined in scope
                        if ( individual_id != null )
                        {
                            var individualAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == individualIDKey );
                            person.Attributes.Add( individualIDKey, AttributeCache.Read( individualAttribute ) );
                            person.AttributeValues.Add( individualIDKey, new List<AttributeValue>() );
                            person.AttributeValues[individualIDKey].Add( new AttributeValue()
                            {
                                AttributeId = individualAttribute.Id,
                                Value = individual_id.ToString()
                            } );
                        }

                        // household_id already defined in scope
                        if ( household_id != null )
                        {
                            var householdAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == householdIDKey );
                            person.Attributes.Add( householdIDKey, AttributeCache.Read( householdAttribute ) );
                            person.AttributeValues.Add( householdIDKey, new List<AttributeValue>() );
                            person.AttributeValues[householdIDKey].Add( new AttributeValue()
                            {
                                AttributeId = householdAttribute.Id,
                                Value = household_id.ToString()
                            } );
                        }

                        string former_church = row["Former_Church"] as string;
                        if ( former_church != null )
                        {
                            var previousChurchAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "PreviousChurch" );
                            person.Attributes.Add( "PreviousChurch", AttributeCache.Read( previousChurchAttribute ) );
                            person.AttributeValues.Add( "PreviousChurch", new List<AttributeValue>() );
                            person.AttributeValues["PreviousChurch"].Add( new AttributeValue()
                            {
                                AttributeId = previousChurchAttribute.Id,
                                Value = former_church
                            } );
                        }

                        string employer = row["Employer"] as string;
                        if ( employer != null )
                        {
                            var employerAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "Employer" );
                            person.Attributes.Add( "Employer", AttributeCache.Read( employerAttribute ) );
                            person.AttributeValues.Add( "Employer", new List<AttributeValue>() );
                            person.AttributeValues["Employer"].Add( new AttributeValue()
                            {
                                AttributeId = employerAttribute.Id,
                                Value = employer
                            } );
                        }

                        string position = row["Occupation_Name"] as string ?? row["Occupation_Description"] as string;
                        if ( position != null )
                        {
                            var positionAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "Position" );
                            person.Attributes.Add( "Position", AttributeCache.Read( positionAttribute ) );
                            person.AttributeValues.Add( "Position", new List<AttributeValue>() );
                            person.AttributeValues["Position"].Add( new AttributeValue()
                            {
                                AttributeId = positionAttribute.Id,
                                Value = former_church
                            } );
                        }

                        string school = row["School_Name"] as string;
                        if ( position != null )
                        {
                            var schoolAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "School" );
                            person.Attributes.Add( "School", AttributeCache.Read( schoolAttribute ) );
                            person.AttributeValues.Add( "School", new List<AttributeValue>() );
                            person.AttributeValues["School"].Add( new AttributeValue()
                            {
                                AttributeId = schoolAttribute.Id,
                                Value = former_church
                            } );
                        }

                        DateTime? first_visit = row["First_Record"] as DateTime?;
                        if ( first_visit != null )
                        {
                            var firstVisitAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "FirstVisit" );
                            person.Attributes.Add( "FirstVisit", AttributeCache.Read( firstVisitAttribute ) );
                            person.AttributeValues.Add( "FirstVisit", new List<AttributeValue>() );
                            person.AttributeValues["FirstVisit"].Add( new AttributeValue()
                            {
                                AttributeId = firstVisitAttribute.Id,
                                Value = first_visit.Value.ToString( "MM/dd/yyyy" )
                            } );
                        }

                        // Other properties (Attributes to create):
                        // former name
                        // bar_code
                        // member_env_code
                        // denomination_name

                        var groupMember = new GroupMember();
                        groupMember.Person = person;
                        groupMember.GroupRoleId = groupRoleId;
                        groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                        familyMembers.Add( groupMember );
                    }
                }

                string primaryHouseholdCampus = householdCampusList.GroupBy( c => c ).OrderByDescending( c => c.Count() ).Select( c => c.Key ).First();
                int? rockCampusId = campusList.Where( c => c.Name == primaryHouseholdCampus || c.ShortCode == primaryHouseholdCampus )
                    .Select( c => (int?)c.Id ).FirstOrDefault();

                RockTransactionScope.WrapTransaction( () =>
                {
                    var groupService = new GroupService();
                    groupService.SaveNewFamily( familyMembers, rockCampusId, ImportPersonAlias );
                } );
            }
        }

        #endregion
    }

    /// <summary>
    /// Helper class to store ID references to people that've been imported
    /// </summary>
    public class ImportedPerson
    {
        /// <summary>
        /// Stores the Rock.Person ID
        /// </summary>
        public int? PersonID;

        /// <summary>
        /// Stores the F1 Individual ID
        /// </summary>
        public int? IndividualID;

        /// <summary>
        /// Stores the F1 Household ID
        /// </summary>
        public int? HouseholdID;
    }
}
