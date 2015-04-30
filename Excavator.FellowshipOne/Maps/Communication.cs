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
    /// <summary>
    /// Partial of F1Component that holds the Email/Phone # import methods
    /// </summary>
    partial class F1Component
    {
        /// <summary>
        /// Maps the communication data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private void MapCommunication( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var personService = new PersonService( lookupContext );
            var attributeService = new AttributeService( lookupContext );

            var definedTypePhoneType = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.PERSON_PHONE_TYPE ), lookupContext );
            var otherNumberType = definedTypePhoneType.DefinedValues.Where( dv => dv.Value.StartsWith( "Other" ) ).Select( v => (int?)v.Id ).FirstOrDefault();
            if ( otherNumberType == null )
            {
                var otherType = new DefinedValue();
                otherType.IsSystem = false;
                otherType.DefinedTypeId = definedTypePhoneType.Id;
                otherType.Order = 0;
                otherType.Value = "Other";
                otherType.Description = "Imported from FellowshipOne and did not match a current phone type";
                otherType.CreatedByPersonAliasId = ImportPersonAlias.Id;

                lookupContext.DefinedValues.Add( otherType );
                lookupContext.SaveChanges( DisableAudit );
            }

            // Look up existing Person attributes
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();

            // Cached Rock attributes: Facebook, Twitter, Instagram
            var twitterAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Twitter" ) );
            var facebookAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Facebook" ) );
            var instagramAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key == "Instagram" ) );
            var secondaryEmailAttribute = AttributeCache.Read( SecondaryEmailAttributeId );
            var infellowshipLoginAttribute = AttributeCache.Read( InfellowshipLoginAttributeId );

            var existingNumbers = new PhoneNumberService( lookupContext ).Queryable().ToList();

            var newNumberList = new List<PhoneNumber>();
            var peopleWithAttributes = new Dictionary<int, Person>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying communication import ({0:N0} found, {1:N0} already exist).", totalRows, existingNumbers.Count() ) );

            foreach ( var groupedRows in tableData.OrderByDescending( r => r["LastUpdatedDate"] ).GroupBy<Row, int?>( r => r["Household_ID"] as int? ) )
            {
                foreach ( var row in groupedRows )
                {
                    string value = row["Communication_Value"] as string;
                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;
                    var personList = new List<int?>();

                    if ( individualId != null )
                    {
                        int? personId = GetPersonAliasId( individualId, householdId );
                        if ( personId != null )
                        {
                            personList.Add( personId );
                        }
                    }
                    else
                    {
                        List<int?> personIds = GetFamilyByHouseholdId( householdId );
                        if ( personIds.Any() )
                        {
                            personList.AddRange( personIds );
                        }
                    }

                    if ( personList.Any() && !string.IsNullOrWhiteSpace( value ) )
                    {
                        DateTime? lastUpdated = row["LastUpdatedDate"] as DateTime?;
                        string communicationComment = row["Communication_Comment"] as string;
                        string type = row["Communication_Type"] as string;
                        bool isListed = (bool)row["Listed"];
                        value = value.RemoveWhitespace();

                        // Communication value is a number
                        if ( type.Contains( "Phone" ) || type.Contains( "Mobile" ) )
                        {
                            var extension = string.Empty;
                            var countryCode = Rock.Model.PhoneNumber.DefaultCountryCode();
                            var normalizedNumber = string.Empty;
                            var countryIndex = value.IndexOf( '+' );
                            int extensionIndex = value.LastIndexOf( 'x' ) > 0 ? value.LastIndexOf( 'x' ) : value.Length;
                            if ( countryIndex >= 0 )
                            {
                                countryCode = value.Substring( countryIndex, countryIndex + 3 ).AsNumeric();
                                normalizedNumber = value.Substring( countryIndex + 3, extensionIndex - 3 ).AsNumeric();
                                extension = value.Substring( extensionIndex );
                            }
                            else if ( extensionIndex > 0 )
                            {
                                normalizedNumber = value.Substring( 0, extensionIndex ).AsNumeric();
                                extension = value.Substring( extensionIndex ).AsNumeric();
                            }
                            else
                            {
                                normalizedNumber = value.AsNumeric();
                            }

                            if ( !string.IsNullOrWhiteSpace( normalizedNumber ) )
                            {
                                foreach ( var familyPersonId in personList )
                                {
                                    bool numberExists = existingNumbers.Any( n => n.PersonId == familyPersonId && n.Number.Equals( value ) );
                                    if ( !numberExists )
                                    {
                                        var newNumber = new PhoneNumber();
                                        newNumber.CreatedByPersonAliasId = ImportPersonAlias.Id;
                                        newNumber.ModifiedDateTime = lastUpdated;
                                        newNumber.PersonId = (int)familyPersonId;
                                        newNumber.IsMessagingEnabled = false;
                                        newNumber.CountryCode = countryCode;
                                        newNumber.IsUnlisted = !isListed;
                                        newNumber.Extension = extension.Left( 20 );
                                        newNumber.Number = normalizedNumber.Left( 20 );
                                        newNumber.Description = communicationComment;

                                        var matchingNumberType = definedTypePhoneType.DefinedValues.Where( v => type.StartsWith( v.Value ) )
                                            .Select( v => (int?)v.Id ).FirstOrDefault();
                                        newNumber.NumberTypeValueId = matchingNumberType ?? otherNumberType;

                                        newNumberList.Add( newNumber );
                                        existingNumbers.Add( newNumber );
                                    }
                                }

                                completed++;
                            }
                        }
                        else
                        {
                            Person person = null;

                            // should every person in the family get these attributes?
                            var personId = (int)personList.FirstOrDefault();
                            if ( !peopleWithAttributes.ContainsKey( personId ) )
                            {
                                // not in dictionary, get person from database
                                person = personService.Queryable( includeDeceased: true ).FirstOrDefault( p => p.Id == personId );
                            }
                            else
                            {
                                // reuse person from dictionary
                                person = peopleWithAttributes[personId];
                            }

                            if ( person.Attributes == null || person.AttributeValues == null )
                            {
                                // make sure we have valid objects to assign to
                                person.Attributes = new Dictionary<string, AttributeCache>();
                                person.AttributeValues = new Dictionary<string, AttributeValue>();
                            }

                            // Check for an Infellowship ID/email before checking other types of email
                            if ( type.Contains( "Infellowship" ) && !person.Attributes.ContainsKey( infellowshipLoginAttribute.Key ) )
                            {
                                AddPersonAttribute( infellowshipLoginAttribute, person, value );
                            }
                            else if ( value.IsEmail() )
                            {
                                // person email is empty
                                if ( string.IsNullOrWhiteSpace( person.Email ) )
                                {
                                    person.Email = value.Left( 75 );
                                    person.IsEmailActive = isListed;
                                    person.EmailPreference = isListed ? EmailPreference.EmailAllowed : EmailPreference.DoNotEmail;
                                    person.ModifiedDateTime = lastUpdated;
                                    person.EmailNote = communicationComment;
                                    lookupContext.SaveChanges( DisableAudit );
                                }
                                // this is a different email, assign it to SecondaryEmail
                                else if ( !person.Email.Equals( value ) && !person.Attributes.ContainsKey( secondaryEmailAttribute.Key ) )
                                {
                                    AddPersonAttribute( secondaryEmailAttribute, person, value );
                                }
                            }
                            else if ( type.Contains( "Twitter" ) && !person.Attributes.ContainsKey( twitterAttribute.Key ) )
                            {
                                AddPersonAttribute( twitterAttribute, person, value );
                            }
                            else if ( type.Contains( "Facebook" ) && !person.Attributes.ContainsKey( facebookAttribute.Key ) )
                            {
                                AddPersonAttribute( facebookAttribute, person, value );
                            }
                            else if ( type.Contains( "Instagram" ) && !person.Attributes.ContainsKey( instagramAttribute.Key ) )
                            {
                                AddPersonAttribute( instagramAttribute, person, value );
                            }

                            if ( !peopleWithAttributes.ContainsKey( personId ) )
                            {
                                peopleWithAttributes.Add( personId, person );
                            }
                            else
                            {
                                peopleWithAttributes[personId] = person;
                            }

                            completed++;
                        }

                        if ( completed % percentage < 1 )
                        {
                            int percentComplete = completed / percentage;
                            ReportProgress( percentComplete, string.Format( "{0:N0} records imported ({1}% complete).", completed, percentComplete ) );
                        }
                        else if ( completed % ReportingNumber < 1 )
                        {
                            if ( newNumberList.Any() || peopleWithAttributes.Any() )
                            {
                                SaveCommunication( newNumberList, peopleWithAttributes );
                            }

                            // reset so context doesn't bloat
                            lookupContext = new RockContext();
                            personService = new PersonService( lookupContext );
                            peopleWithAttributes.Clear();
                            newNumberList.Clear();
                            ReportPartialProgress();
                        }
                    }
                }
            }

            if ( newNumberList.Any() || peopleWithAttributes.Any() )
            {
                SaveCommunication( newNumberList, peopleWithAttributes );
            }

            ReportProgress( 100, string.Format( "Finished communication import: {0:N0} records imported.", completed ) );
        }

        /// <summary>
        /// Saves the communication.
        /// </summary>
        /// <param name="newNumberList">The new number list.</param>
        /// <param name="updatedPersonList">The updated person list.</param>
        private static void SaveCommunication( List<PhoneNumber> newNumberList, Dictionary<int, Person> updatedPersonList )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;

                if ( newNumberList.Any() )
                {
                    rockContext.PhoneNumbers.AddRange( newNumberList );
                }

                if ( updatedPersonList.Any() )
                {
                    foreach ( var person in updatedPersonList.Values.Where( p => p.Attributes.Any() ) )
                    {
                        // save current values before loading from the db
                        var newPersonAttributes = person.Attributes;
                        var newPersonValues = person.AttributeValues;
                        person.LoadAttributes( rockContext );

                        foreach ( var attributeCache in newPersonAttributes.Select( a => a.Value ) )
                        {
                            var currentAttributeValue = person.AttributeValues[attributeCache.Key];
                            var newAttributeValue = newPersonValues[attributeCache.Key].Value;
                            if ( currentAttributeValue.Value != newAttributeValue && !string.IsNullOrWhiteSpace( newAttributeValue ) )
                            {
                                // set the new value and add it to the database
                                currentAttributeValue.Value = newAttributeValue;
                                if ( currentAttributeValue.EntityId == null )
                                {
                                    currentAttributeValue.EntityId = person.Id;
                                    rockContext.AttributeValues.Add( currentAttributeValue );
                                }
                                else
                                {
                                    rockContext.AttributeValues.Attach( currentAttributeValue );
                                    rockContext.Entry( currentAttributeValue ).State = EntityState.Modified;
                                }
                            }
                        }
                    }
                }

                rockContext.ChangeTracker.DetectChanges();
                rockContext.SaveChanges( DisableAudit );
            } );
        }
    }
}