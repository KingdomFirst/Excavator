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
using Excavator.Utility;
using OrcaMDF.Core.MetaData;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    public class People : F1Component, IFellowshipOne
    {

        RockContext lookupContext = new RockContext();
        List<Group> businessList = new List<Group>();

        // Record status: Active, Inactive, Pending
        int? statusActiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ) ).Id;
        int? statusInactiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ) ).Id;
        int? statusPendingId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ) ).Id;

        // Record type: Business
        int? businessRecordTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_BUSINESS ) ).Id;

        // Group role: Adult
        //int groupRoleId = new GroupTypeRoleService( lookupContext ).Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;

        // Group type: Family
        int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;


        /// <summary>
        /// Maps the specified table data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        public void Map( IQueryable<Row> tableData )
        {
        }
    }

    /// <summary>
    /// Partial of F1Component that holds the People import methods
    /// </summary>
    public partial class F1Component
    {
        /// <summary>
        /// Maps the company.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private void MapCompany( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var businessList = new List<Group>();

            // Record status: Active, Inactive, Pending
            int? statusActiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ), lookupContext ).Id;
            int? statusInactiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ), lookupContext ).Id;
            int? statusPendingId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ), lookupContext ).Id;

            // Record type: Business
            int? businessRecordTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_BUSINESS ), lookupContext ).Id;

            // Group role: Adult
            int groupRoleId = new GroupTypeRoleService( lookupContext ).Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;

            // Group type: Family
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying company import ({0:N0} found).", totalRows ) );

            foreach ( var row in tableData.Where( r => r != null ) )
            {
                int? householdId = row["Household_ID"] as int?;
                if ( GetPersonKeys( null, householdId ) == null )
                {
                    var businessGroup = new Group();
                    var businessPerson = new Person();

                    businessPerson.CreatedByPersonAliasId = ImportPersonAliasId;
                    businessPerson.CreatedDateTime = row["Created_Date"] as DateTime?;
                    businessPerson.RecordTypeValueId = businessRecordTypeId;

                    var businessName = row["Household_Name"] as string;
                    if ( businessName != null )
                    {
                        businessName = businessName.Replace( "&#39;", "'" );
                        businessName = businessName.Replace( "&amp;", "&" );
                        businessPerson.LastName = businessName.Left( 50 );
                        businessGroup.Name = businessName.Left( 50 );
                    }

                    businessPerson.Attributes = new Dictionary<string, AttributeCache>();
                    businessPerson.AttributeValues = new Dictionary<string, AttributeValueCache>();
                    AddPersonAttribute( HouseholdIdAttribute, businessPerson, householdId.ToString() );

                    var groupMember = new GroupMember();
                    groupMember.Person = businessPerson;
                    groupMember.GroupRoleId = groupRoleId;
                    groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                    businessGroup.Members.Add( groupMember );
                    businessGroup.GroupTypeId = familyGroupTypeId;
                    businessGroup.ForeignKey = householdId.ToString();
                    businessGroup.ForeignId = householdId;
                    businessList.Add( businessGroup );

                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} companies imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveCompanies( businessList );
                        ReportPartialProgress();
                        businessList.Clear();
                    }
                }
            }

            if ( businessList.Any() )
            {
                SaveCompanies( businessList );
            }

            ReportProgress( 100, string.Format( "Finished company import: {0:N0} companies imported.", completed ) );
        }

        /// <summary>
        /// Saves the companies.
        /// </summary>
        /// <param name="businessList">The business list.</param>
        private void SaveCompanies( List<Group> businessList )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.Groups.AddRange( businessList );
                rockContext.SaveChanges( DisableAuditing );

                foreach ( var newBusiness in businessList )
                {
                    foreach ( var groupMember in newBusiness.Members )
                    {
                        // don't call LoadAttributes, it only rewrites existing cache objects
                        // groupMember.Person.LoadAttributes( rockContext );

                        foreach ( var attributeCache in groupMember.Person.Attributes.Select( a => a.Value ) )
                        {
                            var existingValue = rockContext.AttributeValues.FirstOrDefault( v => v.Attribute.Key == attributeCache.Key && v.EntityId == groupMember.Person.Id );
                            var newAttributeValue = groupMember.Person.AttributeValues[attributeCache.Key];

                            // set the new value and add it to the database
                            if ( existingValue == null )
                            {
                                existingValue = new AttributeValue();
                                existingValue.AttributeId = newAttributeValue.AttributeId;
                                existingValue.EntityId = groupMember.Person.Id;
                                existingValue.Value = newAttributeValue.Value;

                                rockContext.AttributeValues.Add( existingValue );
                            }
                            else
                            {
                                existingValue.Value = newAttributeValue.Value;
                                rockContext.Entry( existingValue ).State = EntityState.Modified;
                            }
                        }

                        if ( !groupMember.Person.Aliases.Any( a => a.AliasPersonId == groupMember.Person.Id ) )
                        {
                            groupMember.Person.Aliases.Add( new PersonAlias { AliasPersonId = groupMember.Person.Id, AliasPersonGuid = groupMember.Person.Guid } );
                        }

                        groupMember.Person.GivingGroupId = newBusiness.Id;
                    }
                }

                rockContext.ChangeTracker.DetectChanges();
                rockContext.SaveChanges( DisableAuditing );

                if ( businessList.Any() )
                {
                    var groupMembers = businessList.SelectMany( gm => gm.Members );
                    ImportedPeople.AddRange( groupMembers.Select( m => new PersonKeys
                    {
                        PersonAliasId = (int)m.Person.PrimaryAliasId,
                        PersonId = m.Person.Id,
                        IndividualId = null,
                        HouseholdId = m.Group.ForeignId,
                        FamilyRoleId = FamilyRole.Adult
                    } ).ToList()
                    );
                }
            } );
        }

        /// <summary>
        /// Maps the person.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        public void MapPerson( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var groupTypeRoleService = new GroupTypeRoleService( lookupContext );

            // Marital statuses: Married, Single, Separated, etc
            var maritalStatusTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS ), lookupContext ).DefinedValues;

            // Connection statuses: Member, Visitor, Attendee, etc
            var connectionStatusTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS ), lookupContext ).DefinedValues;
            int memberStatusId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER ) ).Id;
            int visitorStatusId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR ) ).Id;
            int attendeeStatusId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_ATTENDEE ) ).Id;

            // Record statuses/reasons: Active, Inactive, Pending, Deceased, etc
            var recordStatuses = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS ) ).DefinedValues;
            var recordStatusReasons = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS_REASON ), lookupContext ).DefinedValues;

            int recordStatusActiveId = recordStatuses.FirstOrDefault( r => r.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ) ).Id;
            int recordStatusInactiveId = recordStatuses.FirstOrDefault( r => r.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ) ).Id;
            int recordStatusPendingId = recordStatuses.FirstOrDefault( r => r.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ) ).Id;
            int statusReasonDeceasedId = recordStatusReasons.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_REASON_DECEASED ) ).Id;
            int statusReasonNoActivityId = recordStatusReasons.Where( dv => dv.Value == "No Activity" ).Select( dv => dv.Id ).FirstOrDefault();

            // Record type: Person
            int? personRecordTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON ), lookupContext ).Id;

            // Suffix type: Dr., Jr., II, etc
            var suffixTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_SUFFIX ) ).DefinedValues;

            // Title type: Mr., Mrs. Dr., etc
            var titleTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_TITLE ), lookupContext ).DefinedValues;

            // Group roles: Owner, Adult, Child, others
            GroupTypeRole ownerRole = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_OWNER ) );
            int adultRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;
            int childRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD ) ).Id;
            int inviteeRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED ) ).Id;
            int invitedByRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED_BY ) ).Id;
            int canCheckInRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_CAN_CHECK_IN ) ).Id;
            int allowCheckInByRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_ALLOW_CHECK_IN_BY ) ).Id;

            // Group type: Family
            int familyGroupTypeId = new GroupTypeService( lookupContext ).Get( new Guid( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY ) ).Id;

            // Look up additional Person attributes (existing)
            var personAttributes = new AttributeService( lookupContext ).GetByEntityTypeId( PersonEntityTypeId ).AsNoTracking().ToList();

            // F1 attributes: IndividualId, HouseholdId
            // Core attributes: PreviousChurch, Position, Employer, School
            var previousChurchAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "PreviousChurch", StringComparison.InvariantCultureIgnoreCase ) ) );
            var membershipDateAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "MembershipDate", StringComparison.InvariantCultureIgnoreCase ) ) );
            var firstVisitAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "FirstVisit", StringComparison.InvariantCultureIgnoreCase ) ) );
            var legalNoteAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "LegalNotes", StringComparison.InvariantCultureIgnoreCase ) ) );
            var employerAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "Employer", StringComparison.InvariantCultureIgnoreCase ) ) );
            var positionAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "Position", StringComparison.InvariantCultureIgnoreCase ) ) );
            var schoolAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "School", StringComparison.InvariantCultureIgnoreCase ) ) );

            var familyList = new List<Group>();
            var visitorList = new List<Group>();
            var householdCampusList = new List<string>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying person import ({0:N0} found, {1:N0} already exist).", totalRows, ImportedPeople.Count ) );

            foreach ( var groupedRows in tableData.GroupBy<Row, int?>( r => r["Household_ID"] as int? ) )
            {
                var familyGroup = new Group();
                householdCampusList.Clear();

                foreach ( var row in groupedRows.Where( r => r != null ) )
                {
                    var familyRoleId = FamilyRole.Adult;
                    string currentCampus = string.Empty;
                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;
                    var personKeys = GetPersonKeys( individualId, householdId );
                    if ( personKeys == null )
                    {
                        var person = new Person();
                        person.FirstName = row["First_Name"] as string;
                        person.MiddleName = row["Middle_Name"] as string;
                        person.NickName = row["Goes_By"] as string ?? person.FirstName;
                        person.LastName = row["Last_Name"] as string;
                        person.IsDeceased = false;

                        var DOB = row["Date_Of_Birth"] as DateTime?;
                        if ( DOB != null )
                        {
                            var birthDate = (DateTime)DOB;
                            person.BirthDay = birthDate.Day;
                            person.BirthMonth = birthDate.Month;
                            person.BirthYear = birthDate.Year;
                        }

                        person.CreatedByPersonAliasId = ImportPersonAliasId;
                        person.RecordTypeValueId = personRecordTypeId;
                        person.ForeignKey = individualId.ToString();
                        person.ForeignId = individualId;

                        var gender = row["Gender"] as string;
                        if ( gender != null )
                        {
                            person.Gender = (Gender)Enum.Parse( typeof( Gender ), gender );
                        }

                        string prefix = row["Prefix"] as string;
                        if ( prefix != null )
                        {
                            prefix = prefix.RemoveSpecialCharacters().Trim();
                            person.TitleValueId = titleTypes.Where( s => prefix == s.Value.RemoveSpecialCharacters() )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string suffix = row["Suffix"] as string;
                        if ( suffix != null )
                        {
                            suffix = suffix.RemoveSpecialCharacters().Trim();
                            person.SuffixValueId = suffixTypes.Where( s => suffix == s.Value.RemoveSpecialCharacters() )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string maritalStatus = row["Marital_Status"] as string;
                        if ( maritalStatus != null )
                        {
                            person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Value == maritalStatus )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                        }
                        else
                        {
                            person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Value == "Unknown" )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                        }

                        string familyRole = row["Household_Position"] as string;
                        if ( familyRole != null )
                        {
                            familyRole = familyRole.ToString().ToLower();
                            if ( familyRole == "visitor" )
                            {
                                familyRoleId = FamilyRole.Visitor;
                            }

                            if ( familyRole == "child" || person.Age < 18 )
                            {
                                familyRoleId = FamilyRole.Child;
                            }
                        }

                        string memberStatus = row["Status_Name"] as string;
                        if ( memberStatus != null )
                        {
                            memberStatus = memberStatus.ToLower();
                            if ( memberStatus.Equals( "member" ) )
                            {
                                person.ConnectionStatusValueId = memberStatusId;
                                person.RecordStatusValueId = recordStatusActiveId;
                            }
                            else if ( memberStatus.Equals( "visitor" ) )
                            {
                                person.ConnectionStatusValueId = visitorStatusId;
                                person.RecordStatusValueId = recordStatusActiveId;

                                // F1 can designate visitors by member status or household position
                                familyRoleId = FamilyRole.Visitor;
                            }
                            else if ( memberStatus.Equals( "deceased" ) )
                            {
                                person.IsDeceased = true;
                                person.RecordStatusReasonValueId = statusReasonDeceasedId;
                                person.RecordStatusValueId = recordStatusInactiveId;
                            }
                            else if ( memberStatus.Equals( "dropped" ) || memberStatus.StartsWith( "inactive" ) )
                            {
                                person.RecordStatusReasonValueId = statusReasonNoActivityId;
                                person.RecordStatusValueId = recordStatusInactiveId;
                            }
                            else
                            {
                                // Lookup others that may be user-defined
                                var customConnectionType = connectionStatusTypes.Where( dv => dv.Value == memberStatus )
                                    .Select( dv => (int?)dv.Id ).FirstOrDefault();

                                person.ConnectionStatusValueId = customConnectionType ?? attendeeStatusId;
                                person.RecordStatusValueId = recordStatusActiveId;
                            }
                        }

                        string campus = row["SubStatus_Name"] as string;
                        if ( campus != null )
                        {
                            currentCampus = campus;
                        }

                        string status_comment = row["Status_Comment"] as string;
                        if ( status_comment != null )
                        {
                            person.SystemNote = status_comment;
                        }

                        // set a processing flag to keep visitors from receiving household info
                        person.ReviewReasonNote = familyRoleId.ToString();

                        // Map F1 attributes
                        person.Attributes = new Dictionary<string, AttributeCache>();
                        person.AttributeValues = new Dictionary<string, AttributeValueCache>();

                        // IndividualId already defined in scope
                        AddPersonAttribute( IndividualIdAttribute, person, individualId.ToString() );

                        // HouseholdId already defined in scope
                        AddPersonAttribute( HouseholdIdAttribute, person, householdId.ToString() );

                        string previousChurch = row["Former_Church"] as string;
                        if ( previousChurch != null )
                        {
                            AddPersonAttribute( previousChurchAttribute, person, previousChurch );
                        }

                        string employer = row["Employer"] as string;
                        if ( employer != null )
                        {
                            AddPersonAttribute( employerAttribute, person, employer );
                        }

                        string position = row["Occupation_Name"] as string ?? row["Occupation_Description"] as string;
                        if ( position != null )
                        {
                            AddPersonAttribute( positionAttribute, person, position );
                        }

                        string school = row["School_Name"] as string;
                        if ( school != null )
                        {
                            AddPersonAttribute( schoolAttribute, person, school );
                        }

                        DateTime? firstVisit = row["First_Record"] as DateTime?;
                        if ( firstVisit != null )
                        {
                            person.CreatedDateTime = firstVisit;
                            AddPersonAttribute( firstVisitAttribute, person, firstVisit.Value.ToString( "MM/dd/yyyy" ) );
                        }

                        // Only import membership date if they are a member
                        DateTime? membershipDate = row["Status_Date"] as DateTime?;
                        if ( membershipDate != null && memberStatus.Contains( "member" ) )
                        {
                            AddPersonAttribute( membershipDateAttribute, person, membershipDate.Value.ToString( "MM/dd/yyyy" ) );
                        }

                        string checkinNote = row["Default_tag_comment"] as string;
                        if ( checkinNote != null )
                        {
                            AddPersonAttribute( legalNoteAttribute, person, checkinNote );
                        }

                        var groupMember = new GroupMember();
                        groupMember.Person = person;
                        groupMember.GroupRoleId = familyRoleId != FamilyRole.Child ? adultRoleId : childRoleId;
                        groupMember.GroupMemberStatus = GroupMemberStatus.Active;

                        if ( familyRoleId != FamilyRole.Visitor )
                        {
                            householdCampusList.Add( currentCampus );
                            familyGroup.Members.Add( groupMember );
                            familyGroup.ForeignKey = householdId.ToString();
                            familyGroup.ForeignId = householdId;
                        }
                        else
                        {
                            var visitorGroup = new Group();
                            visitorGroup.Members.Add( groupMember );
                            visitorGroup.GroupTypeId = familyGroupTypeId;
                            visitorGroup.ForeignKey = householdId.ToString();
                            visitorGroup.ForeignId = householdId;
                            visitorGroup.Name = person.LastName + " Family";
                            visitorGroup.CampusId = CampusList.Where( c => c.Name.StartsWith( currentCampus ) || c.ShortCode == currentCampus )
                                .Select( c => (int?)c.Id ).FirstOrDefault();
                            familyList.Add( visitorGroup );
                            completed += visitorGroup.Members.Count;

                            visitorList.Add( visitorGroup );
                        }
                    }
                }

                if ( familyGroup.Members.Any() )
                {
                    familyGroup.Name = familyGroup.Members.OrderByDescending( p => p.Person.Age )
                        .FirstOrDefault().Person.LastName + " Family";
                    familyGroup.GroupTypeId = familyGroupTypeId;

                    string primaryHouseholdCampus = householdCampusList.GroupBy( c => c ).OrderByDescending( c => c.Count() )
                        .Select( c => c.Key ).FirstOrDefault();
                    if ( !string.IsNullOrWhiteSpace( primaryHouseholdCampus ) )
                    {
                        familyGroup.CampusId = CampusList.Where( c => c.Name.StartsWith( primaryHouseholdCampus ) || c.ShortCode == primaryHouseholdCampus )
                             .Select( c => (int?)c.Id ).FirstOrDefault();
                    }

                    familyList.Add( familyGroup );
                    completed += familyGroup.Members.Count;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} people imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SavePeople( familyList, visitorList, ownerRole, childRoleId, inviteeRoleId, invitedByRoleId, canCheckInRoleId, allowCheckInByRoleId );

                        familyList.Clear();
                        visitorList.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            // Save any remaining families in the batch
            if ( familyList.Any() )
            {
                SavePeople( familyList, visitorList, ownerRole, childRoleId, inviteeRoleId, invitedByRoleId, canCheckInRoleId, allowCheckInByRoleId );
            }

            ReportProgress( 100, string.Format( "Finished person import: {0:N0} people imported.", completed ) );
        }

        /// <summary>
        /// Saves the people.
        /// </summary>
        /// <param name="familyList">The family list.</param>
        /// <param name="visitorList">The visitor list.</param>
        /// <param name="ownerRole">The owner role.</param>
        /// <param name="childRoleId">The child role identifier.</param>
        /// <param name="inviteeRoleId">The invitee role identifier.</param>
        /// <param name="invitedByRoleId">The invited by role identifier.</param>
        /// <param name="canCheckInRoleId">The can check in role identifier.</param>
        /// <param name="allowCheckInByRoleId">The allow check in by role identifier.</param>
        private void SavePeople( List<Group> familyList, List<Group> visitorList, GroupTypeRole ownerRole, int childRoleId, int inviteeRoleId, int invitedByRoleId, int canCheckInRoleId, int allowCheckInByRoleId )
        {
            var rockContext = new RockContext();
            var groupMemberService = new GroupMemberService( rockContext );
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.Groups.AddRange( familyList );
                rockContext.SaveChanges( DisableAuditing );

                foreach ( var familyGroups in familyList.GroupBy<Group, int?>( g => g.ForeignId ) )
                {
                    bool visitorsExist = familyGroups.Count() > 1;
                    foreach ( var newFamilyGroup in familyGroups )
                    {
                        foreach ( var groupMember in newFamilyGroup.Members )
                        {
                            // don't call LoadAttributes, it only rewrites existing cache objects
                            // groupMember.Person.LoadAttributes( rockContext );

                            foreach ( var attributeCache in groupMember.Person.Attributes.Select( a => a.Value ) )
                            {
                                var existingValue = rockContext.AttributeValues.FirstOrDefault( v => v.Attribute.Key == attributeCache.Key && v.EntityId == groupMember.Person.Id );
                                var newAttributeValue = groupMember.Person.AttributeValues[attributeCache.Key];

                                // set the new value and add it to the database
                                if ( existingValue == null )
                                {
                                    existingValue = new AttributeValue();
                                    existingValue.AttributeId = newAttributeValue.AttributeId;
                                    existingValue.EntityId = groupMember.Person.Id;
                                    existingValue.Value = newAttributeValue.Value;

                                    rockContext.AttributeValues.Add( existingValue );
                                }
                                else
                                {
                                    existingValue.Value = newAttributeValue.Value;
                                    rockContext.Entry( existingValue ).State = EntityState.Modified;
                                }
                            }

                            if ( !groupMember.Person.Aliases.Any( a => a.AliasPersonId == groupMember.Person.Id ) )
                            {
                                groupMember.Person.Aliases.Add( new PersonAlias
                                {
                                    AliasPersonId = groupMember.Person.Id,
                                    AliasPersonGuid = groupMember.Person.Guid,
                                    ForeignId = groupMember.Person.ForeignId,
                                    ForeignKey = groupMember.Person.ForeignKey
                                } );
                            }

                            if ( groupMember.GroupRoleId != childRoleId )
                            {
                                groupMember.Person.GivingGroupId = newFamilyGroup.Id;
                            }

                            if ( visitorsExist )
                            {
                                // Retrieve or create the group this person is an owner of
                                var ownerGroup = groupMemberService.Queryable()
                                    .Where( m => m.PersonId == groupMember.Person.Id && m.GroupRoleId == ownerRole.Id )
                                    .Select( m => m.Group )
                                    .FirstOrDefault();

                                if ( ownerGroup == null )
                                {
                                    var ownerGroupMember = new GroupMember();
                                    ownerGroupMember.PersonId = groupMember.Person.Id;
                                    ownerGroupMember.GroupRoleId = ownerRole.Id;

                                    ownerGroup = new Group();
                                    ownerGroup.Name = ownerRole.GroupType.Name;
                                    ownerGroup.GroupTypeId = ownerRole.GroupTypeId.Value;
                                    ownerGroup.Members.Add( ownerGroupMember );
                                    rockContext.Groups.Add( ownerGroup );
                                }

                                // if this is a visitor, then add relationships to the family member(s)
                                if ( visitorList.Where( v => v.ForeignId == newFamilyGroup.ForeignId )
                                        .Any( v => v.Members.Any( m => m.Person.ForeignId.Equals( groupMember.Person.ForeignId ) ) ) )
                                {
                                    var familyMembers = familyGroups.Except( visitorList ).SelectMany( g => g.Members );
                                    foreach ( var familyMember in familyMembers.Select( m => m.Person ) )
                                    {
                                        var invitedByMember = new GroupMember();
                                        invitedByMember.PersonId = familyMember.Id;
                                        invitedByMember.GroupRoleId = invitedByRoleId;
                                        ownerGroup.Members.Add( invitedByMember );

                                        if ( groupMember.Person.Age < 18 && familyMember.Age > 18 )
                                        {
                                            var allowCheckinMember = new GroupMember();
                                            allowCheckinMember.PersonId = familyMember.Id;
                                            allowCheckinMember.GroupRoleId = allowCheckInByRoleId;
                                            ownerGroup.Members.Add( allowCheckinMember );
                                        }
                                    }
                                }
                                else
                                {   // not a visitor, add the visitors to the family member's known relationship
                                    var visitors = visitorList.Where( v => v.ForeignId == newFamilyGroup.ForeignId )
                                        .SelectMany( g => g.Members ).ToList();
                                    foreach ( var visitor in visitors.Select( g => g.Person ) )
                                    {
                                        var inviteeMember = new GroupMember();
                                        inviteeMember.PersonId = visitor.Id;
                                        inviteeMember.GroupRoleId = inviteeRoleId;
                                        ownerGroup.Members.Add( inviteeMember );

                                        // if visitor can be checked in and this person is considered an adult
                                        if ( visitor.Age < 18 && groupMember.Person.Age > 18 )
                                        {
                                            var canCheckInMember = new GroupMember();
                                            canCheckInMember.PersonId = visitor.Id;
                                            canCheckInMember.GroupRoleId = canCheckInRoleId;
                                            ownerGroup.Members.Add( canCheckInMember );
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                rockContext.ChangeTracker.DetectChanges();
                rockContext.SaveChanges( DisableAuditing );

                if ( familyList.Any() )
                {
                    var familyMembers = familyList.SelectMany( gm => gm.Members );
                    ImportedPeople.AddRange( familyMembers.Select( m => new PersonKeys
                    {
                        PersonAliasId = (int)m.Person.PrimaryAliasId,
                        PersonId = m.Person.Id,
                        IndividualId = m.Person.ForeignId,
                        HouseholdId = m.Group.ForeignId,
                        FamilyRoleId = m.Person.ReviewReasonNote.ConvertToEnum<FamilyRole>()
                    } ).ToList()
                    );
                }

                if ( visitorList.Any() )
                {
                    var visitors = visitorList.SelectMany( gm => gm.Members );
                    ImportedPeople.AddRange( visitors.Select( m => new PersonKeys
                    {
                        PersonAliasId = (int)m.Person.PrimaryAliasId,
                        PersonId = m.Person.Id,
                        IndividualId = m.Person.ForeignId,
                        HouseholdId = m.Group.ForeignId,
                        FamilyRoleId = m.Person.ReviewReasonNote.ConvertToEnum<FamilyRole>()
                    } ).ToList()
                    );
                }
            } );
            // end wrap transaction
        }

        /// <summary>
        /// Maps the users.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        public void MapUsers( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var personService = new PersonService( lookupContext );

            int rockAuthenticatedTypeId = EntityTypeCache.Read( "Rock.Security.Authentication.Database" ).Id;

            int staffGroupId = new GroupService( lookupContext ).GetByGuid( new Guid( Rock.SystemGuid.Group.GROUP_STAFF_MEMBERS ) ).Id;

            int memberGroupRoleId = new GroupTypeRoleService( lookupContext ).Queryable()
                .Where( r => r.Guid.Equals( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_SECURITY_GROUP_MEMBER ) ) )
                .Select( r => r.Id ).FirstOrDefault();

            var userLoginService = new UserLoginService( lookupContext );
            var importedUserCount = userLoginService.Queryable().Count( u => u.ForeignId != null );

            var allUsers = userLoginService.Queryable().AsNoTracking()
                .ToDictionary( t => t.UserName.ToLower().Trim(), t => t.PersonId );

            var newUserLogins = new List<UserLogin>();
            var newStaffMembers = new List<GroupMember>();
            var updatedPersonList = new List<Person>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying user import ({0:N0} found, {1:N0} already exist).", totalRows, importedUserCount ) );

            foreach ( var row in tableData.Where( r => r != null ) )
            {
                int? individualId = row["LinkedIndividualID"] as int?;
                string userName = row["UserLogin"] as string;
                int? userId = row["UserID"] as int?;
                if ( userId != null && individualId != null && !string.IsNullOrWhiteSpace( userName ) && !allUsers.ContainsKey( userName.ToLower().Trim() ) )
                {
                    var personKeys = GetPersonKeys( individualId, null );
                    if ( personKeys != null )
                    {
                        DateTime? createdDate = row["UserCreatedDate"] as DateTime?;
                        string userEmail = row["UserEmail"] as string;
                        string userTitle = row["UserTitle"] as string;
                        string userPhone = row["UserPhone"] as string;
                        bool? isEnabled = row["IsUserEnabled"] as bool?;
                        bool? isStaff = row["IsStaff"] as bool?;
                        bool isActive = isEnabled ?? false;

                        var user = new UserLogin();
                        user.CreatedDateTime = createdDate;
                        user.CreatedByPersonAliasId = ImportPersonAliasId;
                        user.EntityTypeId = rockAuthenticatedTypeId;
                        user.IsConfirmed = isEnabled;
                        user.UserName = userName.Trim();
                        user.PersonId = personKeys.PersonId;
                        user.ForeignKey = userId.ToString();
                        user.ForeignId = userId;

                        if ( isStaff == true )
                        {
                            // add this user to the staff group
                            var staffMember = new GroupMember();
                            staffMember.GroupId = staffGroupId;
                            staffMember.PersonId = personKeys.PersonId;
                            staffMember.GroupRoleId = memberGroupRoleId;
                            staffMember.CreatedDateTime = createdDate;
                            staffMember.CreatedByPersonAliasId = ImportPersonAliasId;
                            staffMember.GroupMemberStatus = isActive ? GroupMemberStatus.Active : GroupMemberStatus.Inactive;

                            newStaffMembers.Add( staffMember );
                        }

                        // set user login email to person's primary email if one isn't set
                        if ( !string.IsNullOrWhiteSpace( userEmail ) && userEmail.IsEmail() )
                        {
                            Person person = null;
                            if ( !updatedPersonList.Any( p => p.Id == personKeys.PersonId ) )
                            {
                                person = personService.Queryable( includeDeceased: true ).FirstOrDefault( p => p.Id == personKeys.PersonId );
                            }
                            else
                            {
                                person = updatedPersonList.FirstOrDefault( p => p.Id == personKeys.PersonId );
                            }

                            if ( person != null && string.IsNullOrWhiteSpace( person.Email ) )
                            {
                                person.Email = userEmail.Left( 75 );
                                person.IsEmailActive = (bool)( isEnabled ?? true );
                                person.EmailPreference = EmailPreference.EmailAllowed;
                                person.EmailNote = userTitle;
                                lookupContext.SaveChanges( DisableAuditing );

                                updatedPersonList.Add( person );
                            }
                        }

                        // other Attributes to save?
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
                            SaveUsers( newUserLogins, newStaffMembers );

                            updatedPersonList.Clear();
                            newUserLogins.Clear();
                            newStaffMembers.Clear();
                            ReportPartialProgress();
                        }
                    }
                }
                else
                {
                    LogException( "User Import", string.Format( "User: {0} - UserName: {1} is not linked to a person or already exists.", userId, userName ) );
                }
            }

            if ( newUserLogins.Any() )
            {
                SaveUsers( newUserLogins, newStaffMembers );
            }

            ReportProgress( 100, string.Format( "Finished user import: {0:N0} users imported.", completed ) );
        }

        /// <summary>
        /// Saves the new user logins.
        /// </summary>
        /// <param name="newUserLogins">The new user logins.</param>
        /// <param name="newStaffMembers">The new staff members.</param>
        /// <param name="updatedPersonList">The updated person list.</param>
        private void SaveUsers( List<UserLogin> newUserLogins, List<GroupMember> newStaffMembers )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.UserLogins.AddRange( newUserLogins );
                rockContext.GroupMembers.AddRange( newStaffMembers );
                rockContext.ChangeTracker.DetectChanges();
                rockContext.SaveChanges( DisableAuditing );
            } );
        }

        /// <summary>
        /// Adds the person attribute.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <param name="person">The person.</param>
        /// <param name="value">The value.</param>
        protected static void AddPersonAttribute( AttributeCache attribute, Person person, string value )
        {
            if ( !string.IsNullOrWhiteSpace( value ) )
            {
                
                person.Attributes.Add( attribute.Key, attribute );
                person.AttributeValues.Add( attribute.Key, new AttributeValueCache()
                {
                    AttributeId = attribute.Id,
                    Value = value
                } );
            }
        }

        /// <summary>
        /// Adds the user login.
        /// </summary>
        /// <param name="authProviderEntityTypeId">The id of the auth provider entity type.</param>
        /// <param name="person">The person.</param>
        /// <param name="value">The value, probably an email.</param>
        protected static void AddUserLogin( int? authProviderEntityTypeId, Person person, string value )
        {
            // Make sure we can create a valid userlogin
            if ( !authProviderEntityTypeId.HasValue || string.IsNullOrWhiteSpace(value) )
            {
                return;
            }

            // Check for an existing userlogin
            if ( person.Users.Any( u => u.EntityTypeId == authProviderEntityTypeId.Value && u.UserName == value ) )
            {
                return;
            }

            // Add a userlogin
            var rockContext = new RockContext();
            var userLoginService = new UserLoginService( rockContext );
            var userLogin = new UserLogin { 
                UserName = value,
                EntityTypeId = authProviderEntityTypeId.Value
            };
            person.Users.Add( userLogin );
            userLoginService.Add( userLogin );
            rockContext.SaveChanges( DisableAuditing );
        }
    }
}