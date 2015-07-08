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
    public partial class F1Component
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
                otherType.Description = "Imported from FellowshipOne";
                otherType.CreatedByPersonAliasId = ImportPersonAliasId;

                lookupContext.DefinedValues.Add( otherType );
                lookupContext.SaveChanges( DisableAuditing );
            }

            // Look up existing Person attributes
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();

            // Cached Rock attributes: Facebook, Twitter, Instagram
            var twitterAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "Twitter", StringComparison.InvariantCultureIgnoreCase ) ) );
            var facebookAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "Facebook", StringComparison.InvariantCultureIgnoreCase ) ) );
            var instagramAttribute = AttributeCache.Read( personAttributes.FirstOrDefault( a => a.Key.Equals( "Instagram", StringComparison.InvariantCultureIgnoreCase ) ) );

            var newNumbers = new List<PhoneNumber>();
            var existingNumbers = new PhoneNumberService( lookupContext ).Queryable().ToList();
            var newPeopleAttributes = new Dictionary<int, Person>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying communication import ({0:N0} found, {1:N0} already exist).", totalRows, existingNumbers.Count ) );

            foreach ( var groupedRows in tableData.OrderByDescending( r => r["LastUpdatedDate"] ).GroupBy<Row, int?>( r => r["Household_ID"] as int? ) )
            {
                foreach ( var row in groupedRows.Where( r => r != null ) )
                {
                    string value = row["Communication_Value"] as string;
                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;
                    var peopleToUpdate = new List<PersonKeys>();

                    if ( individualId != null )
                    {
                        var matchingPerson = GetPersonKeys( individualId, householdId, includeVisitors: false );
                        if ( matchingPerson != null )
                        {
                            peopleToUpdate.Add( matchingPerson );
                        }
                    }
                    else
                    {
                        peopleToUpdate = GetFamilyByHouseholdId( householdId, includeVisitors: false );
                    }

                    if ( peopleToUpdate.Any() && !string.IsNullOrWhiteSpace( value ) )
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
                            var countryCode = PhoneNumber.DefaultCountryCode();
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
                                foreach ( var personKeys in peopleToUpdate )
                                {
                                    bool numberExists = existingNumbers.Any( n => n.PersonId == personKeys.PersonId && n.Number.Equals( value ) );
                                    if ( !numberExists )
                                    {
                                        var newNumber = new PhoneNumber();
                                        newNumber.CreatedByPersonAliasId = ImportPersonAliasId;
                                        newNumber.ModifiedDateTime = lastUpdated;
                                        newNumber.PersonId = (int)personKeys.PersonId;
                                        newNumber.IsMessagingEnabled = false;
                                        newNumber.CountryCode = countryCode;
                                        newNumber.IsUnlisted = !isListed;
                                        newNumber.Extension = extension.Left( 20 );
                                        newNumber.Number = normalizedNumber.Left( 20 );
                                        newNumber.Description = communicationComment;
                                        newNumber.NumberFormatted = PhoneNumber.FormattedNumber( countryCode, newNumber.Number, true );

                                        var matchingNumberType = definedTypePhoneType.DefinedValues.Where( v => type.StartsWith( v.Value ) )
                                            .Select( v => (int?)v.Id ).FirstOrDefault();
                                        newNumber.NumberTypeValueId = matchingNumberType ?? otherNumberType;

                                        newNumbers.Add( newNumber );
                                        existingNumbers.Add( newNumber );
                                    }
                                }

                                completed++;
                            }
                        }
                        else
                        {
                            Person person = null;

                            var personKeys = peopleToUpdate.FirstOrDefault();
                            if ( !newPeopleAttributes.ContainsKey( personKeys.PersonId ) )
                            {
                                // not in dictionary, get person from database
                                person = personService.Queryable( includeDeceased: true ).FirstOrDefault( p => p.Id == personKeys.PersonId );
                            }
                            else
                            {
                                // reuse person from dictionary
                                person = newPeopleAttributes[personKeys.PersonId];
                            }

                            if ( person != null )
                            {
                                if ( person.Attributes == null || person.AttributeValues == null )
                                {
                                    // make sure we have valid objects to assign to
                                    person.Attributes = new Dictionary<string, AttributeCache>();
                                    person.AttributeValues = new Dictionary<string, AttributeValue>();
                                }

                                // Check for an InFellowship ID/email before checking other types of email
                                if ( type.Contains( "InFellowship" ) && !person.Attributes.ContainsKey( InFellowshipLoginAttribute.Key ) )
                                {
                                    AddPersonAttribute( InFellowshipLoginAttribute, person, value );
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
                                        lookupContext.SaveChanges( DisableAuditing );
                                    }
                                    // this is a different email, assign it to SecondaryEmail
                                    else if ( !person.Email.Equals( value ) && !person.Attributes.ContainsKey( SecondaryEmailAttribute.Key ) )
                                    {
                                        AddPersonAttribute( SecondaryEmailAttribute, person, value );
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

                                if ( !newPeopleAttributes.ContainsKey( personKeys.PersonId ) )
                                {
                                    newPeopleAttributes.Add( personKeys.PersonId, person );
                                }
                                else
                                {
                                    newPeopleAttributes[personKeys.PersonId] = person;
                                }
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
                            if ( newNumbers.Any() || newPeopleAttributes.Any() )
                            {
                                SaveCommunication( newNumbers, newPeopleAttributes );
                            }

                            // reset so context doesn't bloat
                            lookupContext = new RockContext();
                            personService = new PersonService( lookupContext );
                            newPeopleAttributes.Clear();
                            newNumbers.Clear();
                            ReportPartialProgress();
                        }
                    }
                }
            }

            if ( newNumbers.Any() || newPeopleAttributes.Any() )
            {
                SaveCommunication( newNumbers, newPeopleAttributes );
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
                        var newAttributes = person.Attributes;
                        var newValues = person.AttributeValues;
                        person.LoadAttributes( rockContext );

                        foreach ( var attributeCache in newAttributes.Select( a => a.Value ) )
                        {
                            var currentAttributeValue = person.AttributeValues[attributeCache.Key];
                            var newAttributeValue = newValues[attributeCache.Key].Value;
                            if ( currentAttributeValue.Value != newAttributeValue && !string.IsNullOrWhiteSpace( newAttributeValue ) )
                            {
                                // set the new value and send it to the database
                                currentAttributeValue.Value = newAttributeValue;
                                if ( currentAttributeValue.Id == 0 )
                                {
                                    currentAttributeValue.EntityId = person.Id;
                                    rockContext.Entry( currentAttributeValue ).State = EntityState.Added;
                                }
                                else
                                {
                                    rockContext.Entry( currentAttributeValue ).State = EntityState.Modified;
                                }
                            }
                        }
                    }
                }

                rockContext.ChangeTracker.DetectChanges();
                rockContext.SaveChanges( DisableAuditing );
            } );
        }
    }
}