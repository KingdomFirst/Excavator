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
using Excavator.Utility;
using Rock;
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
        private int LoadIndividuals( CSVInstance csvData )
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

            // Personal note type id
            var personalNoteTypeId = new NoteTypeService( lookupContext ).Get( new Guid( Rock.SystemGuid.NoteType.PERSON_TIMELINE_NOTE ) ).Id;

            // School defined type
            var schoolDefinedType = DefinedTypeCache.Read( new Guid( "576FF1E2-6225-4565-A16D-230E26167A3D" ) );

            // Look up existing Person attributes
            var personAttributes = new AttributeService( lookupContext ).GetByEntityTypeId( PersonEntityTypeId ).ToList();
            var schoolAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "School" ) );

            // Text field type id
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ), lookupContext ).Id;
            int dateFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.DATE ), lookupContext ).Id;

            // Attribute entity type id
            int attributeEntityTypeId = EntityTypeCache.Read( "Rock.Model.Attribute" ).Id;

            // Visit info category
            var visitInfoCategory = new CategoryService( lookupContext ).GetByEntityTypeId( attributeEntityTypeId )
                    .Where( c => c.Name == "Visit Information" ).FirstOrDefault();

            // Look for custom attributes in the Individual file
            var allFields = csvData.TableNodes.FirstOrDefault().Children.Select( ( node, index ) => new { node = node, index = index } ).ToList();
            Dictionary<int, string> customAttributes = allFields
                .Where( f => f.index > SecurityNote )
                .ToDictionary( f => f.index, f => f.node.Name.RemoveWhitespace() );

            // Add any attributes if they don't already exist
            if ( customAttributes.Any() )
            {
                var newAttributes = new List<Rock.Model.Attribute>();
                foreach ( var newAttributePair in customAttributes.Where( ca => !personAttributes.Any( a => a.Key == ca.Value ) ) )
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
                lookupContext.SaveChanges( DisableAuditing );
                personAttributes.AddRange( newAttributes );
            }

            // Set the supported date formats
            var dateFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "MM/dd/yy" };

            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var newVisitorList = new List<Group>();
            var newNoteList = new List<Note>();

            int completed = 0;
            int newFamilies = 0;
            int newPeople = 0;
            ReportProgress( 0, string.Format( "Starting Individual import ({0:N0} already exist).", ImportedPeopleKeys.Count() ) );

            string[] row;
            row = csvData.Database.FirstOrDefault();
            while ( row != null )
            {
                int groupRoleId = adultRoleId;
                bool isFamilyRelationship = true;

                string rowFamilyName = row[FamilyName];
                string rowFamilyKey = row[FamilyId];
                string rowPersonKey = row[PersonId];
                int? rowFamilyId = rowFamilyKey.AsType<int?>();
                int? rowPersonId = rowPersonKey.AsType<int?>();

                // Check that this person isn't already in our data
                var personExists = ImportedFamilies.Any( g => g.Members.Any( m => m.Person.ForeignKey == rowPersonKey ) );
                if ( !personExists )
                {
                    #region person create

                    var person = new Person();
                    person.ForeignKey = rowPersonKey;
                    person.ForeignId = rowPersonId;
                    person.SystemNote = string.Format( "Imported via Excavator on {0}", ImportDateTime );
                    person.RecordTypeValueId = personRecordTypeId;
                    person.CreatedByPersonAliasId = ImportPersonAliasId;
                    string firstName = row[FirstName].Left( 50 );
                    string nickName = row[NickName].Left( 50 );
                    person.FirstName = firstName;
                    person.NickName = string.IsNullOrWhiteSpace( nickName ) ? firstName : nickName;
                    person.MiddleName = row[MiddleName].Left( 50 );
                    person.LastName = row[LastName].Left( 50 );

                    DateTime createdDateValue;
                    if ( DateTime.TryParseExact( row[CreatedDate], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out createdDateValue ) )
                    {
                        person.CreatedDateTime = createdDateValue;
                        person.ModifiedDateTime = ImportDateTime;
                    }
                    else
                    {
                        person.CreatedDateTime = ImportDateTime;
                        person.ModifiedDateTime = ImportDateTime;
                    }

                    DateTime birthDate;
                    if ( DateTime.TryParseExact( row[DateOfBirth], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out birthDate ) )
                    {
                        person.BirthDay = birthDate.Day;
                        person.BirthMonth = birthDate.Month;
                        person.BirthYear = birthDate.Year;
                    }

                    DateTime graduationDate;
                    if ( DateTime.TryParseExact( row[GraduationDate], dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out graduationDate ) )
                    {
                        person.GraduationYear = graduationDate.Year;
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
                            .Select( dv => ( int? )dv.Id ).FirstOrDefault();
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
                        switch ( recordStatus.Trim().ToLower() )
                        {
                            case "active":
                                person.RecordStatusValueId = recordStatusActiveId;
                                break;

                            case "inactive":
                                person.RecordStatusValueId = recordStatusInactiveId;
                                break;

                            default:
                                person.RecordStatusValueId = recordStatusPendingId;
                                break;
                        }
                    }

                    string isDeceasedValue = row[IsDeceased];
                    if ( !string.IsNullOrWhiteSpace( isDeceasedValue ) )
                    {
                        switch ( isDeceasedValue.Trim().ToLower() )
                        {
                            case "y":
                            case "yes":
                                person.IsDeceased = true;
                                person.RecordStatusReasonValueId = recordStatusDeceasedId;
                                person.RecordStatusValueId = recordStatusInactiveId;
                                break;

                            default:
                                person.IsDeceased = false;
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
                            normalizedNumber = numberPair.Value.Substring( countryIndex + 3, extensionIndex - 3 ).AsNumeric().TrimStart( new Char[] { '0' } );
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
                            currentNumber.CreatedByPersonAliasId = ImportPersonAliasId;
                            currentNumber.Extension = extension.Left( 20 );
                            currentNumber.Number = normalizedNumber.TrimStart( new Char[] { '0' } ).Left( 20 );
                            currentNumber.NumberFormatted = PhoneNumber.FormattedNumber( currentNumber.CountryCode, currentNumber.Number );
                            currentNumber.NumberTypeValueId = numberTypeValues.Where( v => v.Value.Equals( numberPair.Key ) )
                                .Select( v => ( int? )v.Id ).FirstOrDefault();
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
                    person.AttributeValues = new Dictionary<string, AttributeValueCache>();

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

                    person.EmailPreference = emailPreference;
                    string primaryEmail = row[Email].Trim().Left( 75 );
                    if ( !string.IsNullOrWhiteSpace( primaryEmail ) )
                    {
                        if ( primaryEmail.IsEmail() )
                        {
                            person.Email = primaryEmail;
                            person.IsEmailActive = isEmailActive;
                        }
                        else
                        {
                            LogException( "InvalidPrimaryEmail", string.Format( "PersonId: {0} - Email: {1}", rowPersonKey, primaryEmail ) );
                        }
                    }

                    string schoolName = row[School];
                    if ( !string.IsNullOrWhiteSpace( schoolName ) )
                    {
                        // Add school if it doesn't exist
                        Guid schoolGuid;
                        var schoolExists = lookupContext.DefinedValues.Any( s => s.DefinedTypeId == schoolDefinedType.Id && s.Value.Equals( schoolName ) );
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
                            schoolGuid = lookupContext.DefinedValues.FirstOrDefault( s => s.Value.Equals( schoolName ) ).Guid;
                        }

                        AddPersonAttribute( schoolAttribute, person, schoolGuid.ToString().ToUpper() );
                    }

                    foreach ( var attributePair in customAttributes )
                    {
                        string newAttributeValue = row[attributePair.Key];
                        if ( !string.IsNullOrWhiteSpace( newAttributeValue ) )
                        {
                            // check if this attribute value is a date
                            DateTime valueAsDateTime;
                            if ( DateTime.TryParseExact( newAttributeValue, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out valueAsDateTime ) )
                            {
                                newAttributeValue = valueAsDateTime.ToString( "yyyy-MM-dd" );
                            }

                            int? newAttributeId = personAttributes.Where( a => a.Key == attributePair.Value.RemoveWhitespace() )
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

                    foreach ( var notePair in notePairs.Where( n => !string.IsNullOrWhiteSpace( n.Value ) ) )
                    {
                        var newNote = new Note();
                        newNote.NoteTypeId = personalNoteTypeId;
                        newNote.CreatedByPersonAliasId = ImportPersonAliasId;
                        newNote.CreatedDateTime = ImportDateTime;
                        newNote.Text = notePair.Value;
                        newNote.ForeignKey = rowPersonKey;
                        newNote.ForeignId = rowPersonId;
                        newNote.Caption = string.Format( "{0} Note", notePair.Key );

                        if ( !notePair.Key.Equals( "General" ) )
                        {
                            newNote.IsAlert = true;
                        }

                        newNoteList.Add( newNote );
                    }

                    #endregion person create

                    var groupMember = new GroupMember();
                    groupMember.Person = person;
                    groupMember.GroupRoleId = groupRoleId;
                    groupMember.CreatedDateTime = ImportDateTime;
                    groupMember.ModifiedDateTime = ImportDateTime;
                    groupMember.CreatedByPersonAliasId = ImportPersonAliasId;
                    groupMember.GroupMemberStatus = GroupMemberStatus.Active;

                    if ( rowFamilyKey != currentFamilyGroup.ForeignKey )
                    {
                        // person not part of the previous family, see if that family exists or create a new one
                        currentFamilyGroup = ImportedFamilies.FirstOrDefault( g => g.ForeignKey == rowFamilyKey );
                        if ( currentFamilyGroup == null )
                        {
                            currentFamilyGroup = CreateFamilyGroup( row[FamilyName], rowFamilyKey );
                            newFamilyList.Add( currentFamilyGroup );
                            newFamilies++;
                        }
                        else
                        {
                            lookupContext.Groups.Attach( currentFamilyGroup );
                            lookupContext.Entry( currentFamilyGroup ).State = EntityState.Modified;
                        }

                        currentFamilyGroup.Members.Add( groupMember );
                    }
                    else
                    {
                        // person is part of this family group, check if they're a visitor
                        if ( isFamilyRelationship || currentFamilyGroup.Members.Count() < 1 )
                        {
                            currentFamilyGroup.Members.Add( groupMember );
                        }
                        else
                        {
                            var visitorFamily = CreateFamilyGroup( person.LastName + " Family", rowFamilyKey );
                            visitorFamily.Members.Add( groupMember );
                            newFamilyList.Add( visitorFamily );
                            newVisitorList.Add( visitorFamily );
                            newFamilies++;
                        }
                    }

                    // look ahead 1 row
                    string rowNextFamilyKey = "-1";
                    if ( (row = csvData.Database.FirstOrDefault()) != null )
                    {
                        rowNextFamilyKey = row[FamilyId];
                    }

                    newPeople++;
                    completed++;
                    if ( completed % (ReportingNumber * 10) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} people imported.", completed ) );
                    }

                    if ( newPeople >= ReportingNumber && rowNextFamilyKey != currentFamilyGroup.ForeignKey )
                    {
                        SaveIndividuals( newFamilyList, newVisitorList, newNoteList );
                        lookupContext.SaveChanges();
                        ReportPartialProgress();

                        // Clear out variables
                        currentFamilyGroup = new Group();
                        newFamilyList.Clear();
                        newVisitorList.Clear();
                        newNoteList.Clear();
                        newPeople = 0;
                    }
                }
                else
                {
                    row = csvData.Database.FirstOrDefault();
                }
            }

            // Save any changes to new families
            if ( newFamilyList.Any() )
            {
                SaveIndividuals( newFamilyList, newVisitorList, newNoteList );
            }

            // Save any changes to existing families
            lookupContext.SaveChanges();
            DetachAllInContext( lookupContext );
            lookupContext.Dispose();

            ReportProgress( 0, string.Format( "Finished individual import: {0:N0} families and {1:N0} people added.", newFamilies, completed ) );
            return completed;
        }

        /// <summary>
        /// Creates the family group.
        /// </summary>
        /// <param name="rowFamilyName">Name of the row family.</param>
        /// <param name="rowFamilyKey">The row family identifier.</param>
        /// <returns></returns>
        private Group CreateFamilyGroup( string rowFamilyName, string rowFamilyKey )
        {
            var familyGroup = new Group();
            if ( !string.IsNullOrWhiteSpace( rowFamilyName ) )
            {
                familyGroup.Name = rowFamilyName;
            }
            else
            {
                familyGroup.Name = string.Format( "Family Group {0}", rowFamilyKey );
            }

            familyGroup.CreatedByPersonAliasId = ImportPersonAliasId;
            familyGroup.GroupTypeId = FamilyGroupTypeId;
            familyGroup.ForeignKey = rowFamilyKey;
            familyGroup.ForeignId = rowFamilyKey.AsType<int?>();
            return familyGroup;
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
                person.AttributeValues.Add( attribute.Key, new AttributeValueCache()
                {
                    AttributeId = attribute.Id,
                    Value = attributeValue
                } );
            }
        }

        /// <summary>
        /// Saves the individuals.
        /// </summary>
        /// <param name="newFamilyList">The family list.</param>
        /// <param name="visitorList">The optional visitor list.</param>
        private void SaveIndividuals( List<Group> newFamilyList, List<Group> visitorList = null, List<Note> newNoteList = null )
        {
            if ( newFamilyList.Any() )
            {
                var rockContext = new RockContext();
                rockContext.WrapTransaction( ( ) =>
                {
                    rockContext.Groups.AddRange( newFamilyList );
                    rockContext.SaveChanges( DisableAuditing );

                    ImportedFamilies.AddRange( newFamilyList );

                    foreach ( var familyGroups in newFamilyList.GroupBy<Group, string>( g => g.ForeignKey ) )
                    {
                        bool visitorsExist = visitorList.Any() && familyGroups.Any();
                        foreach ( var newFamilyGroup in familyGroups )
                        {
                            foreach ( var person in newFamilyGroup.Members.Select( m => m.Person ) )
                            {
                                // Set notes on this person
                                if ( newNoteList.Any( n => n.ForeignKey == person.ForeignKey ) )
                                {
                                    newNoteList.Where( n => n.ForeignKey == person.ForeignKey ).ToList()
                                        .ForEach( n => n.EntityId = person.Id );
                                }

                                // Set attributes on this person
                                foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                                {
                                    var existingValue = rockContext.AttributeValues.FirstOrDefault( v => v.Attribute.Key == attributeCache.Key && v.EntityId == person.Id );
                                    var newAttributeValue = person.AttributeValues[attributeCache.Key];

                                    // set the new value and add it to the database
                                    if ( existingValue == null )
                                    {
                                        existingValue = new AttributeValue();
                                        existingValue.AttributeId = newAttributeValue.AttributeId;
                                        existingValue.EntityId = person.Id;
                                        existingValue.Value = newAttributeValue.Value;

                                        rockContext.AttributeValues.Add( existingValue );
                                    }
                                    else
                                    {
                                        existingValue.Value = newAttributeValue.Value;
                                        rockContext.Entry( existingValue ).State = EntityState.Modified;
                                    }
                                }

                                // Set aliases on this person
                                if ( !person.Aliases.Any( a => a.PersonId == person.Id ) )
                                {
                                    person.Aliases.Add( new PersonAlias
                                    {
                                        AliasPersonId = person.Id,
                                        AliasPersonGuid = person.Guid,
                                        ForeignKey = person.ForeignKey,
                                        ForeignId = person.ForeignId,
                                        PersonId = person.Id
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
                                    if ( visitorList.Where( v => v.ForeignKey == newFamilyGroup.ForeignKey )
                                            .Any( v => v.Members.Any( m => m.Person.ForeignKey.Equals( person.ForeignKey ) ) ) )
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
                                        var familyVisitors = visitorList.Where( v => v.ForeignKey == newFamilyGroup.ForeignKey ).SelectMany( g => g.Members ).ToList();
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

                    // Save notes and all changes
                    rockContext.Notes.AddRange( newNoteList );
                    rockContext.SaveChanges( DisableAuditing );
                } );
            }
        }

        #endregion Main Methods
    }
}