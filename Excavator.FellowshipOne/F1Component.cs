using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data.Entity;
using System.Linq;
using OrcaMDF.Core.Engine;
using OrcaMDF.Core.MetaData;
using Rock;
using Rock.Data;
using Rock.Model;
using static Excavator.Utility.CachedTypes;
using static Excavator.Utility.Extensions;
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
        /// Gets the full name of the Excavator type.
        /// </summary>
        /// <value>
        /// The full name.
        /// </value>
        public override string FullName => "FellowshipOne";

        /// <summary>
        /// Gets the supported file extension type(s).
        /// </summary>
        /// <value>
        /// The supported extension type(s).
        /// </value>
        public override string ExtensionType => ".mdf";

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
        /// All the group types that have been imported
        /// </summary>
        private List<GroupType> ImportedGroupTypes;

        /// <summary>
        /// The F1.UserId and Rock.PersonAliasId for portal users
        /// </summary>
        private Dictionary<int, int> PortalUsers;

        /// <summary>
        /// All the group types that have been imported
        /// </summary>
        private List<Group> ImportedGroups;

        /// <summary>
        /// All imported batches. Used in Batches & Contributions
        /// </summary>
        protected static Dictionary<int, int?> ImportedBatches;

        // Custom attribute types

        protected static Rock.Model.Attribute IndividualIdAttribute;
        protected static Rock.Model.Attribute HouseholdIdAttribute;
        protected static Rock.Model.Attribute InFellowshipLoginAttribute;
        protected static Rock.Model.Attribute SecondaryEmailAttribute;

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
            var tables = Database.Dmvs.Tables.Where( t => !t.IsMSShipped )
                .OrderBy( t => t.Name ).ToList();

            foreach ( var table in tables )
            {
                // ignore tables that can't be read successfully
                Row rowData = null;
                try
                {
                    rowData = scanner.ScanTable( table.Name ).FirstOrDefault();
                }
                catch
                {
                    LogException( string.Empty, $"Could not get data preview for {table.Name}. A blank record will preview instead." );
                }

                var tableItem = new DataNode
                {
                    Name = table.Name
                };

                // get the table schema
                foreach ( var column in Database.Dmvs.Columns.Where( x => x.ObjectID == table.ObjectID ) )
                {
                    var childItem = new DataNode
                    {
                        Name = column.Name,
                        Value = DBNull.Value
                    };

                    // try to read data for this table
                    if ( rowData != null )
                    {
                        var dataColumn = rowData.Columns.FirstOrDefault( d => d.Name == column.Name );
                        if ( dataColumn != null )
                        {
                            childItem.NodeType = GetSQLType( dataColumn.Type );
                            childItem.Value = rowData[dataColumn] ?? DBNull.Value;
                        }
                    }

                    childItem.Parent.Add( tableItem );
                    tableItem.Children.Add( childItem );
                }

                DataNodes.Add( tableItem );
            }

            return DataNodes.Count > 0 ? true : false;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <param name="settings">todo: describe settings parameter on TransformData</param>
        /// <returns></returns>
        public override int TransformData( Dictionary<string, string> settings )
        {
            var importUser = settings["ImportUser"];

            ReportProgress( 0, "Starting health checks..." );
            var scanner = new DataScanner( Database );
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
            LoadGlobalObjects( scanner );

            ReportProgress( 0, "Checking for existing people..." );
            var isValidImport = ImportedPeople.Any() || tableList.Any( n => n.Name.Equals( "Individual_Household" ) );

            var tableDependencies = new List<string>();
            tableDependencies.Add( "ContactFormData" );      // needed for individual contact notes
            tableDependencies.Add( "Groups" );               // needed for home group structure
            tableDependencies.Add( "RLC" );                  // needed for bottom-level group and location structure
            tableDependencies.Add( "Activity_Group" );       // needed for mid-level group structure
            tableDependencies.Add( "ActivityMinistry" );     // needed for top-level group structure
            tableDependencies.Add( "Batch" );                // needed to attribute contributions properly
            tableDependencies.Add( "Users" );                // needed for notes, user logins
            tableDependencies.Add( "Company" );              // needed to attribute any business items
            tableDependencies.Add( "Individual_Household" ); // needed for just about everything

            if ( isValidImport )
            {
                ReportProgress( 0, "Checking for table dependencies..." );
                // Order tables so dependencies are imported first
                if ( tableList.Any( n => tableDependencies.Contains( n.Name ) ) )
                {
                    tableList = tableList.OrderByDescending( n => tableDependencies.IndexOf( n.Name ) ).ToList();
                }

                // get list of objects to grab their rowcounts
                var objectNameIds = Database.Dmvs.Objects.Where( o => !o.IsMSShipped ).ToDictionary( t => t.Name, t => t.ObjectID );

                ReportProgress( 0, "Starting data import..." );
                foreach ( var table in tableList )
                {
                    var totalRows = Database.Dmvs.Partitions.FirstOrDefault( p => p.ObjectID == objectNameIds[table.Name] ).Rows;

                    switch ( table.Name )
                    {
                        case "Account":
                            MapBankAccount( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Batch":
                            MapBatch( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Communication":
                            MapCommunication( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Company":
                            MapCompany( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "ContactFormData":
                            MapContactFormData( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Contribution":
                            MapContribution( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Household_Address":
                            MapFamilyAddress( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "IndividualContactNotes":
                            MapIndividualContactNotes( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Individual_Household":
                            MapPerson( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Notes":
                            MapNotes( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Pledge":
                            MapPledge( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
                            break;

                        case "Users":
                            MapUsers( scanner.ScanTable( table.Name ).AsQueryable(), totalRows );
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
        /// <param name="scanner">The scanner.</param>
        private void LoadGlobalObjects( DataScanner scanner )
        {
            var lookupContext = new RockContext();
            var attributeValueService = new AttributeValueService( lookupContext );
            var attributeService = new AttributeService( lookupContext );

            var visitInfoCategoryId = new CategoryService( lookupContext ).GetByEntityTypeId( AttributeEntityTypeId )
                .Where( c => c.Name == "Visit Information" ).Select( c => c.Id ).FirstOrDefault();

            // Look up and create attributes for F1 unique identifiers if they don't exist
            var attributeKey = "F1HouseholdId";
            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).AsNoTracking().ToList();
            HouseholdIdAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( attributeKey, StringComparison.InvariantCultureIgnoreCase ) );
            if ( HouseholdIdAttribute == null )
            {
                HouseholdIdAttribute = AddEntityAttribute( lookupContext, PersonEntityTypeId, string.Empty, string.Empty, string.Format( "{0} imported {1}", attributeKey, ImportDateTime ),
                    "Visit Information", "F1 Household Id", attributeKey, IntegerFieldTypeId
                );
            }

            attributeKey = "F1IndividualId";
            IndividualIdAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( attributeKey, StringComparison.InvariantCultureIgnoreCase ) );
            if ( IndividualIdAttribute == null )
            {
                IndividualIdAttribute = AddEntityAttribute( lookupContext, PersonEntityTypeId, string.Empty, string.Empty, string.Format( "{0} imported {1}", attributeKey, ImportDateTime ),
                    "Visit Information", "F1 Individual Id", attributeKey, IntegerFieldTypeId
                );
            }

            attributeKey = "SecondaryEmail";
            SecondaryEmailAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( attributeKey, StringComparison.InvariantCultureIgnoreCase ) );
            if ( SecondaryEmailAttribute == null )
            {
                SecondaryEmailAttribute = AddEntityAttribute( lookupContext, PersonEntityTypeId, string.Empty, string.Empty, string.Format( "{0} imported {1}", attributeKey, ImportDateTime ),
                    "Visit Information", "Secondary Email", attributeKey, TextFieldTypeId
                );
            }

            attributeKey = "InFellowshipLogin";
            InFellowshipLoginAttribute = personAttributes.FirstOrDefault( a => a.Key.Equals( attributeKey, StringComparison.InvariantCultureIgnoreCase ) );
            if ( InFellowshipLoginAttribute == null )
            {
                InFellowshipLoginAttribute = AddEntityAttribute( lookupContext, PersonEntityTypeId, string.Empty, string.Empty, string.Format( "{0} imported {1}", attributeKey, ImportDateTime ),
                    "Visit Information", "InFellowship Login", attributeKey, TextFieldTypeId
                );
            }

            var aliasIdList = new PersonAliasService( lookupContext ).Queryable().AsNoTracking()
                .Select( pa => new
                {
                    PersonAliasId = pa.Id,
                    PersonId = pa.PersonId,
                    ForeignId = pa.ForeignId,
                    FamilyRole = pa.Person.ReviewReasonNote
                } ).ToList();
            var householdIdList = attributeValueService.GetByAttributeId( HouseholdIdAttribute.Id ).AsNoTracking()
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
                    PersonForeignId = aliases.Select( a => a.ForeignId ).FirstOrDefault(),
                    GroupForeignId = household.HouseholdId.AsType<int?>(),
                    FamilyRoleId = aliases.Select( a => a.FamilyRole.ConvertToEnum<FamilyRole>( 0 ) ).FirstOrDefault()
                }
                ).ToList();

            ImportedGroupTypes = new GroupTypeService( lookupContext ).Queryable().AsNoTracking()
                .Where( t => t.Id != FamilyGroupTypeId && t.ForeignKey != null )
                .ToList();

            ImportedGroups = new GroupService( lookupContext ).Queryable().AsNoTracking()
                    .Where( g => g.GroupTypeId != FamilyGroupTypeId && g.ForeignKey != null )
                    .ToList();

            ImportedBatches = new FinancialBatchService( lookupContext ).Queryable().AsNoTracking()
                .Where( b => b.ForeignId.HasValue )
                .ToDictionary( t => (int)t.ForeignId, t => (int?)t.Id );

            // get the portal users for lookups on notes
            var userIdList = scanner.ScanTable( "Users" )
                .Select( s => new
                {
                    UserId = s["UserID"] as int?,
                    ForeignId = s["LinkedIndividualID"] as int?
                } ).ToList();

            PortalUsers = userIdList.Join( aliasIdList,
                users => users.ForeignId,
                aliases => aliases.ForeignId,
                ( users, aliases ) => new
                {
                    UserId = users.UserId,
                    PersonAliasId = aliases.PersonAliasId
                } ).ToDictionary( t => (int)t.UserId, t => t.PersonAliasId );
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
            if ( individualId.HasValue )
            {
                return ImportedPeople.FirstOrDefault( p => p.PersonForeignId == individualId );
            }
            else if ( householdId.HasValue )
            {
                return ImportedPeople.Where( p => p.GroupForeignId == householdId && ( includeVisitors || p.FamilyRoleId != FamilyRole.Visitor ) )
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
            return ImportedPeople.Where( p => p.GroupForeignId == householdId && ( includeVisitors || p.FamilyRoleId != FamilyRole.Visitor ) ).ToList();
        }

        #endregion Methods
    }

    #region Helper Classes

    /// <summary>
    /// Generic map interface
    /// </summary>
    public interface IFellowshipOne
    {
        void Map( IQueryable<Row> tableData );
    }

    /// <summary>
    /// Adapter helper method to call the right object Map()
    /// </summary>
    public static class IMapAdapterFactory
    {
        public static IFellowshipOne GetAdapter( string fileName )
        {
            IFellowshipOne adapter = null;

            //var configFileTypes = ConfigurationManager.GetSection( "binaryFileTypes" ) as NameValueCollection;

            // by default will assume a ministry document
            //var iBinaryFileType = typeof( IBinaryFile );
            //var mappedFileTypes = iBinaryFileType.Assembly.ExportedTypes
            //    .Where( p => iBinaryFileType.IsAssignableFrom( p ) && !p.IsInterface );
            //var selectedType = mappedFileTypes.FirstOrDefault( t => fileName.StartsWith( t.Name.RemoveSpecialCharacters() ) );
            //if ( selectedType != null )
            //{
            //    adapter = (IBinaryFile)Activator.CreateInstance( selectedType );
            //}
            //else
            //{
            //    adapter = new MinistryDocument();
            //}

            return adapter;
        }
    }

    #endregion
}
