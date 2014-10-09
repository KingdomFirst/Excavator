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
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.CSV
{
    /// <summary>
    /// Partial of CSVComponent that holds the People import methods
    /// </summary>
    partial class CSVComponent
    {
        #region Fields

        /// <summary>
        /// The list of families
        /// </summary>
        private List<Group> FamilyList = new List<Group>();

        /// <summary>
        /// The list of visitors
        /// </summary>
        private List<Group> VisitorList = new List<Group>();

        /// <summary>
        /// The list of families
        /// </summary>
        private Dictionary<string, List<Location>> FamilyGroupLocations = new Dictionary<string, List<Location>>();

        /// <summary>
        /// Whether the family file is included
        /// </summary>
        //private bool FamilyFileIsIncluded = false;

        #endregion

        #region Main Methods

        /// <summary>
        /// Maps the family data.
        /// </summary>
        private int MapFamilyData()
        {
            int completed = 0;

            //FamilyFileIsIncluded = CsvDataToImport.FirstOrDefault( n => n.RecordType.Equals( CsvDataModel.RockDataType.FAMILY ) ) == null ? false : true;

            // only import things that the user checked
            List<CsvDataModel> selectedCsvData = CsvDataToImport.Where( c => c.TableNodes.Any( n => n.Checked != false ) ).ToList();

            // Family data is important, so load it first
            if ( selectedCsvData.Any( d => d.RecordType == CsvDataModel.RockDataType.FAMILY ) )
            {
                selectedCsvData.OrderBy( d => d.RecordType != CsvDataModel.RockDataType.FAMILY );
            }

            foreach ( var csvData in selectedCsvData )
            {
                if ( csvData.RecordType == CsvDataModel.RockDataType.FAMILY )
                {
                    completed += LoadFamily( csvData );
                }
                else if ( csvData.RecordType == CsvDataModel.RockDataType.INDIVIDUAL )
                {
                    completed += LoadIndividuals( csvData );
                }
            } //read all files

            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", completed ) );
            return completed;
        }

        /// <summary>
        /// Loads the family data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private int LoadFamily( CsvDataModel csvData )
        {
            // Required variables
            var lookupContext = new RockContext();
            var locationService = new LocationService( lookupContext );
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            int numImportedFamilies = new GroupService( lookupContext ).Queryable()
                .Where( f => f.ForeignId != null ).Count();

            string currentFamilyId = string.Empty;
            int completed = 0;

            ReportProgress( 0, string.Format( "Starting family import ({0:N0} already exist).", numImportedFamilies ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                string rowFamilyId = row[FamilyId];
                if ( !string.IsNullOrWhiteSpace( rowFamilyId ) && rowFamilyId != currentFamilyId )
                {
                    var familyGroup = GetFamilyGroup( rowFamilyId );
                    if ( familyGroup == null )
                    {
                        familyGroup = new Group();
                        familyGroup.ForeignId = rowFamilyId;
                        familyGroup.Name = row[FamilyName] ?? row[FamilyLastName] + " Family";
                        familyGroup.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        familyGroup.GroupTypeId = familyGroupTypeId;

                        var campusName = row[Campus] as string;
                        if ( !string.IsNullOrWhiteSpace( campusName ) )
                        {
                            var familyCampus = CampusList.Where( c => c.Name.StartsWith( campusName ) ).FirstOrDefault();
                            if ( familyCampus == null )
                            {
                                familyCampus = new Campus();
                                familyCampus.IsSystem = false;
                                familyCampus.Name = campusName;
                                lookupContext.Campuses.Add( familyCampus );
                            }

                            familyGroup.CampusId = familyCampus.Id;
                        }

                        // Add the family addresses since they exist in this file
                        var famAddress = row[Address] as string;
                        var famAddress2 = row[Address2] as string;
                        var famCity = row[City] as string;
                        var famState = row[State] as string;
                        var famZip = row[Zip] as string;
                        var famCountry = row[Country] as string;

                        // Use the core Rock location service to add or lookup an address
                        Location primaryAddress = locationService.Get( famAddress, famAddress2, famCity, famState, famZip, famCountry );
                        if ( primaryAddress != null )
                        {
                            primaryAddress.Name = familyGroup.Name + " Home";
                            AddFamilyGroupLocations( primaryAddress, rowFamilyId );
                        }

                        var famSecondAddress = row[SecondaryAddress] as string;
                        var famSecondAddress2 = row[SecondaryAddress2] as string;
                        var famSecondCity = row[SecondaryCity] as string;
                        var famSecondState = row[SecondaryState] as string;
                        var famSecondZip = row[SecondaryZip] as string;
                        var famSecondCountry = row[SecondaryCountry] as string;

                        Location secondaryAddress = locationService.Get( famSecondAddress, famSecondAddress2, famSecondCity, famSecondState, famSecondZip, famSecondCountry );
                        if ( secondaryAddress != null )
                        {
                            secondaryAddress.Name = familyGroup.Name + " Work";
                            AddFamilyGroupLocations( secondaryAddress, rowFamilyId );
                        }

                        FamilyList.Add( familyGroup );
                    }

                    // Set current family id
                    currentFamilyId = rowFamilyId;

                    completed++;
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} families imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveAllFamilyChanges();
                        ReportPartialProgress();
                        FamilyList.Clear();

                        // Reset lookup context
                        lookupContext = new RockContext();
                        locationService = new LocationService( lookupContext );
                    }
                }
            }

            // Check to see if any rows didn't get saved to the database
            if ( FamilyList.Any() )
            {
                SaveAllFamilyChanges();
            }

            ReportProgress( 0, string.Format( "Finished family import: {0:N0} families imported.", completed ) );
            return completed;
        }

        /// <summary>
        /// Saves all family changes.
        /// </summary>
        private void SaveAllFamilyChanges()
        {
            var familiesToSave = FamilyList.Where( f => f.Id == 0 ).ToList();
            if ( familiesToSave.Count() < 1 )
            {
                return;
            }

            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Groups.AddRange( familiesToSave );
                rockContext.SaveChanges();
            } );

            if ( FamilyGroupLocations.Count() > 0 )
            {
                SaveFamilyGroupLocations( familiesToSave );
            }
        }

        /// <summary>
        /// Loads the individual data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private int LoadIndividuals( CsvDataModel csvData )
        {
            var lookupContext = new RockContext();
            var groupTypeRoleService = new GroupTypeRoleService( lookupContext );
            var groupMemberService = new GroupMemberService( lookupContext );
            var dvService = new DefinedValueService( lookupContext );

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
            int noteCommentTypeId = new NoteTypeService( lookupContext ).Get( new Guid( "7E53487C-D650-4D85-97E2-350EB8332763" ) ).Id;

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
            var personAttributes = new AttributeService( lookupContext ).GetByEntityTypeId( PersonEntityTypeId ).ToList();

            // Core attributes: PreviousChurch, Position, Employer, School, etc
            var previousChurchAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "PreviousChurch" ) );
            var employerAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Employer" ) );
            var positionAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Position" ) );
            var firstVisitAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "FirstVisit" ) );
            var schoolAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "School" ) );
            var membershipDateAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "MembershipDate" ) );
            var baptismDateAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "BaptismDate" ) );
            var facebookAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Facebook" ) );
            var twitterAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Twitter" ) );
            var instagramAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Instagram" ) );

            string currentFamilyId = string.Empty;
            Group currentFamilyGroup = null;

            int completed = 0;
            ReportProgress( 0, string.Format( "Starting Individual import ({0:N0} already exist).", ImportedPeople.Count() ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                int groupRoleId = adultRoleId;
                bool isFamilyRelationship = true;
                var memberIdValue = row[MemberId] as string;

                //this matches back to the groups we just loaded
                string rowFamilyId = row[PersonFamilyId] as string;
                string rowFamilyName = row[LastName].ToString() + " Family";

                if ( rowFamilyId != currentFamilyId )
                {
                    currentFamilyGroup = GetFamilyGroup( rowFamilyId );
                    if ( currentFamilyGroup == null )
                    {
                        currentFamilyGroup = new Group();
                        currentFamilyGroup.ForeignId = rowFamilyId;
                        currentFamilyGroup.Name = rowFamilyName;
                        FamilyList.Add( currentFamilyGroup );
                        currentFamilyId = rowFamilyId;
                    }
                }

                // Verify this person isn't already in our data
                var person = currentFamilyGroup.Members.Where( m => m.Person.ForeignId == memberIdValue )
                    .Select( m => m.Person ).FirstOrDefault();
                if ( person == null )
                {
                    person = new Person();
                    person.ForeignId = memberIdValue;
                    person.RecordTypeValueId = personRecordTypeId;
                    person.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    person.GivingGroupId = currentFamilyGroup.Id;
                    person.FirstName = row[FirstName];
                    person.NickName = row[NickName];
                    person.LastName = row[LastName];
                    person.Email = row[Email];

                    #region Assign values to the Person record

                    string activeEmail = row[IsEmailActive] as string;
                    if ( !string.IsNullOrEmpty( activeEmail ) )
                    {
                        bool emailIsActive = false;
                        if ( bool.TryParse( activeEmail, out emailIsActive ) )
                        {
                            person.IsEmailActive = emailIsActive;
                        }
                    }

                    DateTime birthDate;
                    if ( DateTime.TryParse( row[DateOfBirth], out birthDate ) )
                    {
                        person.BirthDate = birthDate;
                    }

                    DateTime anniversary;
                    if ( DateTime.TryParse( row[Anniversary], out anniversary ) )
                    {
                        person.AnniversaryDate = anniversary;
                    }

                    var gender = row[Gender] as string;
                    if ( gender != null )
                    {
                        switch ( gender.Trim().ToLower() )
                        {
                            case "m":
                            case "male":
                                person.Gender = Rock.Model.Gender.Male;
                                break;

                            case "f":
                            case "female":
                                person.Gender = Rock.Model.Gender.Female;
                                break;

                            default:
                                person.Gender = Rock.Model.Gender.Unknown;
                                break;
                        }
                    }

                    var prefix = row[Prefix] as string;
                    if ( prefix != null )
                    {
                        prefix = prefix.RemoveSpecialCharacters().Trim();
                        person.TitleValueId = titleTypes.Where( s => prefix == s.Value.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    var suffix = row[Suffix] as string;
                    if ( suffix != null )
                    {
                        suffix = suffix.RemoveSpecialCharacters().Trim();
                        person.SuffixValueId = suffixTypes.Where( s => suffix == s.Value.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    var maritalStatus = row[MaritalStatus] as string;
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

                    var familyRole = row[FamilyRole] as string;
                    if ( familyRole != null )
                    {
                        if ( familyRole == "Visitor" )
                        {
                            isFamilyRelationship = false;
                        }

                        if ( familyRole == "Child" || person.Age < 18 )
                        {
                            groupRoleId = childRoleId;
                        }
                    }

                    var connectionStatus = row[ConnectionStatus] as string;
                    if ( connectionStatus == "Member" )
                    {
                        person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER ) ).Id;
                    }
                    else if ( connectionStatus == "Visitor" )
                    {
                        person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR ) ).Id;
                    }
                    else if ( connectionStatus == "Deceased" )
                    {
                        person.IsDeceased = true;
                        person.RecordStatusReasonValueId = recordStatusReasons.Where( dv => dv.Value == "Deceased" )
                            .Select( dv => dv.Id ).FirstOrDefault();
                    }
                    else
                    {
                        // look for user-defined connection type or default to Attendee
                        var customConnectionType = connectionStatusTypes.Where( dv => dv.Value == connectionStatus )
                            .Select( dv => (int?)dv.Id ).FirstOrDefault();

                        int attendeeId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( "39F491C5-D6AC-4A9B-8AC0-C431CB17D588" ) ).Id;
                        person.ConnectionStatusValueId = customConnectionType ?? attendeeId;
                        person.RecordStatusValueId = recordStatusActiveId;
                    }

                    var recordStatus = row[RecordStatus] as string;
                    switch ( recordStatus.Trim() )
                    {
                        case "Active":
                            person.RecordStatusValueId = recordStatusActiveId;
                            break;

                        case "Inactive":
                            person.RecordStatusValueId = recordStatusInactiveId;
                            break;

                        default:
                            person.RecordStatusValueId = recordStatusPendingId;
                            break;
                    }

                    // Map Person attributes
                    person.Attributes = new Dictionary<string, AttributeCache>();
                    person.AttributeValues = new Dictionary<string, AttributeValue>();

                    DateTime membershipDateValue;
                    if ( DateTime.TryParse( row[MembershipDate], out membershipDateValue ) )
                    {
                        person.Attributes.Add( membershipDateAttribute.Key, membershipDateAttribute );
                        person.AttributeValues.Add( membershipDateAttribute.Key, new AttributeValue()
                        {
                            AttributeId = membershipDateAttribute.Id,
                            Value = membershipDateValue.ToString()
                        } );
                    }

                    DateTime baptismDateValue;
                    if ( DateTime.TryParse( row[BaptismDate], out baptismDateValue ) )
                    {
                        person.Attributes.Add( baptismDateAttribute.Key, baptismDateAttribute );
                        person.AttributeValues.Add( baptismDateAttribute.Key, new AttributeValue()
                        {
                            AttributeId = baptismDateAttribute.Id,
                            Value = baptismDateValue.ToString()
                        } );
                    }

                    DateTime firstVisitValue;
                    if ( DateTime.TryParse( row[FirstVisit], out firstVisitValue ) )
                    {
                        person.Attributes.Add( firstVisitAttribute.Key, firstVisitAttribute );
                        person.AttributeValues.Add( firstVisitAttribute.Key, new AttributeValue()
                        {
                            AttributeId = firstVisitAttribute.Id,
                            Value = firstVisitValue.ToString()
                        } );
                    }

                    var previousChurchValue = row[PreviousChurch] as string;
                    if ( previousChurchValue != null )
                    {
                        person.Attributes.Add( previousChurchAttribute.Key, previousChurchAttribute );
                        person.AttributeValues.Add( previousChurchAttribute.Key, new AttributeValue()
                        {
                            AttributeId = previousChurchAttribute.Id,
                            Value = previousChurchValue
                        } );
                    }

                    var position = row[Occupation] as string;
                    if ( position != null )
                    {
                        person.Attributes.Add( positionAttribute.Key, positionAttribute );
                        person.AttributeValues.Add( positionAttribute.Key, new AttributeValue()
                        {
                            AttributeId = positionAttribute.Id,
                            Value = position
                        } );
                    }

                    var employerValue = row[Employer] as string;
                    if ( employerValue != null )
                    {
                        person.Attributes.Add( employerAttribute.Key, employerAttribute );
                        person.AttributeValues.Add( employerAttribute.Key, new AttributeValue()
                        {
                            AttributeId = employerAttribute.Id,
                            Value = employerValue
                        } );
                    }

                    var schoolValue = row[School] as string;
                    if ( schoolValue != null )
                    {
                        person.Attributes.Add( schoolAttribute.Key, schoolAttribute );
                        person.AttributeValues.Add( schoolAttribute.Key, new AttributeValue()
                        {
                            AttributeId = schoolAttribute.Id,
                            Value = schoolValue
                        } );
                    }

                    var facebookValue = row[Facebook] as string;
                    if ( facebookValue != null )
                    {
                        person.Attributes.Add( facebookAttribute.Key, facebookAttribute );
                        person.AttributeValues.Add( facebookAttribute.Key, new AttributeValue()
                        {
                            AttributeId = facebookAttribute.Id,
                            Value = facebookValue
                        } );
                    }

                    var twitterValue = row[Twitter] as string;
                    if ( twitterValue != null )
                    {
                        person.Attributes.Add( twitterAttribute.Key, twitterAttribute );
                        person.AttributeValues.Add( twitterAttribute.Key, new AttributeValue()
                        {
                            AttributeId = twitterAttribute.Id,
                            Value = twitterValue
                        } );
                    }

                    var instagramValue = row[Instagram] as string;
                    if ( instagramValue != null )
                    {
                        person.Attributes.Add( instagramAttribute.Key, instagramAttribute );
                        person.AttributeValues.Add( instagramAttribute.Key, new AttributeValue()
                        {
                            AttributeId = instagramAttribute.Id,
                            Value = instagramValue
                        } );
                    }

                    #endregion

                    var groupMember = new GroupMember();
                    groupMember.Person = person;
                    groupMember.GroupRoleId = groupRoleId;
                    groupMember.GroupMemberStatus = GroupMemberStatus.Active;

                    if ( isFamilyRelationship || currentFamilyGroup.Members.Count() < 1 )
                    {
                        currentFamilyGroup.Members.Add( groupMember );
                        completed++;
                    }
                    else
                    {
                        var visitorGroup = new Group();
                        visitorGroup.ForeignId = rowFamilyId.ToString();
                        visitorGroup.Members.Add( groupMember );
                        visitorGroup.GroupTypeId = familyGroupTypeId;
                        visitorGroup.Name = person.LastName + " Family";
                        VisitorList.Add( visitorGroup );
                        completed++;
                    }

                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} people imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveIndividualChanges();
                        ReportPartialProgress();
                    }
                }
            }

            SaveIndividualChanges();

            return completed;
        }

        /// <summary>
        /// Saves the individual changes.
        /// </summary>
        private void SaveIndividualChanges()
        {
            var rockContext = new RockContext();

            var peopleToSave = FamilyList.Where( f => f.Members.Any( p => p.Person.Id == 0 ) )
                .SelectMany( f => f.Members ).ToList();
            if ( peopleToSave.Count() < 1 )
            {
                return;
            }

            //var groupMemberService = new GroupMemberService( rockContext );
            //var groupTypeRoleService = new GroupTypeRoleService( rockContext );

            //var ownerRole = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_OWNER ) );
            //int inviteeRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED ) ).Id;
            //int invitedByRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED_BY ) ).Id;
            //int canCheckInRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_CAN_CHECK_IN ) ).Id;
            //int allowCheckInByRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_ALLOW_CHECK_IN_BY ) ).Id;

            rockContext.WrapTransaction( () =>
            {
                rockContext.People.AddRange( peopleToSave.Select( m => m.Person ) );
                rockContext.SaveChanges( true );

                foreach ( var familyGroups in FamilyList.GroupBy<Group, int?>( g => g.ForeignId.AsType<int?>() ) )
                {
                    bool visitorsExist = familyGroups.Count() > 1;
                    foreach ( var newFamilyGroup in familyGroups )
                    {
                        foreach ( var person in newFamilyGroup.Members.Select( m => m.Person ) )
                        {
                            foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                            {
                                var newValue = person.AttributeValues[attributeCache.Key];
                                if ( newValue != null )
                                {
                                    newValue.EntityId = person.Id;
                                    rockContext.AttributeValues.Add( newValue );
                                }
                            }

                            if ( !person.Aliases.Any( a => a.AliasPersonId == person.Id ) )
                            {
                                person.Aliases.Add( new PersonAlias
                                {
                                    AliasPersonId = person.Id,
                                    AliasPersonGuid = person.Guid
                                } );
                            }

                            //if ( visitorsExist )
                            //{
                            //    // Retrieve or create the group this person is an owner of
                            //    var ownerGroup = groupMemberService.Queryable()
                            //        .Where( m => m.PersonId == person.Id && m.GroupRoleId == ownerRole.Id )
                            //        .Select( m => m.Group )
                            //        .FirstOrDefault();

                            //    if ( ownerGroup == null )
                            //    {
                            //        var ownerGroupMember = new GroupMember();
                            //        ownerGroupMember.PersonId = person.Id;
                            //        ownerGroupMember.GroupRoleId = ownerRole.Id;

                            //        ownerGroup = new Group();
                            //        ownerGroup.Name = ownerRole.GroupType.Name;
                            //        ownerGroup.GroupTypeId = ownerRole.GroupTypeId.Value;
                            //        ownerGroup.Members.Add( ownerGroupMember );
                            //        rockContext.Groups.Add( ownerGroup );
                            //    }

                            //    // if this is a visitor, then add proper relationships to the family member
                            //    if ( VisitorList.Where( v => v.ForeignId == newFamilyGroup.ForeignId )
                            //            .Any( v => v.Members.Any( m => m.Person.ForeignId.Equals( person.Id ) ) ) )
                            //    {
                            //        var familyMembers = familyGroups.Except( VisitorList ).SelectMany( g => g.Members );
                            //        foreach ( var familyMember in familyMembers.Select( m => m.Person ) )
                            //        {
                            //            var invitedByMember = new GroupMember();
                            //            invitedByMember.PersonId = familyMember.Id;
                            //            invitedByMember.GroupRoleId = invitedByRoleId;
                            //            ownerGroup.Members.Add( invitedByMember );

                            //            if ( person.Age < 18 && familyMember.Age > 15 )
                            //            {
                            //                var allowCheckinMember = new GroupMember();
                            //                allowCheckinMember.PersonId = familyMember.Id;
                            //                allowCheckinMember.GroupRoleId = allowCheckInByRoleId;
                            //                ownerGroup.Members.Add( allowCheckinMember );
                            //            }
                            //        }
                            //    }
                            //    else
                            //    {   // not a visitor, add the visitors to the family member's known relationship
                            //        var visitors = VisitorList.Where( v => v.ForeignId == newFamilyGroup.ForeignId ).SelectMany( g => g.Members );
                            //        foreach ( var visitor in visitors.Select( g => g.Person ) )
                            //        {
                            //            var inviteeMember = new GroupMember();
                            //            inviteeMember.PersonId = visitor.Id;
                            //            inviteeMember.GroupRoleId = inviteeRoleId;
                            //            ownerGroup.Members.Add( inviteeMember );

                            //            // if visitor can be checked in and this person is considered an adult
                            //            if ( visitor.Age < 18 && person.Age > 15 )
                            //            {
                            //                var canCheckInMember = new GroupMember();
                            //                canCheckInMember.PersonId = visitor.Id;
                            //                canCheckInMember.GroupRoleId = canCheckInRoleId;
                            //                ownerGroup.Members.Add( canCheckInMember );
                            //            }
                            //        }
                            //    }
                            //}
                        }
                    }
                }

                rockContext.SaveChanges( true );
            } );
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Adds the family locations.
        /// </summary>
        /// <param name="familyAddress">The family address.</param>
        /// <param name="rowFamilyId">The row family identifier.</param>
        private void AddFamilyGroupLocations( Location familyAddress, string rowFamilyId )
        {
            List<Location> locations = null;
            if ( FamilyGroupLocations.Keys.Contains( rowFamilyId ) )
            {
                locations = FamilyGroupLocations[rowFamilyId];
            }

            if ( locations == null )
            {
                locations = new List<Location>();
                FamilyGroupLocations.Add( rowFamilyId, locations );
            }

            locations.Add( familyAddress );
        }

        /// <summary>
        /// Consolidate logic that's accessed from multiple locations,
        /// see if a family exists (either in the list or in the table)
        /// and return it if it does.
        /// </summary>
        /// <param name="familyid"></param>
        /// <returns></returns>
        private Group GetFamilyGroup( string familyid )
        {
            //if its not in familylist, check the database
            var group = FamilyList.Where( n => n.ForeignId.Equals( familyid, StringComparison.OrdinalIgnoreCase ) ).FirstOrDefault();
            if ( group == null )
            {
                using ( var rockContext = new RockContext() )
                {
                    group = rockContext.Groups.Include( "Members" ).Where( n => n.ForeignId == familyid ).FirstOrDefault();
                }
            }
            return group;
        }

        #endregion
    }
}
