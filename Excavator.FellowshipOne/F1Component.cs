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
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using Excavator.Utility;
using OrcaMDF.Core.Engine;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// This extends the base Excavator class to consume FellowshipOne's database model.
    /// Data models and mapping methods are in the Models and Maps directories, respectively.
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    partial class F1Component : ExcavatorComponent
    {
        #region Fields

        /// <summary>
        /// Gets the full name of the excavator type.
        /// </summary>
        /// <value>
        /// The full name.
        /// </value>
        public override string FullName
        {
            get { return "FellowshipOne"; }
        }

        /// <summary>
        /// Gets the supported file extension type(s).
        /// </summary>
        /// <value>
        /// The supported extension type(s).
        /// </value>
        public override string ExtensionType
        {
            get { return ".mdf"; }
        }

        /// <summary>
        /// The local database
        /// </summary>
        protected Database Database;

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        protected static PersonAlias ImportPersonAlias;

        /// <summary>
        /// All the people who've been imported
        /// </summary>
        protected static List<ImportedPerson> ImportedPeople;

        /// <summary>
        /// All imported batches. Used in Batches & Contributions
        /// </summary>
        protected static Dictionary<int, int?> ImportedBatches;

        /// <summary>
        /// All campuses
        /// </summary>
        protected static List<CampusCache> CampusList;

        // Existing entity types

        protected static int IntegerFieldTypeId;
        protected static int TextFieldTypeId;
        protected static int PersonEntityTypeId;

        // Custom attribute types

        protected static AttributeCache IndividualIdAttribute;
        protected static AttributeCache HouseholdIdAttribute;
        protected static AttributeCache InfellowshipLoginAttribute;
        protected static AttributeCache SecondaryEmailAttribute;

        // Flag to run postprocessing audits during save
        protected static bool DisableAudit = true;

        // Flag to designate household visitors
        protected const string FamilyVisitor = "1";

        #endregion Fields

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override bool LoadSchema( string fileName )
        {
            Database = new Database( fileName );
            TableNodes = new List<DatabaseNode>();
            var scanner = new DataScanner( Database );
            var tables = Database.Dmvs.Tables;

            foreach ( var table in tables.Where( t => !t.IsMSShipped ).OrderBy( t => t.Name ) )
            {
                var rows = scanner.ScanTable( table.Name );
                var tableItem = new DatabaseNode();
                tableItem.Name = table.Name;

                var rowData = rows.FirstOrDefault();
                if ( rowData != null )
                {
                    foreach ( var column in rowData.Columns )
                    {
                        var childItem = new DatabaseNode();
                        childItem.Name = column.Name;
                        childItem.NodeType = Extensions.GetSQLType( column.Type );
                        childItem.Value = rowData[column] ?? DBNull.Value;
                        childItem.Table.Add( tableItem );
                        tableItem.Columns.Add( childItem );
                    }
                }

                TableNodes.Add( tableItem );
            }

            return TableNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override int TransformData( string importUser = null )
        {
            ReportProgress( 0, "Starting import..." );
            var rockContext = new RockContext();
            var personService = new PersonService( rockContext );
            var importPerson = personService.GetByFullName( importUser, allowFirstNameOnly: true ).FirstOrDefault();

            if ( importPerson == null )
            {
                importPerson = personService.Queryable().FirstOrDefault();
            }

            ImportPersonAlias = new PersonAliasService( rockContext ).Get( importPerson.Id );
            var tableList = TableNodes.Where( n => n.Checked != false ).ToList();

            ReportProgress( 0, "Checking for existing attributes..." );
            LoadExistingRockData();

            ReportProgress( 0, "Checking for table dependencies..." );
            bool isValidImport = ImportedPeople.Any() || tableList.Any( n => n.Name.Equals( "Individual_Household" ) );

            var tableDependencies = new List<string>();
            tableDependencies.Add( "Batch" );                // needed to attribute contributions properly
            tableDependencies.Add( "Users" );                // needed for notes, user logins
            tableDependencies.Add( "Company" );              // needed to attribute any business items
            tableDependencies.Add( "Individual_Household" ); // needed for just about everything

            if ( isValidImport )
            {
                // Order tables so non-dependents are imported first
                if ( tableList.Any( n => tableDependencies.Contains( n.Name ) ) )
                {
                    tableList = tableList.OrderByDescending( n => tableDependencies.IndexOf( n.Name ) ).ToList();
                }

                var scanner = new DataScanner( Database );
                foreach ( var table in tableList )
                {
                    if ( !tableDependencies.Contains( table.Name ) )
                    {
                        switch ( table.Name )
                        {
                            case "Account":
                                MapBankAccount( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Attribute":
                                MapAttribute( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Communication":
                                MapCommunication( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Contribution":
                                MapContribution( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Household_Address":
                                MapFamilyAddress( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Notes":
                                MapNotes( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            case "Pledge":
                                MapPledge( scanner.ScanTable( table.Name ).AsQueryable() );
                                break;

                            default:
                                break;
                        }
                    }
                    else
                    {
                        if ( table.Name == "Batch" )
                        {
                            MapBatch( scanner.ScanTable( table.Name ).AsQueryable() );
                        }
                        else if ( table.Name == "Company" )
                        {
                            MapCompany( scanner.ScanTable( table.Name ).AsQueryable() );
                        }
                        else if ( table.Name == "Individual_Household" )
                        {
                            MapPerson( scanner.ScanTable( table.Name ).AsQueryable() );
                        }
                        else if ( table.Name == "Users" )
                        {
                            MapUsers( scanner.ScanTable( table.Name ).AsQueryable() );
                        }
                    }
                }

                ReportProgress( 100, "Import completed.  " );
            }
            else
            {
                ReportProgress( 0, "No imported people exist. Please include the Individual_Household table during the import." );
            }

            return 100; // return total number of rows imported?
        }

        /// <summary>
        /// Loads Rock data that's used globally by the transform
        /// </summary>
        private void LoadExistingRockData()
        {
            var lookupContext = new RockContext();
            var attributeValueService = new AttributeValueService( lookupContext );
            var attributeService = new AttributeService( lookupContext );

            IntegerFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;
            TextFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;
            PersonEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            CampusList = CampusCache.All();

            int attributeEntityTypeId = EntityTypeCache.Read( "Rock.Model.Attribute" ).Id;
            int batchEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialBatch" ).Id;
            int userLoginTypeId = EntityTypeCache.Read( "Rock.Model.UserLogin" ).Id;

            int visitInfoCategoryId = new CategoryService( lookupContext ).GetByEntityTypeId( attributeEntityTypeId )
                .Where( c => c.Name == "Visit Information" ).Select( c => c.Id ).FirstOrDefault();

            // Look up and create attributes for F1 unique identifiers if they don't exist
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();

            var householdAttribute = personAttributes.FirstOrDefault( a => a.Key == "F1HouseholdId" );
            if ( householdAttribute == null )
            {
                householdAttribute = new Rock.Model.Attribute();
                householdAttribute.Key = "F1HouseholdId";
                householdAttribute.Name = "F1 Household Id";
                householdAttribute.FieldTypeId = IntegerFieldTypeId;
                householdAttribute.EntityTypeId = PersonEntityTypeId;
                householdAttribute.EntityTypeQualifierValue = string.Empty;
                householdAttribute.EntityTypeQualifierColumn = string.Empty;
                householdAttribute.Description = "The FellowshipOne household identifier for the person that was imported";
                householdAttribute.DefaultValue = string.Empty;
                householdAttribute.IsMultiValue = false;
                householdAttribute.IsRequired = false;
                householdAttribute.Order = 0;

                lookupContext.Attributes.Add( householdAttribute );
                lookupContext.SaveChanges( DisableAudit );
                personAttributes.Add( householdAttribute );
            }

            var individualAttribute = personAttributes.FirstOrDefault( a => a.Key == "F1IndividualId" );
            if ( individualAttribute == null )
            {
                individualAttribute = new Rock.Model.Attribute();
                individualAttribute.Key = "F1IndividualId";
                individualAttribute.Name = "F1 Individual Id";
                individualAttribute.FieldTypeId = IntegerFieldTypeId;
                individualAttribute.EntityTypeId = PersonEntityTypeId;
                individualAttribute.EntityTypeQualifierValue = string.Empty;
                individualAttribute.EntityTypeQualifierColumn = string.Empty;
                individualAttribute.Description = "The FellowshipOne individual identifier for the person that was imported";
                individualAttribute.DefaultValue = string.Empty;
                individualAttribute.IsMultiValue = false;
                individualAttribute.IsRequired = false;
                individualAttribute.Order = 0;

                lookupContext.Attributes.Add( individualAttribute );
                lookupContext.SaveChanges( DisableAudit );
                personAttributes.Add( individualAttribute );
            }

            var secondaryEmailAttribute = personAttributes.FirstOrDefault( a => a.Key == "SecondaryEmail" );
            if ( secondaryEmailAttribute == null )
            {
                secondaryEmailAttribute = new Rock.Model.Attribute();
                secondaryEmailAttribute.Key = "SecondaryEmail";
                secondaryEmailAttribute.Name = "Secondary Email";
                secondaryEmailAttribute.FieldTypeId = TextFieldTypeId;
                secondaryEmailAttribute.EntityTypeId = PersonEntityTypeId;
                secondaryEmailAttribute.EntityTypeQualifierValue = string.Empty;
                secondaryEmailAttribute.EntityTypeQualifierColumn = string.Empty;
                secondaryEmailAttribute.Description = "The secondary email for this person";
                secondaryEmailAttribute.DefaultValue = string.Empty;
                secondaryEmailAttribute.IsMultiValue = false;
                secondaryEmailAttribute.IsRequired = false;
                secondaryEmailAttribute.Order = 0;

                lookupContext.Attributes.Add( secondaryEmailAttribute );
                var visitInfoCategory = new CategoryService( lookupContext ).Get( visitInfoCategoryId );
                secondaryEmailAttribute.Categories.Add( visitInfoCategory );
                lookupContext.SaveChanges( DisableAudit );
            }

            var infellowshipLoginAttribute = personAttributes.FirstOrDefault( a => a.Key == "InfellowshipLogin" );
            if ( infellowshipLoginAttribute == null )
            {
                infellowshipLoginAttribute = new Rock.Model.Attribute();
                infellowshipLoginAttribute.Key = "InfellowshipLogin";
                infellowshipLoginAttribute.Name = "Infellowship Login";
                infellowshipLoginAttribute.FieldTypeId = TextFieldTypeId;
                infellowshipLoginAttribute.EntityTypeId = PersonEntityTypeId;
                infellowshipLoginAttribute.EntityTypeQualifierValue = string.Empty;
                infellowshipLoginAttribute.EntityTypeQualifierColumn = string.Empty;
                infellowshipLoginAttribute.Description = "The infellowship login for this person";
                infellowshipLoginAttribute.DefaultValue = string.Empty;
                infellowshipLoginAttribute.IsMultiValue = false;
                infellowshipLoginAttribute.IsRequired = false;
                infellowshipLoginAttribute.Order = 0;

                // don't add a category as this attribute is only used via the API
                lookupContext.Attributes.Add( infellowshipLoginAttribute );
                lookupContext.SaveChanges( DisableAudit );
            }

            IndividualIdAttribute = AttributeCache.Read( individualAttribute.Id );
            HouseholdIdAttribute = AttributeCache.Read( householdAttribute.Id );
            InfellowshipLoginAttribute = AttributeCache.Read( infellowshipLoginAttribute.Id );
            SecondaryEmailAttribute = AttributeCache.Read( secondaryEmailAttribute.Id );

            ReportProgress( 0, "Checking for existing people..." );
            var visitorIdList = new PersonService( lookupContext ).Queryable().Where( p => p.ReviewReasonNote.Equals( FamilyVisitor ) ).OrderBy( p => p.Id ).Select( p => (int?)p.Id ).ToList();
            var aliasIdList = new PersonAliasService( lookupContext ).Queryable().Where( p => p.ForeignId != null ).Select( pa => new { PersonAliasId = pa.Id, PersonId = pa.PersonId, IndividualId = pa.ForeignId } ).ToList();
            var householdIdList = attributeValueService.GetByAttributeId( householdAttribute.Id ).Select( av => new { PersonId = av.EntityId, HouseholdId = av.Value } ).ToList();

            ImportedPeople = householdIdList.GroupJoin( aliasIdList,
                household => household.PersonId,
                aliases => aliases.PersonId,
                ( household, aliases ) => new ImportedPerson
                    {
                        PersonAliasId = aliases.Select( a => a.PersonAliasId ).FirstOrDefault(),
                        HouseholdId = household.HouseholdId.AsType<int?>(),
                        IndividualId = aliases.Select( a => a.IndividualId.AsType<int?>() ).FirstOrDefault(),
                        IsFamilyMember = visitorIdList.Contains( (int)household.PersonId )
                    }
                ).ToList();

            ImportedBatches = new FinancialBatchService( lookupContext ).Queryable()
                .Where( b => b.ForeignId != null )
                .ToDictionary( t => t.ForeignId.AsType<int>(), t => (int?)t.Id );
        }

        /// <summary>
        /// Gets the person alias identifier.
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <param name="householdId">The household identifier.</param>
        /// <param name="includeVisitors">if set to <c>true</c> [include visitors].</param>
        /// <returns></returns>
        protected static int? GetPersonAliasId( int? individualId = null, int? householdId = null, bool includeVisitors = true )
        {
            if ( individualId != null && householdId != null )
            {
                return ImportedPeople.Where( p => p.IndividualId == individualId && p.HouseholdId == householdId )
                    .Select( p => p.PersonAliasId ).FirstOrDefault();
            }
            else if ( individualId != null )
            {
                return ImportedPeople.Where( p => p.IndividualId == individualId && ( includeVisitors || p.IsFamilyMember ) )
                    .Select( p => p.PersonAliasId ).FirstOrDefault();
            }
            else if ( householdId != null )
            {
                return ImportedPeople.Where( p => p.HouseholdId == householdId && ( includeVisitors || p.IsFamilyMember ) )
                    .Select( p => p.PersonAliasId ).FirstOrDefault();
            }
            else
            {
                return null;
            }
            //else
            //{
            //    var rockContext = new RockContext();
            //    int lookupAttributeId = individualId.HasValue ? IndividualAttributeId : HouseholdAttributeId;
            //    string lookupValueId = individualId.HasValue ? individualId.ToString() : householdId.ToString();
            //    var lookup = new AttributeValueService( rockContext ).Queryable()
            //        .Where( av => av.AttributeId == lookupAttributeId && av.Value == lookupValueId );

            //    var lookupPerson = lookup.FirstOrDefault();
            //    if ( lookupPerson != null )
            //    {
            //        ImportedPeople.Add( new ImportedPerson()
            //        {
            //            PersonAliasId = lookupPerson.EntityId,
            //            HouseholdId = householdId,
            //            IndividualId = individualId
            //        } );

            //        return lookupPerson.EntityId;
            //    }
            //}
        }

        /// <summary>
        /// Gets the family by household identifier.
        /// </summary>
        /// <param name="householdId">The household identifier.</param>
        /// <param name="includeVisitors">if set to <c>true</c> [include visitors].</param>
        /// <returns></returns>
        protected static List<int?> GetFamilyByHouseholdId( int? householdId, bool includeVisitors = true )
        {
            return ImportedPeople.Where( p => p.HouseholdId == householdId && p.PersonAliasId != null && ( includeVisitors || p.IsFamilyMember ) )
                .Select( p => p.PersonAliasId ).ToList();
        }

        #endregion Methods
    }

    /// <summary>
    /// Helper class to store references to people that've been imported
    /// </summary>
    public class ImportedPerson
    {
        /// <summary>
        /// Stores the Rock.PersonAliasId
        /// </summary>
        public int? PersonAliasId;

        /// <summary>
        /// Stores the F1 Individual Id
        /// </summary>
        public int? IndividualId;

        /// <summary>
        /// Stores the F1 Household Id
        /// </summary>
        public int? HouseholdId;

        /// <summary>
        /// Stores whether the person was part of the family
        /// </summary>
        public bool IsFamilyMember;
    }
}