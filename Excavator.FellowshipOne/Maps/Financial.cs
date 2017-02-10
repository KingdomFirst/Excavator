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
using OrcaMDF.Core.MetaData;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// Partial of F1Component that holds the Financial import methods
    /// </summary>
    public partial class F1Component
    {
        /// <summary>
        /// Maps the account data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private void MapBankAccount( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var importedBankAccounts = new FinancialPersonBankAccountService( lookupContext ).Queryable().AsNoTracking().ToList();
            var newBankAccounts = new List<FinancialPersonBankAccount>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying check number import ({0:N0} found, {1:N0} already exist).", totalRows, importedBankAccounts.Count ) );

            foreach ( var row in tableData.Where( r => r != null ) )
            {
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                var personKeys = GetPersonKeys( individualId, householdId );
                if ( personKeys != null && personKeys.PersonAliasId > 0 )
                {
                    int? routingNumber = row["Routing_Number"] as int?;
                    string accountNumber = row["Account"] as string;
                    if ( routingNumber != null && !string.IsNullOrWhiteSpace( accountNumber ) )
                    {
                        accountNumber = accountNumber.Replace( " ", string.Empty );
                        string encodedNumber = FinancialPersonBankAccount.EncodeAccountNumber( routingNumber.ToString().PadLeft( 9, '0' ), accountNumber );
                        if ( !importedBankAccounts.Any( a => a.PersonAliasId == personKeys.PersonAliasId && a.AccountNumberSecured == encodedNumber ) )
                        {
                            var bankAccount = new FinancialPersonBankAccount();
                            bankAccount.CreatedByPersonAliasId = ImportPersonAliasId;
                            bankAccount.AccountNumberSecured = encodedNumber;
                            bankAccount.AccountNumberMasked = accountNumber.ToString().Masked();
                            bankAccount.PersonAliasId = (int)personKeys.PersonAliasId;

                            newBankAccounts.Add( bankAccount );
                            completed++;
                            if ( completed % percentage < 1 )
                            {
                                int percentComplete = completed / percentage;
                                ReportProgress( percentComplete, string.Format( "{0:N0} numbers imported ({1}% complete).", completed, percentComplete ) );
                            }
                            else if ( completed % ReportingNumber < 1 )
                            {
                                SaveBankAccounts( newBankAccounts );
                                newBankAccounts.Clear();
                                ReportPartialProgress();
                            }
                        }
                    }
                }
            }

            if ( newBankAccounts.Any() )
            {
                SaveBankAccounts( newBankAccounts );
            }

            ReportProgress( 100, string.Format( "Finished check number import: {0:N0} numbers imported.", completed ) );
        }

        /// <summary>
        /// Saves the bank accounts.
        /// </summary>
        /// <param name="newBankAccounts">The new bank accounts.</param>
        private static void SaveBankAccounts( List<FinancialPersonBankAccount> newBankAccounts )
        {
            using ( var rockContext = new RockContext() )
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialPersonBankAccounts.AddRange( newBankAccounts );
                rockContext.SaveChanges( DisableAuditing );
            }
        }

        /// <summary>
        /// Maps the batch data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void MapBatch( IQueryable<Row> tableData )
        {
            var batchStatusClosed = Rock.Model.BatchStatus.Closed;
            var newBatches = new List<FinancialBatch>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying batch import ({0:N0} found, {1:N0} already exist).", totalRows, ImportedBatches.Count ) );
            foreach ( var row in tableData.Where( r => r != null ) )
            {
                int? batchId = row["BatchID"] as int?;
                if ( batchId != null && !ImportedBatches.ContainsKey( (int)batchId ) )
                {
                    var batch = new FinancialBatch();
                    batch.CreatedByPersonAliasId = ImportPersonAliasId;
                    batch.ForeignKey = batchId.ToString();
                    batch.ForeignId = batchId;
                    batch.Note = string.Empty;
                    batch.Status = batchStatusClosed;
                    batch.AccountingSystemCode = string.Empty;

                    string name = row["BatchName"] as string;
                    if ( name != null )
                    {
                        name = name.Trim();
                        batch.Name = name.Left( 50 );
                        batch.CampusId = CampusList.Where( c => name.StartsWith( c.Name ) || name.StartsWith( c.ShortCode ) )
                            .Select( c => (int?)c.Id ).FirstOrDefault();
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

                    newBatches.Add( batch );
                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} batches imported ({1}% complete).", completed, percentComplete ) );
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
                newBatches.ForEach( b => ImportedBatches.Add( (int)b.ForeignId, (int?)b.Id ) );
            }

            ReportProgress( 100, string.Format( "Finished batch import: {0:N0} batches imported.", completed ) );
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
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapContribution( IQueryable<Row> tableData, List<string> selectedColumns = null )
        {
            var lookupContext = new RockContext();
            int transactionEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialTransaction" ).Id;
            var transactionTypeContributionId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION ), lookupContext ).Id;

            var currencyTypes = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_CURRENCY_TYPE ) );
            int currencyTypeACH = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH ) ) ).Id;
            int currencyTypeCash = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH ) ) ).Id;
            int currencyTypeCheck = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ) ) ).Id;
            int currencyTypeCreditCard = currencyTypes.DefinedValues.FirstOrDefault( dv => dv.Guid.Equals( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD ) ) ).Id;
            int? currencyTypeNonCash = currencyTypes.DefinedValues.Where( dv => dv.Value.Equals( "Non-Cash" ) ).Select( dv => (int?)dv.Id ).FirstOrDefault();
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
               .ToDictionary( t => (int)t.ForeignId, t => (int?)t.Id );

            // List for batching new contributions
            var newTransactions = new List<FinancialTransaction>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying contribution import ({0:N0} found, {1:N0} already exist).", totalRows, importedContributions.Count ) );
            foreach ( var row in tableData.Where( r => r != null ) )
            {
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                int? contributionId = row["ContributionID"] as int?;

                if ( contributionId != null && !importedContributions.ContainsKey( (int)contributionId ) )
                {
                    var transaction = new FinancialTransaction();
                    transaction.CreatedByPersonAliasId = ImportPersonAliasId;
                    transaction.ModifiedByPersonAliasId = ImportPersonAliasId;
                    transaction.TransactionTypeValueId = transactionTypeContributionId;
                    transaction.ForeignKey = contributionId.ToString();
                    transaction.ForeignId = contributionId;

                    int? giverAliasId = null;
                    var personKeys = GetPersonKeys( individualId, householdId );
                    if ( personKeys != null && personKeys.PersonAliasId > 0 )
                    {
                        giverAliasId = personKeys.PersonAliasId;
                        transaction.CreatedByPersonAliasId = giverAliasId;
                        transaction.AuthorizedPersonAliasId = giverAliasId;
                        transaction.ProcessedByPersonAliasId = giverAliasId;
                    }

                    string summary = row["Memo"] as string;
                    if ( summary != null )
                    {
                        transaction.Summary = summary;
                    }

                    int? batchId = row["BatchID"] as int?;
                    if ( batchId != null && ImportedBatches.Any( b => b.Key.Equals( batchId ) ) )
                    {
                        transaction.BatchId = ImportedBatches.FirstOrDefault( b => b.Key.Equals( batchId ) ).Value;
                    }
                    else
                    {
                        // use the default batch for any non-matching transactions
                        transaction.BatchId = defaultBatchId;
                    }

                    DateTime? receivedDate = row["Received_Date"] as DateTime?;
                    if ( receivedDate != null )
                    {
                        transaction.TransactionDateTime = receivedDate;
                        transaction.CreatedDateTime = receivedDate;
                        transaction.ModifiedDateTime = ImportDateTime;
                    }

                    var contributionFields = row.Columns.Select( c => c.Name ).ToList();
                    string cardType = contributionFields.Contains( "Card_Type" ) ? row["Card_Type"] as string : string.Empty;
                    string cardLastFour = contributionFields.Contains( "Last_Four" ) ? row["Last_Four"] as string : string.Empty;
                    string contributionType = contributionFields.Contains( "Contribution_Type_Name" ) ? row["Contribution_Type_Name"] as string : string.Empty;

                    if ( !string.IsNullOrEmpty( contributionType ) )
                    {
                        // set default source to onsite, exceptions listed below
                        transaction.SourceTypeValueId = sourceTypeOnsite;
                        contributionType = contributionType.ToLower();

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

                            if ( !string.IsNullOrEmpty( cardType ) )
                            {
                                creditCardTypeId = creditCardTypes.Where( t => t.Value.Equals( cardType ) ).Select( t => (int?)t.Id ).FirstOrDefault();
                            }
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
                        paymentDetail.AccountNumberMasked = cardLastFour;
                        paymentDetail.ForeignKey = contributionId.ToString();
                        paymentDetail.ForeignId = contributionId;

                        transaction.FinancialPaymentDetail = paymentDetail;
                    }

                    string checkNumber = row["Check_Number"] as string;
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

                    string fundName = contributionFields.Contains( "Fund_Name" ) ? row["Fund_Name"] as string : string.Empty;
                    string subFund = contributionFields.Contains( "Sub_Fund_Name" ) ? row["Sub_Fund_Name"] as string : string.Empty;
                    string fundGLAccount = contributionFields.Contains( "Fund_GL_Account" ) ? row["Fund_GL_Account"] as string : string.Empty;
                    string subFundGLAccount = contributionFields.Contains( "Sub_Fund_GL_Account" ) ? row["Sub_Fund_GL_Account"] as string : string.Empty;
                    string isFundActive = contributionFields.Contains( "Fund_Is_active" ) ? row["Fund_Is_active"] as string : null;
                    decimal? statedValue = row["Stated_Value"] as decimal?;
                    decimal? amount = row["Amount"] as decimal?;
                    if ( fundName != null & amount != null )
                    {
                        int transactionAccountId;
                        var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName.Left( 50 ) ) && a.CampusId == null );
                        if ( parentAccount == null )
                        {
                            parentAccount = AddAccount( lookupContext, fundName, fundGLAccount, null, null, isFundActive.AsBooleanOrNull() );
                            accountList.Add( parentAccount );
                        }

                        if ( subFund != null )
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

                            var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund.Left( 50 ) ) && c.ParentAccountId == parentAccount.Id );
                            if ( childAccount == null )
                            {
                                // create a child account with a campusId if it was set
                                childAccount = AddAccount( lookupContext, subFund, subFundGLAccount, campusFundId, parentAccount.Id, isFundActive.AsBooleanOrNull() );
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
                        transactionDetail.Amount = (decimal)amount;
                        transactionDetail.CreatedDateTime = receivedDate;
                        transactionDetail.AccountId = transactionAccountId;
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
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} contributions imported ({1}% complete).", completed, percentComplete ) );
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
        private void MapPledge( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var accountList = new FinancialAccountService( lookupContext ).Queryable().AsNoTracking().ToList();
            var importedPledges = new FinancialPledgeService( lookupContext ).Queryable().AsNoTracking().ToList();

            var pledgeFrequencies = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_FREQUENCY ), lookupContext ).DefinedValues;
            int oneTimePledgeFrequencyId = pledgeFrequencies.FirstOrDefault( f => f.Guid == new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME ) ).Id;

            var newPledges = new List<FinancialPledge>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying pledge import ({0:N0} found).", totalRows ) );

            foreach ( var row in tableData.Where( r => r != null ) )
            {
                decimal? amount = row["Total_Pledge"] as decimal?;
                DateTime? startDate = row["Start_Date"] as DateTime?;
                DateTime? endDate = row["End_Date"] as DateTime?;
                if ( amount != null && startDate != null && endDate != null )
                {
                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;

                    var personKeys = GetPersonKeys( individualId, householdId, includeVisitors: false );
                    if ( personKeys != null && personKeys.PersonAliasId > 0 )
                    {
                        var pledge = new FinancialPledge();
                        pledge.PersonAliasId = personKeys.PersonAliasId;
                        pledge.CreatedByPersonAliasId = ImportPersonAliasId;
                        pledge.ModifiedDateTime = ImportDateTime;
                        pledge.StartDate = (DateTime)startDate;
                        pledge.EndDate = (DateTime)endDate;
                        pledge.TotalAmount = (decimal)amount;

                        string frequency = row["Pledge_Frequency_Name"].ToString().ToLower();
                        if ( frequency != null )
                        {
                            frequency = frequency.ToLower();
                            if ( frequency.Equals( "one time" ) || frequency.Equals( "as can" ) )
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

                        string fundName = row["Fund_Name"] as string;
                        string subFund = row["Sub_Fund_Name"] as string;
                        if ( fundName != null )
                        {
                            var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName.Left( 50 ) ) && a.CampusId == null );
                            if ( parentAccount == null )
                            {
                                parentAccount = AddAccount( lookupContext, fundName, string.Empty, null, null, null );
                                accountList.Add( parentAccount );
                            }

                            if ( subFund != null )
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

                                var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund.Left( 50 ) ) && c.ParentAccountId == parentAccount.Id );
                                if ( childAccount == null )
                                {
                                    // create a child account with a campusId if it was set
                                    childAccount = AddAccount( lookupContext, subFund, string.Empty, campusFundId, parentAccount.Id, null );
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
                        if ( completed % percentage < 1 )
                        {
                            int percentComplete = completed / percentage;
                            ReportProgress( percentComplete, string.Format( "{0:N0} pledges imported ({1}% complete).", completed, percentComplete ) );
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
            account.Name = fundName.Left( 50 );
            account.PublicName = fundName.Left( 50 );
            account.GlCode = accountGL;
            account.IsTaxDeductible = true;
            account.IsActive = isActive ?? true;
            account.CampusId = fundCampusId;
            account.ParentAccountId = parentAccountId;
            account.CreatedByPersonAliasId = ImportPersonAliasId;
            account.Order = 0;

            lookupContext.FinancialAccounts.Add( account );
            lookupContext.SaveChanges( DisableAuditing );

            return account;
        }
    }
}
