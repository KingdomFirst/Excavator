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

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override bool MapData()
        {
            var scanner = new DataScanner( database );
            int tableCount = 0;

            foreach ( var node in selectedNodes.Where( n => n.Checked != false ) )
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

        #endregion

        #region Mapped Data

        /// <summary>
        /// Maps the contribution.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapContribution( IQueryable<Row> tableData, List<string> selectedColumns )
        {
        }

        /// <summary>
        /// Maps the person.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapPerson( IQueryable<Row> tableData, List<string> selectedColumns )
        {
            var groupTypeRoleService = new GroupTypeRoleService();
            var fieldTypeService = new FieldTypeService();
            var attributeService = new AttributeService();
            var noteTypeService = new NoteTypeService();
            var dvService = new DefinedValueService();
            var personService = new PersonService();
            var campusService = new CampusService();
            var noteService = new NoteService();

            // change this to user-defined person
            var aliasService = new PersonAliasService();
            var CurrentPersonAlias = aliasService.Get( 1 );
            var campusDesignation = new List<string>();

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
            int noteCommentTypeId = noteTypeService.Get( new Guid( "7E53487C-D650-4D85-97E2-350EB8332763" ) ).Id;

            // Group roles: Adult, Child
            int adultRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;
            int childRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD ) ).Id;

            // Campuses: user-defined, should match F1 Campus designation
            List<Campus> campusList = campusService.Queryable().ToList();

            // Add person Attributes to store F1 unique ID's
            string householdIDKey = "Household_ID";
            string individualIDKey = "Individual_ID";
            int personEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;
            int numberFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;
            //int numberFieldTypeId = fieldTypeService.Get( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;
            var personAttributeList = attributeService.Queryable().Where( a => a.EntityTypeId == personEntityTypeId ).ToList();
            var householdAttributeId = personAttributeList.FirstOrDefault( a => a.Key == householdIDKey );
            if ( householdAttributeId == null )
            {
                householdAttributeId = new Rock.Model.Attribute();
                householdAttributeId.Key = householdIDKey;
                householdAttributeId.Name = "F1 Household ID";
                householdAttributeId.FieldTypeId = numberFieldTypeId;
                householdAttributeId.EntityTypeId = personEntityTypeId;
                householdAttributeId.Description = "The FellowshipOne household identifier for the person that was imported";
                householdAttributeId.DefaultValue = string.Empty;
                householdAttributeId.IsMultiValue = false;
                householdAttributeId.IsRequired = false;
                householdAttributeId.Order = 0;

                attributeService.Add( householdAttributeId, CurrentPersonAlias );
                attributeService.Save( householdAttributeId, CurrentPersonAlias );
            }

            var individualAttributeId = personAttributeList.FirstOrDefault( a => a.Key == individualIDKey );
            if ( individualAttributeId == null )
            {
                individualAttributeId = new Rock.Model.Attribute();
                individualAttributeId.Key = individualIDKey;
                individualAttributeId.Name = "F1 Individual ID";
                individualAttributeId.FieldTypeId = numberFieldTypeId;
                individualAttributeId.EntityTypeId = personEntityTypeId;
                individualAttributeId.Description = "The FellowshipOne individual identifier for the person that was imported";
                individualAttributeId.DefaultValue = string.Empty;
                individualAttributeId.IsMultiValue = false;
                individualAttributeId.IsRequired = false;
                individualAttributeId.Order = 0;

                attributeService.Add( individualAttributeId, CurrentPersonAlias );
                attributeService.Save( individualAttributeId, CurrentPersonAlias );
            }

            foreach ( var groupedRows in tableData.GroupBy<Row, int?>( r => r[householdIDKey] as int? ) )
            {
                // only import where selectedColumns.Contains( row.Column )

                var familyMembers = new List<GroupMember>();

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

                    string join_date = row["Status_Date"] as string;
                    if ( join_date != null )
                    {
                        DateTime firstCreated;
                        if ( DateTime.TryParse( join_date, out firstCreated ) )
                        {
                            person.CreatedDateTime = firstCreated;
                        }
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
                        campusDesignation.Add( campus );
                    }

                    string status_comment = row["Status_Comment"] as string;
                    if ( status_comment != null )
                    {
                        Note comment = new Note();
                        comment.Text = status_comment;
                        comment.NoteTypeId = noteCommentTypeId;
                        RockTransactionScope.WrapTransaction( () =>
                        {
                            noteService.Save( comment );
                        } );
                    }

                    // start adding person attributes
                    person.Attributes = new Dictionary<string, AttributeCache>();
                    person.AttributeValues = new Dictionary<string, List<AttributeValue>>();

                    string household_id = row["Household_ID"] as string;
                    if ( household_id != null )
                    {
                        person.Attributes.Add( householdIDKey, AttributeCache.Read( householdAttributeId ) );
                        var attributeValue = new AttributeValue() { Value = household_id };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( householdIDKey, valueList );
                    }

                    string individual_id = row["Individual_ID"] as string;
                    if ( individual_id != null )
                    {
                        person.Attributes.Add( individualIDKey, AttributeCache.Read( individualAttributeId ) );
                        var attributeValue = new AttributeValue() { Value = individual_id };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( individualIDKey, valueList );
                    }

                    string former_church = row["Former_Church"] as string;
                    if ( former_church != null )
                    {
                        var previousChurchAttribute = personAttributeList.FirstOrDefault( a => a.Key == "PreviousChurch" );
                        person.Attributes.Add( "PreviousChurch", AttributeCache.Read( previousChurchAttribute ) );
                        var attributeValue = new AttributeValue() { Value = former_church };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "PreviousChurch", valueList );
                    }

                    string employer = row["Employer"] as string;
                    if ( employer != null )
                    {
                        var employerAttribute = personAttributeList.FirstOrDefault( a => a.Key == "Employer" );
                        person.Attributes.Add( "Employer", AttributeCache.Read( employerAttribute ) );
                        var attributeValue = new AttributeValue() { Value = employer };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "Employer", valueList );
                    }

                    string position = row["Occupation_Name"] as string ?? row["Occupation_Description"] as string;
                    if ( position != null )
                    {
                        var positionAttribute = personAttributeList.FirstOrDefault( a => a.Key == "Position" );
                        person.Attributes.Add( "Position", AttributeCache.Read( positionAttribute ) );
                        var attributeValue = new AttributeValue() { Value = former_church };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "Position", valueList );
                    }

                    string school = row["School_Name"] as string;
                    if ( position != null )
                    {
                        var schoolAttribute = personAttributeList.FirstOrDefault( a => a.Key == "School" );
                        person.Attributes.Add( "School", AttributeCache.Read( schoolAttribute ) );
                        var attributeValue = new AttributeValue() { Value = former_church };
                        var valueList = new List<AttributeValue>() { attributeValue };
                        person.AttributeValues.Add( "School", valueList );
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

                //RockTransactionScope.WrapTransaction( () =>
                //{
                //    personService.Add( person, CurrentPersonAlias );
                //    personService.Save( person, CurrentPersonAlias );
                //} );

                string primaryHouseholdCampus = campusDesignation.GroupBy( c => c ).OrderByDescending( c => c.Count() ).Select( c => c.Key ).First();
                int? rockCampusId = campusList.Where( c => c.Name == primaryHouseholdCampus || c.ShortCode == primaryHouseholdCampus )
                    .Select( c => (int?)c.Id ).FirstOrDefault();

                RockTransactionScope.WrapTransaction( () =>
                {
                    var groupService = new GroupService();
                    var familyGroup = groupService.SaveNewFamily( familyMembers, rockCampusId, CurrentPersonAlias );
                } );
            }
        }

        #endregion
    }
}
