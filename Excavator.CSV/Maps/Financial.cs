using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using static Excavator.Utility.CachedTypes;

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
            var newBatches = new List<FinancialBatch>();
            var earliestBatchDate = ImportDateTime;

            var completed = 0;
            ReportProgress( 0, $"Verifying batch import ({ImportedBatches.Count:N0} already exist)." );
            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                var batchIdKey = row[BatchID];
                var batchId = batchIdKey.AsType<int?>();
                if ( batchId != null && !ImportedBatches.ContainsKey( (int)batchId ) )
                {
                    var batch = new FinancialBatch
                    {
                        CreatedByPersonAliasId = ImportPersonAliasId,
                        ForeignKey = batchId.ToString(),
                        ForeignId = batchId,
                        Note = string.Empty,
                        Status = BatchStatus.Closed,
                        AccountingSystemCode = string.Empty
                    };

                    var name = row[BatchName] as string;
                    if ( !string.IsNullOrWhiteSpace( name ) )
                    {
                        name = name.Trim();
                        batch.Name = name.Left( 50 );
                        batch.CampusId = CampusList.Where( c => name.StartsWith( c.Name ) || name.StartsWith( c.ShortCode ) )
                            .Select( c => (int?)c.Id ).FirstOrDefault();
                    }

                    var batchDateKey = row[BatchDate];
                    var batchDate = batchDateKey.AsType<DateTime?>();
                    if ( batchDate != null )
                    {
                        batch.BatchStartDateTime = batchDate;
                        batch.BatchEndDateTime = batchDate;

                        if ( earliestBatchDate > batchDate )
                        {
                            earliestBatchDate = (DateTime)batchDate;
                        }
                    }

                    var amountKey = row[BatchAmount];
                    var amount = amountKey.AsType<decimal?>();
                    if ( amount != null )
                    {
                        batch.ControlAmount = amount.HasValue ? amount.Value : new decimal();
                    }

                    newBatches.Add( batch );
                    completed++;
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, $"{completed:N0} batches imported." );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        SaveFinancialBatches( newBatches );
                        newBatches.ForEach( b => ImportedBatches.Add( (int)b.ForeignId, (int?)b.Id ) );
                        newBatches.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            // add a default batch to use with contributions
            if ( !ImportedBatches.ContainsKey( 0 ) )
            {
                var defaultBatch = new FinancialBatch
                {
                    CreatedDateTime = ImportDateTime,
                    CreatedByPersonAliasId = ImportPersonAliasId,
                    Status = BatchStatus.Closed,
                    BatchStartDateTime = earliestBatchDate,
                    Name = $"Default Batch {ImportDateTime}",
                    ControlAmount = 0.0m,
                    ForeignKey = "0",
                    ForeignId = 0
                };

                newBatches.Add( defaultBatch );
            }

            if ( newBatches.Any() )
            {
                SaveFinancialBatches( newBatches );
                newBatches.ForEach( b => ImportedBatches.Add( (int)b.ForeignId, (int?)b.Id ) );
            }

            ReportProgress( 100, $"Finished batch import: {completed:N0} batches imported." );
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

            var currencyTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE ) );
            var currencyTypeACH = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH ) ) ).Id;
            var currencyTypeCash = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH ) ) ).Id;
            var currencyTypeCheck = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ) ) ).Id;
            var currencyTypeCreditCard = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD ) ) ).Id;
            var currencyTypeNonCash = currencyTypes.DefinedValues.Where( dv => dv.Value.Equals( "Non-Cash" ) ).Select( dv => (int?)dv.Id ).FirstOrDefault();
            if ( currencyTypeNonCash == null )
            {
                var newTenderNonCash = new DefinedValue
                {
                    Value = "Non-Cash",
                    Description = "Non-Cash",
                    DefinedTypeId = currencyTypes.Id
                };
                lookupContext.DefinedValues.Add( newTenderNonCash );
                lookupContext.SaveChanges();
                currencyTypeNonCash = newTenderNonCash.Id;
            }

            var creditCardTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_CREDIT_CARD_TYPE ) ).DefinedValues;

            var sourceTypeOnsite = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_ONSITE_COLLECTION ), lookupContext ).Id;
            var sourceTypeWebsite = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE ), lookupContext ).Id;
            var sourceTypeKiosk = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_KIOSK ), lookupContext ).Id;

            var refundReasons = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_REFUND_REASON ), lookupContext ).DefinedValues;

            var accountList = new FinancialAccountService( lookupContext ).Queryable().AsNoTracking().ToList();

            int? defaultBatchId = null;
            if ( ImportedBatches.ContainsKey( 0 ) )
            {
                defaultBatchId = ImportedBatches[0];
            }

            // Look for custom attributes in the Contribution file
            var allFields = csvData.TableNodes.FirstOrDefault().Children.Select( ( node, index ) => new { node = node, index = index } ).ToList();
            var customAttributes = allFields
                .Where( f => f.index > ContributionCreditCardType )
                .ToDictionary( f => f.index, f => f.node.Name );

            // Get all imported contributions
            var importedContributions = new FinancialTransactionService( lookupContext ).Queryable().AsNoTracking()
               .Where( c => c.ForeignId != null )
               .Select( t => (int)t.ForeignId )
               .OrderBy( t => t ).ToList();

            // List for batching new contributions
            var newTransactions = new List<FinancialTransaction>();

            var completed = 0;
            ReportProgress( 0, $"Verifying contribution import ({importedContributions.Count:N0} already exist)." );
            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                var individualIdKey = row[IndividualID];
                var contributionIdKey = row[ContributionID];
                var contributionId = contributionIdKey.AsType<int?>();

                if ( contributionId != null && !importedContributions.Contains( (int)contributionId ) )
                {
                    var transaction = new FinancialTransaction
                    {
                        CreatedByPersonAliasId = ImportPersonAliasId,
                        ModifiedByPersonAliasId = ImportPersonAliasId,
                        TransactionTypeValueId = TransactionTypeContributionId,
                        ForeignKey = contributionId.ToString(),
                        ForeignId = contributionId
                    };

                    int? giverAliasId = null;
                    var personKeys = GetPersonKeys( individualIdKey );
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

                    var summary = row[Memo] as string;
                    if ( !string.IsNullOrWhiteSpace( summary ) )
                    {
                        transaction.Summary = summary;
                    }

                    var batchIdKey = row[ContributionBatchID];
                    var batchId = batchIdKey.AsType<int?>();
                    if ( batchId != null && ImportedBatches.Any( b => b.Key.Equals( batchId ) ) )
                    {
                        transaction.BatchId = ImportedBatches.FirstOrDefault( b => b.Key.Equals( batchId ) ).Value;
                    }
                    else
                    {
                        // use the default batch for any non-matching transactions
                        transaction.BatchId = defaultBatchId;
                    }

                    var receivedDateKey = row[ReceivedDate];
                    var receivedDate = receivedDateKey.AsType<DateTime?>();
                    if ( receivedDate != null )
                    {
                        transaction.TransactionDateTime = receivedDate;
                        transaction.CreatedDateTime = receivedDate;
                        transaction.ModifiedDateTime = ImportDateTime;
                    }

                    var contributionType = row[ContributionTypeName];
                    var creditCardType = row[ContributionCreditCardType];
                    if ( !string.IsNullOrWhiteSpace( contributionType ) )
                    {
                        // set default source to onsite, exceptions listed below
                        transaction.SourceTypeValueId = sourceTypeOnsite;

                        int? paymentCurrencyTypeId = null, creditCardTypeId = null;

                        if ( contributionType.Equals( "cash", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            paymentCurrencyTypeId = currencyTypeCash;
                        }
                        else if ( contributionType.Equals( "check", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            paymentCurrencyTypeId = currencyTypeCheck;
                        }
                        else if ( contributionType.Equals( "ach", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            paymentCurrencyTypeId = currencyTypeACH;
                            transaction.SourceTypeValueId = sourceTypeWebsite;
                        }
                        else if ( contributionType.Equals( "credit card", StringComparison.CurrentCultureIgnoreCase ) )
                        {
                            paymentCurrencyTypeId = currencyTypeCreditCard;
                            transaction.SourceTypeValueId = sourceTypeWebsite;

                            // Determine CC Type
                            if ( !string.IsNullOrWhiteSpace( creditCardType ) )
                            {
                                creditCardTypeId = creditCardTypes.Where( c => c.Value.StartsWith( creditCardType, StringComparison.CurrentCultureIgnoreCase )
                                        || c.Description.StartsWith( creditCardType, StringComparison.CurrentCultureIgnoreCase ) )
                                    .Select( c => c.Id ).FirstOrDefault();
                            }
                        }
                        else
                        {
                            paymentCurrencyTypeId = currencyTypeNonCash;
                        }

                        var paymentDetail = new FinancialPaymentDetail
                        {
                            CreatedDateTime = receivedDate,
                            CreatedByPersonAliasId = giverAliasId,
                            ModifiedDateTime = ImportDateTime,
                            ModifiedByPersonAliasId = giverAliasId,
                            CurrencyTypeValueId = paymentCurrencyTypeId,
                            CreditCardTypeValueId = creditCardTypeId,
                            ForeignKey = contributionId.ToString(),
                            ForeignId = contributionId
                        };

                        transaction.FinancialPaymentDetail = paymentDetail;
                    }

                    var transactionCode = row[CheckNumber] as string;
                    // if transaction code provided, put it in the transaction code
                    if ( !string.IsNullOrEmpty( transactionCode ) )
                    {
                        transaction.TransactionCode = transactionCode;

                        // check for SecureGive kiosk transactions
                        if ( transactionCode.StartsWith( "SG" ) )
                        {
                            transaction.SourceTypeValueId = sourceTypeKiosk;
                        }
                    }

                    var fundName = row[FundName] as string;
                    var subFund = row[SubFundName] as string;
                    var fundGLAccount = row[FundGLAccount] as string;
                    var subFundGLAccount = row[SubFundGLAccount] as string;
                    var isFundActiveKey = row[FundIsActive];
                    var isFundActive = isFundActiveKey.AsType<bool?>();
                    var isSubFundActiveKey = row[SubFundIsActive];
                    var isSubFundActive = isSubFundActiveKey.AsType<bool?>();
                    var statedValueKey = row[StatedValue];
                    var statedValue = statedValueKey.AsType<decimal?>();
                    var amountKey = row[Amount];
                    var amount = amountKey.AsType<decimal?>();
                    if ( !string.IsNullOrWhiteSpace( fundName ) & amount != null )
                    {
                        int transactionAccountId;
                        var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName.Truncate( 50 ) ) );
                        if ( parentAccount == null )
                        {
                            parentAccount = AddAccount( lookupContext, fundName, fundGLAccount, null, null, isFundActive, null, null, null, null, "", "", null );
                            accountList.Add( parentAccount );
                        }

                        if ( !string.IsNullOrWhiteSpace( subFund ) )
                        {
                            int? campusFundId = null;
                            // assign a campus if the subfund is a campus fund
                            var campusFund = CampusList.FirstOrDefault( c => subFund.Contains( c.Name ) || subFund.Contains( c.ShortCode ) );
                            if ( campusFund != null )
                            {
                                campusFundId = campusFund.Id;
                            }

                            // add info to easily find/assign this fund in the view
                            subFund = $"{fundName} {subFund}";

                            var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund.Truncate( 50 ) ) && c.ParentAccountId == parentAccount.Id );
                            if ( childAccount == null )
                            {
                                // create a child account with a campusId if it was set
                                childAccount = AddAccount( lookupContext, subFund, subFundGLAccount, campusFundId, parentAccount.Id, isSubFundActive, null, null, null, null, "", "", null );
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

                        var transactionDetail = new FinancialTransactionDetail
                        {
                            Amount = (decimal)amount,
                            CreatedDateTime = receivedDate,
                            AccountId = transactionAccountId
                        };
                        transaction.TransactionDetails.Add( transactionDetail );

                        if ( amount < 0 )
                        {
                            transaction.RefundDetails = new FinancialTransactionRefund();
                            transaction.RefundDetails.CreatedDateTime = receivedDate;
                            transaction.RefundDetails.RefundReasonValueId = refundReasons.Where( dv => summary != null && dv.Value.Contains( summary ) )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                            transaction.RefundDetails.RefundReasonSummary = summary;
                        }
                    }

                    newTransactions.Add( transaction );
                    completed++;
                    if ( completed % ( ReportingNumber * 10 ) < 1 )
                    {
                        ReportProgress( 0, $"{completed:N0} contributions imported." );
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

            ReportProgress( 100, $"Finished contribution import: {completed:N0} contributions imported." );
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
        /// <param name="csvData">todo: describe csvData parameter on MapPledge</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        private int MapPledge( CSVInstance csvData )
        {
            var lookupContext = new RockContext();
            var accountList = new FinancialAccountService( lookupContext ).Queryable().AsNoTracking().ToList();
            var importedPledges = new FinancialPledgeService( lookupContext ).Queryable().AsNoTracking()
               .Where( p => p.ForeignId != null )
               .ToDictionary( t => (int)t.ForeignId, t => (int?)t.Id );

            var pledgeFrequencies = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_FREQUENCY ), lookupContext ).DefinedValues;
            var oneTimePledgeFrequencyId = pledgeFrequencies.FirstOrDefault( f => f.Guid == new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME ) ).Id;

            var newPledges = new List<FinancialPledge>();

            var completed = 0;
            ReportProgress( 0, $"Verifying pledge import ({importedPledges.Count:N0} already exist)." );

            string[] row;
            // Uses a look-ahead enumerator: this call will move to the next record immediately
            while ( ( row = csvData.Database.FirstOrDefault() ) != null )
            {
                var amountKey = row[TotalPledge];
                var amount = amountKey.AsType<decimal?>();
                var startDateKey = row[StartDate];
                if ( string.IsNullOrWhiteSpace( startDateKey ) )
                {
                    startDateKey = "01/01/0001";
                }
                var startDate = startDateKey.AsType<DateTime?>();
                var endDateKey = row[EndDate];
                if ( string.IsNullOrWhiteSpace( endDateKey ) )
                {
                    endDateKey = "12/31/9999";
                }
                var endDate = endDateKey.AsType<DateTime?>();
                var createdDateKey = row[PledgeCreatedDate];
                if ( string.IsNullOrWhiteSpace( createdDateKey ) )
                {
                    createdDateKey = ImportDateTime.ToString();
                }
                var createdDate = createdDateKey.AsType<DateTime?>();
                var modifiedDateKey = row[PledgeModifiedDate];
                if ( string.IsNullOrWhiteSpace( modifiedDateKey ) )
                {
                    modifiedDateKey = ImportDateTime.ToString();
                }
                var modifiedDate = modifiedDateKey.AsType<DateTime?>();

                var pledgeIdKey = row[PledgeId];
                var pledgeId = pledgeIdKey.AsType<int?>();
                if ( amount != null && !importedPledges.ContainsKey( (int)pledgeId ) )
                {
                    var individualIdKey = row[IndividualID];

                    var personKeys = GetPersonKeys( individualIdKey );
                    if ( personKeys != null && personKeys.PersonAliasId > 0 )
                    {
                        var pledge = new FinancialPledge
                        {
                            PersonAliasId = personKeys.PersonAliasId,
                            CreatedByPersonAliasId = ImportPersonAliasId,
                            StartDate = (DateTime)startDate,
                            EndDate = (DateTime)endDate,
                            TotalAmount = (decimal)amount,
                            CreatedDateTime = createdDate,
                            ModifiedDateTime = modifiedDate,
                            ModifiedByPersonAliasId = ImportPersonAliasId,
                            ForeignKey = pledgeIdKey,
                            ForeignId = pledgeId
                        };

                        var frequency = row[PledgeFrequencyName].ToString().ToLower();
                        if ( !string.IsNullOrWhiteSpace( frequency ) )
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

                        var fundName = row[FundName] as string;
                        var subFund = row[SubFundName] as string;
                        var fundGLAccount = row[FundGLAccount] as string;
                        var subFundGLAccount = row[SubFundGLAccount] as string;
                        var isFundActiveKey = row[FundIsActive];
                        var isFundActive = isFundActiveKey.AsType<bool?>();
                        var isSubFundActiveKey = row[SubFundIsActive];
                        var isSubFundActive = isSubFundActiveKey.AsType<bool?>();

                        if ( !string.IsNullOrWhiteSpace( fundName ) )
                        {
                            var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName.Truncate( 50 ) ) );
                            if ( parentAccount == null )
                            {
                                parentAccount = AddAccount( lookupContext, fundName, string.Empty, null, null, isFundActive, null, null, null, null, "", "", null );
                                accountList.Add( parentAccount );
                            }

                            if ( !string.IsNullOrWhiteSpace( subFund ) )
                            {
                                int? campusFundId = null;
                                // assign a campus if the subfund is a campus fund
                                var campusFund = CampusList.FirstOrDefault( c => subFund.Contains( c.Name ) || subFund.Contains( c.ShortCode ) );
                                if ( campusFund != null )
                                {
                                    campusFundId = campusFund.Id;
                                }

                                // add info to easily find/assign this fund in the view
                                subFund = $"{fundName} {subFund}";

                                var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund.Truncate( 50 ) ) && c.ParentAccountId == parentAccount.Id );
                                if ( childAccount == null )
                                {
                                    // create a child account with a campusId if it was set
                                    childAccount = AddAccount( lookupContext, subFund, string.Empty, campusFundId, parentAccount.Id, isSubFundActive, null, null, null, null, "", "", null );
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
                        if ( completed % ( ReportingNumber * 10 ) < 1 )
                        {
                            ReportProgress( 0, $"{completed:N0} pledges imported." );
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

            ReportProgress( 100, $"Finished pledge import: {completed:N0} pledges imported." );
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
        /// <param name="accountGL">The account gl.</param>
        /// <param name="fundCampusId">The fund campus identifier.</param>
        /// <param name="parentAccountId">The parent account identifier.</param>
        /// <param name="isActive">The is active.</param>
        /// <param name="startDate">The start date.</param>
        /// <param name="endDate">The end date.</param>
        /// <param name="order">The order.</param>
        /// <param name="foreignId">The foreign identifier.</param>
        /// <param name="fundDescription">The fund description.</param>
        /// <param name="fundPublicName">Name of the fund public.</param>
        /// <param name="isTaxDeductible">The is tax deductible.</param>
        /// <returns></returns>
        private static FinancialAccount AddAccount( RockContext lookupContext, string fundName, string accountGL, int? fundCampusId, int? parentAccountId, bool? isActive, DateTime? startDate, DateTime? endDate, int? order, int? foreignId, string fundDescription, string fundPublicName, bool? isTaxDeductible )
        {
            lookupContext = lookupContext ?? new RockContext();

            var account = new FinancialAccount
            {
                Name = fundName.Truncate( 50 ),
                Description = fundDescription,
                GlCode = accountGL.Truncate( 50 ),
                IsTaxDeductible = isTaxDeductible ?? true,
                IsActive = isActive ?? true,
                IsPublic = false,
                CampusId = fundCampusId,
                ParentAccountId = parentAccountId,
                CreatedByPersonAliasId = ImportPersonAliasId,
                StartDate = startDate,
                EndDate = endDate,
                ForeignId = foreignId,
                ForeignKey = foreignId.ToString()
            };

            if ( !string.IsNullOrWhiteSpace( fundPublicName ) )
            {
                account.PublicName = fundPublicName.Truncate( 50 );
            }
            else
            {
                account.PublicName = fundName.Truncate( 50 );
            }

            if ( order != null )
            {
                account.Order = order ?? -1;
            }

            lookupContext.FinancialAccounts.Add( account );
            lookupContext.SaveChanges( DisableAuditing );

            return account;
        }
    }
}
