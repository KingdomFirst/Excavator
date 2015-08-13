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
using System.ComponentModel.Composition;
using System.Data.Entity;
using System.Linq;
using Excavator.Utility;
using OrcaMDF.Core.Engine;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using Database = OrcaMDF.Core.Engine.Database;

namespace Excavator.F1
{
    /// <summary>
    /// This extends the base Excavator class to consume FellowshipOne's database model.
    /// Data models and mapping methods are in the Models and Maps directories, respectively.
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    public partial class F1Component : ExcavatorComponent
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
        protected static int? ImportPersonAliasId;

        /// <summary>
        /// All the people who've been imported
        /// </summary>
        protected static List<PersonKeys> ImportedPeople;

        /// <summary>
        /// All imported batches. Used in Batches & Contributions
        /// </summary>
        protected static Dictionary<int, int?> ImportedBatches;

        /// <summary>
        /// All campuses
        /// </summary>
        protected static List<CampusCache> CampusList;

        // Existing entity types

        protected static int TextFieldTypeId;
        protected static int IntegerFieldTypeId;
        protected static int PersonEntityTypeId;

        // Custom attribute types

        protected static AttributeCache IndividualIdAttribute;
        protected static AttributeCache HouseholdIdAttribute;
        protected static AttributeCache InFellowshipLoginAttribute;
        protected static AttributeCache SecondaryEmailAttribute;

        // Flag to designate household role
        public enum FamilyRole
        {
            Adult = 0,
            Child = 1,
            Visitor = 2
        };

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
            DataNodes = new List<DataNode>();
            var scanner = new DataScanner( Database );
            var tables = Database.Dmvs.Tables;

            foreach ( var table in tables.Where( t => !t.IsMSShipped ).OrderBy( t => t.Name ) )
            {
                var rows = scanner.ScanTable( table.Name );
                var tableItem = new DataNode();
                tableItem.Name = table.Name;

                var rowData = rows.FirstOrDefault();
                if ( rowData != null )
                {
                    foreach ( var column in rowData.Columns )
                    {
                        var childItem = new DataNode();
                        childItem.Name = column.Name;
                        childItem.NodeType = Extensions.GetSQLType( column.Type );
                        childItem.Value = rowData[column] ?? DBNull.Value;
                        childItem.Parent.Add( tableItem );
                        tableItem.Children.Add( childItem );
                    }
                }

                DataNodes.Add( tableItem );
            }

