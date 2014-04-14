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
using System.Data.Entity;
using System.Linq;
using OrcaMDF.Core.MetaData;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// Partial of F1Component that holds the People import methods
    /// </summary>
    partial class F1Component
    {
        /// <summary>
        /// Maps the company.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private void MapCompany( IQueryable<Row> tableData )
        {
            var groupTypeRoleService = new GroupTypeRoleService();
            var attributeService = new AttributeService();
            var businessList = new List<Group>();

            // Record status: Active, Inactive, Pending
            int? statusActiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ) ).Id;
            int? statusInactiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ) ).Id;
            int? statusPendingId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ) ).Id;

            // Record type: Business
            int? businessRecordTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_BUSINESS ) ).Id;

            // Group role: TBD
            int groupRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;

            // Group type: Family
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            // Cached F1 attribute: HouseholdId
            var householdIdAttribute = AttributeCache.Read( HouseholdAttributeId );

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying company import ({0:N0} found).", totalRows ) );

            foreach ( var row in tableData )
            {
                int? householdId = row["Household_ID"] as int?;
                if ( GetPersonId( null, householdId ) == null )
                {
                    var businessGroup = new Group();
                    var business = new Person();

                    var businessName = row["Household_Name"] as string;

                    if ( businessName != null )
                    {
                        businessName.Replace( "&#39;", "'" );
                        businessName.Replace( "&amp;", "&" );
                        business.FirstName = businessName.Left( 50 );
                        businessGroup.Name = businessName.Left( 50 );
                    }

                    business.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    business.CreatedDateTime = row["Created_Date"] as DateTime?;
                    business.RecordTypeValueId = businessRecordTypeId;

                    business.Attributes = new Dictionary<string, AttributeCache>();
                    business.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                    business.Attributes.Add( householdIdAttribute.Key, householdIdAttribute );
                    business.AttributeValues.Add( householdIdAttribute.Key, new List<AttributeValue>() );
                    business.AttributeValues[householdIdAttribute.Key].Add( new AttributeValue()
                    {
                        AttributeId = householdIdAttribute.Id,
                        Value = householdId.ToString(),
                        Order = 0
                    } );

                    var groupMember = new GroupMember();
                    groupMember.Person = business;
                    groupMember.GroupRoleId = groupRoleId;
                    groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                    businessGroup.Members.Add( groupMember );
                    businessGroup.GroupTypeId = familyGroupTypeId;
                    businessList.Add( businessGroup );

                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} companies imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        RockTransactionScope.WrapTransaction( () =>
                        {
                            var rockContext = new RockContext();
                            rockContext.Groups.AddRange( businessList );
                            rockContext.SaveChanges();

                            foreach ( var newBusiness in businessList )
                            {
                                foreach ( var businessMember in newBusiness.Members )
                                {
                                    var person = businessMember.Person;
                                    foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                                    {
                                        var newValue = person.AttributeValues[attributeCache.Key].FirstOrDefault();
                                        if ( newValue != null )
                                        {
                                            newValue.EntityId = person.Id;
                                            rockContext.AttributeValues.Add( newValue );
                                        }
                                    }

                                    person.GivingGroupId = newBusiness.Id;
                                }
                            }

                            rockContext.SaveChanges();
                        } );

                        businessList.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            if ( businessList.Any() )
            {
                RockTransactionScope.WrapTransaction( () =>
                {
                    var rockContext = new RockContext();
                    rockContext.Groups.AddRange( businessList );
                    rockContext.SaveChanges();

                    foreach ( var newBusiness in businessList )
                    {
                        foreach ( var businessMember in newBusiness.Members )
                        {
                            var person = businessMember.Person;
                            foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                            {
                                var newValue = person.AttributeValues[attributeCache.Key].FirstOrDefault();
                                if ( newValue != null )
                                {
                                    newValue.EntityId = person.Id;
                                    rockContext.AttributeValues.Add( newValue );
                                }
                            }

                            person.GivingGroupId = newBusiness.Id;
                        }
                    }

                    rockContext.SaveChanges();
                } );
            }

            ReportProgress( 100, string.Format( "Finished company import: {0:N0} companies imported.", completed ) );
        }

        /// <summary>
        /// Maps the person.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapPerson( IQueryable<Row> tableData, List<string> selectedColumns = null )
        {
            var groupTypeRoleService = new GroupTypeRoleService();
            var attributeService = new AttributeService();
            var dvService = new DefinedValueService();
            var familyList = new List<Group>();

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
            int? recordStatusActiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ) ).Id;
            int? recordStatusInactiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ) ).Id;
            int? recordStatusPendingId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ) ).Id;

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

            // Group roles: Adult, Child, others
            int adultRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;
            int childRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD ) ).Id;

            // Group type: Family
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            // Look up additional Person attributes (existing)
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();

            // Cached F1 attributes: IndividualId, HouseholdId, PreviousChurch, Position, Employer, School
            var individualIdAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "F1IndividualId" ) );
            var householdIdAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "F1HouseholdId" ) );
            var previousChurchAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "PreviousChurch" ) );
            var employerAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Employer" ) );
            var positionAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Position" ) );
            var firstVisitAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "FirstVisit" ) );
            var schoolAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "School" ) );
            var membershipDateAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "MembershipDate" ) );

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying person import ({0:N0} found, {1:N0} already exist).", totalRows, ImportedPeople.Count() ) );

            foreach ( var groupedRows in tableData.GroupBy<Row, int?>( r => r["Household_ID"] as int? ) )
            {
                var familyGroup = new Group();
                var householdCampusList = new List<string>();

                foreach ( var row in groupedRows )
                {
                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;
                    if ( GetPersonId( individualId, householdId ) == null )
                    {
                        var person = new Person();
                        person.FirstName = row["First_Name"] as string;
                        person.MiddleName = row["Middle_Name"] as string;
                        person.NickName = row["Goes_By"] as string ?? person.FirstName;
                        person.LastName = row["Last_Name"] as string;
                        person.BirthDate = row["Date_Of_Birth"] as DateTime?;
                        person.CreatedByPersonAliasId = ImportPersonAlias.Id;
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
                            prefix = prefix.RemoveSpecialCharacters().Trim();
                            person.TitleValueId = titleTypes.Where( s => prefix == s.Name.RemoveSpecialCharacters() )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string suffix = row["Suffix"] as string;
                        if ( suffix != null )
                        {
                            suffix = suffix.RemoveSpecialCharacters().Trim();
                            person.SuffixValueId = suffixTypes.Where( s => suffix == s.Name.RemoveSpecialCharacters() )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string maritalStatus = row["Marital_Status"] as string;
                        if ( maritalStatus != null )
                        {
                            person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Name == maritalStatus )
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

                        string memberStatus = row["Status_Name"] as string;
                        if ( memberStatus == "Member" )
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER ) ).Id;
                            person.RecordStatusValueId = recordStatusActiveId;
                        }
                        else if ( memberStatus == "Visitor" )
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR ) ).Id;
                            person.RecordStatusValueId = recordStatusActiveId;
                        }
                        else if ( memberStatus == "Deceased" )
                        {
                            person.IsDeceased = true;
                            person.RecordStatusValueId = recordStatusInactiveId;
                            person.RecordStatusReasonValueId = recordStatusReasons.Where( dv => dv.Name == "Deceased" )
                                .Select( dv => dv.Id ).FirstOrDefault();
                        }
                        else
                        {
                            // F1 defaults are Member & Visitor; all others are user-defined
                            var customConnectionType = connectionStatusTypes.Where( dv => dv.Name == memberStatus )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();

                            int attendeeId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( "39F491C5-D6AC-4A9B-8AC0-C431CB17D588" ) ).Id;
                            person.ConnectionStatusValueId = customConnectionType ?? attendeeId;
                            person.RecordStatusValueId = recordStatusActiveId;
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

                        // Map F1 attributes
                        person.Attributes = new Dictionary<string, AttributeCache>();
                        person.AttributeValues = new Dictionary<string, List<AttributeValue>>();

                        // individual_id already defined in scope
                        if ( individualId != null )
                        {
                            person.Attributes.Add( individualIdAttribute.Key, individualIdAttribute );
                            person.AttributeValues.Add( individualIdAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[individualIdAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = individualIdAttribute.Id,
                                Value = individualId.ToString(),
                                Order = 0
                            } );
                        }

                        // household_id already defined in scope
                        if ( householdId != null )
                        {
                            person.Attributes.Add( householdIdAttribute.Key, householdIdAttribute );
                            person.AttributeValues.Add( householdIdAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[householdIdAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = householdIdAttribute.Id,
                                Value = householdId.ToString(),
                                Order = 0
                            } );
                        }

                        string previousChurch = row["Former_Church"] as string;
                        if ( previousChurch != null )
                        {
                            person.Attributes.Add( previousChurchAttribute.Key, previousChurchAttribute );
                            person.AttributeValues.Add( previousChurchAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[previousChurchAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = previousChurchAttribute.Id,
                                Value = previousChurch,
                                Order = 0
                            } );
                        }

                        string employer = row["Employer"] as string;
                        if ( employer != null )
                        {
                            person.Attributes.Add( employerAttribute.Key, employerAttribute );
                            person.AttributeValues.Add( employerAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[employerAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = employerAttribute.Id,
                                Value = employer,
                                Order = 0
                            } );
                        }

                        string position = row["Occupation_Name"] as string ?? row["Occupation_Description"] as string;
                        if ( position != null )
                        {
                            person.Attributes.Add( positionAttribute.Key, positionAttribute );
                            person.AttributeValues.Add( positionAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[positionAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = positionAttribute.Id,
                                Value = position,
                                Order = 0
                            } );
                        }

                        string school = row["School_Name"] as string;
                        if ( school != null )
                        {
                            person.Attributes.Add( schoolAttribute.Key, schoolAttribute );
                            person.AttributeValues.Add( schoolAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[schoolAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = schoolAttribute.Id,
                                Value = school,
                                Order = 0
                            } );
                        }

                        DateTime? membershipDate = row["Status_Date"] as DateTime?;
                        if ( membershipDate != null )
                        {
                            person.CreatedDateTime = membershipDate;
                            person.Attributes.Add( membershipDateAttribute.Key, membershipDateAttribute );
                            person.AttributeValues.Add( membershipDateAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[membershipDateAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = membershipDateAttribute.Id,
                                Value = membershipDate.Value.ToString( "MM/dd/yyyy" ),
                                Order = 0
                            } );
                        }

                        DateTime? firstVisit = row["First_Record"] as DateTime?;
                        if ( firstVisit != null )
                        {
                            person.CreatedDateTime = firstVisit;
                            // will always pick firstVisit if membershipDate is null
                            firstVisit = firstVisit > membershipDate ? membershipDate : firstVisit;
                            person.Attributes.Add( firstVisitAttribute.Key, firstVisitAttribute );
                            person.AttributeValues.Add( firstVisitAttribute.Key, new List<AttributeValue>() );
                            person.AttributeValues[firstVisitAttribute.Key].Add( new AttributeValue()
                            {
                                AttributeId = firstVisitAttribute.Id,
                                Value = firstVisit.Value.ToString( "MM/dd/yyyy" ),
                                Order = 0
                            } );
                        }

                        // Other Attributes to create:
                        // former name
                        // bar_code
                        // member_env_code
                        // denomination_name

                        var groupMember = new GroupMember();
                        groupMember.Person = person;
                        groupMember.GroupRoleId = groupRoleId;
                        groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                        familyGroup.Members.Add( groupMember );
                    }
                }

                if ( familyGroup.Members.Any() )
                {
                    familyGroup.Name = familyGroup.Members.FirstOrDefault().Person.LastName + " Family";
                    familyGroup.GroupTypeId = familyGroupTypeId;

                    string primaryHouseholdCampus = householdCampusList.GroupBy( c => c ).OrderByDescending( c => c.Count() )
                        .Select( c => c.Key ).FirstOrDefault();
                    if ( primaryHouseholdCampus != null )
                    {
                        familyGroup.CampusId = CampusList.Where( c => c.Name.StartsWith( primaryHouseholdCampus ) || c.ShortCode == primaryHouseholdCampus )
                             .Select( c => (int?)c.Id ).FirstOrDefault();
                    }

                    familyList.Add( familyGroup );
                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} people imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        RockTransactionScope.WrapTransaction( () =>
                        {
                            var rockContext = new RockContext();
                            rockContext.Groups.AddRange( familyList );
                            rockContext.SaveChanges();

                            foreach ( var newFamilyGroup in familyList )
                            {
                                foreach ( var groupMember in newFamilyGroup.Members )
                                {
                                    var person = groupMember.Person;
                                    foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                                    {
                                        var newValue = person.AttributeValues[attributeCache.Key].FirstOrDefault();
                                        if ( newValue != null )
                                        {
                                            newValue.EntityId = person.Id;
                                            rockContext.AttributeValues.Add( newValue );
                                        }
                                    }

                                    if ( !person.Aliases.Any( a => a.AliasPersonId == person.Id ) )
                                    {
                                        person.Aliases.Add( new PersonAlias { AliasPersonId = person.Id, AliasPersonGuid = person.Guid } );
                                    }

                                    if ( groupMember.GroupRoleId != childRoleId )
                                    {
                                        person.GivingGroupId = newFamilyGroup.Id;
                                    }
                                }
                            }

                            rockContext.SaveChanges();
                        } );

                        familyList.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            // Save any remaining families in the batch
            if ( familyList.Any() )
            {
                RockTransactionScope.WrapTransaction( () =>
                {
                    var rockContext = new RockContext();
                    rockContext.Groups.AddRange( familyList );
                    rockContext.SaveChanges();

                    foreach ( var newFamilyGroup in familyList )
                    {
                        foreach ( var groupMember in newFamilyGroup.Members )
                        {
                            var person = groupMember.Person;
                            foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                            {
                                var newValue = person.AttributeValues[attributeCache.Key].FirstOrDefault();
                                if ( newValue != null )
                                {
                                    newValue.EntityId = person.Id;
                                    rockContext.AttributeValues.Add( newValue );
                                }
                            }

                            if ( !person.Aliases.Any( a => a.AliasPersonId == person.Id ) )
                            {
                                person.Aliases.Add( new PersonAlias { AliasPersonId = person.Id, AliasPersonGuid = person.Guid } );
                            }

                            if ( groupMember.GroupRoleId != childRoleId )
                            {
                                person.GivingGroupId = newFamilyGroup.Id;
                            }
                        }
                    }

                    rockContext.SaveChanges();
                } );
            }

            ReportProgress( 100, string.Format( "Finished person import: {0:N0} people imported.", completed ) );
        }

        /// <summary>
        /// Maps the users.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        private void MapUsers( IQueryable<Row> tableData )
        {
            var attributeService = new AttributeService();
            var personService = new PersonService();

            int rockAuthenticatedTypeId = EntityTypeCache.Read( "Rock.Security.Authentication.Database" ).Id;

            int secondaryEmailAttributeId = new AttributeService().GetByEntityTypeId( PersonEntityTypeId )
                .Where( a => a.Key == "SecondaryEmail" ).Select( a => a.Id ).FirstOrDefault();
            var secondaryEmailAttribute = AttributeCache.Read( SecondaryEmailAttributeId );
            var userLoginAttribute = AttributeCache.Read( UserLoginAttributeId );

            int staffGroupId = new GroupService().GetByGuid( new Guid( Rock.SystemGuid.Group.GROUP_STAFF_MEMBERS ) ).Id;
            int memberGroupRoleId = new GroupTypeRoleService().Queryable().Where( r => r.Guid.Equals( new Guid( "00F3AC1C-71B9-4EE5-A30E-4C48C8A0BF1F" ) ) )
                .Select( r => r.Id ).FirstOrDefault();

            var importedUsers = new AttributeValueService().GetByAttributeId( UserLoginAttributeId )
               .Select( av => new { UserId = av.Value.AsType<int?>(), PersonId = av.EntityId } )
               .ToDictionary( t => t.UserId, t => t.PersonId );

            var newUserLogins = new List<UserLogin>();
            var newStaffMembers = new List<GroupMember>();
            var updatedPersonList = new List<Person>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying user import ({0:N0} found, {1:N0} already exist).", totalRows, importedUsers.Count() ) );

            foreach ( var row in tableData )
            {
                int? individualId = row["LinkedIndividualID"] as int?;
                string userName = row["UserLogin"] as string;
                int? userId = row["UserID"] as int?;
                if ( userId != null && individualId != null && !string.IsNullOrWhiteSpace( userName ) && !importedUsers.ContainsKey( userId ) )
                {
                    int? personId = GetPersonId( individualId, null );
                    if ( personId != null )
                    {
                        DateTime? createdDate = row["UserCreatedDate"] as DateTime?;
                        string userPhone = row["UserPhone"] as string;
                        string userEmail = row["UserEmail"] as string;
                        string userTitle = row["UserTitle"] as string;
                        bool? isEnabled = row["IsUserEnabled"] as bool?;
                        bool? isStaff = row["IsStaff"] as bool?;
                        bool isActive = isEnabled ?? false;

                        var user = new UserLogin();
                        user.CreatedDateTime = createdDate;
                        user.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        user.EntityTypeId = rockAuthenticatedTypeId;
                        user.IsConfirmed = isEnabled;
                        user.UserName = userName;
                        user.PersonId = personId;

                        if ( isStaff == true )
                        {
                            // add this user to the staff group
                            var staffMember = new GroupMember();
                            staffMember.GroupId = staffGroupId;
                            staffMember.PersonId = (int)personId;
                            staffMember.GroupRoleId = memberGroupRoleId;
                            staffMember.CreatedDateTime = createdDate;
                            staffMember.CreatedByPersonAliasId = ImportPersonAlias.Id;
                            staffMember.GroupMemberStatus = isActive ? GroupMemberStatus.Active : GroupMemberStatus.Inactive;

                            newStaffMembers.Add( staffMember );
                        }

                        user.Attributes = new Dictionary<string, AttributeCache>();
                        user.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                        user.Attributes.Add( userLoginAttribute.Key, userLoginAttribute );
                        user.AttributeValues.Add( userLoginAttribute.Key, new List<AttributeValue>() );
                        user.AttributeValues[userLoginAttribute.Key].Add( new AttributeValue()
                        {
                            AttributeId = userLoginAttribute.Id,
                            Value = userId.ToString(),
                            Order = 0
                        } );

                        // set user login email to primary email
                        if ( userEmail.IsValidEmail() )
                        {
                            var person = personService.Get( (int)personId );
                            string secondaryEmail = string.Empty;
                            userEmail = userEmail.Trim();
                            if ( string.IsNullOrWhiteSpace( person.Email ) )
                            {
                                secondaryEmail = person.Email;
                                person.Email = userEmail.Left( 75 );
                                person.IsEmailActive = isEnabled;
                                person.EmailNote = userTitle;
                            }
                            else if ( !person.Email.Equals( userEmail ) )
                            {
                                secondaryEmail = userEmail;
                            }

                            if ( !string.IsNullOrWhiteSpace( secondaryEmail ) )
                            {
                                person.Attributes = new Dictionary<string, AttributeCache>();
                                person.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                                person.Attributes.Add( secondaryEmailAttribute.Key, secondaryEmailAttribute );
                                person.AttributeValues.Add( secondaryEmailAttribute.Key, new List<AttributeValue>() );
                                person.AttributeValues[secondaryEmailAttribute.Key].Add( new AttributeValue()
                                {
                                    AttributeId = secondaryEmailAttribute.Id,
                                    Value = secondaryEmail,
                                    Order = 0
                                } );
                            }

                            updatedPersonList.Add( person );
                        }

                        // other Attributes to save
                        // UserBio
                        // DepartmentName
                        // IsPastor

                        newUserLogins.Add( user );
                        completed++;

                        if ( completed % percentage < 1 )
                        {
                            int percentComplete = completed / percentage;
                            ReportProgress( percentComplete, string.Format( "{0:N0} users imported ({1}% complete).", completed, percentComplete ) );
                        }
                        else if ( completed % ReportingNumber < 1 )
                        {
                            RockTransactionScope.WrapTransaction( () =>
                            {
                                var rockContext = new RockContext();
                                rockContext.UserLogins.AddRange( newUserLogins );
                                rockContext.GroupMembers.AddRange( newStaffMembers );
                                rockContext.SaveChanges();

                                // save email changes to person
                                if ( updatedPersonList.Any() )
                                {
                                    personService.RockContext.SaveChanges();
                                    foreach ( var person in updatedPersonList.Where( p => p.Attributes != null ) )
                                    {
                                        var attributeValue = person.AttributeValues[secondaryEmailAttribute.Key].FirstOrDefault();
                                        if ( attributeValue != null )
                                        {
                                            attributeValue.EntityId = person.Id;
                                            rockContext.AttributeValues.Add( attributeValue );
                                        }
                                    }

                                    updatedPersonList.Clear();
                                }

                                foreach ( var userLogin in newUserLogins.Where( p => p.Attributes != null ) )
                                {
                                    var attributeValue = userLogin.AttributeValues[userLoginAttribute.Key].FirstOrDefault();
                                    if ( attributeValue != null )
                                    {
                                        attributeValue.EntityId = userLogin.Id;
                                        rockContext.AttributeValues.Add( attributeValue );
                                    }
                                }

                                rockContext.SaveChanges();
                            } );

                            newUserLogins.Clear();
                            ReportPartialProgress();
                        }
                    }
                }
            }

            if ( newUserLogins.Any() )
            {
                RockTransactionScope.WrapTransaction( () =>
                {
                    var rockContext = new RockContext();
                    rockContext.UserLogins.AddRange( newUserLogins );
                    rockContext.GroupMembers.AddRange( newStaffMembers );
                    rockContext.SaveChanges();

                    // save email changes to person
                    if ( updatedPersonList.Any() )
                    {
                        personService.RockContext.SaveChanges();
                        foreach ( var person in updatedPersonList.Where( p => p.Attributes != null ) )
                        {
                            var attributeValue = person.AttributeValues[secondaryEmailAttribute.Key].FirstOrDefault();
                            if ( attributeValue != null )
                            {
                                attributeValue.EntityId = person.Id;
                                rockContext.AttributeValues.Add( attributeValue );
                            }
                        }

                        updatedPersonList.Clear();
                    }

                    foreach ( var userLogin in newUserLogins.Where( p => p.Attributes != null ) )
                    {
                        var attributeValue = userLogin.AttributeValues[userLoginAttribute.Key].FirstOrDefault();
                        if ( attributeValue != null )
                        {
                            attributeValue.EntityId = userLogin.Id;
                            rockContext.AttributeValues.Add( attributeValue );
                        }
                    }

                    rockContext.SaveChanges();
                } );
            }

            ReportProgress( 100, string.Format( "Finished user import: {0:N0} users imported.", completed ) );
        }
    }
}
