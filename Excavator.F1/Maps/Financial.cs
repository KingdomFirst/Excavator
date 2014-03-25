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
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// Partial of F1Component that holds the Financial import methods
    /// </summary>
    partial class F1Component
    {
        /// <summary>
        /// Maps the account data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private void MapAccount( IQueryable<Row> tableData )
        {
            var accountService = new FinancialPersonBankAccountService();
            var importedAccounts = accountService.Queryable().ToList();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Starting check number import ({0:N0} to import).", totalRows ) );

            foreach ( var row in tableData )
            {
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                int? personId = GetPersonId( individualId, householdId );
                if ( personId != null )
                {
                    int? routingNumber = row["Routing_Number"] as int?;
                    string accountNumber = row["Account"] as string;
                    if ( routingNumber != null && !string.IsNullOrWhiteSpace( accountNumber ) )
                    {
                        accountNumber = accountNumber.Replace( " ", string.Empty );
                        string encodedNumber = FinancialPersonBankAccount.EncodeAccountNumber( routingNumber.ToString(), accountNumber );
                        if ( !importedAccounts.Any( a => a.PersonId == personId && a.AccountNumberSecured == encodedNumber ) )
                        {
                            var account = new FinancialPersonBankAccount();
                            account.CreatedByPersonAliasId = ImportPersonAlias.Id;
                            account.AccountNumberSecured = encodedNumber;
                            account.PersonId = (int)personId;

                            // Other Attributes (not used):
                            // Account_Type_Name

                            accountService.RockContext.FinancialPersonBankAccounts.Add( account );
                            completed++;
                            if ( completed % percentage < 1 )
                            {
                                int percentComplete = completed / percentage;
                                ReportProgress( percentComplete, string.Format( "{0:N0} numbers imported ({1}% complete).", completed, percentComplete ) );
                            }
                            else if ( completed % ReportingNumber < 1 )
                            {
                                accountService.RockContext.SaveChanges();
                                ReportPartialProgress();
                            }
                        }
                    }
                }
            }

            accountService.RockContext.SaveChanges();
            ReportProgress( 100, string.Format( "Finished check number import: {0:N0} numbers imported.", completed ) );
        }

        /// <summary>
        /// Maps the batch data.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void MapBatch( IQueryable<Row> tableData )
        {
            var attributeValueService = new AttributeValueService();
            var attributeService = new AttributeService();
            var batchService = new FinancialBatchService();
            var batchAttribute = AttributeCache.Read( BatchAttributeId );
            var batchStatusClosed = Rock.Model.BatchStatus.Closed;
            var newBatches = new List<FinancialBatch>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Starting batch import ({0:N0} found, {1:N0} already imported).", totalRows, ImportedBatches.Count() ) );
            foreach ( var row in tableData )
            {
                int? batchId = row["BatchID"] as int?;
                if ( batchId != null && !ImportedBatches.ContainsKey( batchId ) )
                {
                    var batch = new FinancialBatch();
                    batch.CreatedByPersonAliasId = ImportPersonAlias.Id;
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

                    batch.Attributes = new Dictionary<string, AttributeCache>();
                    batch.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                    batch.Attributes.Add( batchAttribute.Key, batchAttribute );
                    batch.AttributeValues.Add( batchAttribute.Key, new List<AttributeValue>() );
                    batch.AttributeValues[batchAttribute.Key].Add( new AttributeValue()
                    {
                        AttributeId = batchAttribute.Id,
                        Value = batchId.ToString()
                    } );

                    newBatches.Add( batch );
                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} batches imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        batchService.RockContext.FinancialBatches.AddRange( newBatches );
                        batchService.RockContext.SaveChanges();

                        foreach ( var newBatch in newBatches.Where( b => b.Attributes.Any() ) )
                        {
                            var attributeValue = newBatch.AttributeValues[batchAttribute.Key].FirstOrDefault();
                            if ( attributeValue != null )
                            {
                                attributeValue.EntityId = newBatch.Id;
                                attributeValueService.RockContext.AttributeValues.Add( attributeValue );
                            }
                        }

                        attributeValueService.RockContext.SaveChanges();
                        newBatches.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            if ( newBatches.Any() )
            {
                batchService.RockContext.FinancialBatches.AddRange( newBatches );
                batchService.RockContext.SaveChanges();

                foreach ( var newBatch in newBatches.Where( b => b.Attributes.Any() ) )
                {
                    var attributeValue = newBatch.AttributeValues[batchAttribute.Key].FirstOrDefault();
                    if ( attributeValue != null )
                    {
                        attributeValue.EntityId = newBatch.Id;
                        attributeValueService.RockContext.AttributeValues.Add( attributeValue );
                    }
                }

                attributeValueService.RockContext.SaveChanges();
            }

            ReportProgress( 100, string.Format( "Finished batch import: {0:N0} batches imported.", completed ) );
        }

        /// <summary>
        /// Maps the contribution.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <param name="selectedColumns">The selected columns.</param>
        private void MapContribution( IQueryable<Row> tableData, List<string> selectedColumns = null )
        {
            int transactionEntityTypeId = EntityTypeCache.Read( "Rock.Model.FinancialTransaction" ).Id;
            var attributeValueService = new AttributeValueService();
            var transactionService = new FinancialTransactionService();
            var accountService = new FinancialAccountService();
            var attributeService = new AttributeService();

            var transactionTypeContributionId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION ) ).Id;

            int currencyTypeACH = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH ) ).Id;
            int currencyTypeCash = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH ) ).Id;
            int currencyTypeCheck = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ) ).Id;
            int currencyTypeCreditCard = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD ) ).Id;

            List<DefinedValue> refundReasons = new DefinedValueService().Queryable().Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_REFUND_REASON ) ).ToList();

            List<FinancialPledge> pledgeList = new FinancialPledgeService().Queryable().ToList();

            List<FinancialAccount> accountList = accountService.Queryable().ToList();

            // Add an Attribute for the unique F1 Contribution Id
            int contributionAttributeId = attributeService.Queryable().Where( a => a.EntityTypeId == transactionEntityTypeId
                && a.Key == "F1ContributionId" ).Select( a => a.Id ).FirstOrDefault();
            if ( contributionAttributeId == 0 )
            {
                var newContributionAttribute = new Rock.Model.Attribute();
                newContributionAttribute.Key = "F1ContributionId";
                newContributionAttribute.Name = "F1 Contribution Id";
                newContributionAttribute.FieldTypeId = IntegerFieldTypeId;
                newContributionAttribute.EntityTypeId = transactionEntityTypeId;
                newContributionAttribute.EntityTypeQualifierValue = string.Empty;
                newContributionAttribute.EntityTypeQualifierColumn = string.Empty;
                newContributionAttribute.Description = "The FellowshipOne identifier for the contribution that was imported";
                newContributionAttribute.DefaultValue = string.Empty;
                newContributionAttribute.IsMultiValue = false;
                newContributionAttribute.IsRequired = false;
                newContributionAttribute.Order = 0;

                attributeService.Add( newContributionAttribute, ImportPersonAlias );
                attributeService.Save( newContributionAttribute, ImportPersonAlias );
                contributionAttributeId = newContributionAttribute.Id;
            }

            var contributionAttribute = AttributeCache.Read( contributionAttributeId );

            // Get all imported contributions
            var importedContributions = attributeValueService.GetByAttributeId( contributionAttributeId )
               .Select( av => new { ContributionId = av.Value.AsType<int?>(), TransactionId = av.EntityId } )
               .ToDictionary( t => t.ContributionId, t => t.TransactionId );

            // List for batching new contributions
            var newContributions = new List<FinancialTransaction>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Starting contribution import ({0:N0} found, {1:N0} already imported).", totalRows, importedContributions.Count() ) );
            foreach ( var row in tableData )
            {
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                int? contributionId = row["ContributionID"] as int?;

                if ( contributionId != null && !importedContributions.ContainsKey( contributionId ) )
                {
                    var transaction = new FinancialTransaction();
                    transaction.TransactionTypeValueId = transactionTypeContributionId;
                    transaction.AuthorizedPersonId = GetPersonId( individualId, householdId );
                    transaction.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    transaction.AuthorizedPersonId = GetPersonId( individualId, householdId );

                    string summary = row["Memo"] as string;
                    if ( summary != null )
                    {
                        transaction.Summary = summary;
                    }

                    int? batchId = row["BatchID"] as int?;
                    if ( batchId != null && ImportedBatches.Any( b => b.Key == batchId ) )
                    {
                        transaction.BatchId = ImportedBatches.FirstOrDefault( b => b.Key == batchId ).Value;
                    }

                    DateTime? receivedDate = row["Received_Date"] as DateTime?;
                    if ( receivedDate != null )
                    {
                        transaction.TransactionDateTime = receivedDate;
                        transaction.CreatedDateTime = receivedDate;
                    }

                    bool isTypeNonCash = false;
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
                            isTypeNonCash = true;
                        }
                    }

                    string checkNumber = row["Check_Number"] as string;
                    if ( checkNumber != null && checkNumber.AsType<int?>() != null )
                    {
                        // routing & account set to zero
                        transaction.CheckMicrEncrypted = Encryption.EncryptString( string.Format( "{0}_{1}_{2}", 0, 0, checkNumber ) );
                    }

                    string fundName = row["Fund_Name"] as string;
                    string subFund = row["Sub_Fund_Name"] as string;
                    decimal? amount = row["Amount"] as decimal?;
                    if ( fundName != null & amount != null )
                    {
                        FinancialAccount matchingAccount = null;
                        fundName = fundName.Trim();

                        int? fundCampusId = null;
                        if ( subFund != null )
                        {
                            subFund = subFund.Trim();
                            fundCampusId = CampusList.Where( c => c.Name.StartsWith( subFund ) || c.ShortCode == subFund )
                                .Select( c => (int?)c.Id ).FirstOrDefault();

                            if ( fundCampusId != null )
                            {
                                matchingAccount = accountList.FirstOrDefault( a => a.Name.StartsWith( fundName )
                                    && a.CampusId != null && a.CampusId.Equals( fundCampusId ) );
                            }
                            else
                            {
                                matchingAccount = accountList.FirstOrDefault( a => a.Name.StartsWith( fundName ) && a.Name.StartsWith( subFund ) );
                            }
                        }
                        else
                        {
                            matchingAccount = accountList.FirstOrDefault( a => a.Name.StartsWith( fundName ) && a.CampusId == null );
                        }

                        if ( matchingAccount == null )
                        {
                            matchingAccount = new FinancialAccount();
                            matchingAccount.Name = fundName;
                            matchingAccount.PublicName = fundName;
                            matchingAccount.IsTaxDeductible = true;
                            matchingAccount.IsActive = true;
                            matchingAccount.CampusId = fundCampusId;
                            matchingAccount.CreatedByPersonAliasId = ImportPersonAlias.Id;

                            accountService.Add( matchingAccount );
                            accountService.Save( matchingAccount );
                            accountList.Add( matchingAccount );
                        }

                        var transactionDetail = new FinancialTransactionDetail();
                        transactionDetail.Amount = (decimal)amount;
                        transactionDetail.CreatedDateTime = receivedDate;
                        transactionDetail.AccountId = matchingAccount.Id;
                        transactionDetail.IsNonCash = isTypeNonCash;
                        transaction.TransactionDetails.Add( transactionDetail );

                        if ( amount < 0 )
                        {
                            var transactionRefund = new FinancialTransactionRefund();
                            transactionRefund.CreatedDateTime = receivedDate;
                            transactionRefund.RefundReasonSummary = summary;
                            transactionRefund.RefundReasonValueId = refundReasons.Where( dv => summary != null && dv.Name.Contains( summary ) )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                            transaction.Refund = transactionRefund;
                        }
                    }

                    // Other Attributes to create:
                    // Pledge_Drive_Name
                    // Stated_Value
                    // True_Value
                    // Liquidation_cost

                    transaction.Attributes = new Dictionary<string, AttributeCache>();
                    transaction.AttributeValues = new Dictionary<string, List<AttributeValue>>();
                    transaction.Attributes.Add( contributionAttribute.Key, contributionAttribute );
                    transaction.AttributeValues.Add( contributionAttribute.Key, new List<AttributeValue>() );
                    transaction.AttributeValues[contributionAttribute.Key].Add( new AttributeValue()
                    {
                        AttributeId = contributionAttribute.Id,
                        Value = contributionId.ToString()
                    } );

                    newContributions.Add( transaction );
                    completed++;
                    if ( completed % percentage < 1 )
                    {
                        int percentComplete = completed / percentage;
                        ReportProgress( percentComplete, string.Format( "{0:N0} contributions imported ({1}% complete).", completed, percentComplete ) );
                    }
                    else if ( completed % ReportingNumber < 1 )
                    {
                        transactionService.RockContext.FinancialTransactions.AddRange( newContributions );
                        transactionService.RockContext.SaveChanges();

                        foreach ( var contribution in newContributions.Where( c => c.Attributes.Any() ) )
                        {
                            var attributeValue = contribution.AttributeValues[contributionAttribute.Key].FirstOrDefault();
                            if ( attributeValue != null )
                            {
                                attributeValue.EntityId = contribution.Id;
                                attributeValueService.RockContext.AttributeValues.Add( attributeValue );
                            }
                        }

                        attributeValueService.RockContext.SaveChanges();
                        newContributions.Clear();
                        ReportPartialProgress();
                    }
                }
            }

            if ( newContributions.Any() )
            {
                transactionService.RockContext.FinancialTransactions.AddRange( newContributions );
                transactionService.RockContext.SaveChanges();

                foreach ( var contribution in newContributions.Where( c => c.Attributes.Any() ) )
                {
                    var attributeValue = contribution.AttributeValues[contributionAttribute.Key].FirstOrDefault();
                    if ( attributeValue != null )
                    {
                        attributeValue.EntityId = contribution.Id;
                        attributeValueService.RockContext.AttributeValues.Add( attributeValue );
                    }
                }

                attributeValueService.RockContext.SaveChanges();
            }

            ReportProgress( 100, string.Format( "Finished contribution import: {0:N0} contributions imported.", completed ) );
        }

        /// <summary>
        /// Maps the pledge.
        /// </summary>
        /// <param name="queryable">The queryable.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private void MapPledge( IQueryable<Row> tableData )
        {
            var accountService = new FinancialAccountService();
            var pledgeService = new FinancialPledgeService();

            List<FinancialAccount> importedAccounts = accountService.Queryable().ToList();

            List<DefinedValue> pledgeFrequencies = new DefinedValueService().Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_FREQUENCY ) ).ToList();

            List<FinancialPledge> importedPledges = new FinancialPledgeService().Queryable().ToList();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Starting pledge import ({0:N0} to import).", totalRows ) );

            foreach ( var row in tableData )
            {
                decimal? amount = row["Total_Pledge"] as decimal?;
                DateTime? startDate = row["Start_Date"] as DateTime?;
                DateTime? endDate = row["End_Date"] as DateTime?;
                if ( amount != null && startDate != null && endDate != null )
                {
                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;
                    int? personId = GetPersonId( individualId, householdId );
                    if ( personId != null && !importedPledges.Any( p => p.PersonId == personId && p.TotalAmount == amount && p.StartDate.Equals( startDate ) ) )
                    {
                        var pledge = new FinancialPledge();
                        pledge.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        pledge.StartDate = (DateTime)startDate;
                        pledge.EndDate = (DateTime)endDate;
                        pledge.TotalAmount = (decimal)amount;

                        string frequency = row["Pledge_Frequency_Name"] as string;
                        if ( frequency != null )
                        {
                            if ( frequency == "One Time" || frequency == "As Can" )
                            {
                                pledge.PledgeFrequencyValueId = pledgeFrequencies.FirstOrDefault( f => f.Guid == new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_FREQUENCY_ONE_TIME ) ).Id;
                            }
                            else
                            {
                                pledge.PledgeFrequencyValueId = pledgeFrequencies
                                    .Where( f => f.Name.StartsWith( frequency ) || f.Description.StartsWith( frequency ) )
                                    .Select( f => f.Id ).FirstOrDefault();
                            }
                        }

                        string fundName = row["Fund_Name"] as string;
                        string subFund = row["Sub_Fund_Name"] as string;
                        if ( fundName != null )
                        {
                            FinancialAccount matchingAccount = null;
                            int? fundCampusId = null;
                            if ( subFund != null )
                            {
                                // match by campus if the subfund appears to be a campus
                                fundCampusId = CampusList.Where( c => c.Name.StartsWith( subFund ) || c.ShortCode == subFund )
                                    .Select( c => (int?)c.Id ).FirstOrDefault();

                                if ( fundCampusId != null )
                                {
                                    matchingAccount = importedAccounts.FirstOrDefault( a => a.Name.StartsWith( fundName ) && a.CampusId != null && a.CampusId.Equals( fundCampusId ) );
                                }
                                else
                                {
                                    matchingAccount = importedAccounts.FirstOrDefault( a => a.Name.StartsWith( fundName ) && a.Name.StartsWith( subFund ) );
                                }
                            }
                            else
                            {
                                matchingAccount = importedAccounts.FirstOrDefault( a => a.Name.StartsWith( fundName ) );
                            }

                            if ( matchingAccount == null )
                            {
                                matchingAccount = new FinancialAccount();
                                matchingAccount.Name = fundName;
                                matchingAccount.PublicName = fundName;
                                matchingAccount.IsTaxDeductible = true;
                                matchingAccount.IsActive = true;
                                matchingAccount.CampusId = fundCampusId;
                                matchingAccount.CreatedByPersonAliasId = ImportPersonAlias.Id;

                                accountService.Add( matchingAccount );
                                accountService.Save( matchingAccount );
                                importedAccounts.Add( matchingAccount );
                                pledge.AccountId = matchingAccount.Id;
                            }
                        }

                        // Attributes to add?
                        // Pledge_Drive_Name

                        pledgeService.RockContext.FinancialPledges.Add( pledge );
                        completed++;
                        if ( completed % percentage < 1 )
                        {
                            int percentComplete = completed / percentage;
                            ReportProgress( percentComplete, string.Format( "{0:N0} pledges imported ({1}% complete).", completed, percentComplete ) );
                        }
                        else if ( completed % ReportingNumber < 1 )
                        {
                            pledgeService.RockContext.SaveChanges();
                            ReportPartialProgress();
                        }
                    }
                }
            }

            pledgeService.RockContext.SaveChanges();
            ReportProgress( 100, string.Format( "Finished pledge import: {0:N0} pledges imported.", completed ) );
        }
    }
}
