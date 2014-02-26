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
    /// <summary>
    /// Partial of F1Component that holds the Attendance import methods
    /// </summary>
    partial class F1Component
    {
        /// <summary>
        /// Maps the attendance.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private int MapAttendance( IQueryable<Row> tableData )
        {
            foreach ( var row in tableData )
            {
                int? individualId = row["Individual_ID"] as int?;
                DateTime? startTime = row["Start_Date_time"] as DateTime?;
                if ( startTime != null ) //&& !ImportedBatches.ContainsKey( batchId )
                {
                    var attendance = new Rock.Model.Attendance();
                    attendance.CreatedByPersonAliasId = ImportPersonAlias.Id;

                    string name = row["BatchName"] as string;
                    if ( name != null )
                    {
                        //attendance.Name = name;
                    }

                    RockTransactionScope.WrapTransaction( () =>
                    {
                        var attendanceService = new AttendanceService();
                        attendanceService.Add( attendance, ImportPersonAlias );
                        attendanceService.Save( attendance, ImportPersonAlias );
                    } );
                }

                // Individual_ID
                // RLC_ID
                // Start_Date_Time
                // Tag_Comment
                // Tag_Code
                // CheckedInAs
                // BreakoutGroup_Name
                // Check_In_Time
                // Check_Out_Time
                // Pager_Code
                // Job_Title
                // Checkin_Machine_Name
            }

            return tableData.Count();
        }
    }
}
