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
using OrcaMDF.Core.Engine;
using OrcaMDF.Core.MetaData;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// This extends the base Excavator class to account for FellowshipOne's database model
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    class F1 : ExcavatorComponent
    {
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
        /// The person assigned to do the import
        /// </summary>
        private PersonAlias ImportPersonAlias;

        /// <summary>
        /// Any attributes associated with Rock Person(s)
        /// </summary>
        private List<Rock.Model.Attribute> PersonAttributeList;

        /// <summary>
        /// Holds a list of all the people who've been imported
        /// </summary>
        private List<ImportedPerson> ImportedPersonList;

        /// <summary>
        /// The imported batches
        /// </summary>
        private Dictionary<int?, int?> ImportedBatches;

        /// <summary>
        /// The imported contributions
        /// </summary>
        private Dictionary<int?, int?> ImportedContributions;

        private int IndividualAttributeId;
        private int HouseholdAttributeId;
        private int ContributionAttributeId;
        private int BatchAttributeId;

        /// <summary>
        /// The list of current campuses
        /// </summary>
        private List<Campus> CampusList;

        #region Methods

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        /// <returns></returns>
        public override bool TransformData()
        {
            VerifyRockAttributes();
            var orderedNodes = SetImportOrder( loadedNodes );

            var scanner = new DataScanner( database );
            int workerCount = 0;

            foreach ( var node in orderedNodes )
            {
                switch ( node.Name )
                {
                    case "Batch":
                        MapBatch( scanner.ScanTable( node.Name ).AsQueryable() );
                        break;

                    case "Contribution":
                        MapContribution( scanner.ScanTable( node.Name ).AsQueryable() );
                        break;

                    case "Individual_Household":
                        MapPerson( scanner.ScanTable( node.Name ).AsQueryable() );
                        break;

                    default:
                        break;
                }

                //BackgroundWorker bwSpawnWorker = new BackgroundWorker();
                //bwSpawnWorker.DoWork += bwSpawnWorker_DoWork;
                //bwSpawnWorker.ProgressChanged += bwSpawnWorker_ProgressChanged;
                //bwSpawnWorker.RunWorkerCompleted += bwSpawnWorker_RunWorkerCompleted;
                //bwSpawnWorker.RunWorkerAsync( node.Name );
                //workerCount++;
            }

            return workerCount > 0 ? true : false;
        }

        /// <summary>
        /// Verifies all Rock attributes exist that are used globally by the transform.
        /// </summary>
        public void VerifyRockAttributes()
        {
            var attributeValueService = new AttributeValueService();
            var attributeService = new AttributeService();

            // change this to user-defined person
            var aliasService = new PersonAliasService();
            ImportPersonAlias = aliasService.Get( 1 );
            // end change

            int personEntityTypeId = EntityTypeCache.Read( "Rock.Model.Person" ).Id;
            int batchEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialBatch" ).Id;
            int transactionEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialTransaction" ).Id;
            int textFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.TEXT ) ).Id;
            int integerFieldTypeId = FieldTypeCache.Read( new Guid( Rock.SystemGuid.FieldType.INTEGER ) ).Id;

            CampusList = new CampusService().Queryable().ToList();

            PersonAttributeList = attributeService.Queryable().Where( a => a.EntityTypeId == personEntityTypeId ).ToList();

            var householdAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "F1HouseholdId" );
            if ( householdAttribute == null )
            {
                householdAttribute = new Rock.Model.Attribute();
                householdAttribute.Key = "F1HouseholdId";
                householdAttribute.Name = "F1 Household Id";
                householdAttribute.FieldTypeId = integerFieldTypeId;
                householdAttribute.EntityTypeId = personEntityTypeId;
                householdAttribute.EntityTypeQualifierValue = string.Empty;
                householdAttribute.EntityTypeQualifierColumn = string.Empty;
                householdAttribute.Description = "The FellowshipOne household identifier for the person that was imported";
                householdAttribute.DefaultValue = string.Empty;
                householdAttribute.IsMultiValue = false;
                householdAttribute.IsRequired = false;
                householdAttribute.Order = 0;

                attributeService.Add( householdAttribute, ImportPersonAlias );
                attributeService.Save( householdAttribute, ImportPersonAlias );
                PersonAttributeList.Add( householdAttribute );
            }

            var individualAttribute = PersonAttributeList.FirstOrDefault( a => a.Key == "F1IndividualId" );
            if ( individualAttribute == null )
            {
                individualAttribute = new Rock.Model.Attribute();
                individualAttribute.Key = "F1IndividualId";
                individualAttribute.Name = "F1 Individual Id";
                individualAttribute.FieldTypeId = integerFieldTypeId;
                individualAttribute.EntityTypeId = personEntityTypeId;
                individualAttribute.EntityTypeQualifierValue = string.Empty;
                individualAttribute.EntityTypeQualifierColumn = string.Empty;
                individualAttribute.Description = "The FellowshipOne individual identifier for the person that was imported";
                individualAttribute.DefaultValue = string.Empty;
                individualAttribute.IsMultiValue = false;
                individualAttribute.IsRequired = false;
                individualAttribute.Order = 0;

                attributeService.Add( individualAttribute, ImportPersonAlias );
                attributeService.Save( individualAttribute, ImportPersonAlias );
                PersonAttributeList.Add( individualAttribute );
            }

            HouseholdAttributeId = householdAttribute.Id;
            IndividualAttributeId = individualAttribute.Id;

            // Get all current people with household & individual Id's
            var listHouseholdId = attributeValueService.GetByAttributeId( householdAttribute.Id ).Select( av => new { PersonId = av.EntityId, HouseholdId = av.Value } ).ToList();
            var listIndividualId = attributeValueService.GetByAttributeId( individualAttribute.Id ).Select( av => new { PersonId = av.EntityId, IndividualId = av.Value } ).ToList();

            ImportedPersonList = listHouseholdId.Join( listIndividualId, household => household.PersonId
                , individual => individual.PersonId
                , ( household, individual ) => new ImportedPerson
                {
                    PersonId = household.PersonId,
                    HouseholdId = household.HouseholdId.AsType<int?>(),
                    IndividualId = individual.IndividualId.AsType<int?>()
                } ).ToList();

            // Get all imported batches
            var batchAttribute = attributeService.Queryable().Where( a => a.EntityTypeId == batchEntityTypeId ).FirstOrDefault( a => a.Key == "F1BatchId" );
            if ( batchAttribute == null )
            {
                batchAttribute = new Rock.Model.Attribute();
                batchAttribute.Key = "F1BatchId";
                batchAttribute.Name = "F1 Batch Id";
                batchAttribute.FieldTypeId = integerFieldTypeId;
                batchAttribute.EntityTypeId = batchEntityTypeId;
                batchAttribute.EntityTypeQualifierValue = string.Empty;
                batchAttribute.EntityTypeQualifierColumn = string.Empty;
                batchAttribute.Description = "The FellowshipOne identifier for the batch that was imported";
                batchAttribute.DefaultValue = string.Empty;
                batchAttribute.IsMultiValue = false;
                batchAttribute.IsRequired = false;
                batchAttribute.Order = 0;

                attributeService.Add( batchAttribute, ImportPersonAlias );
                attributeService.Save( batchAttribute, ImportPersonAlias );
            }

            BatchAttributeId = batchAttribute.Id;
            ImportedBatches = attributeValueService.GetByAttributeId( batchAttribute.Id )
                .Select( av => new { F1BatchId = av.Value.AsType<int?>(), RockBatchId = av.EntityId } )
                .ToDictionary( t => t.F1BatchId, t => t.RockBatchId );

            var contributionAttribute = attributeService.Queryable().Where( a => a.EntityTypeId == transactionEntityTypeId ).FirstOrDefault( a => a.Key == "F1ContributionId" );
            if ( contributionAttribute == null )
            {
                contributionAttribute = new Rock.Model.Attribute();
                contributionAttribute.Key = "F1ContributionId";
                contributionAttribute.Name = "F1 Contribution Id";
                contributionAttribute.FieldTypeId = integerFieldTypeId;
                contributionAttribute.EntityTypeId = transactionEntityTypeId;
                contributionAttribute.EntityTypeQualifierValue = string.Empty;
                contributionAttribute.EntityTypeQualifierColumn = string.Empty;
                contributionAttribute.Description = "The FellowshipOne identifier for the contribution that was imported";
                contributionAttribute.DefaultValue = string.Empty;
                contributionAttribute.IsMultiValue = false;
                contributionAttribute.IsRequired = false;
                contributionAttribute.Order = 0;

                attributeService.Add( contributionAttribute, ImportPersonAlias );
                attributeService.Save( contributionAttribute, ImportPersonAlias );
            }

            ContributionAttributeId = contributionAttribute.Id;
            ImportedContributions = attributeValueService.GetByAttributeId( contributionAttribute.Id )
               .Select( av => new { ContributionId = av.Value.AsType<int?>(), TransactionId = av.EntityId } )
               .ToDictionary( t => t.ContributionId, t => t.TransactionId );
        }

        /// <summary>
        /// Orders the nodes so the primary tables get imported first
        /// </summary>
        /// <param name="nodeList">The node list.</param>
        private List<DatabaseNode> SetImportOrder( List<DatabaseNode> nodeList )
        {
            nodeList = nodeList.Where( n => n.Checked != false ).ToList();
            if ( nodeList.Any() )
            {
                var household = nodeList.Where( node => node.Name.Equals( "Individual_Household" ) ).FirstOrDefault();
                var batch = nodeList.Where( node => node.Name.Equals( "Batch" ) ).FirstOrDefault();
                var rlc = nodeList.Where( node => node.Name.Equals( "RLC" ) ).FirstOrDefault();

                nodeList.Remove( household );
                nodeList.Remove( batch );
                nodeList.Remove( rlc );
                var primaryTables = new List<DatabaseNode>() { household, batch, rlc };
                primaryTables.RemoveAll( n => n == null );
                nodeList.InsertRange( 0, primaryTables );
            }

            return nodeList;
        }

        /// <summary>
        /// Checks if this person has been imported and returns the Rock.Person Id
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <param name="householdId">The household identifier.</param>
        /// <returns></returns>
        private int? GetPersonId( int? individualId = null, int? householdId = null )
        {
            var existingPerson = ImportedPersonList.FirstOrDefault( p => p.IndividualId == individualId && p.HouseholdId == householdId );
            if ( existingPerson != null )
            {
                return existingPerson.PersonId;
            }
            else
            {
                string f1IndividualId = individualId.ToString();
                var lookupPerson = new AttributeValueService().Queryable().FirstOrDefault( av => av.AttributeId == IndividualAttributeId && av.Value == f1IndividualId );
                if ( lookupPerson != null )
                {
                    ImportedPersonList.Add( new ImportedPerson() { PersonId = lookupPerson.Id, HouseholdId = householdId, IndividualId = individualId } );
                    return lookupPerson.Id;
                }
            }

            return null;
        }

        #endregion

        #region Async Tasks

        /// <summary>
        /// Runs the background worker method that matches the selected table name
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DoWorkEventArgs"/> instance containing the event data.</param>
        private void bwSpawnWorker_DoWork( object sender, DoWorkEventArgs e )
        {
            var nodeName = (string)e.Argument;
            if ( nodeName != null )
            {
                var scanner = new DataScanner( database );
                IQueryable<Row> tableData = scanner.ScanTable( nodeName ).AsQueryable();
            }
        }

        /// <summary>
        /// Runs when the background process for each method completes
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RunWorkerCompletedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwSpawnWorker_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
        {
            //return completed to original thread;
        }

        /// <summary>
        /// Reports the progress for each background worker that was started
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ProgressChangedEventArgs"/> instance containing the event data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void bwSpawnWorker_ProgressChanged( object sender, ProgressChangedEventArgs e )
        {
            //throw new NotImplementedException();
        }

        #endregion

        #region Mapped Data

        /// <summary>
        /// Maps the batch data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private int MapBatch( IQueryable<Row> tableData )
        {
            var batchAttribute = AttributeCache.Read( BatchAttributeId );

            foreach ( var row in tableData )
            {
                int? batchId = row["BatchID"] as int?;
                if ( batchId != null && !ImportedBatches.ContainsKey( batchId ) )
                {
                    var batch = new FinancialBatch();

                    string name = row["BatchName"] as string;
                    if ( name != null )
                    {
                        batch.Name = name;
                    }

                    DateTime? batchDate = row["BatchDate"] as DateTime?;
                    if ( batchDate != null )
                    {
                        batch.BatchStartDateTime = batchDate;
                        batch.BatchEndDateTime = batchDate;
                    }

                    decimal? amount = row["BatchAmount"] as decimal?;
                    if ( amount != null )
                    {
                        batch.ControlAmount = amount.HasValue ? amount.Value : new decimal();
                    }

                    RockTransactionScope.WrapTransaction( () =>
                    {
                        var batchService = new FinancialBatchService();
                        batchService.Add( batch, ImportPersonAlias );
                        batchService.Save( batch, ImportPersonAlias );

                        batch.Attributes = new Dictionary<string, AttributeCache>();
                        batch.Attributes.Add( "F1BatchId", batchAttribute );
                        batch.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                        Rock.Attribute.Helper.SaveAttributeValue( batch, batchAttribute, batchId.ToString(), ImportPersonAlias );
                    } );
                }
            }

            return tableData.Count();
        }

        /// <summary>
        /// Maps the contribution.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private int MapContribution( IQueryable<Row> tableData, List<string> selectedColumns = null )
        {
            var detailService = new FinancialTransactionDetailService();
            var accountService = new FinancialAccountService();
            var dvService = new DefinedValueService();

            var contributionAttribute = AttributeCache.Read( ContributionAttributeId );

            int transactionTypeContribution = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION ) ).Id;

            int currencyTypeACH = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH ) ).Id;
            int currencyTypeCash = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH ) ).Id;
            int currencyTypeCheck = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ) ).Id;
            int currencyTypeCreditCard = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD ) ).Id;

            List<DefinedValue> refundReasons = dvService.Queryable().Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_REFUND_REASON ) ).ToList();

            List<FinancialPledge> pledgeList = new FinancialPledgeService().Queryable().ToList();

            List<FinancialAccount> accountList = accountService.Queryable().ToList();

            foreach ( var row in tableData )
            {
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                int? contributionId = row["ContributionID"] as int?;

                if ( contributionId != null && !ImportedContributions.ContainsKey( contributionId ) )
                {
                    var transaction = new FinancialTransaction();
                    transaction.TransactionTypeValueId = transactionTypeContribution;

                    int? personId = GetPersonId( individualId, householdId );
                    transaction.AuthorizedPersonId = personId;

                    string summary = row["Memo"] as string;
                    if ( summary != null )
                    {
                        transaction.Summary = summary;
                    }

                    int? batchId = row["BatchID"] as int?;
                    if ( batchId != null && ImportedBatches.Any( b => b.Key == batchId ) )
                    {
                        transaction.BatchId = batchId;
                    }

                    DateTime? receivedDate = row["Received_Date"] as DateTime?;
                    if ( receivedDate != null )
                    {
                        transaction.CreatedDateTime = receivedDate;
                    }

                    string contributionType = row["Contribution_Type_Name"] as string;
                    if ( contributionType != null )
                    {
                        if ( contributionType == "ACH" )
                        {
                            transaction.CurrencyTypeValueId = currencyTypeACH;
                        }
                        else if ( contributionType == "Cash" )
                        {
                            transaction.CurrencyTypeValueId = currencyTypeCash;
                        }
                        else if ( contributionType == "Check" )
                        {
                            transaction.CurrencyTypeValueId = currencyTypeCheck;
                        }
                        else if ( contributionType == "Credit Card" )
                        {
                            transaction.CurrencyTypeValueId = currencyTypeCreditCard;
                        }
                        else
                        {
                            //transaction.CurrencyTypeValueId = currencyTypeOther;
                        }
                    }

                    string checkNumber = row["Check_Number"] as string;
                    if ( checkNumber != null && checkNumber.AsType<int?>() != null )
                    {
                        transaction.CheckMicrEncrypted = Encryption.EncryptString( string.Format( "{0}_{1}_{2}", null, null, checkNumber ) );
                    }

                    string fundName = row["Fund_Name"] as string;
                    string subFund = row["Sub_Fund_Name"] as string;
                    decimal? amount = row["Amount"] as decimal?;
                    if ( fundName != null & amount != null )
                    {
                        // check if the subFund is a campus
                        var fundCampusId = CampusList.Where( c => c.Name.StartsWith( subFund ) || c.ShortCode == subFund )
                                .Select( c => (int?)c.Id ).FirstOrDefault();

                        // check if an account already exists
                        var matchingAccount = accountList.FirstOrDefault( a => ( a.Name.StartsWith( fundName ) && a.CampusId == fundCampusId ) ||
                            ( a.ParentAccount.Name.StartsWith( fundName ) && a.Name.StartsWith( subFund ) ) );
                        if ( matchingAccount == null )
                        {
                            matchingAccount = new FinancialAccount();
                            matchingAccount.Name = fundName;
                            matchingAccount.PublicName = fundName;
                            matchingAccount.IsTaxDeductible = true;
                            matchingAccount.IsActive = true;
                            matchingAccount.CampusId = fundCampusId;

                            accountService.Add( matchingAccount );
                            accountService.Save( matchingAccount );
                            accountList.Add( matchingAccount );
                        }

                        var transactionDetail = new FinancialTransactionDetail();
                        transactionDetail.Amount = (decimal)amount;
                        transactionDetail.CreatedDateTime = receivedDate;
                        transactionDetail.AccountId = matchingAccount.Id;
                        transaction.TransactionDetails.Add( transactionDetail );

                        if ( amount < 0 )
                        {
                            var transactionRefund = new FinancialTransactionRefund();
                            transactionRefund.CreatedDateTime = receivedDate;
                            transactionRefund.RefundReasonSummary = summary;
                            transactionRefund.RefundReasonValueId = refundReasons.Where( dv => summary.Contains( dv.Name ) )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                            transaction.Refund = transactionRefund;
                        }
                    }

                    // Other properties (Attributes to create):
                    // Pledge_Drive_Name
                    // Stated_Value
                    // True_Value
                    // Liquidation_cost

                    RockTransactionScope.WrapTransaction( () =>
                    {
                        var transactionService = new FinancialTransactionService();
                        transactionService.Add( transaction, ImportPersonAlias );
                        transactionService.Save( transaction, ImportPersonAlias );

                        transaction.Attributes = new Dictionary<string, AttributeCache>();
                        transaction.Attributes.Add( "F1ContributionId", contributionAttribute );
                        transaction.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                        Rock.Attribute.Helper.SaveAttributeValue( transaction, contributionAttribute, contributionId.ToString(), ImportPersonAlias );
                    } );
                }
            }

            return tableData.Count();
        }

        /// <summary>
        /// Maps the person.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private int MapPerson( IQueryable<Row> tableData, List<string> selectedColumns = null )
        {
            var groupTypeRoleService = new GroupTypeRoleService();
            var dvService = new DefinedValueService();

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
            int? statusActiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE ) ).Id;
            int? statusInactiveId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE ) ).Id;
            int? statusPendingId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING ) ).Id;

            // Record type: Person
            int? personRecordTypeId = dvService.Get( new Guid( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON ) ).Id;

            // Suffix type: Dr., Jr., II, etc
            List<DefinedValue> suffixTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_SUFFIX ) ).ToList();

            // Title type: Mr., Mrs. Dr., etc
            List<DefinedValue> titleTypes = dvService.Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.PERSON_TITLE ) ).ToList();

            // Note type: Comment
            int noteCommentTypeId = new NoteTypeService().Get( new Guid( "7E53487C-D650-4D85-97E2-350EB8332763" ) ).Id;

            // Group roles: Adult, Child, others
            int adultRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT ) ).Id;
            int childRoleId = groupTypeRoleService.Get( new Guid( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD ) ).Id;
            //int visitorRoleId
            //int canCheckInRoleId
            //int allowCheckInRoleId
            //int invitedRoleId
            //int invitedByRoleId

            // Group type: Family
            int familyGroupTypeId = GroupTypeCache.GetFamilyGroupType().Id;

            // Cached F1 attributes: IndividualId, HouseholdId, PreviousChurch, Position, Employer, School
            var individualIdAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "F1IndividualId" ) );
            var householdIdAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "F1HouseholdId" ) );
            var previousChurchAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "PreviousChurch" ) );
            var employerAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "Employer" ) );
            var positionAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "Position" ) );
            var firstVisitAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "FirstVisit" ) );
            var schoolAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "School" ) );
            var membershipDateAttribute = AttributeCache.Read( PersonAttributeList.FirstOrDefault( a => a.Key == "MembershipDate" ) );

            foreach ( var groupedRows in tableData.GroupBy<Row, int?>( r => r["Household_ID"] as int? ) )
            {
                // only import where selectedColumns.Contains( row.Column )

                var familyGroup = new Group();
                var householdCampusList = new List<string>();

                foreach ( var row in groupedRows )
                {
                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;

                    // Check if person already imported
                    if ( GetPersonId( individualId, householdId ) == null )
                    {
                        var person = new Person();
                        person.FirstName = row["First_Name"] as string;
                        person.MiddleName = row["Middle_Name"] as string;
                        person.NickName = row["Goes_By"] as string ?? person.FirstName;
                        person.LastName = row["Last_Name"] as string;
                        person.BirthDate = row["Date_Of_Birth"] as DateTime?;
                        person.RecordTypeValueId = personRecordTypeId;
                        int groupRoleId = adultRoleId;

                        var gender = row["Gender"] as string;
                        if ( gender != null )
                        {
                            person.Gender = (Gender)Enum.Parse( typeof( Gender ), gender );
                        }

                        string prefix = row["Prefix"] as string;
                        if ( prefix != null )
                        {
                            person.TitleValueId = titleTypes.Where( s => s.Name == prefix )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string suffix = row["Suffix"] as string;
                        if ( suffix != null )
                        {
                            person.SuffixValueId = suffixTypes.Where( s => s.Name == suffix )
                                .Select( s => (int?)s.Id ).FirstOrDefault();
                        }

                        string maritalStatus = row["Marital_Status"] as string;
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

                        string familyRole = row["Household_Position"] as string;
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

                        string memberStatus = row["Status_Name"] as string;
                        if ( memberStatus == "Member" )
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER ) ).Id;
                            person.RecordStatusValueId = statusActiveId;
                        }
                        else if ( memberStatus == "Visitor" )
                        {
                            person.ConnectionStatusValueId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR ) ).Id;
                            person.RecordStatusValueId = statusActiveId;
                        }
                        else if ( memberStatus == "Deceased" )
                        {
                            person.IsDeceased = true;
                            person.RecordStatusValueId = statusInactiveId;
                            person.RecordStatusReasonValueId = recordStatusReasons.Where( dv => dv.Name == "Deceased" )
                                .Select( dv => dv.Id ).FirstOrDefault();
                        }
                        else
                        {
                            // F1 defaults are Member & Visitor; all others are user-defined
                            var customConnectionType = connectionStatusTypes.Where( dv => dv.Name == memberStatus )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();

                            int attendeeId = connectionStatusTypes.FirstOrDefault( dv => dv.Guid == new Guid( "39F491C5-D6AC-4A9B-8AC0-C431CB17D588" ) ).Id;
                            person.ConnectionStatusValueId = customConnectionType ?? attendeeId;
                            person.RecordStatusValueId = statusActiveId;
                        }

                        string campus = row["SubStatus_Name"] as string;
                        if ( campus != null )
                        {
                            householdCampusList.Add( campus );
                        }

                        string status_comment = row["Status_Comment"] as string;
                        if ( status_comment != null )
                        {
                            var comment = new Note();
                            comment.Text = status_comment;
                            comment.NoteTypeId = noteCommentTypeId;
                            RockTransactionScope.WrapTransaction( () =>
                            {
                                var noteService = new NoteService();
                                noteService.Save( comment );
                            } );
                        }

                        // Map F1 attributes
                        person.Attributes = new Dictionary<string, AttributeCache>();
                        person.AttributeValues = new Dictionary<string, List<AttributeValue>>();

                        // individual_id already defined in scope
                        if ( individualId != null )
                        {
                            person.Attributes.Add( "F1IndividualId", individualIdAttribute );
                            person.AttributeValues.Add( "F1IndividualId", new List<AttributeValue>() );
                            person.AttributeValues["F1IndividualId"].Add( new AttributeValue()
                            {
                                AttributeId = individualIdAttribute.Id,
                                Value = individualId.ToString()
                            } );
                        }

                        // household_id already defined in scope
                        if ( householdId != null )
                        {
                            person.Attributes.Add( "F1HouseholdId", householdIdAttribute );
                            person.AttributeValues.Add( "F1HouseholdId", new List<AttributeValue>() );
                            person.AttributeValues["F1HouseholdId"].Add( new AttributeValue()
                            {
                                AttributeId = householdIdAttribute.Id,
                                Value = householdId.ToString()
                            } );
                        }

                        string previousChurch = row["Former_Church"] as string;
                        if ( previousChurch != null )
                        {
                            person.Attributes.Add( "PreviousChurch", previousChurchAttribute );
                            person.AttributeValues.Add( "PreviousChurch", new List<AttributeValue>() );
                            person.AttributeValues["PreviousChurch"].Add( new AttributeValue()
                            {
                                AttributeId = previousChurchAttribute.Id,
                                Value = previousChurch
                            } );
                        }

                        string employer = row["Employer"] as string;
                        if ( employer != null )
                        {
                            person.Attributes.Add( "Employer", employerAttribute );
                            person.AttributeValues.Add( "Employer", new List<AttributeValue>() );
                            person.AttributeValues["Employer"].Add( new AttributeValue()
                            {
                                AttributeId = employerAttribute.Id,
                                Value = employer
                            } );
                        }

                        string position = row["Occupation_Name"] as string ?? row["Occupation_Description"] as string;
                        if ( position != null )
                        {
                            person.Attributes.Add( "Position", positionAttribute );
                            person.AttributeValues.Add( "Position", new List<AttributeValue>() );
                            person.AttributeValues["Position"].Add( new AttributeValue()
                            {
                                AttributeId = positionAttribute.Id,
                                Value = position
                            } );
                        }

                        string school = row["School_Name"] as string;
                        if ( school != null )
                        {
                            person.Attributes.Add( "School", schoolAttribute );
                            person.AttributeValues.Add( "School", new List<AttributeValue>() );
                            person.AttributeValues["School"].Add( new AttributeValue()
                            {
                                AttributeId = schoolAttribute.Id,
                                Value = school
                            } );
                        }

                        DateTime? firstVisit = row["First_Record"] as DateTime?;
                        if ( firstVisit != null )
                        {
                            person.CreatedDateTime = firstVisit;
                            person.Attributes.Add( "FirstVisit", firstVisitAttribute );
                            person.AttributeValues.Add( "FirstVisit", new List<AttributeValue>() );
                            person.AttributeValues["FirstVisit"].Add( new AttributeValue()
                            {
                                AttributeId = firstVisitAttribute.Id,
                                Value = firstVisit.Value.ToString( "MM/dd/yyyy" )
                            } );
                        }

                        DateTime? membershipDate = row["Status_Date"] as DateTime?;
                        if ( membershipDate != null )
                        {
                            person.CreatedDateTime = membershipDate;
                            person.Attributes.Add( "MembershipDate", membershipDateAttribute );
                            person.AttributeValues.Add( "MembershipDate", new List<AttributeValue>() );
                            person.AttributeValues["MembershipDate"].Add( new AttributeValue()
                            {
                                AttributeId = membershipDateAttribute.Id,
                                Value = membershipDate.Value.ToString( "MM/dd/yyyy" )
                            } );
                        }

                        // Other properties (Attributes to create):
                        // former name
                        // bar_code
                        // member_env_code
                        // denomination_name

                        var groupMember = new GroupMember();
                        groupMember.Person = person;
                        groupMember.GroupRoleId = groupRoleId;
                        groupMember.GroupMemberStatus = GroupMemberStatus.Active;
                        familyGroup.Members.Add( groupMember );
                    }
                }

                // If this family hasn't already been imported
                if ( familyGroup.Members.Any() )
                {
                    familyGroup.Name = familyGroup.Members.FirstOrDefault().Person.LastName + " Family";
                    familyGroup.GroupTypeId = familyGroupTypeId;
                    string primaryHouseholdCampus = householdCampusList.GroupBy( c => c ).OrderByDescending( c => c.Count() ).Select( c => c.Key ).FirstOrDefault();
                    familyGroup.CampusId = CampusList.Where( c => c.Name.StartsWith( primaryHouseholdCampus ) || c.ShortCode == primaryHouseholdCampus )
                        .Select( c => (int?)c.Id ).FirstOrDefault();

                    RockTransactionScope.WrapTransaction( () =>
                    {
                        var groupService = new GroupService();
                        groupService.Add( familyGroup, ImportPersonAlias );
                        groupService.Save( familyGroup, ImportPersonAlias );

                        var personService = new PersonService();
                        foreach ( var groupMember in familyGroup.Members )
                        {
                            var person = groupMember.Person;
                            foreach ( var attributeCache in person.Attributes.Select( a => a.Value ) )
                            {
                                string newValue = person.AttributeValues[attributeCache.Key][0].Value ?? string.Empty;
                                Rock.Attribute.Helper.SaveAttributeValue( person, attributeCache, newValue, ImportPersonAlias );
                            }

                            person = personService.Get( groupMember.PersonId );
                            if ( person != null )
                            {
                                if ( !person.Aliases.Any( a => a.AliasPersonId == person.Id ) )
                                {
                                    person.Aliases.Add( new PersonAlias { AliasPersonId = person.Id, AliasPersonGuid = person.Guid } );
                                }

                                if ( groupMember.GroupRoleId != childRoleId )
                                {
                                    person.GivingGroupId = familyGroup.Id;
                                }

                                personService.Save( person, ImportPersonAlias );
                            }
                        }
                    } );
                }
            }

            return tableData.Count();
        }

        #endregion
    }

    /// <summary>
    /// Helper class to store Id references to people that've been imported
    /// </summary>
    public class ImportedPerson
    {
        /// <summary>
        /// Stores the Rock.Person Id
        /// </summary>
        public int? PersonId;

        /// <summary>
        /// Stores the F1 Individual Id
        /// </summary>
        public int? IndividualId;

        /// <summary>
        /// Stores the F1 Household Id
        /// </summary>
        public int? HouseholdId;
    }
}
