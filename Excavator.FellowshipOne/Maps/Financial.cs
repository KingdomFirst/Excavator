﻿// <copyright>
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
                        string encodedNumber = FinancialPersonBankAccount.EncodeAccountNumber( routingNumber.ToString(), accountNumber );
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
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialPersonBankAccounts.AddRange( newBankAccounts );
                rockContext.SaveChanges( DisableAuditing );
            } );
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
                    batch.ForeignId = batchId;
                    batch.Status = batchStatusClosed;

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
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialBatches.AddRange( newBatches );
                rockContext.SaveChanges( DisableAuditing );
            } );
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

            int currencyTypeACH = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH ), lookupContext ).Id;
            int currencyTypeCash = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH ), lookupContext ).Id;
            int currencyTypeCheck = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ), lookupContext ).Id;
            int currencyTypeCreditCard = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD ), lookupContext ).Id;

            var refundReasons = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_REFUND_REASON ), lookupContext ).DefinedValues;

            var accountList = new FinancialAccountService( lookupContext ).Queryable().AsNoTracking().ToList();

            // Get all imported contributions
            var importedContributions = new FinancialTransactionService( lookupContext ).Queryable().AsNoTracking()
               .Where( c => c.ForeignId != null )
               .ToDictionary( t => t.ForeignId, t => (int?)t.Id );

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
                    transaction.TransactionTypeValueId = transactionTypeContributionId;
                    transaction.ForeignId = contributionId;

                    var personKeys = GetPersonKeys( individualId, householdId );
                    if ( personKeys != null && personKeys.PersonAliasId > 0 )
                    {
                        transaction.AuthorizedPersonAliasId = personKeys.PersonAliasId;
                        transaction.ProcessedByPersonAliasId = personKeys.PersonAliasId;
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

                    DateTime? receivedDate = row["Received_Date"] as DateTime?;
                    if ( receivedDate != null )
                    {
                        transaction.TransactionDateTime = receivedDate;
                        transaction.CreatedDateTime = receivedDate;
                    }

                    bool isTypeNonCash = false;
                    string contributionType = row["Contribution_Type_Name"].ToString().ToLower();
                    if ( contributionType != null )
                    {
                        transaction.FinancialPaymentDetail = new FinancialPaymentDetail();

                        if ( contributionType == "ach" )
                        {
                            transaction.FinancialPaymentDetail.CurrencyTypeValueId = currencyTypeACH;
                        }
                        else if ( contributionType == "cash" )
                        {
                            transaction.FinancialPaymentDetail.CurrencyTypeValueId = currencyTypeCash;
                        }
                        else if ( contributionType == "check" )
                        {
                            transaction.FinancialPaymentDetail.CurrencyTypeValueId = currencyTypeCheck;
                        }
                        else if ( contributionType == "credit card" )
                        {
                            transaction.FinancialPaymentDetail.CurrencyTypeValueId = currencyTypeCreditCard;
                            // set FTD.CreditCardTypeValueId once we have access to that column
                        }
                        else
                        {
                            isTypeNonCash = true;
                        }
                    }

                    string checkNumber = row["Check_Number"] as string;
                    if ( checkNumber != null && checkNumber.AsType<int?>() != null )
                    {
                        // set the transaction code to the check number
                        transaction.TransactionCode = checkNumber;
                    }

                    string fundName = row["Fund_Name"] as string;
                    string subFund = row["Sub_Fund_Name"] as string;
                    decimal? amount = row["Amount"] as decimal?;
                    decimal? statedValue = row["Stated_Value"] as decimal?;
                    if ( fundName != null & amount != null )
                    {
                        int transactionAccountId;
                        var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName ) && a.CampusId == null );
                        if ( parentAccount == null )
                        {
                            parentAccount = AddAccount( lookupContext, fundName, null, null );
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

                            var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund ) && c.ParentAccountId == parentAccount.Id );
                            if ( childAccount == null )
                            {
                                // create a child account with a campusId if it was set
                                childAccount = AddAccount( lookupContext, subFund, campusFundId, parentAccount.Id );
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
                            isTypeNonCash = true;
                        }

                        var transactionDetail = new FinancialTransactionDetail();
                        transactionDetail.Amount = (decimal)amount;
                        transactionDetail.CreatedDateTime = receivedDate;
                        transactionDetail.AccountId = transactionAccountId;
                        transactionDetail.IsNonCash = isTypeNonCash;
                        transaction.TransactionDetails.Add( transactionDetail );

                        if ( amount < 0 )
                        {
                            var transactionRefund = new FinancialTransactionRefund();
                            transactionRefund.CreatedDateTime = receivedDate;
                            transactionRefund.RefundReasonSummary = summary;
                            transactionRefund.RefundReasonValueId = refundReasons.Where( dv => summary != null && dv.Value.Contains( summary ) )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                            transaction.Refunds.Add( transactionRefund );
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
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialTransactions.AddRange( newTransactions );
                rockContext.SaveChanges( DisableAuditing );
            } );
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
                            var parentAccount = accountList.FirstOrDefault( a => a.Name.Equals( fundName ) && a.CampusId == null );
                            if ( parentAccount == null )
                            {
                                parentAccount = AddAccount( lookupContext, fundName, null, null );
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

                                var childAccount = accountList.FirstOrDefault( c => c.Name.Equals( subFund ) && c.ParentAccountId == parentAccount.Id );
                                if ( childAccount == null )
                                {
                                    // create a child account with a campusId if it was set
                                    childAccount = AddAccount( lookupContext, subFund, campusFundId, parentAccount.Id );
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
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.FinancialPledges.AddRange( newPledges );
                rockContext.SaveChanges( DisableAuditing );
            } );
        }

        /// <summary>
        /// Adds the account.
        /// </summary>
        /// <param name="lookupContext">The lookup context.</param>
        /// <param name="fundName">Name of the fund.</param>
        /// <param name="fundCampusId">The fund campus identifier.</param>
        /// <returns></returns>
        private static FinancialAccount AddAccount( RockContext lookupContext, string fundName, int? fundCampusId, int? parentAccountId )
        {
            lookupContext = lookupContext ?? new RockContext();

            var account = new FinancialAccount();
            account.Name = fundName;
            account.PublicName = fundName;
            account.IsTaxDeductible = true;
            account.IsActive = true;
            account.CampusId = fundCampusId;
            account.ParentAccountId = parentAccountId;
            account.CreatedByPersonAliasId = ImportPersonAliasId;

            lookupContext.FinancialAccounts.Add( account );
            lookupContext.SaveChanges( DisableAuditing );

            return account;
        }
    }
}