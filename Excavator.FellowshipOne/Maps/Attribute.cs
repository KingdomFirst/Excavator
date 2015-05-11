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
using Excavator.Utility;
using OrcaMDF.Core.MetaData;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    /// <summary>
    /// Partial of F1Component that holds attribute methods
    /// </summary>
    partial class F1Component
    {
        /// <summary>
        /// Maps the attributes.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        private void MapAttribute( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var personService = new PersonService( lookupContext );
            var attributeService = new AttributeService( lookupContext );

            var personAttributes = attributeService.GetByEntityTypeId( PersonEntityTypeId ).ToList();
            var newAttributes = new List<AttributeCache>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying attribute import ({0:N0} found).", totalRows ) );

            foreach ( var groupedRows in tableData.GroupBy<Row, int?>( r => r["Individual_ID"] as int? ) )
            {
                foreach ( var row in groupedRows.Where( r => r != null ) )
                {
                }

                completed++;
                if ( completed % percentage < 1 )
                {
                    int percentComplete = completed / percentage;
                    ReportProgress( percentComplete, string.Format( "{0:N0} attributes imported ({1}% complete).", completed, percentComplete ) );
                }
                else if ( completed % ReportingNumber < 1 )
                {
                    // Save attributes here

                    ReportPartialProgress();
                }
            }

            //if ( businessList.Any() )
            //{
            //    SaveCompanies( businessList );
            //}

            ReportProgress( 100, string.Format( "Finished attribute import: {0:N0} attributes imported.", completed ) );
        }
    }
}