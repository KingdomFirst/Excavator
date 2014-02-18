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
        /// The Household ID Attribute Type
        /// </summary>
        private Rock.Model.Attribute HouseholdAttribute;

        /// <summary>
        /// The key name to use for the FellowshipOne individual identifier
        /// </summary>
        private string individualIDKey = "Individual_ID";

        /// <summary>
        /// The Individual ID Attribute Type
        /// </summary>
        private Rock.Model.Attribute IndividualAttribute;

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
        private List<ImportedPerson> ImportedPeople;

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override bool TransformData()
        {
            // Verify we have everything we need
            VerifyRockAttributes();

            var scanner = new DataScanner( database );
            int tableCount = 0;

            foreach ( var node in loadedNodes.Where( n => n.Checked != false ) )
            {
                IQueryable<Row> tableData = scanner.ScanTable( node.Name ).AsQueryable();
                List<string> selectedColumns = node.Columns.Where( c => c.Checked == true )
                    .Select( c => c.Name ).ToList();

                switch ( node.Name )
                {
                    case "Contribution":
                        MapContribution( tableData, selectedColumns );
                        break;

                    case "Individual_Household":
                        MapPerson( tableData, selectedColumns );
                        break;

                    default:
                        tableCount--;
                        break;
                }

                tableCount++;
            }

            return tableCount > 0 ? true : false;
        }

        /// <summary>
        /// Verifies all Rock attributes exist that are used globally by the transform.
        /// </summary>
        public void VerifyRockAttributes()
        {
            var attributeService = new AttributeService();

            // change this to user-defined person
            var aliasService = new PersonAliasService();
            ImportPersonAlias = aliasService.Get( 1 );

            int personEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;
            int numberFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;

            PersonAttributeList = attributeService.Queryable().Where( a => a.EntityTypeId == personEntityTypeId ).ToList();

            HouseholdAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == householdIDKey );
            if ( HouseholdAttribute == null )
            {
                HouseholdAttribute = new Rock.Model.Attribute();
                HouseholdAttribute.Key = householdIDKey;
                HouseholdAttribute.Name = "F1 Household ID";
                HouseholdAttribute.FieldTypeId = numberFieldTypeId;
                HouseholdAttribute.EntityTypeId = personEntityTypeId;
                HouseholdAttribute.Description = "The FellowshipOne household identifier for the person that was imported";
                HouseholdAttribute.DefaultValue = string.Empty;
                HouseholdAttribute.IsMultiValue = false;
                HouseholdAttribute.IsRequired = false;
                HouseholdAttribute.Order = 0;

                attributeService.Add( HouseholdAttribute, ImportPersonAlias );
                attributeService.Save( HouseholdAttribute, ImportPersonAlias );
                PersonAttributeList.Add( HouseholdAttribute );
            }

            IndividualAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == individualIDKey );
            if ( IndividualAttribute == null )
            {
                IndividualAttribute = new Rock.Model.Attribute();
                IndividualAttribute.Key = individualIDKey;
                IndividualAttribute.Name = "F1 Individual ID";
                IndividualAttribute.FieldTypeId = numberFieldTypeId;
                IndividualAttribute.EntityTypeId = personEntityTypeId;
                IndividualAttribute.Description = "The FellowshipOne individual identifier for the person that was imported";
                IndividualAttribute.DefaultValue = string.Empty;
                IndividualAttribute.IsMultiValue = false;
                IndividualAttribute.IsRequired = false;
                IndividualAttribute.Order = 0;

                attributeService.Add( IndividualAttribute, ImportPersonAlias );
                attributeService.Save( IndividualAttribute, ImportPersonAlias );
                PersonAttributeList.Add( IndividualAttribute );
            }
        }

        /// <summary>
        /// Checks if this person has been imported and returns the Rock.Person ID
        /// </summary>
        /// <param name="individualID">The individual identifier.</param>
        /// <param name="householdID">The household identifier.</param>
        /// <returns></returns>
        private int? GetPersonId( int? individualID = null, int? householdID = null )
        {
            var importedPerson = ImportedPeople.Where( p => p.HouseholdID == householdID && p.IndividualID == individualID ).FirstOrDefault();
            if ( importedPerson != null )
            {
                return importedPerson.PersonID;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Mapped Data

        /// <summary>
        /// Maps the contribution.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapContribution( IQueryable<Row> tableData, List<string> selectedColumns )
        {
            //get list of all person id with their individual & household ID's?
            //var attributeValueService = new AttributeValueService();

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
        private void MapPerson( IQueryable<Row> tableData, List<string> selectedColumns )
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
                        person.ConnectionStatusValueId = connectionStatusTypes.Where( dv => dv.Name == member_status )
                            .Select( dv => dv.Id ).FirstOrDefault();
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

                    // start adding person attributes
                    person.Attributes = new Dictionary<string, AttributeCache>();
                    person.AttributeValues = new Dictionary<string, List<AttributeValue>>();

                    int? household_id = row["Household_ID"] as int?;
                    if ( household_id != null )
                    {
                        person.Attributes.Add( householdIDKey, AttributeCache.Read( HouseholdAttribute ) );
                        var attributeValue = new AttributeValue() { Value = household_id.ToString() };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( householdIDKey, valueList );
                    }

                    int? individual_id = row["Individual_ID"] as int?;
                    if ( individual_id != null )
                    {
                        person.Attributes.Add( individualIDKey, AttributeCache.Read( IndividualAttribute ) );
                        var attributeValue = new AttributeValue() { Value = individual_id.ToString() };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( individualIDKey, valueList );
                    }

                    string former_church = row["Former_Church"] as string;
                    if ( former_church != null )
                    {
                        var previousChurchAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "PreviousChurch" );
                        person.Attributes.Add( "PreviousChurch", AttributeCache.Read( previousChurchAttribute ) );
                        var attributeValue = new AttributeValue() { Value = former_church };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "PreviousChurch", valueList );
                    }

                    string employer = row["Employer"] as string;
                    if ( employer != null )
                    {
                        var employerAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "Employer" );
                        person.Attributes.Add( "Employer", AttributeCache.Read( employerAttribute ) );
                        var attributeValue = new AttributeValue() { Value = employer };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "Employer", valueList );
                    }

                    string position = row["Occupation_Name"] as string ?? row["Occupation_Description"] as string;
                    if ( position != null )
                    {
                        var positionAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "Position" );
                        person.Attributes.Add( "Position", AttributeCache.Read( positionAttribute ) );
                        var attributeValue = new AttributeValue() { Value = former_church };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "Position", valueList );
                    }

                    string school = row["School_Name"] as string;
                    if ( position != null )
                    {
                        var schoolAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "School" );
                        person.Attributes.Add( "School", AttributeCache.Read( schoolAttribute ) );
                        var attributeValue = new AttributeValue() { Value = former_church };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "School", valueList );
                    }

                    DateTime? first_visit = row["First_Record"] as DateTime?;
                    if ( first_visit != null )
                    {
                        var firstVisitAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "FirstVisit" );
                        person.Attributes.Add( "FirstVisit", AttributeCache.Read( firstVisitAttribute ) );
                        var attributeValue = new AttributeValue() { Value = first_visit.Value.ToString( "MM/dd/yyyy" ) };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "FirstVisit", valueList );
                    }

                    // Other properties (Attributes to create):
                    // former name
                    // first_record date
                    // bar_code
                    // member_env_code
                    // denomination_name

                    var groupMember = new GroupMember();
                    groupMember.Person = person;
                    groupMember.GroupRoleId = groupRoleId;
                    groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                    familyMembers.Add( groupMember );
                }

                string primaryHouseholdCampus = householdCampusList.GroupBy( c => c ).OrderByDescending( c => c.Count() ).Select( c => c.Key ).First();
                int? rockCampusId = campusList.Where( c => c.Name == primaryHouseholdCampus || c.ShortCode == primaryHouseholdCampus )
                    .Select( c => (int?)c.Id ).FirstOrDefault();

                RockTransactionScope.WrapTransaction( () =>
                {
                    var groupService = new GroupService();
                    var familyGroup = groupService.SaveNewFamily( familyMembers, rockCampusId, ImportPersonAlias );
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
