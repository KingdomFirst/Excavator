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
using System.Globalization;
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
        #region Main Methods

        /// <summary>
        /// Loads the individual data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private int LoadIndividuals( CsvDataModel csvData )
        {
            var lookupContext = new RockContext();
            var groupTypeRoleService = new GroupTypeRoleService( lookupContext );
            var groupMemberService = new GroupMemberService( lookupContext );

            // Marital statuses: Married, Single, Separated, etc
            var maritalStatusTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS ), lookupContext ).DefinedValues;

            // Connection statuses: Member, Visitor, Attendee, etc
            var connectionStatusTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS ), lookupContext ).DefinedValues;
            int memberConnectionStatusId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER ) ).Id;
            int visitorConnectionStatusId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR ) ).Id;
            int attendeeConnectionStatusId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_ATTENDEE ) ).Id;

            // Suffix type: Dr., Jr., II, etc
            var suffixTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_SUFFIX ), lookupContext ).DefinedValues;

            // Title type: Mr., Mrs. Dr., etc
            var titleTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_TITLE ), lookupContext ).DefinedValues;

            // Record statuses: Active, Inactive, Pending
            int? recordStatusActiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ), lookupContext ).Id;
            int? recordStatusInactiveId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ), lookupContext ).Id;
            int? recordStatusPendingId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ), lookupContext ).Id;

            // Deceased record status reason (others available: No Activity, Moved, etc)
            var recordStatusDeceasedId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_REASON_DECEASED ) ).Id;

            // Record type: Person
            int? personRecordTypeId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON ), lookupContext ).Id;

            // Group roles: Owner, Adult, Child, others
            GroupTypeRole ownerRole = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_OWNER ) );
            int adultRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;
            int childRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD ) ).Id;

            // Phone types: Home, Work, Mobile
            var numberTypeValues = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_PHONE_TYPE ), lookupContext ).DefinedValues;

            // Timeline note type id
            var noteTimelineTypeId = new NoteTypeService( lookupContext ).Get( new Guid( "7E53487C-D650-4D85-97E2-350EB8332763" ) ).Id;

            // School defined type
            var schoolDefinedType = DefinedTypeCache.Read( new Guid( "576FF1E2-6225-4565-A16D-230E26167A3D" ) );

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

            // Text field type id
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ), lookupContext ).Id;

            // Attribute entity type id
            int attributeEntityTypeId = EntityTypeCache.Read( "Rock.Model.Attribute" ).Id;

            // Visit info category
            var visitInfoCategory = new CategoryService( lookupContext ).GetByEntityTypeId( attributeEntityTypeId )
                    .Where( c => c.Name == "Visit Information" ).FirstOrDefault();

            // Add a Secondary Email attribute if it doesn't exist
            var secondaryEmail = personAttributes.FirstOrDefault( a => a.Key == "SecondaryEmail" );
            if ( secondaryEmail == null )
            {
                secondaryEmail = new Rock.Model.Attribute();
                secondaryEmail.Key = "SecondaryEmail";
                secondaryEmail.Name = "Secondary Email";
                secondaryEmail.FieldTypeId = textFieldTypeId;
                secondaryEmail.EntityTypeId = PersonEntityTypeId;
                secondaryEmail.EntityTypeQualifierValue = string.Empty;
                secondaryEmail.EntityTypeQualifierColumn = string.Empty;
                secondaryEmail.Description = "The secondary email for this person";
                secondaryEmail.DefaultValue = string.Empty;
                secondaryEmail.IsMultiValue = false;
                secondaryEmail.IsRequired = false;
                secondaryEmail.Order = 0;

                lookupContext.Attributes.Add( secondaryEmail );
                secondaryEmail.Categories.Add( visitInfoCategory );
                lookupContext.SaveChanges( true );
            }

            var secondaryEmailAttribute = AttributeCache.Read( secondaryEmail.Id, lookupContext );

            // Add a former name attribute
            var formerName = personAttributes.FirstOrDefault( a => a.Key == "FormerName" );
            if ( formerName == null )
            {
                formerName = new Rock.Model.Attribute();
                formerName.Key = "FormerName";
                formerName.Name = "Former Name";
                formerName.FieldTypeId = textFieldTypeId;
                formerName.EntityTypeId = PersonEntityTypeId;
                formerName.EntityTypeQualifierValue = string.Empty;
                formerName.EntityTypeQualifierColumn = string.Empty;
                formerName.Description = "The former name for this person";
                formerName.DefaultValue = string.Empty;
                formerName.IsMultiValue = false;
                formerName.IsRequired = false;
                formerName.Order = 0;

                lookupContext.Attributes.Add( formerName );
                secondaryEmail.Categories.Add( visitInfoCategory );
                lookupContext.SaveChanges( true );
            }

            var formerNameAttribute = AttributeCache.Read( formerName.Id, lookupContext );

            // Look for custom attributes in the Individual file
            var allFields = csvData.TableNodes.FirstOrDefault().Columns.Select( ( node, index ) => new { node = node, index = index } ).ToList();
            Dictionary<int, string> customAttributes = allFields.Where( f => f.index > Twitter ).ToDictionary( f => f.index, f => f.node.Name );

            // Add any if they don't already exist
            if ( customAttributes.Any() )
            {
                var newAttributes = new List<Rock.Model.Attribute>();
                foreach ( var newAttributePair in customAttributes.Where( ca => !personAttributes.Any( a => a.Name == ca.Value ) ) )
                {
                    var newAttribute = new Rock.Model.Attribute();
                    newAttribute.Name = newAttributePair.Value;
                    newAttribute.Key = newAttributePair.Value.RemoveWhitespace();
                    newAttribute.Description = newAttributePair.Value + " created by CSV import";
                    newAttribute.EntityTypeQualifierValue = string.Empty;
                    newAttribute.EntityTypeQualifierColumn = string.Empty;
                    newAttribute.EntityTypeId = PersonEntityTypeId;
                    newAttribute.FieldTypeId = textFieldTypeId;
                    newAttribute.DefaultValue = string.Empty;
                    newAttribute.IsMultiValue = false;
                    newAttribute.IsGridColumn = false;
                    newAttribute.IsRequired = false;
                    newAttribute.Order = 0;
                    newAttributes.Add( newAttribute );
                }

                lookupContext.Attributes.AddRange( newAttributes );
                lookupContext.SaveChanges( true );
                personAttributes.AddRange( newAttributes );
            }

            var dateFormats = new[] { "MM/dd/yyyy", "MM/dd/yy" };

            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var newVisitorList = new List<Group>();
            var importDate = DateTime.Now;

            int completed = 0;
            ReportProgress( 0, string.Format( "Starting Individual import ({0:N0} already exist).", ImportedPeople.Count( p => p.Members.Any( m => m.Person.ForeignId != null ) ) ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                int groupRoleId = adultRoleId;
                bool isFamilyRelationship = true;

                string rowFamilyId = row[FamilyId];
                string rowPersonId = row[PersonId];
                string rowFamilyName = row[FamilyName];

                if ( !string.IsNullOrWhiteSpace( rowFamilyId ) && rowFamilyId != currentFamilyGroup.ForeignId )
                {
                    currentFamilyGroup = ImportedPeople.FirstOrDefault( p => p.ForeignId == rowFamilyId );
                    if ( currentFamilyGroup == null )
                    {
                        currentFamilyGroup = new Group();
                        currentFamilyGroup.ForeignId = rowFamilyId;
                        currentFamilyGroup.Name = row[FamilyName];
                        currentFamilyGroup.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        currentFamilyGroup.GroupTypeId = FamilyGroupTypeId;
                    }
                }

                // Verify this person isn't already in our data
                var personExists = ImportedPeople.Any( p => p.Members.Any( m => m.Person.ForeignId == rowPersonId ) );
                if ( !personExists )
                {
                    var person = new Person();
                    person.ForeignId = rowPersonId;
                    person.SystemNote = string.Format( "Imported via Excavator on {0}", importDate.ToString() );
                    person.RecordTypeValueId = personRecordTypeId;
                    person.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    string firstName = row[FirstName];
                    person.FirstName = firstName;
                    person.NickName = row[NickName] ?? firstName;
                    person.MiddleName = row[MiddleName];
                    person.LastName = row[LastName];

                    #region Assign values to the Person record

                    DateTime birthDate;
                    if ( DateTime.TryParseExact( row[DateOfBirth], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDate ) )
                    {
                        person.BirthDate = birthDate;
                    }

                    DateTime anniversary;
                    if ( DateTime.TryParseExact( row[Anniversary], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out anniversary ) )
                    {
                        person.AnniversaryDate = anniversary;
                    }

                    string gender = row[Gender];
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

                    string prefix = row[Prefix];
                    if ( !string.IsNullOrWhiteSpace( prefix ) )
                    {
                        prefix = prefix.RemoveSpecialCharacters().Trim();
                        person.TitleValueId = titleTypes.Where( s => prefix == s.Value.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    string suffix = row[Suffix];
                    if ( !string.IsNullOrWhiteSpace( suffix ) )
                    {
                        suffix = suffix.RemoveSpecialCharacters().Trim();
                        person.SuffixValueId = suffixTypes.Where( s => suffix == s.Value.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    string maritalStatus = row[MaritalStatus];
                    if ( !string.IsNullOrWhiteSpace( maritalStatus ) )
                    {
                        person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Value == maritalStatus )
                            .Select( dv => (int?)dv.Id ).FirstOrDefault();
                    }
                    else
                    {
                        person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Value == "Unknown" )
                            .Select( dv => (int?)dv.Id ).FirstOrDefault();
                    }

                    string familyRole = row[FamilyRole];
                    if ( !string.IsNullOrWhiteSpace( familyRole ) )
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

                    string connectionStatus = row[ConnectionStatus];
                    if ( !string.IsNullOrWhiteSpace( connectionStatus ) )
                    {
                        if ( connectionStatus == "Member" )
                        {
                            person.ConnectionStatusValueId = memberConnectionStatusId;
                        }
                        else if ( connectionStatus == "Visitor" )
                        {
                            person.ConnectionStatusValueId = visitorConnectionStatusId;
                        }
                        else if ( connectionStatus == "Deceased" )
                        {
                            person.IsDeceased = true;
                            person.RecordStatusReasonValueId = recordStatusDeceasedId;
                        }
                        else
                        {
                            // look for user-defined connection type or default to Attendee
                            var customConnectionType = connectionStatusTypes.Where( dv => dv.Value == connectionStatus )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();

                            person.ConnectionStatusValueId = customConnectionType ?? attendeeConnectionStatusId;
                            person.RecordStatusValueId = recordStatusActiveId;
                        }
                    }

                    string recordStatus = row[RecordStatus];
                    if ( !string.IsNullOrWhiteSpace( recordStatus ) )
                    {
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
                    }

                    var personNumbers = new Dictionary<string, string>();
                    personNumbers.Add( "Home", row[HomePhone] );
                    personNumbers.Add( "Mobile", row[MobilePhone] );
                    personNumbers.Add( "Work", row[WorkPhone] );
                    string smsAllowed = row[AllowSMS];

                    foreach ( var numberPair in personNumbers.Where( n => !string.IsNullOrWhiteSpace( n.Value ) ) )
                    {
                        var extension = string.Empty;
                        var countryCode = Rock.Model.PhoneNumber.DefaultCountryCode();
                        var normalizedNumber = string.Empty;
                        var countryIndex = numberPair.Value.IndexOf( '+' );
                        int extensionIndex = numberPair.Value.LastIndexOf( 'x' ) > 0 ? numberPair.Value.LastIndexOf( 'x' ) : numberPair.Value.Length;
                        if ( countryIndex >= 0 )
                        {
                            countryCode = numberPair.Value.Substring( countryIndex, countryIndex + 3 ).AsNumeric();
                            normalizedNumber = numberPair.Value.Substring( countryIndex + 3, extensionIndex - 3 ).AsNumeric();
                            extension = numberPair.Value.Substring( extensionIndex );
                        }
                        else if ( extensionIndex > 0 )
                        {
                            normalizedNumber = numberPair.Value.Substring( 0, extensionIndex ).AsNumeric();
                            extension = numberPair.Value.Substring( extensionIndex ).AsNumeric();
                        }
                        else
                        {
                            normalizedNumber = numberPair.Value.AsNumeric();
                        }

                        if ( !string.IsNullOrWhiteSpace( normalizedNumber ) )
                        {
                            var currentNumber = new PhoneNumber();
                            currentNumber.CountryCode = countryCode;
                            currentNumber.CreatedByPersonAliasId = ImportPersonAlias.Id;
                            currentNumber.Extension = extension.Left( 20 );
                            currentNumber.Number = normalizedNumber.Left( 20 );
                            currentNumber.NumberTypeValueId = numberTypeValues.Where( v => v.Value.Equals( numberPair.Key ) )
                                .Select( v => (int?)v.Id ).FirstOrDefault();
                            if ( numberPair.Key == "Mobile" )
                            {
                                switch ( smsAllowed.Trim().ToLower() )
                                {
                                    case "y":
                                    case "yes":
                                    case "active":
                                        currentNumber.IsMessagingEnabled = true;
                                        break;

                                    default:
                                        currentNumber.IsMessagingEnabled = false;
                                        break;
                                }
                            }

                            person.PhoneNumbers.Add( currentNumber );
                        }
                    }

                    // Map Person attributes
                    person.Attributes = new Dictionary<string, AttributeCache>();
                    person.AttributeValues = new Dictionary<string, AttributeValue>();

                    string formerNameValue = row[FormerName];
                    if ( !string.IsNullOrWhiteSpace( formerNameValue ) )
                    {
                        AddPersonAttribute( formerNameAttribute, person, formerNameValue );
                    }

                    bool isEmailActive;
                    switch ( row[IsEmailActive].Trim().ToLower() )
                    {
                        case "n":
                        case "no":
                        case "inactive":
                            isEmailActive = false;
                            break;

                        default:
                            isEmailActive = true;
                            break;
                    }

                    EmailPreference emailPreference;
                    switch ( row[AllowBulkEmail].Trim().ToLower() )
                    {
                        case "n":
                        case "no":
                        case "inactive":
                            emailPreference = EmailPreference.NoMassEmails;
                            break;

                        default:
                            emailPreference = EmailPreference.EmailAllowed;
                            break;
                    }

                    string primaryEmail = row[Email];
                    if ( !string.IsNullOrWhiteSpace( primaryEmail ) )
                    {
                        person.Email = primaryEmail;
                        person.IsEmailActive = isEmailActive;
                        person.EmailPreference = emailPreference;
                    }

                    string secondaryEmailValue = row[SecondaryEmail];
                    if ( !string.IsNullOrWhiteSpace( secondaryEmailValue ) )
                    {
                        AddPersonAttribute( secondaryEmailAttribute, person, secondaryEmailValue );
                    }

                    DateTime membershipDateValue;
                    if ( DateTime.TryParseExact( row[MembershipDate], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out membershipDateValue ) )
                    {
                        AddPersonAttribute( membershipDateAttribute, person, membershipDateValue.ToString() );
                    }

                    DateTime baptismDateValue;
                    if ( DateTime.TryParseExact( row[BaptismDate], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out baptismDateValue ) )
                    {
                        AddPersonAttribute( baptismDateAttribute, person, baptismDateValue.ToString() );
                    }

                    DateTime firstVisitValue;
                    if ( DateTime.TryParseExact( row[FirstVisit], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out firstVisitValue ) )
                    {
                        AddPersonAttribute( firstVisitAttribute, person, firstVisitValue.ToString() );
                    }

                    string previousChurchValue = row[PreviousChurch];
                    if ( !string.IsNullOrWhiteSpace( previousChurchValue ) )
                    {
                        AddPersonAttribute( previousChurchAttribute, person, previousChurchValue );
                    }

                    string positionValue = row[Occupation];
                    if ( !string.IsNullOrWhiteSpace( positionValue ) )
                    {
                        AddPersonAttribute( positionAttribute, person, positionValue );
                    }

                    string employerValue = row[Employer];
                    if ( !string.IsNullOrWhiteSpace( employerValue ) )
                    {
                        AddPersonAttribute( employerAttribute, person, employerValue );
                    }

                    string schoolName = row[School];
                    if ( !string.IsNullOrWhiteSpace( schoolName ) )
                    {
                        // Add school if it doesn't exist
                        Guid schoolGuid;
                        var schoolExists = schoolDefinedType.DefinedValues.Any( s => s.Value.Equals( schoolName ) );
                        if ( !schoolExists )
                        {
                            var newSchool = new DefinedValue();
                            newSchool.DefinedTypeId = schoolDefinedType.Id;
                            newSchool.Value = schoolName;
                            newSchool.Order = 0;

                            lookupContext.DefinedValues.Add( newSchool );
                            lookupContext.SaveChanges();

                            schoolGuid = newSchool.Guid;
                        }
                        else
                        {
                            schoolGuid = schoolDefinedType.DefinedValues.FirstOrDefault( s => s.Value.Equals( schoolName ) ).Guid;
                        }

                        AddPersonAttribute( schoolAttribute, person, schoolGuid.ToString() );
                    }

                    string facebookValue = row[Facebook];
                    if ( !string.IsNullOrWhiteSpace( facebookValue ) )
                    {
                        AddPersonAttribute( facebookAttribute, person, facebookValue );
                    }

                    string twitterValue = row[Twitter];
                    if ( !string.IsNullOrWhiteSpace( twitterValue ) )
                    {
                        AddPersonAttribute( twitterAttribute, person, twitterValue );
                    }

                    string instagramValue = row[Instagram];
                    if ( !string.IsNullOrWhiteSpace( instagramValue ) )
                    {
                        AddPersonAttribute( instagramAttribute, person, instagramValue );
                    }

                    foreach ( var attributePair in customAttributes )
                    {
                        string newAttributeValue = row[attributePair.Key];
                        if ( !string.IsNullOrWhiteSpace( newAttributeValue ) )
                        {
                            int? newAttributeId = personAttributes.Where( a => a.Key == attributePair.Value )
                                .Select( a => (int?)a.Id ).FirstOrDefault();
                            if ( newAttributeId != null )
                            {
                                var newAttribute = AttributeCache.Read( (int)newAttributeId );
                                AddPersonAttribute( newAttribute, person, newAttributeValue );
                            }
                        }
                    }

                    // Add notes to timeline
                    var notePairs = new Dictionary<string, string>();
                    notePairs.Add( "General", row[GeneralNote] );
                    notePairs.Add( "Medical", row[MedicalNote] );
                    notePairs.Add( "Security", row[SecurityNote] );

                    var newNoteList = new List<Note>();
                    foreach ( var notePair in notePairs.Where( n => !string.IsNullOrWhiteSpace( n.Value ) ) )
                    {
                        var newNote = new Note();
                        newNote.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        newNote.CreatedDateTime = importDate;
                        newNote.EntityId = person.Id;
                        newNote.Text = notePair.Value;
                        newNote.NoteTypeId = noteTimelineTypeId;
                        newNote.Caption = string.Format( "{0} Note", notePair.Key );

                        if ( !notePair.Key.Equals( "General" ) )
                        {
                            newNote.IsAlert = true;
                        }

                        newNoteList.Add( newNote );
                    }

                    if ( newNoteList.Any() )
                    {
                        lookupContext.Notes.AddRange( newNoteList );
                        lookupContext.SaveChanges( true );
                    }

                    #endregion

                    var groupMember = new GroupMember();
                    groupMember.Person = person;
                    groupMember.GroupRoleId = groupRoleId;
                    groupMember.GroupMemberStatus = GroupMemberStatus.Active;

                    if ( isFamilyRelationship || currentFamilyGroup.Members.Count() < 1 )
                    {
                        currentFamilyGroup.Members.Add( groupMember );
                        newFamilyList.Add( currentFamilyGroup );
                        completed++;
                    }
                    else
                    {
                        var visitorGroup = new Group();
                        visitorGroup.ForeignId = rowFamilyId.ToString();
                        visitorGroup.Members.Add( groupMember );
                        visitorGroup.GroupTypeId = FamilyGroupTypeId;
                        visitorGroup.Name = person.LastName + " Family";
                        newFamilyList.Add( visitorGroup );
                        completed++;

                        newVisitorList.Add( visitorGroup );
                    }

                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} people imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveIndividuals( newFamilyList, newVisitorList );
                        ReportPartialProgress();
                        newFamilyList.Clear();
                    }
                }
            }

            if ( newFamilyList.Any() )
            {
                SaveIndividuals( newFamilyList, newVisitorList );
            }

            ReportProgress( 0, string.Format( "Finished individual import: {0:N0} people imported.", completed ) );
            return completed;
        }

        /// <summary>
        /// Saves the individuals.
        /// </summary>
        /// <param name="newFamilyList">The family list.</param>
        /// <param name="visitorList">The optional visitor list.</param>
        private void SaveIndividuals( List<Group> newFamilyList, List<Group> visitorList = null )
        {
            if ( newFamilyList.Any() )
            {
                var rockContext = new RockContext();
                rockContext.WrapTransaction( () =>
                {
                    rockContext.Groups.AddRange( newFamilyList );
                    rockContext.SaveChanges( true );

                    ImportedPeople.AddRange( newFamilyList );

                    foreach ( var familyGroups in newFamilyList.GroupBy<Group, string>( g => g.ForeignId ) )
                    {
                        bool visitorsExist = visitorList.Any() && familyGroups.Count() > 1;
                        foreach ( var newFamilyGroup in familyGroups )
                        {
                            foreach ( var person in newFamilyGroup.Members.Select( m => m.Person ) )
                            {
                                foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                                {
                                    var newAttributeValue = person.AttributeValues[attributeCache.Key];

                                    if ( newAttributeValue != null )
                                    {
                                        newAttributeValue.EntityId = person.Id;
                                        rockContext.AttributeValues.Add( newAttributeValue );
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

                                person.GivingGroupId = newFamilyGroup.Id;

                                if ( visitorsExist )
                                {
                                    var groupTypeRoleService = new GroupTypeRoleService( rockContext );
                                    var ownerRole = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_OWNER ) );
                                    int inviteeRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED ) ).Id;
                                    int invitedByRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED_BY ) ).Id;
                                    int canCheckInRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_CAN_CHECK_IN ) ).Id;
                                    int allowCheckInByRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_ALLOW_CHECK_IN_BY ) ).Id;

                                    // Retrieve or create the group this person is an owner of
                                    var ownerGroup = new GroupMemberService( rockContext ).Queryable()
                                        .Where( m => m.PersonId == person.Id && m.GroupRoleId == ownerRole.Id )
                                        .Select( m => m.Group ).FirstOrDefault();
                                    if ( ownerGroup == null )
                                    {
                                        var ownerGroupMember = new GroupMember();
                                        ownerGroupMember.PersonId = person.Id;
                                        ownerGroupMember.GroupRoleId = ownerRole.Id;

                                        ownerGroup = new Group();
                                        ownerGroup.Name = ownerRole.GroupType.Name;
                                        ownerGroup.GroupTypeId = ownerRole.GroupTypeId.Value;
                                        ownerGroup.Members.Add( ownerGroupMember );
                                        rockContext.Groups.Add( ownerGroup );
                                    }

                                    // Visitor, add relationships to the family members
                                    if ( visitorList.Where( v => v.ForeignId == newFamilyGroup.ForeignId )
                                            .Any( v => v.Members.Any( m => m.Person.ForeignId.Equals( person.ForeignId ) ) ) )
                                    {
                                        var familyMembers = familyGroups.Except( visitorList ).SelectMany( g => g.Members );
                                        foreach ( var familyMember in familyMembers )
                                        {
                                            // Add visitor invitedBy relationship
                                            var invitedByMember = new GroupMember();
                                            invitedByMember.PersonId = familyMember.Person.Id;
                                            invitedByMember.GroupRoleId = invitedByRoleId;
                                            ownerGroup.Members.Add( invitedByMember );

                                            if ( person.Age < 18 && familyMember.Person.Age > 15 )
                                            {
                                                // Add visitor allowCheckInBy relationship
                                                var allowCheckinMember = new GroupMember();
                                                allowCheckinMember.PersonId = familyMember.Person.Id;
                                                allowCheckinMember.GroupRoleId = allowCheckInByRoleId;
                                                ownerGroup.Members.Add( allowCheckinMember );
                                            }
                                        }
                                    }
                                    else
                                    {   // Family member, add relationships to the visitor(s)
                                        var familyVisitors = visitorList.Where( v => v.ForeignId == newFamilyGroup.ForeignId ).SelectMany( g => g.Members ).ToList();
                                        foreach ( var visitor in familyVisitors )
                                        {
                                            // Add invited visitor relationship
                                            var inviteeMember = new GroupMember();
                                            inviteeMember.PersonId = visitor.Person.Id;
                                            inviteeMember.GroupRoleId = inviteeRoleId;
                                            ownerGroup.Members.Add( inviteeMember );

                                            if ( visitor.Person.Age < 18 && person.Age > 15 )
                                            {
                                                // Add canCheckIn visitor relationship
                                                var canCheckInMember = new GroupMember();
                                                canCheckInMember.PersonId = visitor.Person.Id;
                                                canCheckInMember.GroupRoleId = canCheckInRoleId;
                                                ownerGroup.Members.Add( canCheckInMember );
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    rockContext.SaveChanges( true );
                } );
            }
        }

        /// <summary>
        /// Adds the person attribute.
        /// </summary>
        /// <param name="attribute">The attribute.</param>
        /// <param name="person">The person.</param>
        /// <param name="attributeValue">The value.</param>
        private static void AddPersonAttribute( AttributeCache attribute, Person person, string attributeValue )
        {
            if ( !string.IsNullOrWhiteSpace( attributeValue ) )
            {
                person.Attributes.Add( attribute.Key, attribute );
                person.AttributeValues.Add( attribute.Key, new AttributeValue()
                {
                    AttributeId = attribute.Id,
                    Value = attributeValue
                } );
            }
        }

        #endregion
    }
}
