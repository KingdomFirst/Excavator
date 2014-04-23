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
using System.Linq;
using OrcaMDF.Core.MetaData;
using Rock.Data;
using Rock.Model;

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
        private void MapAttendance( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();

            foreach ( var row in tableData )
            {
                DateTime? startTime = row["Start_Date_time"] as DateTime?;
                if ( startTime != null )
                {
                    var attendance = new Rock.Model.Attendance();
                    attendance.CreatedByPersonAliasId = ImportPersonAlias.Id;
                    attendance.StartDateTime = (DateTime)startTime;
                    attendance.DidAttend = true;

                    string position = row["CheckedInAs"] as string;
                    string jobTitle = row["Job_Title"] as string;
                    string machineName = row["Checkin_Machine_Name"] as string;
                    int? rlcId = row["RLC_ID"] as int?;

                    // look up location, schedule, group, and device

                    int? individualId = row["Individual_ID"] as int?;
                    if ( individualId != null )
                    {
                        attendance.PersonId = GetPersonId( individualId );
                    }

                    DateTime? checkInTime = row["Check_In_Time"] as DateTime?;
                    if ( checkInTime != null )
                    {
                        // set the start time to the time they actually checked in
                        attendance.StartDateTime = (DateTime)checkInTime;
                    }

                    DateTime? checkOutTime = row["Check_Out_Time"] as DateTime?;
                    if ( checkOutTime != null )
                    {
                        attendance.EndDateTime = (DateTime)checkOutTime;
                    }

                    string f1AttendanceCode = row["Tag_Code"] as string;
                    if ( f1AttendanceCode != null )
                    {
                        attendance.AttendanceCode = new AttendanceCode();
                        attendance.AttendanceCode.Code = f1AttendanceCode;
                    }

                    // Other Attributes to create:
                    // Tag_Comment
                    // BreakoutGroup_Name
                    // Pager_Code

                    RockTransactionScope.WrapTransaction( () =>
                    {
                        var rockContext = new RockContext();
                        rockContext.Configuration.AutoDetectChangesEnabled = false;
                        rockContext.Attendances.Add( attendance );
                        rockContext.SaveChanges( DisableAudit );
                    } );
                }
            }
        }
    }
}
