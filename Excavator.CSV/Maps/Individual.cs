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
        #region Main Methods

        /// <summary>
        /// Maps the family data.
        /// </summary>
        private int MapFamilyData()
        {
            int completed = 0;

            // only import things that the user checked
            List<CsvDataModel> selectedCsvData = CsvDataToImport.Where( c => c.TableNodes.Any( n => n.Checked != false ) ).ToList();

            // Person data is important, so load it first
            if ( selectedCsvData.Any( d => d.RecordType == CsvDataModel.RockDataType.INDIVIDUAL ) )
            {
                selectedCsvData = selectedCsvData.OrderByDescending( d => d.RecordType == CsvDataModel.RockDataType.INDIVIDUAL ).ToList();
            }

            foreach ( var csvData in selectedCsvData )
            {
                if ( csvData.RecordType == CsvDataModel.RockDataType.INDIVIDUAL )
                {
                    completed += LoadIndividuals( csvData );
                }
                else if ( csvData.RecordType == CsvDataModel.RockDataType.FAMILY )
                {
                    completed += LoadFamily( csvData );
                }
            } //read all files

            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", completed ) );
            return completed;
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

            var numberTypeValues = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_PHONE_TYPE ) ).DefinedValues;
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;

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

            // Add a Secondary Email attribute if it doesn't exist
            var secondaryEmail = personAttributes.FirstOrDefault( a => a.Key == "SecondaryEmail" );
            if ( secondaryEmail == null )
            {
                int attributeEntityTypeId = EntityTypeCache.Read( "Rock.Model.Attribute" ).Id;
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
                var visitInfoCategory = new CategoryService( lookupContext ).GetByEntityTypeId( attributeEntityTypeId )
                    .Where( c => c.Name == "Visit Information" ).FirstOrDefault();
                secondaryEmail.Categories.Add( visitInfoCategory );
                lookupContext.SaveChanges( true );
            }

            var secondaryEmailAttribute = AttributeCache.Read( secondaryEmail.Id );

            // Look for custom attributes in the Individual file
            var allFields = csvData.TableNodes.FirstOrDefault().Columns.Select( ( node, index ) => new { node = node, index = index } ).ToList();
            Dictionary<int, string> customAttributes = allFields.Where( f => f.index > 40 ).ToDictionary( f => f.index, f => f.node.Name );

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

            var currentFamilyGroup = new Group();
            var newFamilyList = new List<Group>();
            var newVisitorList = new List<Group>();

            int completed = 0;
            ReportProgress( 0, string.Format( "Starting Individual import ({0:N0} already exist).", ImportedPeople.Count( p => p.Members.Any( m => m.Person.ForeignId != null ) ) ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                int groupRoleId = adultRoleId;
                bool isFamilyRelationship = true;

                string rowFamilyId = row[FamilyId] as string;
                string rowPersonId = row[PersonId] as string;
                string rowFamilyName = row[FamilyName] as string;

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
                    person.RecordTypeValueId = personRecordTypeId;
                    person.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    person.FirstName = row[FirstName];
                    person.NickName = row[NickName];
                    person.LastName = row[LastName];
                    person.Email = row[Email];

                    #region Assign values to the Person record

                    string activeEmail = row[IsEmailActive] as string;
                    if ( !string.IsNullOrWhiteSpace( activeEmail ) )
                    {
                        bool emailIsActive = false;
                        if ( bool.TryParse( activeEmail, out emailIsActive ) )
                        {
                            person.IsEmailActive = emailIsActive;
                        }
                    }

                    DateTime birthDate;
                    string approximateAge = row[Age] as string;
                    if ( DateTime.TryParse( row[DateOfBirth], out birthDate ) )
                    {
                        person.BirthDate = birthDate;
                    }
                    else if ( !string.IsNullOrWhiteSpace( approximateAge ) )
                    {
                        int age = approximateAge.AsType<int>();
                        person.BirthDate = DateTime.Now.AddYears( -age );
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
                    if ( !string.IsNullOrWhiteSpace( prefix ) )
                    {
                        prefix = prefix.RemoveSpecialCharacters().Trim();
                        person.TitleValueId = titleTypes.Where( s => prefix == s.Value.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    var suffix = row[Suffix] as string;
                    if ( !string.IsNullOrWhiteSpace( suffix ) )
                    {
                        suffix = suffix.RemoveSpecialCharacters().Trim();
                        person.SuffixValueId = suffixTypes.Where( s => suffix == s.Value.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    var maritalStatus = row[MaritalStatus] as string;
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

                    var familyRole = row[FamilyRole] as string;
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

                    var connectionStatus = row[ConnectionStatus] as string;
                    if ( !string.IsNullOrWhiteSpace( connectionStatus ) )
                    {
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

                    var personNumbers = new Dictionary<string, string>();
                    personNumbers.Add( "Home", row[HomePhone] );
                    personNumbers.Add( "Mobile", row[MobilePhone] );
                    personNumbers.Add( "Work", row[WorkPhone] );
                    var smsAllowed = row[AllowSMS] as string;

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

                    var primaryEmail = row[Email] as string;
                    if ( !string.IsNullOrWhiteSpace( primaryEmail ) )
                    {
                        person.Email = primaryEmail;
                        person.IsEmailActive = isEmailActive;
                        person.EmailPreference = emailPreference;
                    }

                    var secondaryEmailValue = row[SecondaryEmail] as string;
                    if ( !string.IsNullOrWhiteSpace( secondaryEmailValue ) )
                    {
                        person.Attributes.Add( secondaryEmailAttribute.Key, secondaryEmailAttribute );
                        person.AttributeValues.Add( secondaryEmailAttribute.Key, new AttributeValue()
                        {
                            AttributeId = secondaryEmailAttribute.Id,
                            Value = secondaryEmailValue
                        } );
                    }

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
                    if ( !string.IsNullOrWhiteSpace( previousChurchValue ) )
                    {
                        person.Attributes.Add( previousChurchAttribute.Key, previousChurchAttribute );
                        person.AttributeValues.Add( previousChurchAttribute.Key, new AttributeValue()
                        {
                            AttributeId = previousChurchAttribute.Id,
                            Value = previousChurchValue
                        } );
                    }

                    var positionValue = row[Occupation] as string;
                    if ( !string.IsNullOrWhiteSpace( positionValue ) )
                    {
                        person.Attributes.Add( positionAttribute.Key, positionAttribute );
                        person.AttributeValues.Add( positionAttribute.Key, new AttributeValue()
                        {
                            AttributeId = positionAttribute.Id,
                            Value = positionValue
                        } );
                    }

                    var employerValue = row[Employer] as string;
                    if ( !string.IsNullOrWhiteSpace( employerValue ) )
                    {
                        person.Attributes.Add( employerAttribute.Key, employerAttribute );
                        person.AttributeValues.Add( employerAttribute.Key, new AttributeValue()
                        {
                            AttributeId = employerAttribute.Id,
                            Value = employerValue
                        } );
                    }

                    var schoolValue = row[School] as string;
                    if ( !string.IsNullOrWhiteSpace( schoolValue ) )
                    {
                        person.Attributes.Add( schoolAttribute.Key, schoolAttribute );
                        person.AttributeValues.Add( schoolAttribute.Key, new AttributeValue()
                        {
                            AttributeId = schoolAttribute.Id,
                            Value = schoolValue
                        } );
                    }

                    var facebookValue = row[Facebook] as string;
                    if ( !string.IsNullOrWhiteSpace( facebookValue ) )
                    {
                        person.Attributes.Add( facebookAttribute.Key, facebookAttribute );
                        person.AttributeValues.Add( facebookAttribute.Key, new AttributeValue()
                        {
                            AttributeId = facebookAttribute.Id,
                            Value = facebookValue
                        } );
                    }

                    var twitterValue = row[Twitter] as string;
                    if ( !string.IsNullOrWhiteSpace( twitterValue ) )
                    {
                        person.Attributes.Add( twitterAttribute.Key, twitterAttribute );
                        person.AttributeValues.Add( twitterAttribute.Key, new AttributeValue()
                        {
                            AttributeId = twitterAttribute.Id,
                            Value = twitterValue
                        } );
                    }

                    var instagramValue = row[Instagram] as string;
                    if ( !string.IsNullOrWhiteSpace( instagramValue ) )
                    {
                        person.Attributes.Add( instagramAttribute.Key, instagramAttribute );
                        person.AttributeValues.Add( instagramAttribute.Key, new AttributeValue()
                        {
                            AttributeId = instagramAttribute.Id,
                            Value = instagramValue
                        } );
                    }

                    foreach ( var attributePair in customAttributes )
                    {
                        var newAttributeValue = row[attributePair.Key] as string;
                        if ( !string.IsNullOrWhiteSpace( newAttributeValue ) )
                        {
                            int? newAttributeId = personAttributes.Where( a => a.Key == attributePair.Value )
                                .Select( a => (int?)a.Id ).FirstOrDefault();
                            if ( newAttributeId != null )
                            {
                                var newAttribute = AttributeCache.Read( (int)newAttributeId );
                                person.Attributes.Add( newAttribute.Key, newAttribute );
                                person.AttributeValues.Add( newAttribute.Key, new AttributeValue()
                                {
                                    AttributeId = newAttribute.Id,
                                    Value = newAttributeValue
                                } );
                            }
                        }
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

        #endregion
    }
}
