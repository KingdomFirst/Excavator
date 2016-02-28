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
using Excavator.Utility;
using OrcaMDF.Core.MetaData;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.F1
{
    public partial class F1Component
    {
        /// <summary>
        /// Maps the family address.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private void MapFamilyAddress( IQueryable<Row> tableData )
        {
            var lookupContext = new RockContext();
            var locationService = new LocationService( lookupContext );

            List<GroupMember> familyGroupMemberList = new GroupMemberService( lookupContext ).Queryable().AsNoTracking()
                .Where( gm => gm.Group.GroupType.Guid == new Guid( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY ) ).ToList();

            var groupLocationDefinedType = DefinedTypeCache.Read( new Guid( Rock.SystemGuid.DefinedType.GROUP_LOCATION_TYPE ), lookupContext );
            int homeGroupLocationTypeId = groupLocationDefinedType.DefinedValues
                .FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME ) ).Id;
            int workGroupLocationTypeId = groupLocationDefinedType.DefinedValues
                .FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_WORK ) ).Id;
            int previousGroupLocationTypeId = groupLocationDefinedType.DefinedValues
                .FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_PREVIOUS ) ).Id;

            string otherGroupLocationName = "Other (Imported)";
            int? otherGroupLocationTypeId = groupLocationDefinedType.DefinedValues
                .Where( dv => dv.TypeName == otherGroupLocationName )
                .Select( dv => (int?)dv.Id ).FirstOrDefault();
            if ( otherGroupLocationTypeId == null )
            {
                var otherGroupLocationType = new DefinedValue();
                otherGroupLocationType.Value = otherGroupLocationName;
                otherGroupLocationType.DefinedTypeId = groupLocationDefinedType.Id;
                otherGroupLocationType.IsSystem = false;
                otherGroupLocationType.Order = 0;

                lookupContext.DefinedValues.Add( otherGroupLocationType );
                lookupContext.SaveChanges( DisableAuditing );

                otherGroupLocationTypeId = otherGroupLocationType.Id;
            }

            var newGroupLocations = new List<GroupLocation>();

            int completed = 0;
            int totalRows = tableData.Count();
            int percentage = ( totalRows - 1 ) / 100 + 1;
            ReportProgress( 0, string.Format( "Verifying address import ({0:N0} found).", totalRows ) );

            foreach ( var row in tableData.Where( r => r != null ) )
            {
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                var personKeys = GetPersonKeys( individualId, householdId, includeVisitors: false );
                if ( personKeys != null )
                {
                    var familyGroup = familyGroupMemberList.Where( gm => gm.PersonId == personKeys.PersonId )
                        .Select( gm => gm.Group ).FirstOrDefault();

                    if ( familyGroup != null )
                    {
                        var groupLocation = new GroupLocation();

                        string street1 = row["Address_1"] as string;
                        string street2 = row["Address_2"] as string;
                        string city = row["City"] as string;
                        string state = row["State"] as string;
                        string country = row["country"] as string; // NOT A TYPO: F1 has property in lower-case
                        string zip = row["Postal_Code"] as string ?? string.Empty;

                        // restrict zip to 5 places to prevent duplicates
                        Location familyAddress = locationService.Get( street1, street2, city, state, zip.Left( 5 ), country, verifyLocation: false );

                        if ( familyAddress != null )
                        {
                            familyAddress.CreatedByPersonAliasId = ImportPersonAliasId;
                            familyAddress.Name = familyGroup.Name;
                            familyAddress.IsActive = true;

                            groupLocation.GroupId = familyGroup.Id;
                            groupLocation.LocationId = familyAddress.Id;
                            groupLocation.IsMailingLocation = true;
                            groupLocation.IsMappedLocation = true;

                            string addressType = row["Address_Type"].ToString().ToLower();
                            if ( addressType.Equals( "primary" ) )
                            {
                                groupLocation.GroupLocationTypeValueId = homeGroupLocationTypeId;
                            }
                            else if ( addressType.Equals( "business" ) || addressType.ToLower().Equals( "org" ) )
                            {
                                groupLocation.GroupLocationTypeValueId = workGroupLocationTypeId;
                            }
                            else if ( addressType.Equals( "previous" ) )
                            {
                                groupLocation.GroupLocationTypeValueId = previousGroupLocationTypeId;
                            }
                            else if ( !string.IsNullOrEmpty( addressType ) )
                            {
                                // look for existing group location types, otherwise mark as imported
                                var customTypeId = groupLocationDefinedType.DefinedValues.Where( dv => dv.Value.ToLower().Equals( addressType ) )
                                    .Select( dv => (int?)dv.Id ).FirstOrDefault();
                                groupLocation.GroupLocationTypeValueId = customTypeId ?? otherGroupLocationTypeId;
                            }

                            newGroupLocations.Add( groupLocation );
                            completed++;

                            if ( completed % percentage < 1 )
                            {
                                int percentComplete = completed / percentage;
                                ReportProgress( percentComplete, string.Format( "{0:N0} addresses imported ({1}% complete).", completed, percentComplete ) );
                            }
                            else if ( completed % ReportingNumber < 1 )
                            {
                                SaveFamilyAddress( newGroupLocations );

                                // Reset context
                                newGroupLocations.Clear();
                                lookupContext = new RockContext();
                                locationService = new LocationService( lookupContext );

                                ReportPartialProgress();
                            }
                        }
                    }
                }
            }

            if ( newGroupLocations.Any() )
            {
                SaveFamilyAddress( newGroupLocations );
            }

            ReportProgress( 100, string.Format( "Finished address import: {0:N0} addresses imported.", completed ) );
        }

        /// <summary>
        /// Saves the family address.
        /// </summary>
        /// <param name="newGroupLocations">The new group locations.</param>
        private static void SaveFamilyAddress( List<GroupLocation> newGroupLocations )
        {
            var rockContext = new RockContext();
            rockContext.WrapTransaction( () =>
            {
                rockContext.Configuration.AutoDetectChangesEnabled = false;
                rockContext.GroupLocations.AddRange( newGroupLocations );
                rockContext.SaveChanges( DisableAuditing );
            } );
        }
    }
}