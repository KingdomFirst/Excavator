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
using System.Linq;
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
            var categoryService = new CategoryService();
            var personService = new PersonService();

            List<DefinedValue> numberTypeValues = new DefinedValueService().Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_PHONE_TYPE ) ).ToList();

            // Add a Social Media category if it doesn't exist
            int attributeEntityTypeId = EntityTypeCache.Read( "Rock.Model.Attribute" ).Id;
            int socialMediaCategoryId = categoryService.Queryable().Where( c => c.EntityType.Id == attributeEntityTypeId && c.Name == "Social Media" ).Select( c => c.Id ).FirstOrDefault();
            if ( socialMediaCategoryId == 0 )
            {
                var socialMediaCategory = new Category();
                socialMediaCategory.IsSystem = false;
                socialMediaCategory.Name = "Social Media";
                socialMediaCategory.IconCssClass = "fa fa-twitter";
                socialMediaCategory.EntityTypeId = attributeEntityTypeId;
                socialMediaCategory.EntityTypeQualifierColumn = "EntityTypeId";
                socialMediaCategory.EntityTypeQualifierValue = PersonEntityTypeId.ToString();
                socialMediaCategory.Order = 0;

                categoryService.Add( socialMediaCategory, ImportPersonAlias );
                categoryService.Save( socialMediaCategory, ImportPersonAlias );
                socialMediaCategoryId = socialMediaCategory.Id;
            }

            int visitInfoCategoryId = categoryService.Queryable().Where( c => c.EntityTypeId == attributeEntityTypeId && c.Name == "Visit Information" ).Select( c => c.Id ).FirstOrDefault();

            // Look up additional Person attributes (existing)
            var personAttributes = new AttributeService().GetByEntityTypeId( PersonEntityTypeId ).ToList();

            // Add an Attribute for the secondary email
            int secondaryEmailAttributeId = personAttributes.Where( a => a.Key == "SecondaryEmail" ).Select( a => a.Id ).FirstOrDefault();
            if ( secondaryEmailAttributeId == 0 )
            {
                var newSecondaryEmailAttribute = new Rock.Model.Attribute();
                newSecondaryEmailAttribute.Key = "SecondaryEmail";
                newSecondaryEmailAttribute.Name = "Secondary Email";
                newSecondaryEmailAttribute.FieldTypeId = TextFieldTypeId;
                newSecondaryEmailAttribute.EntityTypeId = PersonEntityTypeId;
                newSecondaryEmailAttribute.EntityTypeQualifierValue = string.Empty;
                newSecondaryEmailAttribute.EntityTypeQualifierColumn = string.Empty;
                newSecondaryEmailAttribute.Description = "The secondary email for this person";
                newSecondaryEmailAttribute.DefaultValue = string.Empty;
                newSecondaryEmailAttribute.IsMultiValue = false;
                newSecondaryEmailAttribute.IsRequired = false;
                newSecondaryEmailAttribute.Order = 0;

                using ( new UnitOfWorkScope() )
                {
                    var attributeService = new AttributeService();
                    attributeService.Add( newSecondaryEmailAttribute );
                    var visitInfoCategory = new CategoryService().Get( visitInfoCategoryId );
                    newSecondaryEmailAttribute.Categories.Add( visitInfoCategory );
                    attributeService.Save( newSecondaryEmailAttribute );
                    secondaryEmailAttributeId = newSecondaryEmailAttribute.Id;
                }
            }

            // Add an Attribute for Twitter
            int twitterAttributeId = personAttributes.Where( a => a.Key == "TwitterUsername" ).Select( a => a.Id ).FirstOrDefault();
            if ( twitterAttributeId == 0 )
            {
                var newTwitterAttribute = new Rock.Model.Attribute();
                newTwitterAttribute.Key = "TwitterUsername";
                newTwitterAttribute.Name = "Twitter Username";
                newTwitterAttribute.FieldTypeId = TextFieldTypeId;
                newTwitterAttribute.EntityTypeId = PersonEntityTypeId;
                newTwitterAttribute.EntityTypeQualifierValue = string.Empty;
                newTwitterAttribute.EntityTypeQualifierColumn = string.Empty;
                newTwitterAttribute.Description = "The Twitter username (or link) for this person";
                newTwitterAttribute.DefaultValue = string.Empty;
                newTwitterAttribute.IsMultiValue = false;
                newTwitterAttribute.IsRequired = false;
                newTwitterAttribute.Order = 0;

                using ( new UnitOfWorkScope() )
                {
                    var attributeService = new AttributeService();
                    attributeService.Add( newTwitterAttribute );
                    var socialMediaCategory = new CategoryService().Get( socialMediaCategoryId );
                    newTwitterAttribute.Categories.Add( socialMediaCategory );
                    attributeService.Save( newTwitterAttribute );
                    twitterAttributeId = newTwitterAttribute.Id;
                }
            }

            // Add an Attribute for Facebook
            var facebookAttributeId = personAttributes.Where( a => a.Key == "FacebookUsername" ).Select( a => a.Id ).FirstOrDefault();
            if ( facebookAttributeId == 0 )
            {
                var newFacebookAttribute = new Rock.Model.Attribute();
                newFacebookAttribute.Key = "FacebookUsername";
                newFacebookAttribute.Name = "Facebook Username";
                newFacebookAttribute.FieldTypeId = TextFieldTypeId;
                newFacebookAttribute.EntityTypeId = PersonEntityTypeId;
                newFacebookAttribute.EntityTypeQualifierValue = string.Empty;
                newFacebookAttribute.EntityTypeQualifierColumn = string.Empty;
                newFacebookAttribute.Description = "The Facebook username (or link) for this person";
                newFacebookAttribute.DefaultValue = string.Empty;
                newFacebookAttribute.IsMultiValue = false;
                newFacebookAttribute.IsRequired = false;
                newFacebookAttribute.Order = 0;

                using ( new UnitOfWorkScope() )
                {
                    var attributeService = new AttributeService();
                    attributeService.Add( newFacebookAttribute );
                    var socialMediaCategory = new CategoryService().Get( socialMediaCategoryId );
                    newFacebookAttribute.Categories.Add( socialMediaCategory );
                    attributeService.Save( newFacebookAttribute );
                    facebookAttributeId = newFacebookAttribute.Id;
                }
            }

            var secondaryEmailAttribute = AttributeCache.Read( secondaryEmailAttributeId );
            var twitterUsernameAttribute = AttributeCache.Read( twitterAttributeId );
            var facebookUsernameAttribute = AttributeCache.Read( facebookAttributeId );

            var existingNumbers = new PhoneNumberService().Queryable().ToList();

            var newNumberList = new List<PhoneNumber>();
            var updatedPersonList = new List<Person>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Checking communication import ({0:N0} found).", totalRows ) );

            foreach ( var row in tableData )
            {
                string value = row["Communication_Value"] as string;
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                int? personId = GetPersonId( individualId, householdId );
                if ( personId != null && !string.IsNullOrWhiteSpace( value ) )
                {
                    DateTime? lastUpdated = row["LastUpdatedDate"] as DateTime?;
                    string communicationComment = row["Communication_Comment"] as string;
                    string type = row["Communication_Type"] as string;
                    bool isListed = (bool)row["Listed"];

                    if ( type.Contains( "Phone" ) || type.Contains( "Mobile" ) )
                    {
                        var extension = string.Empty;
                        int extensionIndex = value.LastIndexOf( 'x' );
                        if ( extensionIndex > 0 )
                        {
                            extension = value.Substring( extensionIndex ).AsNumeric();
                            value = value.Substring( 0, extensionIndex ).AsNumeric();
                        }
                        else
                        {
                            value = value.AsNumeric();
                        }

                        if ( !string.IsNullOrWhiteSpace( value ) )
                        {
                            bool numberExists = existingNumbers.Any( n => n.PersonId == (int)personId && n.Number.Equals( value ) );
                            if ( !numberExists )
                            {
                                var newNumber = new PhoneNumber();
                                newNumber.CreatedByPersonAliasId = ImportPersonAlias.Id;
                                newNumber.ModifiedDateTime = lastUpdated;
                                newNumber.PersonId = (int)personId;
                                newNumber.IsMessagingEnabled = false;
                                newNumber.IsUnlisted = !isListed;
                                newNumber.Extension = extension.Left( 20 );
                                newNumber.Number = value.Left( 20 );
                                newNumber.Description = communicationComment;

                                newNumber.NumberTypeValueId = numberTypeValues.Where( v => type.StartsWith( v.Name ) )
                                    .Select( v => (int?)v.Id ).FirstOrDefault();

                                newNumberList.Add( newNumber );
                                completed++;
                            }
                        }
                    }
                    else
                    {
                        var person = personService.Get( (int)personId );
                        person.Attributes = new Dictionary<string, AttributeCache>();
                        person.AttributeValues = new Dictionary<string, List<AttributeValue>>();

                        if ( value.IsValidEmail() )
                        {
                            string secondaryEmail = string.Empty;
                            if ( string.IsNullOrWhiteSpace( person.Email ) || ( isListed && person.IsEmailActive == false ) )
                            {
                                secondaryEmail = person.Email;
                                person.Email = value.Left( 75 );
                                person.IsEmailActive = isListed;
                                person.DoNotEmail = !isListed;
                                person.ModifiedDateTime = lastUpdated;
                                person.EmailNote = communicationComment;
                            }
                            else if ( !person.Email.Equals( value ) )
                            {
                                secondaryEmail = value;
                            }

                            if ( !string.IsNullOrWhiteSpace( secondaryEmail ) )
                            {
                                person.Attributes.Add( "SecondaryEmail", secondaryEmailAttribute );
                                person.AttributeValues.Add( "SecondaryEmail", new List<AttributeValue>() );
                                person.AttributeValues["SecondaryEmail"].Add( new AttributeValue()
                                {
                                    AttributeId = secondaryEmailAttribute.Id,
                                    Value = secondaryEmail,
                                    Order = 0
                                } );
                            }
                        }
                        else if ( type.Contains( "Twitter" ) )
                        {
                            person.Attributes.Add( "TwitterUsername", twitterUsernameAttribute );
                            person.AttributeValues.Add( "TwitterUsername", new List<AttributeValue>() );
                            person.AttributeValues["TwitterUsername"].Add( new AttributeValue()
                            {
                                AttributeId = twitterUsernameAttribute.Id,
                                Value = value,
                                Order = 0
                            } );
                        }
                        else if ( type.Contains( "Facebook" ) )
                        {
                            person.Attributes.Add( "FacebookUsername", facebookUsernameAttribute );
                            person.AttributeValues.Add( "FacebookUsername", new List<AttributeValue>() );
                            person.AttributeValues["FacebookUsername"].Add( new AttributeValue()
                            {
                                AttributeId = facebookUsernameAttribute.Id,
                                Value = value,
                                Order = 0
                            } );
                        }

                        updatedPersonList.Add( person );
                        completed++;
                    }

                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} records imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        RockTransactionScope.WrapTransaction( () =>
                        {
                            var numberService = new PhoneNumberService();
                            numberService.RockContext.PhoneNumbers.AddRange( newNumberList );
                            numberService.RockContext.SaveChanges();

                            // don't add updatedPeople, they're already tracked with current context
                            personService.RockContext.SaveChanges();

                            var attributeValueService = new AttributeValueService();
                            foreach ( var updatedPerson in updatedPersonList.Where( p => p.Attributes.Any() ) )
                            {
                                foreach ( var attributeCache in updatedPerson.Attributes.Select( a => a.Value ) )
                                {
                                    var newValue = updatedPerson.AttributeValues[attributeCache.Key].FirstOrDefault();
                                    if ( newValue != null )
                                    {
                                        newValue.EntityId = updatedPerson.Id;
                                        attributeValueService.RockContext.AttributeValues.Add( newValue );
                                    }
                                }
                            }

                            attributeValueService.RockContext.SaveChanges();
                        } );

                        // reset the person context so it doesn't bloat
                        if ( updatedPersonList.Any() )
                        {
                            personService = new PersonService();
                            updatedPersonList.Clear();
                        }

                        newNumberList.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            if ( newNumberList.Any() || updatedPersonList.Any() )
            {
                RockTransactionScope.WrapTransaction( () =>
                {
                    var numberService = new PhoneNumberService();
                    numberService.RockContext.PhoneNumbers.AddRange( newNumberList );
                    numberService.RockContext.SaveChanges();
                    personService.RockContext.SaveChanges();

                    var attributeValueService = new AttributeValueService();
                    foreach ( var updatedPerson in updatedPersonList.Where( p => p.Attributes.Any() ) )
                    {
                        foreach ( var attributeCache in updatedPerson.Attributes.Select( a => a.Value ) )
                        {
                            var newValue = updatedPerson.AttributeValues[attributeCache.Key].FirstOrDefault();
                            if ( newValue != null )
                            {
                                newValue.EntityId = updatedPerson.Id;
                                attributeValueService.RockContext.AttributeValues.Add( newValue );
                            }
                        }
                    }

                    attributeValueService.RockContext.SaveChanges();
                } );
            }

            ReportProgress( 100, string.Format( "Finished communication import: {0:N0} records imported.", completed ) );
        }
    }
}
