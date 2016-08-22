using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.CSV
{
    /// <summary>
    /// Partial of CSVComponent that holds the Financial import methods
    /// </summary>
    public partial class CSVComponent
    {
        /// <summary>
        /// Maps the batch data.
        /// </summary>
        /// <param name="csvData">The table data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private int MapBatch( CSVInstance csvData )
        {
            var batchStatusClosed = Rock.Model.BatchStatus.Closed;
            var newBatches = new List<FinancialBatch>();

            int completed = 0;
            ReportProgress( 0, string.Format( "Verifying batch import ({0:N0} already exist).", ImportedBatches.Count ) );
            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( (row = csvData.Database.FirstOrDefault()) != null )
            {
                string batchIdKey = row[BatchID];
                int? batchId = batchIdKey.AsType<int?>();
                if ( batchId != null && !ImportedBatches.ContainsKey( ( int )batchId ) )
                {
                    var batch = new FinancialBatch();
                    batch.CreatedByPersonAliasId = ImportPersonAliasId;
                    batch.ForeignKey = batchId.ToString();
                    batch.ForeignId = batchId;
                    batch.Note = string.Empty;
                    batch.Status = batchStatusClosed;
                    batch.AccountingSystemCode = string.Empty;

                    string name = row[BatchName] as string;
                    if ( !String.IsNullOrWhiteSpace( name ) )
                    {
                        name = name.Trim();
                        batch.Name = name.Left( 50 );
                        batch.CampusId = CampusList.Where( c => name.StartsWith( c.Name ) || name.StartsWith( c.ShortCode ) )
                            .Select( c => ( int? )c.Id ).FirstOrDefault();
                    }

                    string batchDateKey = row[BatchDate];
                    DateTime? batchDate = batchDateKey.AsType<DateTime?>();
                    if ( batchDate != null )
                    {
                        batch.BatchStartDateTime = batchDate;
                        batch.BatchEndDateTime = batchDate;
                    }

                    string amountKey = row[BatchAmount];
                    decimal? amount = amountKey.AsType<decimal?>();
                    if ( amount != null )
                    {
                        batch.ControlAmount = amount.HasValue ? amount.Value : new decimal();
                    }

                    newBatches.Add( batch );
                    completed++;
                    if ( completed % (ReportingNumber * 10) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} batches imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFinancialBatches( newBatches );
                        newBatches.ForEach( b => ImportedBatches.Add( ( int )b.ForeignId, ( int? )b.Id ) );
                        newBatches.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            // add a default batch to use with contributions
            if ( !ImportedBatches.ContainsKey( 0 ) )
            {
                var defaultBatch = new FinancialBatch();
                defaultBatch.CreatedDateTime = ImportDateTime;
                defaultBatch.CreatedByPersonAliasId = ImportPersonAliasId;
                defaultBatch.Status = Rock.Model.BatchStatus.Closed;
                defaultBatch.Name = string.Format( "Default Batch (Imported {0})", ImportDateTime );
                defaultBatch.ControlAmount = 0.0m;
                defaultBatch.ForeignKey = "0";
                defaultBatch.ForeignId = 0;

                newBatches.Add( defaultBatch );
            }

            if ( newBatches.Any() )
            {
                SaveFinancialBatches( newBatches );
                newBatches.ForEach( b => ImportedBatches.Add( ( int )b.ForeignId, ( int? )b.Id ) );
            }

            ReportProgress( 100, string.Format( "Finished batch import: {0:N0} batches imported.", completed ) );
            return completed;
        }

        /// <summary>
        /// Saves the financial batches.
        /// </summary>
        /// <param name="newBatches">The new batches.</param>
        private static void SaveFinancialBatches( List<FinancialBatch> newBatches )
        {
            using ( var rockContext = new RockContext() )
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialBatches.AddRange( newBatches );
                rockContext.SaveChanges( DisableAuditing );
            }
        }

        /// <summary>
        /// Maps the contribution.
        /// </summary>
        /// <param name="csvData">The table data.</param>
        private int MapContribution( CSVInstance csvData )
        {
            var lookupContext = new RockContext();
            int transactionEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialTransaction" ).Id;
            var transactionTypeContributionId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION ), lookupContext ).Id;

            var currencyTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE ) );
            int currencyTypeACH = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH ) ) ).Id;
            int currencyTypeCash = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH ) ) ).Id;
            int currencyTypeCheck = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ) ) ).Id;
            int currencyTypeCreditCard = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD ) ) ).Id;
            int? currencyTypeNonCash = currencyTypes.DefinedValues.Where( dv => dv.Value.Equals( "Non-Cash" ) ).Select( dv => ( int? )dv.Id ).FirstOrDefault();
            if ( currencyTypeNonCash == null )
            {
                var newTenderNonCash = new DefinedValue();
                newTenderNonCash.Value = "Non-Cash";
                newTenderNonCash.Description = "Non-Cash";
                newTenderNonCash.DefinedTypeId = currencyTypes.Id;
                lookupContext.DefinedValues.Add( newTenderNonCash );
                lookupContext.SaveChanges();

                currencyTypeNonCash = newTenderNonCash.Id;
            }

            var creditCardTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_CREDIT_CARD_TYPE ) ).DefinedValues;

            int sourceTypeOnsite = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_ONSITE_COLLECTION ), lookupContext ).Id;
            int sourceTypeWebsite = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE ), lookupContext ).Id;
            int sourceTypeKiosk = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_KIOSK ), lookupContext ).Id;

            var refundReasons = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_REFUND_REASON ), lookupContext ).DefinedValues;

            var accountList = new FinancialAccountService( lookupContext ).Queryable().AsNoTracking().ToList();

            int? defaultBatchId = null;
            if ( ImportedBatches.ContainsKey( 0 ) )
            {
                defaultBatchId = ImportedBatches[0];
            }

            // Get all imported contributions
            var importedContributions = new FinancialTransactionService( lookupContext ).Queryable().AsNoTracking()
               .Where( c => c.ForeignId != null )
               .ToDictionary( t => ( int )t.ForeignId, t => ( int? )t.Id );

            // List for batching new contributions
            var newTransactions = new List<FinancialTransaction>();

            int completed = 0;
            ReportProgress( 0, string.Format( "Verifying contribution import ({0:N0} already exist).", importedContributions.Count ) );
            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( (row = csvData.Database.FirstOrDefault()) != null )
            {
                string individualIdKey = row[IndividualID];
                int? individualId = individualIdKey.AsType<int?>();
                string contributionIdKey = row[ContributionID];
                int? contributionId = contributionIdKey.AsType<int?>();

                if ( contributionId != null && !importedContributions.ContainsKey( ( int )contributionId ) )
                {
                    var transaction = new FinancialTransaction();
                    transaction.CreatedByPersonAliasId = ImportPersonAliasId;
                    transaction.ModifiedByPersonAliasId = ImportPersonAliasId;
                    transaction.TransactionTypeValueId = transactionTypeContributionId;
                    transaction.ForeignKey = contributionId.ToString();
                    transaction.ForeignId = contributionId;

                    int? giverAliasId = null;
                    var personKeys = GetPersonKeys( individualId );
                    if ( personKeys != null && personKeys.PersonAliasId > 0 )
                    {
                        giverAliasId = personKeys.PersonAliasId;
                        transaction.CreatedByPersonAliasId = giverAliasId;
                        transaction.AuthorizedPersonAliasId = giverAliasId;
                        transaction.ProcessedByPersonAliasId = giverAliasId;
                    }
                    else if ( AnonymousGiverAliasId != null && AnonymousGiverAliasId > 0 )
                    {
                        giverAliasId = AnonymousGiverAliasId;
                        transaction.AuthorizedPersonAliasId = giverAliasId;
                        transaction.ProcessedByPersonAliasId = giverAliasId;
                    }

                    string summary = row[Memo] as string;
                    if ( !String.IsNullOrWhiteSpace( summary ) )
                    {
                        transaction.Summary = summary;
                    }

                    string batchIdKey = row[ContributionBatchID];
                    int? batchId = batchIdKey.AsType<int?>();
                    if ( batchId != null && ImportedBatches.Any( b => b.Key.Equals( batchId ) ) )
                    {
                        transaction.BatchId = ImportedBatches.FirstOrDefault( b => b.Key.Equals( batchId ) ).Value;
                    }
                    else
                    {
                        // use the default batch for any non-matching transactions
                        transaction.BatchId = defaultBatchId;
                    }

                    string receivedDateKey = row[ReceivedDate];
                    DateTime? receivedDate = receivedDateKey.AsType<DateTime?>();
                    if ( receivedDate != null )
                    {
                        transaction.TransactionDateTime = receivedDate;
                        transaction.CreatedDateTime = receivedDate;
                        transaction.ModifiedDateTime = ImportDateTime;
                    }

                    string contributionType = row[ContributionTypeName].ToStringSafe().ToLower();
                    if ( !String.IsNullOrWhiteSpace( contributionType ) )
                    {
                        // set default source to onsite, exceptions listed below
                        transaction.SourceTypeValueId = sourceTypeOnsite;

                        int? paymentCurrencyTypeId = null, creditCardTypeId = null;

                        if ( contributionType == "cash" )
                        {
                            paymentCurrencyTypeId = currencyTypeCash;
                        }
                        else if ( contributionType == "check" )
                        {
                            paymentCurrencyTypeId = currencyTypeCheck;
                        }
                        else if ( contributionType == "ach" )
                        {
                            paymentCurrencyTypeId = currencyTypeACH;
                            transaction.SourceTypeValueId = sourceTypeWebsite;
                        }
                        else if ( contributionType == "credit card" )
                        {
                            paymentCurrencyTypeId = currencyTypeCreditCard;
                            transaction.SourceTypeValueId = sourceTypeWebsite;
                        }
                        else
                        {
                            paymentCurrencyTypeId = currencyTypeNonCash;
                        }

                        var paymentDetail = new FinancialPaymentDetail();
                        paymentDetail.CreatedDateTime = receivedDate;
                        paymentDetail.CreatedByPersonAliasId = giverAliasId;
                        paymentDetail.ModifiedDateTime = ImportDateTime;
                        paymentDetail.ModifiedByPersonAliasId = giverAliasId;
                        paymentDetail.CurrencyTypeValueId = paymentCurrencyTypeId;
                        paymentDetail.CreditCardTypeValueId = creditCardTypeId;
                        paymentDetail.ForeignKey = contributionId.ToString();
                        paymentDetail.ForeignId = contributionId;

                        transaction.FinancialPaymentDetail = paymentDetail;
                    }

                    string checkNumber = row[CheckNumber] as string;
                    // if the check number is valid, put it in the transaction code
                    if ( checkNumber.AsType<int?>() != null )
                    {
                        transaction.TransactionCode = checkNumber;
                    }
                    // check for SecureGive kiosk transactions
                    else if ( !string.IsNullOrEmpty( checkNumber ) && checkNumber.StartsWith( "SG" ) )
                    {
                        transaction.SourceTypeValueId = sourceTypeKiosk;
                    }

                    string fundName = row[FundName] as string;
                    string subFund = row[SubFundName] as string;
                    string fundGLAccount = row[FundGLAccount] as string;
                    string subFundGLAccount = row[SubFundGLAccount] as string;
                    string isFundActiveKey = row[FundIsActive];
                    Boolean? isFundActive = isFundActiveKey.AsType<Boolean?>();
                    string isSubFundActiveKey = row[SubFundIsActive];
                    Boolean? isSubFundActive = isSubFundActiveKey.AsType<Boolean?>();
                    string statedValueKey = row[StatedValue];
                    decimal? statedValue = statedValueKey.AsType<decimal?>();
                    string amountKey = row[Amount];
                    decimal? amount = amountKey.AsType<decimal?>();
                    if ( !String.IsNullOrWhiteSpace( fundName ) & amount != null )
                    {
                        int transactionAccountId;
                        var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName.Truncate( 50 ) ) && a.CampusId == null );
                        if ( parentAccount == null )
                        {
                            parentAccount = AddAccount( lookupContext, fundName, fundGLAccount, null, null, isFundActive );
                            accountList.Add( parentAccount );
                        }

                        if ( !String.IsNullOrWhiteSpace( subFund ) )
                        {
                            int? campusFundId = null;
                            // assign a campus if the subfund is a campus fund
                            var campusFund = CampusList.FirstOrDefault( c => subFund.StartsWith( c.Name ) || subFund.StartsWith( c.ShortCode ) );
                            if ( campusFund != null )
                            {
                                // use full campus name as the subfund
                                subFund = campusFund.Name;
                                campusFundId = campusFund.Id;
                            }

                            // add info to easily find/assign this fund in the view
                            subFund = string.Format( "{0} {1}", subFund, fundName );

                            var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund.Truncate( 50 ) ) && c.ParentAccountId == parentAccount.Id );
                            if ( childAccount == null )
                            {
                                // create a child account with a campusId if it was set
                                childAccount = AddAccount( lookupContext, subFund, subFundGLAccount, campusFundId, parentAccount.Id, isSubFundActive );
                                accountList.Add( childAccount );
                            }

                            transactionAccountId = childAccount.Id;
                        }
                        else
                        {
                            transactionAccountId = parentAccount.Id;
                        }

                        if ( amount == 0 && statedValue != null && statedValue != 0 )
                        {
                            amount = statedValue;
                        }

                        var transactionDetail = new FinancialTransactionDetail();
                        transactionDetail.Amount = ( decimal )amount;
                        transactionDetail.CreatedDateTime = receivedDate;
                        transactionDetail.AccountId = transactionAccountId;
                        transaction.TransactionDetails.Add( transactionDetail );

                        if ( amount < 0 )
                        {
                            transaction.RefundDetails = new FinancialTransactionRefund();
                            transaction.RefundDetails.CreatedDateTime = receivedDate;
                            transaction.RefundDetails.RefundReasonValueId = refundReasons.Where( dv => summary != null && dv.Value.Contains( summary ) )
                                .Select( dv => ( int? )dv.Id ).FirstOrDefault();
                            transaction.RefundDetails.RefundReasonSummary = summary;
                        }
                    }

                    newTransactions.Add( transaction );
                    completed++;
                    if ( completed % (ReportingNumber * 10) < 1 )
                    {
                        ReportProgress( 0, string.Format( "{0:N0} contributions imported.", completed ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveContributions( newTransactions );
                        newTransactions.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            if ( newTransactions.Any() )
            {
                SaveContributions( newTransactions );
            }

            ReportProgress( 100, string.Format( "Finished contribution import: {0:N0} contributions imported.", completed ) );
            return completed;
        }

        /// <summary>
        /// Saves the contributions.
        /// </summary>
        /// <param name="newTransactions">The new transactions.</param>
        private static void SaveContributions( List<FinancialTransaction> newTransactions )
        {
            using ( var rockContext = new RockContext() )
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialTransactions.AddRange( newTransactions );
                rockContext.SaveChanges( DisableAuditing );
            }
        }

        /// <summary>
        /// Maps the pledge.
        /// </summary>
        /// <param name="queryable">The queryable.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private int MapPledge( CSVInstance csvData )
        {
            var lookupContext = new RockContext();
            var accountList = new FinancialAccountService( lookupContext ).Queryable().AsNoTracking().ToList();
            var importedPledges = new FinancialPledgeService( lookupContext ).Queryable().AsNoTracking()
               .Where( p => p.ForeignId != null )
               .ToDictionary( t => ( int )t.ForeignId, t => ( int? )t.Id );

            var pledgeFrequencies = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_FREQUENCY ), lookupContext ).DefinedValues;
            int oneTimePledgeFrequencyId = pledgeFrequencies.FirstOrDefault( f => f.Guid == new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME ) ).Id;

            var newPledges = new List<FinancialPledge>();

            int completed = 0;
            ReportProgress( 0, string.Format( "Verifying pledge import ({0:N0} already exist).", importedPledges.Count ) );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( (row = csvData.Database.FirstOrDefault()) != null )
            {
                string amountKey = row[TotalPledge];
                decimal? amount = amountKey.AsType<decimal?>();
                string startDateKey = row[StartDate];
                if ( String.IsNullOrWhiteSpace( startDateKey ) )
                {
                    startDateKey = "01/01/0001";
                }
                DateTime? startDate = startDateKey.AsType<DateTime?>();
                string endDateKey = row[EndDate];
                if ( String.IsNullOrWhiteSpace( endDateKey ) )
                {
                    endDateKey = "12/31/9999";
                }
                DateTime? endDate = endDateKey.AsType<DateTime?>();
                string pledgeIdKey = row[PledgeId];
                int? pledgeId = pledgeIdKey.AsType<int?>();
                if ( amount != null && !importedPledges.ContainsKey( ( int )pledgeId ) )
                {
                    string individualIdKey = row[IndividualID];
                    int? individualId = individualIdKey.AsType<int?>();

                    var personKeys = GetPersonKeys( individualId );
                    if ( personKeys != null && personKeys.PersonAliasId > 0 )
                    {
                        var pledge = new FinancialPledge();
                        pledge.PersonAliasId = personKeys.PersonAliasId;
                        pledge.CreatedByPersonAliasId = ImportPersonAliasId;
                        pledge.StartDate = ( DateTime )startDate;
                        pledge.EndDate = ( DateTime )endDate;
                        pledge.TotalAmount = ( decimal )amount;
                        pledge.CreatedDateTime = ImportDateTime;
                        pledge.ModifiedDateTime = ImportDateTime;
                        pledge.ModifiedByPersonAliasId = ImportPersonAliasId;
                        pledge.ForeignKey = pledgeIdKey;
                        pledge.ForeignId = pledgeId;

                        string frequency = row[PledgeFrequencyName].ToString().ToLower();
                        if ( !String.IsNullOrWhiteSpace( frequency ) )
                        {
                            frequency = frequency.ToLower();
                            if ( frequency.Equals( "one time" ) || frequency.Equals( "one-time" ) || frequency.Equals( "as can" ) )
                            {
                                pledge.PledgeFrequencyValueId = oneTimePledgeFrequencyId;
                            }
                            else
                            {
                                pledge.PledgeFrequencyValueId = pledgeFrequencies
                                    .Where( f => f.Value.ToLower().StartsWith( frequency ) || f.Description.ToLower().StartsWith( frequency ) )
                                    .Select( f => f.Id ).FirstOrDefault();
                            }
                        }

                        string fundName = row[FundName] as string;
                        string subFund = row[SubFundName] as string;
                        string fundGLAccount = row[FundGLAccount] as string;
                        string subFundGLAccount = row[SubFundGLAccount] as string;
                        string isFundActiveKey = row[FundIsActive];
                        Boolean? isFundActive = isFundActiveKey.AsType<Boolean?>();
                        string isSubFundActiveKey = row[SubFundIsActive];
                        Boolean? isSubFundActive = isSubFundActiveKey.AsType<Boolean?>();

                        if ( !String.IsNullOrWhiteSpace( fundName ) )
                        {
                            var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName.Truncate( 50 ) ) && a.CampusId == null );
                            if ( parentAccount == null )
                            {
                                parentAccount = AddAccount( lookupContext, fundName, string.Empty, null, null, isFundActive );
                                accountList.Add( parentAccount );
                            }

                            if ( !String.IsNullOrWhiteSpace( subFund ) )
                            {
                                int? campusFundId = null;
                                // assign a campus if the subfund is a campus fund
                                var campusFund = CampusList.FirstOrDefault( c => subFund.StartsWith( c.Name ) || subFund.StartsWith( c.ShortCode ) );
                                if ( campusFund != null )
                                {
                                    // use full campus name as the subfund
                                    subFund = campusFund.Name;
                                    campusFundId = campusFund.Id;
                                }

                                // add info to easily find/assign this fund in the view
                                subFund = string.Format( "{0} {1}", subFund, fundName );

                                var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund.Truncate( 50 ) ) && c.ParentAccountId == parentAccount.Id );
                                if ( childAccount == null )
                                {
                                    // create a child account with a campusId if it was set
                                    childAccount = AddAccount( lookupContext, subFund, string.Empty, campusFundId, parentAccount.Id, isSubFundActive );
                                    accountList.Add( childAccount );
                                }

                                pledge.AccountId = childAccount.Id;
                            }
                            else
                            {
                                pledge.AccountId = parentAccount.Id;
                            }
                        }

                        newPledges.Add( pledge );
                        completed++;
                        if ( completed % (ReportingNumber * 10) < 1 )
                        {
                            ReportProgress( 0, string.Format( "{0:N0} pledges imported.", completed ) );
                        }
                        else if ( completed % ReportingNumber < 1 )
                        {
                            SavePledges( newPledges );
                            ReportPartialProgress();
                            newPledges.Clear();
                        }
                    }
                }
            }

            if ( newPledges.Any() )
            {
                SavePledges( newPledges );
            }

            ReportProgress( 100, string.Format( "Finished pledge import: {0:N0} pledges imported.", completed ) );
            return completed;
        }

        /// <summary>
        /// Saves the pledges.
        /// </summary>
        /// <param name="newPledges">The new pledges.</param>
        private static void SavePledges( List<FinancialPledge> newPledges )
        {
            using ( var rockContext = new RockContext() )
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialPledges.AddRange( newPledges );
                rockContext.SaveChanges( DisableAuditing );
            }
        }

        /// <summary>
        /// Adds the account.
        /// </summary>
        /// <param name="lookupContext">The lookup context.</param>
        /// <param name="fundName">Name of the fund.</param>
        /// <param name="fundCampusId">The fund campus identifier.</param>
        /// <returns></returns>
        private static FinancialAccount AddAccount( RockContext lookupContext, string fundName, string accountGL, int? fundCampusId, int? parentAccountId, bool? isActive )
        {
            lookupContext = lookupContext ?? new RockContext();

            var account = new FinancialAccount();
            account.Name = fundName.Truncate( 50 );
            account.GlCode = accountGL.Truncate( 50 );
            account.PublicName = fundName.Truncate( 50 );
            account.IsTaxDeductible = true;
            account.IsActive = isActive ?? true;
            account.CampusId = fundCampusId;
            account.ParentAccountId = parentAccountId;
            account.CreatedByPersonAliasId = ImportPersonAliasId;

            lookupContext.FinancialAccounts.Add( account );
            lookupContext.SaveChanges( DisableAuditing );

            return account;
        }
    }
}