            return DataNodes.Count > 0 ? true : false;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override int TransformData( Dictionary<string, string> settings )
        {
            var importUser = settings["ImportUser"];

            ReportProgress( 0, "Starting health checks..." );
            var rockContext = new RockContext();
            var personService = new PersonService( rockContext );
            var importPerson = personService.GetByFullName( importUser, allowFirstNameOnly: true ).FirstOrDefault();

            if ( importPerson == null )
            {
                importPerson = personService.Queryable().AsNoTracking().FirstOrDefault();
            }

            ImportPersonAliasId = importPerson.PrimaryAliasId;
            var tableList = DataNodes.Where( n => n.Checked != false ).ToList();

            ReportProgress( 0, "Checking for existing attributes..." );
            LoadExistingRockData();

            ReportProgress( 0, "Checking for existing people..." );
            bool isValidImport = ImportedPeople.Any() || tableList.Any( n => n.Name.Equals( "Individual_Household" ) );

            var tableDependencies = new List<string>();
            tableDependencies.Add( "Batch" );                // needed to attribute contributions properly
            tableDependencies.Add( "Users" );                // needed for notes, user logins
            tableDependencies.Add( "Company" );              // needed to attribute any business items
            tableDependencies.Add( "Individual_Household" ); // needed for just about everything

            if ( isValidImport )
            {
                ReportProgress( 0, "Checking for table dependencies..." );
                // Order tables so non-dependents are imported first
                if ( tableList.Any( n => tableDependencies.Contains( n.Name ) ) )
                {
                    tableList = tableList.OrderByDescending( n => tableDependencies.IndexOf( n.Name ) ).ToList();
                }

                ReportProgress( 0, "Starting data import..." );
                var scanner = new DataScanner( Database );
                foreach ( var table in tableList )
                {
                    switch ( table.Name )
                    {
                        case "Account":
                            MapBankAccount( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Batch":
                            MapBatch( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Communication":
                            MapCommunication( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Company":
                            MapCompany( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Contribution":
                            MapContribution( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Household_Address":
                            MapFamilyAddress( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Individual_Household":
                            MapPerson( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Notes":
                            MapNotes( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Pledge":
                            MapPledge( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        case "Users":
                            MapUsers( scanner.ScanTable( table.Name ).AsQueryable() );
                            break;

                        default:
                            break;
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
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).AsNoTracking().ToList();

            var householdAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( "F1HouseholdId", StringComparison.InvariantCultureIgnoreCase ) );
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
                lookupContext.SaveChanges( DisableAuditing );
                personAttributes.Add( householdAttribute );
            }

            var individualAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( "F1IndividualId", StringComparison.InvariantCultureIgnoreCase ) );
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
                lookupContext.SaveChanges( DisableAuditing );
                personAttributes.Add( individualAttribute );
            }

            var secondaryEmailAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( "SecondaryEmail", StringComparison.InvariantCultureIgnoreCase ) );
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
                lookupContext.SaveChanges( DisableAuditing );
            }

            var infellowshipLoginAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( "InFellowshipLogin", StringComparison.InvariantCultureIgnoreCase ) );
            if ( infellowshipLoginAttribute == null )
            {
                infellowshipLoginAttribute = new Rock.Model.Attribute();
                infellowshipLoginAttribute.Key = "InFellowshipLogin";
                infellowshipLoginAttribute.Name = "InFellowship Login";
                infellowshipLoginAttribute.FieldTypeId = TextFieldTypeId;
                infellowshipLoginAttribute.EntityTypeId = PersonEntityTypeId;
                infellowshipLoginAttribute.EntityTypeQualifierValue = string.Empty;
                infellowshipLoginAttribute.EntityTypeQualifierColumn = string.Empty;
                infellowshipLoginAttribute.Description = "The InFellowship login for this person";
                infellowshipLoginAttribute.DefaultValue = string.Empty;
                infellowshipLoginAttribute.IsMultiValue = false;
                infellowshipLoginAttribute.IsRequired = false;
                infellowshipLoginAttribute.Order = 0;

                // don't add a category as this attribute is only used via the API
                lookupContext.Attributes.Add( infellowshipLoginAttribute );
                lookupContext.SaveChanges( DisableAuditing );
            }

            IndividualIdAttribute = AttributeCache.Read( individualAttribute.Id );
            HouseholdIdAttribute = AttributeCache.Read( householdAttribute.Id );
            InFellowshipLoginAttribute = AttributeCache.Read( infellowshipLoginAttribute.Id );
            SecondaryEmailAttribute = AttributeCache.Read( secondaryEmailAttribute.Id );

            var currentDate = DateTime.Now;
            var aliasIdList = new PersonAliasService( lookupContext ).Queryable().AsNoTracking()
                .Select( pa => new
                {
                    PersonAliasId = pa.Id,
                    PersonId = pa.PersonId,
                    IndividualId = pa.ForeignId,
                    FamilyRole = pa.Person.ReviewReasonNote
                } ).ToList();
            var householdIdList = attributeValueService.GetByAttributeId( householdAttribute.Id ).AsNoTracking()
                .Select( av => new
                {
                    PersonId = (int)av.EntityId,
                    HouseholdId = av.Value
                } ).ToList();

            ImportedPeople = householdIdList.GroupJoin( aliasIdList,
                household => household.PersonId,
                aliases => aliases.PersonId,
                ( household, aliases ) => new PersonKeys
                    {
                        PersonAliasId = aliases.Select( a => a.PersonAliasId ).FirstOrDefault(),
                        PersonId = household.PersonId,
                        IndividualId = aliases.Select( a => a.IndividualId.AsType<int?>() ).FirstOrDefault(),
                        HouseholdId = household.HouseholdId.AsType<int?>(),
                        FamilyRoleId = aliases.Select( a => a.FamilyRole.ConvertToEnum<FamilyRole>( 0 ) ).FirstOrDefault()
                    }
                ).ToList();

            ImportedBatches = new FinancialBatchService( lookupContext ).Queryable().AsNoTracking()
                .Where( b => b.ForeignId != null )
                .ToDictionary( t => t.ForeignId.AsType<int>(), t => (int?)t.Id );
        }

        /// <summary>
        /// Gets the person keys.
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <param name="householdId">The household identifier.</param>
        /// <param name="includeVisitors">if set to <c>true</c> [include visitors].</param>
        /// <returns></returns>
        protected static PersonKeys GetPersonKeys( int? individualId = null, int? householdId = null, bool includeVisitors = true )
        {
            if ( individualId != null )
            {
                return ImportedPeople.FirstOrDefault( p => p.IndividualId == individualId );
            }
            else if ( householdId != null )
            {
                return ImportedPeople.Where( p => p.HouseholdId == householdId && ( includeVisitors || p.FamilyRoleId != FamilyRole.Visitor ) )
                    .OrderBy( p => (int)p.FamilyRoleId )
                    .FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the family by household identifier.
        /// </summary>
        /// <param name="householdId">The household identifier.</param>
        /// <param name="includeVisitors">if set to <c>true</c> [include visitors].</param>
        /// <returns></returns>
        protected static List<PersonKeys> GetFamilyByHouseholdId( int? householdId, bool includeVisitors = true )
        {
            return ImportedPeople.Where( p => p.HouseholdId == householdId && ( includeVisitors || p.FamilyRoleId != FamilyRole.Visitor ) ).ToList();
        }

        #endregion Methods
    }

    /// <summary>
    /// Helper class to store references to people that've been imported
    /// </summary>
    public class PersonKeys
    {
        /// <summary>
        /// Stores the Rock PersonAliasId
        /// </summary>
        public int PersonAliasId;

        /// <summary>
        /// Stores the Rock PersonId
        /// </summary>
        public int PersonId;

        /// <summary>
        /// Stores the F1 Individual Id
        /// </summary>
        public int? IndividualId;

        /// <summary>
        /// Stores the F1 Household Id
        /// </summary>
        public int? HouseholdId;

        /// <summary>
        /// Stores how the person is connected to the family
        /// </summary>
        public F1Component.FamilyRole FamilyRoleId;
    }
}