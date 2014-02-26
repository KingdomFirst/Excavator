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
using Rock.Web.Cache;

namespace Excavator.F1
{
    partial class F1Component
    {
        /// <summary>
        /// The imported batches
        /// </summary>
        private Dictionary<int?, int?> ImportedBatches;

        /// <summary>
        /// The imported contributions
        /// </summary>
        private Dictionary<int?, int?> ImportedContributions;

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
                    batch.CreatedByPersonAliasId = ImportPersonAlias.Id;

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
            var accountService = new FinancialAccountService();

            var transactionTypeContributionId = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION ) ).Id;

            int currencyTypeACH = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_ACH ) ).Id;
            int currencyTypeCash = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CASH ) ).Id;
            int currencyTypeCheck = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CHECK ) ).Id;
            int currencyTypeCreditCard = DefinedValueCache.Read( new Guid( Rock.SystemGuid.DefinedValue.CURRENCY_TYPE_CREDIT_CARD ) ).Id;

            var contributionAttribute = AttributeCache.Read( ContributionAttributeId );

            List<DefinedValue> refundReasons = new DefinedValueService().Queryable().Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_TRANSACTION_REFUND_REASON ) ).ToList();

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
                        // encryption here would require a public key already set
                        // and routing and account numbers aren't available
                        transaction.CheckMicrEncrypted = string.Format( "ImportedCheck_{0}", checkNumber );
                    }

                    string fundName = row["Fund_Name"] as string;
                    string subFund = row["Sub_Fund_Name"] as string;
                    decimal? amount = row["Amount"] as decimal?;
                    if ( fundName != null & amount != null )
                    {
                        // match the fund account if we can
                        FinancialAccount matchingAccount = null;
                        int? fundCampusId = null;
                        if ( subFund != null )
                        {
                            fundCampusId = CampusList.Where( c => c.Name.StartsWith( subFund ) || c.ShortCode == subFund )
                                .Select( c => (int?)c.Id ).FirstOrDefault();
                            matchingAccount = accountList.FirstOrDefault( a => ( a.Name.StartsWith( fundName ) && a.CampusId == fundCampusId ) ||
                                ( a.ParentAccount.Name.StartsWith( fundName ) && a.Name.StartsWith( subFund ) ) );
                        }
                        else
                        {
                            matchingAccount = accountList.FirstOrDefault( a => a.Name.StartsWith( fundName ) );
                        }

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
        /// Maps the pledge.
        /// </summary>
        /// <param name="queryable">The queryable.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        private int MapPledge( IQueryable<Row> tableData )
        {
            List<FinancialAccount> accountList = new FinancialAccountService().Queryable().ToList();

            List<DefinedValue> pledgeFrequencies = new DefinedValueService().Queryable()
                .Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.FINANCIAL_FREQUENCY ) ).ToList();

            foreach ( var row in tableData )
            {
                decimal? amount = row["Total_Pledge"] as decimal?;
                DateTime? startDate = row["Start_Date"] as DateTime?;
                DateTime? endDate = row["End_Date"] as DateTime?;
                if ( amount != null && startDate != null && endDate != null )
                {
                    var pledge = new FinancialPledge();
                    pledge.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    pledge.StartDate = (DateTime)startDate;
                    pledge.EndDate = (DateTime)endDate;

                    string fundName = row["Fund_Name"] as string;
                    string subFund = row["Sub_Fund_Name"] as string;
                    if ( fundName != null )
                    {
                        // does subFund match a campus?
                        int? fundCampusId = CampusList.Where( c => c.Name.StartsWith( subFund ) || c.ShortCode == subFund )
                            .Select( c => (int?)c.Id ).FirstOrDefault();

                        // if not, try to match subFund by name
                        pledge.AccountId = accountList.Where( a => ( a.Name.StartsWith( fundName ) && a.CampusId == fundCampusId )
                            || ( a.ParentAccount.Name.StartsWith( fundName ) && a.Name.StartsWith( subFund ) ) )
                            .Select( a => (int?)a.Id ).FirstOrDefault();
                    }
                    else
                    {
                        pledge.AccountId = accountList.Where( a => a.Name.StartsWith( fundName ) )
                            .Select( a => (int?)a.Id ).FirstOrDefault();
                    }

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

                    int? individualId = row["Individual_ID"] as int?;
                    int? householdId = row["Household_ID"] as int?;
                    if ( householdId != null )
                    {
                        pledge.PersonId = GetPersonId( individualId, householdId );
                    }

                    // Attributes to add
                    // Pledge_Drive_Name

                    RockTransactionScope.WrapTransaction( () =>
                    {
                        var pledgeService = new FinancialPledgeService();
                        pledgeService.Add( pledge, ImportPersonAlias );
                        pledgeService.Save( pledge, ImportPersonAlias );
                    } );
                }
            }

            return tableData.Count();
        }
    }
}
