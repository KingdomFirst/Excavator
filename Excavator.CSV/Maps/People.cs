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
        private List<Group> familyList = null;

        /// <summary>
        /// The family group
        /// </summary>
        private Group familyGroup = null;

        /// <summary>
        /// Whether the family file is included
        /// </summary>
        private bool FamilyFileIsIncluded = false;

        #endregion

        #region Maps

        /// <summary>
        /// Maps the family data.
        /// </summary>
        private void MapFamilyData()
        {
            familyList = new List<Group>();

            int completed = 0;
            ReportProgress( 0, string.Format( "Adding family data ({0:N0} people already exist).", ImportedPeople.Count() ) );

            FamilyFileIsIncluded = CsvDataToImport.FirstOrDefault( n => n.RecordType.Equals( CsvDataModel.RockDataType.FAMILY ) ) == null ? true : false;

            // only import things that the user checked
            List<CsvDataModel> selectedCsvData = CsvDataToImport.Where( c => c.TableNodes.Any( n => n.Checked != false ) ).ToList();

            foreach ( var csvData in selectedCsvData )
            {
                if ( csvData.RecordType == CsvDataModel.RockDataType.FAMILY )
                {
                    LoadFamily( csvData );
                }
                else
                {
                    LoadIndividuals( csvData );
                }
            } //read all files

            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", completed ) );
        }

        /// <summary>
        /// Loads the family data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private void LoadFamily( CsvDataModel csvData )
        {
            // Family group type id (required)
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            string currentFamilyId = string.Empty;
            int completed = 0;

            do
            {
                var row = csvData.Database.First();
                if ( row != null )
                {
                    string rowFamilyId = row[FamilyId];
                    if ( !string.IsNullOrWhiteSpace( rowFamilyId ) && rowFamilyId != currentFamilyId )
                    {
                        familyGroup = new Group();
                        familyGroup.ForeignId = rowFamilyId;
                        familyGroup.Name = row[FamilyName] ?? row[FamilyLastName] + " Family";
                        familyGroup.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        familyGroup.GroupTypeId = familyGroupTypeId;

                        // Bobbi - campus could be a column in the individual or family file.
                        // Since Rock doesn't support campuses by individual we'll just put it on family;
                        // there's an example in FellowshipOne if you want to put it on individual.

                        var campus = row[Campus] as string;
                        if ( !string.IsNullOrWhiteSpace( campus ) )
                        {
                            familyGroup.CampusId = CampusList.Where( c => c.Name.StartsWith( campus ) )
                                .Select( c => (int?)c.Id ).FirstOrDefault();
                        }

                        familyList.Add( familyGroup );

                        // TODO: Add the family addresses since they exist in this file

                        // Set current family id
                        currentFamilyId = rowFamilyId;
                    }
                    completed++;
                    if ( completed % ReportingNumber < 1 )
                    {
                        RockTransactionScope.WrapTransaction( () =>
                        {
                            var rockContext = new RockContext();
                            rockContext.Groups.AddRange( familyList );
                            rockContext.SaveChanges();
                        } );

                        familyList.Clear();
                        ReportPartialProgress();
                    }
                }
            } while ( csvData.Database.ReadNextRecord() );

            // Check to see if any rows didn't get saved to the database
            if ( familyList.Any() )
            {
                RockTransactionScope.WrapTransaction( () =>
                {
                    var rockContext = new RockContext();
                    rockContext.Groups.AddRange( familyList );
                    rockContext.SaveChanges();
                } );
            }
        }

        /// <summary>
        /// Loads the individual data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private void LoadIndividuals( CsvDataModel csvData )
        {
            var lookupContext = new RockContext();
            var groupTypeRoleService = new GroupTypeRoleService( lookupContext );
            var attributeService = new AttributeService( lookupContext );

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

            // Group roles: Adult, Child, others
            int adultRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;
            int childRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD ) ).Id;

            // Group type: Family
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            // Look up additional Person attributes (existing)
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();

            // Cached attributes: PreviousChurch, Position, Employer, School
            var membershipDateAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "MembershipDate" ) );
            var baptismDateAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "BaptismDate" ) );
            var firstVisitAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "FirstVisit" ) );
            var previousChurchAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "PreviousChurch" ) );
            var employerAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Employer" ) );
            var positionAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Position" ) );
            var schoolAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "School" ) );

            int currentFamilyId = 0;
            int completed = 0;

            do
            {
                var row = csvData.Database.First();
                if ( row != null )
                {
                    int groupRoleId = adultRoleId;
                    var personIdValue = row[PersonId] as string;
                    int rowPersonId = row[PersonId].AsType<int>();
                    int rowFamilyId = row[FamilyId].AsType<int>();
                    var rowFamilyName = row[FamilyName];

                    //keep track of family here if we're not loading a separate family file
                    if ( rowFamilyId > 1 && rowFamilyId != currentFamilyId && FamilyFileIsIncluded )
                    {
                        familyList.Add( familyGroup );
                        familyGroup = new Group();
                        currentFamilyId = rowFamilyId;
                    }

                    Person person = new Person();
                    person.ForeignId = personIdValue;
                    person.RecordTypeValueId = personRecordTypeId;
                    person.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    person.FirstName = row[FirstName];
                    person.NickName = row[NickName];
                    person.LastName = row[LastName];
                    person.Email = row[Email];

                    var activeEmail = row[IsEmailActive] as string;
                    if ( activeEmail != null )
                    {
                        person.IsEmailActive = bool.Parse( activeEmail );
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
                        person.TitleValueId = titleTypes.Where( s => prefix == s.Name.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    var suffix = row[Suffix] as string;
                    if ( suffix != null )
                    {
                        suffix = suffix.RemoveSpecialCharacters().Trim();
                        person.SuffixValueId = suffixTypes.Where( s => suffix == s.Name.RemoveSpecialCharacters() )
                            .Select( s => (int?)s.Id ).FirstOrDefault();
                    }

                    var maritalStatus = row[MaritalStatus] as string;
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

                    var familyRole = row[FamilyRole] as string;
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
                        person.RecordStatusReasonValueId = recordStatusReasons.Where( dv => dv.Name == "Deceased" )
                            .Select( dv => dv.Id ).FirstOrDefault();
                    }
                    else
                    {
                        // look for user-defined connection type or default to Attendee
                        var customConnectionType = connectionStatusTypes.Where( dv => dv.Name == connectionStatus )
                            .Select( dv => (int?)dv.Id ).FirstOrDefault();

                        int attendeeId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( "39F491C5-D6AC-4A9B-8AC0-C431CB17D588" ) ).Id;
                        person.ConnectionStatusValueId = customConnectionType ?? attendeeId;
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
                    person.AttributeValues = new Dictionary<string, List<AttributeValue>>();

                    DateTime membershipDateValue;
                    if ( DateTime.TryParse( row[MembershipDate], out membershipDateValue ) )
                    {
                        person.Attributes.Add( membershipDateAttribute.Key, membershipDateAttribute );
                        person.AttributeValues.Add( membershipDateAttribute.Key, new List<AttributeValue>() );
                        person.AttributeValues[membershipDateAttribute.Key].Add( new AttributeValue()
                        {
                            AttributeId = membershipDateAttribute.Id,
                            Value = membershipDateValue.ToString(),
                            Order = 0
                        } );
                    }

                    DateTime baptismDateValue;
                    if ( DateTime.TryParse( row[BaptismDate], out baptismDateValue ) )
                    {
                        person.Attributes.Add( baptismDateAttribute.Key, baptismDateAttribute );
                        person.AttributeValues.Add( baptismDateAttribute.Key, new List<AttributeValue>() );
                        person.AttributeValues[baptismDateAttribute.Key].Add( new AttributeValue()
                        {
                            AttributeId = baptismDateAttribute.Id,
                            Value = baptismDateValue.ToString(),
                            Order = 0
                        } );
                    }

                    DateTime firstVisitValue;
                    if ( DateTime.TryParse( row[FirstVisit], out firstVisitValue ) )
                    {
                        person.Attributes.Add( firstVisitAttribute.Key, firstVisitAttribute );
                        person.AttributeValues.Add( firstVisitAttribute.Key, new List<AttributeValue>() );
                        person.AttributeValues[firstVisitAttribute.Key].Add( new AttributeValue()
                        {
                            AttributeId = firstVisitAttribute.Id,
                            Value = firstVisitValue.ToString(),
                            Order = 0
                        } );
                    }

                    var previousChurchValue = row[PreviousChurch] as string;
                    if ( previousChurchValue != null )
                    {
                        person.Attributes.Add( previousChurchAttribute.Key, previousChurchAttribute );
                        person.AttributeValues.Add( previousChurchAttribute.Key, new List<AttributeValue>() );
                        person.AttributeValues[previousChurchAttribute.Key].Add( new AttributeValue()
                        {
                            AttributeId = previousChurchAttribute.Id,
                            Value = previousChurchValue,
                            Order = 0
                        } );
                    }

                    var position = row[Occupation] as string;
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

                    var employerValue = row[Employer] as string;
                    if ( employerValue != null )
                    {
                        person.Attributes.Add( employerAttribute.Key, employerAttribute );
                        person.AttributeValues.Add( employerAttribute.Key, new List<AttributeValue>() );
                        person.AttributeValues[employerAttribute.Key].Add( new AttributeValue()
                        {
                            AttributeId = employerAttribute.Id,
                            Value = employerValue,
                            Order = 0
                        } );
                    }

                    var schoolValue = row[School] as string;
                    if ( schoolValue != null )
                    {
                        person.Attributes.Add( schoolAttribute.Key, schoolAttribute );
                        person.AttributeValues.Add( schoolAttribute.Key, new List<AttributeValue>() );
                        person.AttributeValues[schoolAttribute.Key].Add( new AttributeValue()
                        {
                            AttributeId = schoolAttribute.Id,
                            Value = schoolValue,
                            Order = 0
                        } );
                    }

                    if ( FamilyFileIsIncluded )
                        continue; //skip saving the family info, it was done in code that processed the family file

                    var groupMember = new GroupMember();
                    groupMember.Person = person;
                    groupMember.GroupRoleId = groupRoleId;
                    groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                    familyGroup.Members.Add( groupMember );

                    completed++;
                    if ( completed % ReportingNumber < 1 )
                    {
                        RockTransactionScope.WrapTransaction( () =>
                        {
                            var rockContext = new RockContext();

                            rockContext.Groups.AddRange( familyList );
                            rockContext.SaveChanges();

                            foreach ( var newFamilyGroup in familyList )
                            {
                                foreach ( var newFamilyMember in newFamilyGroup.Members )
                                {
                                    var newPerson = newFamilyMember.Person;
                                    foreach ( var attributeCache in newPerson.Attributes.Select( a => a.Value ) )
                                    {
                                        var newValue = newPerson.AttributeValues[attributeCache.Key].FirstOrDefault();
                                        if ( newValue != null )
                                        {
                                            newValue.EntityId = newPerson.Id;
                                            rockContext.AttributeValues.Add( newValue );
                                        }
                                    }

                                    if ( !newPerson.Aliases.Any( a => a.AliasPersonId == newPerson.Id ) )
                                    {
                                        newPerson.Aliases.Add( new PersonAlias
                                        {
                                            AliasPersonId = newPerson.Id,
                                            AliasPersonGuid = newPerson.Guid
                                        } );
                                    }

                                    if ( newFamilyMember.GroupRoleId != childRoleId )
                                    {
                                        newPerson.GivingGroupId = newFamilyGroup.Id;
                                    }
                                }
                            }

                            rockContext.SaveChanges();
                        } );

                        familyList.Clear();
                        ReportPartialProgress();
                    }
                }
            } while ( csvData.Database.ReadNextRecord() );
        }

        #endregion
    }
}
