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

using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

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
        private List<Group> _FamilyList = null;

        protected List<Group> familyList
        {
            get
            {
                if (_FamilyList == null)
                {
                    _FamilyList = new List<Group>();
                }
                return _FamilyList;
            }
        }

        /// <summary>
        /// The list of families
        /// </summary>
        private Dictionary<string, List<Location>> _FamilyGroupLocationsList = null;

        protected Dictionary<string, List<Location>> familyGroupLocationsList
        {
            get
            {
                if (_FamilyGroupLocationsList == null)
                {
                    _FamilyGroupLocationsList = new Dictionary<string, List<Location>>();
                }
                return _FamilyGroupLocationsList;
            }
        }

        /// <summary>
        /// The family group
        /// </summary>
        private Group _familyGroup = null;

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
            int completed = 0;
            ReportProgress(0, string.Format("Adding family data ({0:N0} people already exist).", ImportedPeople.Count()));

            FamilyFileIsIncluded = CsvDataToImport.FirstOrDefault(n => n.RecordType.Equals(CsvDataModel.RockDataType.FAMILY)) == null ? false : true;

            // only import things that the user checked
            List<CsvDataModel> selectedCsvData = CsvDataToImport.Where(c => c.TableNodes.Any(n => n.Checked != false)).ToList();

            foreach (var csvData in selectedCsvData)
            {
                if (csvData.RecordType == CsvDataModel.RockDataType.FAMILY)
                {
                    LoadFamily(csvData);
                }
                else
                {
                    completed = LoadIndividuals(csvData);
                }
            } //read all files

            ReportProgress(100, string.Format("Completed import: {0:N0} records imported.", completed));
        }

        /// <summary>
        /// Loads the family data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private void LoadFamily(CsvDataModel csvData)
        {
            // Family group type id (required)
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            string currentFamilyId = string.Empty;
            int completed = 0;

            do
            {
                var row = csvData.Database.FirstOrDefault();
                if (row != null)
                {
                    string rowFamilyId = row[FamilyId];
                    if (!string.IsNullOrWhiteSpace(rowFamilyId) && rowFamilyId != currentFamilyId)
                    {
                        //see if it's already loaded, and if it needs added to local storage for quick retrieval
                        bool needsAddedToList = false;
                        _familyGroup = getFamilyGroup(rowFamilyId, ref needsAddedToList);

                        if (_familyGroup == null)
                        {
                            needsAddedToList = true;
                            FillInFamilyGroupInfo(rowFamilyId, familyGroupTypeId, row);
                        }
                        if (needsAddedToList)
                        {
                            familyList.Add(_familyGroup);
                        }
                        // Set current family id
                        currentFamilyId = rowFamilyId;
                    }
                    completed++;
                    if (completed % ReportingNumber < 1)
                    {
                        WriteAllFamilyChanges();
                        familyList.Clear();
                        ReportPartialProgress();
                    }
                }
            } while (csvData.Database.ReadNextRecord());

            // Check to see if any rows didn't get saved to the database
            if (familyList.Any())
            {
                WriteAllFamilyChanges();
            }
        }

        private void FillInFamilyGroupInfo(string rowFamilyId, int familyGroupTypeId, string[] row)
        {
            _familyGroup = new Group();
            _familyGroup.ForeignId = rowFamilyId;
            _familyGroup.Name = row[FamilyName] ?? row[FamilyLastName] + " Family";
            _familyGroup.CreatedByPersonAliasId = ImportPersonAlias.Id;
            _familyGroup.GroupTypeId = familyGroupTypeId;

            // campus could be a column in the individual or family file.
            // Since Rock doesn't support campuses by individual we'll just put it on family;
            var campus = row[Campus] as string;
            if (!string.IsNullOrWhiteSpace(campus))
            {
                _familyGroup.CampusId = CampusList.Where(c => c.Name.StartsWith(campus))
                    .Select(c => (int?)c.Id).FirstOrDefault();
            }

            // Add the family addresses since they exist in this file
            var famAddress = row[Address] as string;
            var famAddress2 = row[Address2] as string;
            var famCity = row[City] as string;
            var famState = row[State] as string;
            var famZip = row[Zip] as string;
            var famCountry = row[Country] as string;
            Location familyAddress = CheckAddress.Get(_familyGroup.Name + " Home", famAddress, famAddress2, famCity, famState, famZip, famCountry, true);
            if (familyAddress != null)
            {
                addToLocationsDictionary(familyAddress, rowFamilyId);
            }

            var famSecondaryAddress = row[SecondaryAddress] as string;
            var famSecondaryAddress2 = row[SecondaryAddress2] as string;
            var famSecondaryCity = row[SecondaryCity] as string;
            var famSecondaryState = row[SecondaryState] as string;
            var famSecondaryZip = row[SecondaryZip] as string;
            var famSecondaryCountry = row[SecondaryCountry] as string;
            Location familyAddress2 = CheckAddress.Get(_familyGroup.Name + " Work", famSecondaryAddress, famSecondaryAddress2, famSecondaryCity, famSecondaryState, famSecondaryZip, famSecondaryCountry, true);
            if (familyAddress2 != null)
            {
                addToLocationsDictionary(familyAddress2, rowFamilyId);
            }
        }

        private void addToLocationsDictionary(Location familyAddress, string rowFamilyId)
        {
            List<Location> locations = null;
            if (familyGroupLocationsList.Keys.Contains(rowFamilyId))
                locations = familyGroupLocationsList[rowFamilyId];
            if (locations == null)
            {
                locations = new List<Location>();
                familyGroupLocationsList.Add(rowFamilyId, locations);
            }
            locations.Add(familyAddress);
        }

        private void WriteAllFamilyChanges()
        {
            var unsavedFamilyRecs = familyList.Where(m => m.Guid == null).ToList();
            if (unsavedFamilyRecs == null || unsavedFamilyRecs.Count < 1)
            {
                return;
            }

            var rockContext = new RockContext();
            rockContext.WrapTransaction(() =>
            {
                rockContext.Groups.AddRange(unsavedFamilyRecs);
                rockContext.SaveChanges();

                var locationService = new Locations();
                foreach (var familyLocations in familyGroupLocationsList)
                {
                    List<Location> locations = familyLocations.Value;
                    var familyGrpForId = rockContext.Groups.FirstOrDefault(m => m.ForeignId == familyLocations.Key);
                    if (familyGrpForId == null) //? shouldn't happen
                    {
                        continue;
                    }

                    var loc1 = locations[0];
                    Location loc2 = null;
                    if (locations.Count > 1)
                        loc2 = locations[1];

                    locationService.MapFamilyAddresses(loc1, loc2, familyGrpForId.Id, familyGrpForId.Name);
                }
            });
        }

        /// <summary>
        /// consolidate logic that's accessed from multiple locations,
        /// see if a family exists (either in the list or in the table)
        /// and return it if it does.
        /// </summary>
        /// <param name="familyid"></param>
        /// <returns></returns>
        private Group getFamilyGroup(string familyid, ref bool needsAddedToList)
        {
            //if its not in familylist, you have to check the database
            var group = familyList.Where(n => n.ForeignId.Equals(familyid, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (group == null)
            {
                needsAddedToList = true; //familyList didn't have it we will add it no need to check familyList again
                using (var rockContext = new RockContext())
                {
                    //var groupservice = new  GroupService(rockContext);
                    ////when it's pulled from the db, get all the parts because we're disposing of that context.
                    //var inDBGroup = rockContext.Groups.FirstOrDefault(n => n.ForeignId == familyid);
                    //if (inDBGroup != null)
                    //    group = groupservice.Get(inDBGroup.Guid);

                    group = rockContext.Groups.Include("Members").Where(n => n.ForeignId == familyid).FirstOrDefault();
                }
            }
            return group;
        }

        private Person getPersonRecord(string rowFamilyId, string memberIdValue)
        {
            if (string.IsNullOrEmpty(memberIdValue))
                return null;

            bool needsAddedToList = false;
            var familyRecord = getFamilyGroup(rowFamilyId, ref needsAddedToList);
            if (familyRecord == null)
                return null;

            //error the objectcontext has been disposed and can no longer be used
            var groupMembers = familyRecord.Members.Where(m => m.Person.ForeignId == memberIdValue).ToList();
            if (groupMembers == null)
                return null;

            if (groupMembers.Count > 1)
                throw new Exception("ForeignId is not unique? Multiple people have the same value associated to ForeignId: " + rowFamilyId);

            if (groupMembers.Count > 0)
                return groupMembers.FirstOrDefault().Person;

            return null;
        }

        /// <summary>
        /// Loads the individual data.
        /// </summary>
        /// <param name="csvData">The CSV data.</param>
        private int LoadIndividuals(CsvDataModel csvData)
        {
            var lookupContext = new RockContext();
            var groupTypeRoleService = new GroupTypeRoleService(lookupContext);
            var attributeService = new AttributeService(lookupContext);

            var dvService = new DefinedValueService(lookupContext);

            // Marital statuses: Married, Single, Separated, etc
            List<DefinedValue> maritalStatusTypes = dvService.Queryable()
                .Where(dv => dv.DefinedType.Guid == new Guid(Rock.SystemGuid.DefinedType.PERSON_MARITAL_STATUS)).ToList();

            // Connection statuses: Member, Visitor, Attendee, etc
            List<DefinedValue> connectionStatusTypes = dvService.Queryable()
                .Where(dv => dv.DefinedType.Guid == new Guid(Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS)).ToList();

            // Record status reasons: No Activity, Moved, Deceased, etc
            List<DefinedValue> recordStatusReasons = dvService.Queryable()
                .Where(dv => dv.DefinedType.Guid == new Guid(Rock.SystemGuid.DefinedType.PERSON_RECORD_STATUS_REASON)).ToList();

            // Record statuses: Active, Inactive, Pending
            var activestatus = dvService.Get(new Guid(Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE));
            int? recordStatusActiveId = activestatus.Id;
            int? recordStatusInactiveId = dvService.Get(new Guid(Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE)).Id;
            int? recordStatusPendingId = dvService.Get(new Guid(Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING)).Id;

            // Record type: Person
            int? personRecordTypeId = dvService.Get(new Guid(Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON)).Id;

            // Suffix type: Dr., Jr., II, etc
            List<DefinedValue> suffixTypes = dvService.Queryable()
                .Where(dv => dv.DefinedType.Guid == new Guid(Rock.SystemGuid.DefinedType.PERSON_SUFFIX)).ToList();

            // Title type: Mr., Mrs. Dr., etc
            List<DefinedValue> titleTypes = dvService.Queryable()
                .Where(dv => dv.DefinedType.Guid == new Guid(Rock.SystemGuid.DefinedType.PERSON_TITLE)).ToList();

            // Note type: Comment
            int noteCommentTypeId = new NoteTypeService(lookupContext).Get(new Guid("7E53487C-D650-4D85-97E2-350EB8332763")).Id;

            // Group roles: Adult, Child, others
            int adultRoleId = groupTypeRoleService.Get(new Guid(Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT)).Id;
            int childRoleId = groupTypeRoleService.Get(new Guid(Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD)).Id;

            // Group type: Family
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            // Look up additional Person attributes (existing)
            var personAttributes = attributeService.GetByEntityTypeId(PersonEntityTypeId).ToList();

            // Cached attributes: PreviousChurch, Position, Employer, School
            var membershipDateAttribute = AttributeCache.Read(personAttributes.FirstOrDefault(a => a.Key == "MembershipDate"));
            var baptismDateAttribute = AttributeCache.Read(personAttributes.FirstOrDefault(a => a.Key == "BaptismDate"));
            var firstVisitAttribute = AttributeCache.Read(personAttributes.FirstOrDefault(a => a.Key == "FirstVisit"));
            var previousChurchAttribute = AttributeCache.Read(personAttributes.FirstOrDefault(a => a.Key == "PreviousChurch"));
            var employerAttribute = AttributeCache.Read(personAttributes.FirstOrDefault(a => a.Key == "Employer"));
            var positionAttribute = AttributeCache.Read(personAttributes.FirstOrDefault(a => a.Key == "Position"));
            var schoolAttribute = AttributeCache.Read(personAttributes.FirstOrDefault(a => a.Key == "School"));

            string currentFamilyId = string.Empty;
            int completed = 0;

            do
            {
                var row = csvData.Database.FirstOrDefault();
                if (row != null)
                {
                    int groupRoleId = adultRoleId;
                    var memberIdValue = row[MemberId] as string;
                    // int rowMemberId = row[MemberId].AsType<int>();

                    //this matches back to the groups we just loaded
                    string rowFamilyId = row[PersonFamilyId] as string;
                    string rowFamilyName = row[LastName].ToString() + " Family";

                    //keep track of family here if we're not loading a separate family file
                    if (!string.IsNullOrEmpty(rowFamilyId))
                    {
                        bool needsAddedToList = false;
                        _familyGroup = getFamilyGroup(rowFamilyId, ref needsAddedToList);
                        if (_familyGroup == null)
                        {
                            needsAddedToList = true;
                            _familyGroup = new Group();
                            _familyGroup.ForeignId = rowFamilyId;
                            _familyGroup.Name = rowFamilyName;

                            currentFamilyId = rowFamilyId;
                        }
                        if (needsAddedToList)
                        {
                            familyList.Add(_familyGroup);
                        }
                    }

                    //see if this person is already in our data
                    Person person = getPersonRecord(rowFamilyId, memberIdValue);
                    if (person == null)
                    {
                        person = new Person();
                        person.ForeignId = memberIdValue;
                        person.RecordTypeValueId = personRecordTypeId;
                        person.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        person.FirstName = row[FirstName];
                        person.NickName = row[NickName];
                        person.LastName = row[LastName];
                        person.Email = row[Email];

                        #region assign values to the Person record

                        string activeEmail = row[IsEmailActive] as string;
                        if (!string.IsNullOrEmpty(activeEmail))
                        {
                            //activeEmail = "Active"
                            bool emailIsActive = false;
                            if (bool.TryParse(activeEmail, out emailIsActive))
                            {
                                person.IsEmailActive = emailIsActive;
                            }
                        }

                        DateTime birthDate;
                        if (DateTime.TryParse(row[DateOfBirth], out birthDate))
                        {
                            person.BirthDate = birthDate;
                        }

                        DateTime anniversary;
                        if (DateTime.TryParse(row[Anniversary], out anniversary))
                        {
                            person.AnniversaryDate = anniversary;
                        }

                        var gender = row[Gender] as string;
                        if (gender != null)
                        {
                            switch (gender.Trim().ToLower())
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
                        if (prefix != null)
                        {
                            prefix = prefix.RemoveSpecialCharacters().Trim();
                            person.TitleValueId = titleTypes.Where(s => prefix == s.Value.RemoveSpecialCharacters())
                                .Select(s => (int?)s.Id).FirstOrDefault();
                        }

                        var suffix = row[Suffix] as string;
                        if (suffix != null)
                        {
                            suffix = suffix.RemoveSpecialCharacters().Trim();
                            person.SuffixValueId = suffixTypes.Where(s => suffix == s.Value.RemoveSpecialCharacters())
                                .Select(s => (int?)s.Id).FirstOrDefault();
                        }

                        var maritalStatus = row[MaritalStatus] as string;
                        if (maritalStatus != null)
                        {
                            person.MaritalStatusValueId = maritalStatusTypes.Where(dv => dv.Value == maritalStatus)
                                .Select(dv => (int?)dv.Id).FirstOrDefault();
                        }
                        else
                        {
                            person.MaritalStatusValueId = maritalStatusTypes.Where(dv => dv.Value == "Unknown")
                                .Select(dv => (int?)dv.Id).FirstOrDefault();
                        }

                        var familyRole = row[FamilyRole] as string;
                        if (familyRole != null)
                        {
                            if (familyRole == "Child" || person.Age < 18)
                            {
                                groupRoleId = childRoleId;
                            }
                            else if (familyRole == "Visitor")
                            {
                                // assign person as a known relationship of this family/group
                            }
                        }

                        var connectionStatus = row[ConnectionStatus] as string;
                        if (connectionStatus == "Member")
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault(dv => dv.Guid == new Guid(Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER)).Id;
                        }
                        else if (connectionStatus == "Visitor")
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault(dv => dv.Guid == new Guid(Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR)).Id;
                        }
                        else if (connectionStatus == "Deceased")
                        {
                            person.IsDeceased = true;
                            person.RecordStatusReasonValueId = recordStatusReasons.Where(dv => dv.Value == "Deceased")
                                .Select(dv => dv.Id).FirstOrDefault();
                        }
                        else
                        {
                            // look for user-defined connection type or default to Attendee
                            var customConnectionType = connectionStatusTypes.Where(dv => dv.Value == connectionStatus)
                                .Select(dv => (int?)dv.Id).FirstOrDefault();

                            int attendeeId = connectionStatusTypes.FirstOrDefault(dv => dv.Guid == new Guid("39F491C5-D6AC-4A9B-8AC0-C431CB17D588")).Id;
                            person.ConnectionStatusValueId = customConnectionType ?? attendeeId;
                        }

                        var recordStatus = row[RecordStatus] as string;
                        switch (recordStatus.Trim())
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
                        if (DateTime.TryParse(row[MembershipDate], out membershipDateValue))
                        {
                            person.Attributes.Add(membershipDateAttribute.Key, membershipDateAttribute);
                            person.AttributeValues.Add(membershipDateAttribute.Key, new List<AttributeValue>());
                            person.AttributeValues[membershipDateAttribute.Key].Add(new AttributeValue()
                            {
                                AttributeId = membershipDateAttribute.Id,
                                Value = membershipDateValue.ToString(),
                                Order = 0
                            });
                        }

                        DateTime baptismDateValue;
                        if (DateTime.TryParse(row[BaptismDate], out baptismDateValue))
                        {
                            person.Attributes.Add(baptismDateAttribute.Key, baptismDateAttribute);
                            person.AttributeValues.Add(baptismDateAttribute.Key, new List<AttributeValue>());
                            person.AttributeValues[baptismDateAttribute.Key].Add(new AttributeValue()
                            {
                                AttributeId = baptismDateAttribute.Id,
                                Value = baptismDateValue.ToString(),
                                Order = 0
                            });
                        }

                        DateTime firstVisitValue;
                        if (DateTime.TryParse(row[FirstVisit], out firstVisitValue))
                        {
                            person.Attributes.Add(firstVisitAttribute.Key, firstVisitAttribute);
                            person.AttributeValues.Add(firstVisitAttribute.Key, new List<AttributeValue>());
                            person.AttributeValues[firstVisitAttribute.Key].Add(new AttributeValue()
                            {
                                AttributeId = firstVisitAttribute.Id,
                                Value = firstVisitValue.ToString(),
                                Order = 0
                            });
                        }

                        var previousChurchValue = row[PreviousChurch] as string;
                        if (previousChurchValue != null)
                        {
                            person.Attributes.Add(previousChurchAttribute.Key, previousChurchAttribute);
                            person.AttributeValues.Add(previousChurchAttribute.Key, new List<AttributeValue>());
                            person.AttributeValues[previousChurchAttribute.Key].Add(new AttributeValue()
                            {
                                AttributeId = previousChurchAttribute.Id,
                                Value = previousChurchValue,
                                Order = 0
                            });
                        }

                        var position = row[Occupation] as string;
                        if (position != null)
                        {
                            person.Attributes.Add(positionAttribute.Key, positionAttribute);
                            person.AttributeValues.Add(positionAttribute.Key, new List<AttributeValue>());
                            person.AttributeValues[positionAttribute.Key].Add(new AttributeValue()
                            {
                                AttributeId = positionAttribute.Id,
                                Value = position,
                                Order = 0
                            });
                        }

                        var employerValue = row[Employer] as string;
                        if (employerValue != null)
                        {
                            person.Attributes.Add(employerAttribute.Key, employerAttribute);
                            person.AttributeValues.Add(employerAttribute.Key, new List<AttributeValue>());
                            person.AttributeValues[employerAttribute.Key].Add(new AttributeValue()
                            {
                                AttributeId = employerAttribute.Id,
                                Value = employerValue,
                                Order = 0
                            });
                        }

                        var schoolValue = row[School] as string;
                        if (schoolValue != null)
                        {
                            person.Attributes.Add(schoolAttribute.Key, schoolAttribute);
                            person.AttributeValues.Add(schoolAttribute.Key, new List<AttributeValue>());
                            person.AttributeValues[schoolAttribute.Key].Add(new AttributeValue()
                            {
                                AttributeId = schoolAttribute.Id,
                                Value = schoolValue,
                                Order = 0
                            });
                        }

                        #endregion
                    }

                    //see if this person is already in the family group, if not then add them.
                    //Is there a guarantee that the memberIdValue will be unique within a group/family?
                    var groupMembers = _familyGroup.Members.Where(m => m.Person.ForeignId == memberIdValue).ToList();
                    if (groupMembers == null || groupMembers.Count < 1)
                    {
                        var groupMember = new GroupMember(); ;
                        //now, add this person to the family membership so it can be persisted.
                        groupMember.Person = person;
                        groupMember.GroupRoleId = groupRoleId;
                        groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                        _familyGroup.Members.Add(groupMember);
                    }

                    completed++;
                    if (completed % ReportingNumber < 1)
                    {
                        WriteAllIndividualChanges(childRoleId);

                        familyList.Clear();
                        ReportPartialProgress();
                    }
                }
            } while (csvData.Database.ReadNextRecord());
            WriteAllIndividualChanges(childRoleId);

            ReportPartialProgress();
            return completed;
        }

        private void WriteAllIndividualChanges(int childRoleId)
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction(() =>
            {
                //if we didn't already save the family info, save it now.
                WriteAllFamilyChanges();

                PersonService personservice = new PersonService(rockContext);

                foreach (var newFamilyGroup in familyList)
                {
                    foreach (var newFamilyMember in newFamilyGroup.Members)
                    {
                        var newPerson = newFamilyMember.Person;
                        personservice.Add(newPerson);
                        rockContext.SaveChanges(true);

                        foreach (var attributeCache in newPerson.Attributes.Select(a => a.Value))
                        {
                            var newValue = newPerson.AttributeValues[attributeCache.Key].FirstOrDefault();
                            if (newValue != null)
                            {
                                newValue.EntityId = newPerson.Id;
                                rockContext.AttributeValues.Add(newValue);
                            }
                        }

                        if (!newPerson.Aliases.Any(a => a.AliasPersonId == newPerson.Id))
                        {
                            newPerson.Aliases.Add(new PersonAlias
                            {
                                AliasPersonId = newPerson.Id,
                                AliasPersonGuid = newPerson.Guid
                            });
                        }

                        if (newFamilyMember.GroupRoleId != childRoleId)
                        {
                            newPerson.GivingGroupId = newFamilyGroup.Id;
                        }
                    }
                }

                rockContext.SaveChanges();
            });
        }

        #endregion

        internal struct CheckAddress
        {
            /// <summary>
            /// COPIED FROM EXCAVATOR.F1, added country
            /// Modified version of Rock.Model.LocationService.Get()
            /// to get or set the specified location in the database
            /// (minus the call for address verification)
            /// </summary>
            /// <param name="street1">The street1.</param>
            /// <param name="street2">The street2.</param>
            /// <param name="city">The city.</param>
            /// <param name="state">The state.</param>
            /// <param name="zip">The zip.</param>
            /// <returns></returns>
            public static Location Get(string addressname, string street1, string street2, string city, string state, string zip, string country, bool DisableAudit = false)
            {
                // Create a new context/service so that save does not affect calling method's context
                var rockContext = new RockContext();
                var locationService = new LocationService(rockContext);

                // Make sure it's not an empty address
                if (string.IsNullOrWhiteSpace(street1) &&
                    string.IsNullOrWhiteSpace(street2) &&
                    string.IsNullOrWhiteSpace(city) &&
                    string.IsNullOrWhiteSpace(state) &&
                    string.IsNullOrWhiteSpace(zip))
                {
                    return null;
                }

                // First check if a location exists with the entered values
                Location existingLocation = locationService.Queryable().FirstOrDefault(t =>
                    (t.Street1 == street1 || (street1 == null && t.Street1 == null)) &&
                    (t.Street2 == street2 || (street2 == null && t.Street2 == null)) &&
                    (t.City == city || (city == null && t.City == null)) &&
                    (t.State == state || (state == null && t.State == null)) &&
                    (t.PostalCode == zip || (zip == null && t.PostalCode == null)));
                if (existingLocation != null)
                {
                    return existingLocation;
                }

                // If existing location wasn't found with entered values, try standardizing the values, and
                // search for an existing value again
                var newLocation = new Location
                {
                    Name = addressname,
                    Street1 = street1,
                    Street2 = street2,
                    City = city,
                    State = state,
                    PostalCode = zip,
                    Country = country
                };

                // uses MEF to look for verification providers (which Excavator doesn't have)
                // Verify( newLocation, false );

                existingLocation = locationService.Queryable().FirstOrDefault(t =>
                    (t.Street1 == newLocation.Street1 || (newLocation.Street1 == null && t.Street1 == null)) &&
                    (t.Street2 == newLocation.Street2 || (newLocation.Street2 == null && t.Street2 == null)) &&
                    (t.City == newLocation.City || (newLocation.City == null && t.City == null)) &&
                    (t.State == newLocation.State || (newLocation.State == null && t.State == null)) &&
                    (t.PostalCode == newLocation.PostalCode || (newLocation.PostalCode == null && t.PostalCode == null)));

                if (existingLocation != null)
                {
                    return existingLocation;
                }

                locationService.Add(newLocation);
                rockContext.SaveChanges(DisableAudit);

                // refetch it from the database to make sure we get a valid .Id
                return locationService.Get(newLocation.Guid);
            }
        }
    }
}
