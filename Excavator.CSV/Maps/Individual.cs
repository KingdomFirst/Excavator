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
using static Excavator.Utility.CachedTypes;
using static Excavator.Utility.Extensions;

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

            // Marital statuses: Married, Single, Separated, etc
            var maritalStatusTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS ), lookupContext ).DefinedValues;

            // Connection statuses: Member, Visitor, Attendee, etc
            var connectionStatusTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS ), lookupContext ).DefinedValues;

            // Suffix types: Dr., Jr., II, etc
            var suffixTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_SUFFIX ), lookupContext ).DefinedValues;

            // Title types: Mr., Mrs. Dr., etc
            var titleTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_TITLE ), lookupContext ).DefinedValues;

            // Group roles: Owner, Adult, Child, others
            var familyRoles = GroupTypeCache.Read( new Guid( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY ), lookupContext ).Roles;

            // Phone types: Home, Work, Mobile
            var numberTypeValues = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_PHONE_TYPE ), lookupContext ).DefinedValues;

            // School Person attribute
            var schoolAttribute = FindEntityAttribute( lookupContext, "Education", "School", PersonEntityTypeId );

            // Visit info category
            var visitInfoCategory = new CategoryService( lookupContext ).GetByEntityTypeId( AttributeEntityTypeId )
                    .Where( c => c.Name == "Visit Information" ).FirstOrDefault();

            // Look for custom attributes in the Individual file
            var allFields = csvData.TableNodes.FirstOrDefault().Children.Select( ( node, index ) => new { node = node, index = index } ).ToList();
            var customAttributes = allFields
                .Where( f => f.index > SecurityNote )
                .ToDictionary( f => f.index, f => f.node.Name );

            var personAttributes = new List<Rock.Model.Attribute>();

            // Add any attributes if they don't already exist
            if ( customAttributes.Any() )
            {
                foreach ( var newAttributePair in customAttributes.Where( ca => !personAttributes.Any( a => a.Key == ca.Value ) ) )
                {
                    var newAttribute = AddEntityAttribute( lookupContext, PersonEntityTypeId, string.Empty, string.Empty,
                        newAttributePair.Value.RemoveWhitespace(), string.Empty, newAttributePair.Value, string.Empty,
                        TextFieldTypeId, true, null, null, ImportPersonAliasId
                    );

                    personAttributes.Add( newAttribute );
                }
            }

            // Set the supported date formats
            var dateFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "MM/dd/yy" };

            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var newVisitorList = new List<Group>();
            var newNoteList = new List<Note>();

            var completed = 0;
            var newFamilies = 0;
            var newPeople = 0;
            ReportProgress( 0, string.Format( "Starting Individual import ({0:N0} already exist).", ImportedPeopleKeys.Count() ) );

            string[] row;
            row = csvData.Database.FirstOrDefault();
            while ( row != null )
            {
                int? groupRoleId = null;
                var isFamilyRelationship = true;

                var rowFamilyName = row[FamilyName];
                var rowFamilyKey = row[FamilyId];
                var rowPersonKey = row[PersonId];
                var rowFamilyId = rowFamilyKey.AsType<int?>();
                var rowPersonId = rowPersonKey.AsType<int?>();

                // Check that this person isn't already in our data
                var newPerson = true;
                if ( ImportedPeopleKeys.Count() > 0 )
                {
                    var personKeys = GetPersonKeys( rowPersonKey );
                    if ( personKeys != null )
                    {
                        newPerson = false;
                    }
                }

                if ( newPerson )
                {
                    #region person create

                    var person = new Person
                    {
                        ForeignKey = rowPersonKey,
                        ForeignId = rowPersonId,
                        SystemNote = string.Format( "Imported via Excavator on {0}", ImportDateTime ),
                        RecordTypeValueId = PersonRecordTypeId,
                        CreatedByPersonAliasId = ImportPersonAliasId
                    };
                    var firstName = row[FirstName].Left( 50 );
                    var nickName = row[NickName].Left( 50 );
                    person.FirstName = firstName;
                    person.NickName = string.IsNullOrWhiteSpace( nickName ) ? firstName : nickName;
                    person.MiddleName = row[MiddleName].Left( 50 );
                    person.LastName = row[LastName].Left( 50 );

                    var createdDateValue = ParseDateOrDefault( row[CreatedDate], null );
                    if ( createdDateValue.HasValue )
                    {
                        person.CreatedDateTime = createdDateValue;
                        person.ModifiedDateTime = ImportDateTime;
                    }
                    else
                    {
                        person.CreatedDateTime = ImportDateTime;
                        person.ModifiedDateTime = ImportDateTime;
                    }

                    var birthDate = ParseDateOrDefault( row[DateOfBirth], null );
                    if ( birthDate.HasValue )
                    {
                        person.BirthDay = ( (DateTime)birthDate ).Day;
                        person.BirthMonth = ( (DateTime)birthDate ).Month;
                        person.BirthYear = ( (DateTime)birthDate ).Year;
                    }

                    var graduationDate = ParseDateOrDefault( row[GraduationDate], null );
                    if ( graduationDate.HasValue )
                    {
                        person.GraduationYear = ( (DateTime)graduationDate ).Year;
                    }

                    var anniversary = ParseDateOrDefault( row[Anniversary], null );
                    if ( anniversary.HasValue )
                    {
                        person.AnniversaryDate = anniversary;
                    }

                    var gender = row[Gender];
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

                    var prefix = row[Prefix];
                    if ( !string.IsNullOrWhiteSpace( prefix ) )
                    {
                        prefix = prefix.RemoveSpecialCharacters();
                        person.TitleValueId = titleTypes.Where( s => prefix.Equals( s.Value.RemoveSpecialCharacters(), StringComparison.CurrentCultureIgnoreCase ) )
                            .Select( s => (int?)s.Id ).FirstOrDefault();

                        if ( !person.TitleValueId.HasValue )
                        {
                            var newTitle = AddDefinedValue( lookupContext, Rock.SystemGuid.DefinedType.PERSON_TITLE, prefix );
                            if ( newTitle != null )
                            {
                                titleTypes.Add( newTitle );
                                person.TitleValueId = newTitle.Id;
                            }
                        }
                    }

                    var suffix = row[Suffix];
                    if ( !string.IsNullOrWhiteSpace( suffix ) )
                    {
                        suffix = suffix.RemoveSpecialCharacters();
                        person.SuffixValueId = suffixTypes.Where( s => suffix.Equals( s.Value.RemoveSpecialCharacters(), StringComparison.CurrentCultureIgnoreCase ) )
                            .Select( s => (int?)s.Id ).FirstOrDefault();

                        if ( !person.SuffixValueId.HasValue )
                        {
                            var newSuffix = AddDefinedValue( lookupContext, Rock.SystemGuid.DefinedType.PERSON_SUFFIX, suffix );
                            if ( newSuffix != null )
                            {
                                suffixTypes.Add( newSuffix );
                                person.SuffixValueId = newSuffix.Id;
                            }
                        }
                    }

                    var maritalStatus = row[MaritalStatus];
                    if ( !string.IsNullOrWhiteSpace( maritalStatus ) )
                    {
                        maritalStatus = maritalStatus.RemoveSpecialCharacters();
                        person.MaritalStatusValueId = maritalStatusTypes.Where( s => maritalStatus.Equals( s.Value.RemoveSpecialCharacters(), StringComparison.CurrentCultureIgnoreCase ) )
                            .Select( dv => (int?)dv.Id ).FirstOrDefault();

                        if ( !person.MaritalStatusValueId.HasValue )
                        {
                            var newMaritalStatus = AddDefinedValue( lookupContext, Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS, maritalStatus );
                            if ( newMaritalStatus != null )
                            {
                                maritalStatusTypes.Add( newMaritalStatus );
                                person.MaritalStatusValueId = newMaritalStatus.Id;
                            }
                        }
                    }

                    if ( person.MaritalStatusValueId == null )
                    {
                        person.MaritalStatusValueId = maritalStatusTypes.Where( dv => dv.Value.Equals( "Unknown", StringComparison.CurrentCultureIgnoreCase ) )
                            .Select( dv => (int?)dv.Id ).FirstOrDefault();
                    }

                    var familyRole = row[FamilyRole];
                    if ( !string.IsNullOrWhiteSpace( familyRole ) )
                    {
                        familyRole = familyRole.RemoveSpecialCharacters().Trim();
                        groupRoleId = familyRoles.Where( dv => string.Equals( dv.Name, familyRole, StringComparison.CurrentCultureIgnoreCase ) )
                            .Select( dv => (int?)dv.Id ).FirstOrDefault();

                        if ( !groupRoleId.HasValue )
                        {
                            AddGroupRole( lookupContext, Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY, familyRole );
                            familyRoles = GroupTypeCache.Read( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY ).Roles;
                            groupRoleId = familyRoles.Where( dv => dv.Name == familyRole )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                        }

                        if ( familyRole.Equals( "Visitor", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            isFamilyRelationship = false;
                        }
                    }

                    if ( groupRoleId == null )
                    {
                        groupRoleId = FamilyAdultRoleId;
                    }

                    var recordStatus = row[RecordStatus];
                    if ( !string.IsNullOrWhiteSpace( recordStatus ) )
                    {
                        switch ( recordStatus.Trim().ToLower() )
                        {
                            case "active":
                                person.RecordStatusValueId = ActivePersonRecordStatusId;
                                break;

                            case "inactive":
                                person.RecordStatusValueId = InactivePersonRecordStatusId;
                                break;

                            default:
                                person.RecordStatusValueId = PendingPersonRecordStatusId;
                                break;
                        }
                    }
                    else
                    {
                        person.RecordStatusValueId = ActivePersonRecordStatusId;
                    }

                    var connectionStatus = row[ConnectionStatus];
                    if ( !string.IsNullOrWhiteSpace( connectionStatus ) )
                    {
                        if ( connectionStatus.Equals( "Member", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            person.ConnectionStatusValueId = MemberConnectionStatusId;
                        }
                        else if ( connectionStatus.Equals( "Visitor", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            person.ConnectionStatusValueId = VisitorConnectionStatusId;
                        }
                        else if ( connectionStatus.Equals( "Business", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            person.RecordTypeValueId = BusinessRecordTypeId;
                        }
                        else if ( connectionStatus.Equals( "Inactive", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            person.RecordStatusValueId = InactivePersonRecordStatusId;
                        }
                        else
                        {
                            // create user-defined connection type if it doesn't exist
                            person.ConnectionStatusValueId = connectionStatusTypes.Where( dv => dv.Value.Equals( connectionStatus, StringComparison.CurrentCultureIgnoreCase ) )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();

                            if ( !person.ConnectionStatusValueId.HasValue )
                            {
                                var newConnectionStatus = AddDefinedValue( lookupContext, Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS, connectionStatus );
                                if ( newConnectionStatus != null )
                                {
                                    connectionStatusTypes.Add( newConnectionStatus );
                                    person.ConnectionStatusValueId = newConnectionStatus.Id;
                                }
                            }
                        }
                    }
                    else
                    {
                        person.ConnectionStatusValueId = VisitorConnectionStatusId;
                    }

                    var isDeceasedValue = row[IsDeceased];
                    if ( !string.IsNullOrWhiteSpace( isDeceasedValue ) )
                    {
                        switch ( isDeceasedValue.Trim().ToLower() )
                        {
                            case "y":
                            case "yes":
                            case "true":
                                person.IsDeceased = true;
                                person.RecordStatusReasonValueId = DeceasedPersonRecordReasonId;
                                person.RecordStatusValueId = InactivePersonRecordStatusId;
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
                    var smsAllowed = row[AllowSMS];

                    foreach ( var numberPair in personNumbers.Where( n => !string.IsNullOrWhiteSpace( n.Value ) && n.Value.AsNumeric().AsType<Int64>() > 0 ) )
                    {
                        var extension = string.Empty;
                        var countryCode = PhoneNumber.DefaultCountryCode();
                        var normalizedNumber = string.Empty;
                        var countryIndex = numberPair.Value.IndexOf( '+' );
                        var extensionIndex = numberPair.Value.LastIndexOf( 'x' ) > 0 ? numberPair.Value.LastIndexOf( 'x' ) : numberPair.Value.Length;
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
                            currentNumber.Number = normalizedNumber.TrimStart( new char[] { '0' } ).Left( 20 );
                            currentNumber.NumberFormatted = PhoneNumber.FormattedNumber( currentNumber.CountryCode, currentNumber.Number );
                            currentNumber.NumberTypeValueId = numberTypeValues.Where( v => v.Value.Equals( numberPair.Key, StringComparison.CurrentCultureIgnoreCase ) )
                                .Select( v => (int?)v.Id ).FirstOrDefault();
                            if ( numberPair.Key == "Mobile" )
                            {
                                switch ( smsAllowed.Trim().ToLower() )
                                {
                                    case "y":
                                    case "yes":
                                    case "active":
                                    case "true":
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
                    var primaryEmail = row[Email].Trim().Left( 75 );
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

                    var schoolName = row[School];
                    if ( !string.IsNullOrWhiteSpace( schoolName ) )
                    {
                        AddEntityAttributeValue( lookupContext, schoolAttribute, person, schoolName, null, true );
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

                            var newAttribute = personAttributes.Where( a => a.Key == attributePair.Value.RemoveWhitespace() )
                                .FirstOrDefault();
                            if ( newAttribute != null )
                            {
                                AddEntityAttributeValue( lookupContext, newAttribute, person, newAttributeValue, null, false );
                            }
                        }
                    }

                    // Add notes to timeline
                    var notePairs = new Dictionary<string, string>
                    {
                        { "General", row[GeneralNote] },
                        { "Medical", row[MedicalNote] },
                        { "Security", row[SecurityNote] }
                    };

                    foreach ( var notePair in notePairs.Where( n => !string.IsNullOrWhiteSpace( n.Value ) ) )
                    {
                        var splitNotePair = notePair.Value.Split( '^' );
                        foreach ( string noteValue in splitNotePair )
                        {
                            var newNote = new Note
                            {
                                NoteTypeId = PersonalNoteTypeId,
                                CreatedByPersonAliasId = ImportPersonAliasId,
                                CreatedDateTime = ImportDateTime,
                                Text = noteValue,
                                ForeignKey = rowPersonKey,
                                ForeignId = rowPersonId,
                                Caption = string.Format( "{0} Note", notePair.Key )
                            };

                            if ( noteValue.StartsWith( "[ALERT]", StringComparison.CurrentCultureIgnoreCase ) )
                            {
                                newNote.IsAlert = true;
                            }

                            if ( notePair.Key.Equals( "Security" ) )
                            {
                                // Pastoral note type id
                                var securityNoteType = new NoteTypeService( lookupContext ).Get( PersonEntityTypeId, "Secure Note", true );
                                if ( securityNoteType != null )
                                {
                                    newNote.NoteTypeId = securityNoteType.Id;
                                }
                            }

                            if ( notePair.Key.Equals( "Medical" ) )
                            {
                                newNote.IsAlert = true;
                            }

                            newNoteList.Add( newNote );
                        }
                    }

                    #endregion person create

                    var groupMember = new GroupMember
                    {
                        Person = person,
                        GroupRoleId = (int)groupRoleId,
                        CreatedDateTime = ImportDateTime,
                        ModifiedDateTime = ImportDateTime,
                        CreatedByPersonAliasId = ImportPersonAliasId,
                        GroupMemberStatus = GroupMemberStatus.Active
                    };

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
                    var rowNextFamilyKey = "-1";
                    if ( ( row = csvData.Database.FirstOrDefault() ) != null )
                    {
                        rowNextFamilyKey = row[FamilyId];
                    }

                    newPeople++;
                    completed++;
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} people processed.", completed ) );
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
        private static Group CreateFamilyGroup( string rowFamilyName, string rowFamilyKey )
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
        /// Saves the individuals.
        /// </summary>
        /// <param name="newFamilyList">The family list.</param>
        /// <param name="visitorList">The optional visitor list.</param>
        /// <param name="newNoteList">The new note list.</param>
        private void SaveIndividuals( List<Group> newFamilyList, List<Group> visitorList = null, List<Note> newNoteList = null )
        {
            if ( newFamilyList.Any() )
            {
                var rockContext = new RockContext();
                rockContext.WrapTransaction( () =>
                {
                    rockContext.Groups.AddRange( newFamilyList );
                    rockContext.SaveChanges( DisableAuditing );

                    // #TODO find out how to track family groups without context locks
                    ImportedFamilies.AddRange( newFamilyList );

                    foreach ( var familyGroups in newFamilyList.GroupBy( g => g.ForeignKey ) )
                    {
                        var visitorsExist = visitorList.Any() && familyGroups.Any();
                        foreach ( var newFamilyGroup in familyGroups )
                        {
                            foreach ( var person in newFamilyGroup.Members.Select( m => m.Person ) )
                            {
                                // Set notes on this person
                                var personNotes = newNoteList.Where( n => n.ForeignKey == person.ForeignKey ).ToList();
                                if ( personNotes.Any() )
                                {
                                    personNotes.ForEach( n => n.EntityId = person.Id );
                                }

                                // Set attributes on this person
                                var personAttributeValues = person.Attributes.Select( a => a.Value )
                                .Select( a => new AttributeValue
                                {
                                    AttributeId = a.Id,
                                    EntityId = person.Id,
                                    Value = person.AttributeValues[a.Key].Value
                                } ).ToList();

                                rockContext.AttributeValues.AddRange( personAttributeValues );

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
                                    // Retrieve or create the group this person is an owner of
                                    var ownerGroup = new GroupMemberService( rockContext ).Queryable()
                                        .Where( m => m.PersonId == person.Id && m.GroupRoleId == KnownRelationshipOwnerRoleId )
                                        .Select( m => m.Group ).FirstOrDefault();
                                    if ( ownerGroup == null )
                                    {
                                        var ownerGroupMember = new GroupMember
                                        {
                                            PersonId = person.Id,
                                            GroupRoleId = KnownRelationshipOwnerRoleId
                                        };

                                        ownerGroup = new Group
                                        {
                                            Name = KnownRelationshipGroupType.Name,
                                            GroupTypeId = KnownRelationshipGroupType.Id
                                        };
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
                                            var invitedByMember = new GroupMember
                                            {
                                                PersonId = familyMember.Person.Id,
                                                GroupRoleId = InvitedByKnownRelationshipId
                                            };

                                            ownerGroup.Members.Add( invitedByMember );

                                            if ( person.Age < 18 && familyMember.Person.Age > 15 )
                                            {
                                                // Add visitor allowCheckInBy relationship
                                                var allowCheckinMember = new GroupMember
                                                {
                                                    PersonId = familyMember.Person.Id,
                                                    GroupRoleId = AllowCheckInByKnownRelationshipId
                                                };

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
                                            var inviteeMember = new GroupMember
                                            {
                                                PersonId = visitor.Person.Id,
                                                GroupRoleId = InviteeKnownRelationshipId
                                            };

                                            ownerGroup.Members.Add( inviteeMember );

                                            if ( visitor.Person.Age < 18 && person.Age > 15 )
                                            {
                                                // Add canCheckIn visitor relationship
                                                var canCheckInMember = new GroupMember
                                                {
                                                    PersonId = visitor.Person.Id,
                                                    GroupRoleId = CanCheckInKnownRelationshipId
                                                };

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

                    if ( refreshIndividualListEachCycle )
                    {
                        // add reference to imported people now that we have ID's
                        ImportedPeopleKeys.AddRange(
                            newFamilyList.Where( m => m.ForeignKey != null )
                            .SelectMany( m => m.Members )
                            .Select( p => new PersonKeys
                            {
                                PersonAliasId = (int)p.Person.PrimaryAliasId,
                                GroupForeignId = p.Group.ForeignId,
                                PersonId = p.Person.Id,
                                PersonForeignId = p.Person.ForeignId,
                                PersonForeignKey = p.Person.ForeignKey
                            } )
                        );
                        ImportedPeopleKeys = ImportedPeopleKeys.OrderBy( k => k.PersonForeignId ).ThenBy( k => k.PersonForeignKey ).ToList();
                    }
                } );
            }
        }

        #endregion Main Methods
    }
}
