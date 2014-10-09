using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.CSV
{
    /// <summary>
    /// Partial of CSVComponent that holds the location import methods
    /// </summary>
    partial class CSVComponent
    {
        /// <summary>
        /// Saves the family group locations.
        /// </summary>
        private void SaveFamilyGroupLocations( List<Group> familiesToSave )
        {
            // Locations are already saved on lookup using locationService.Get(),
            // just need to associate the group now with the location
            var rockContext = new RockContext();
            var definedValueService = new DefinedValueService( rockContext );
            int homeGroupLocationTypeId = definedValueService.GetByGuid( new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME ) ).Id;
            int workGroupLocationTypeId = definedValueService.GetByGuid( new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_WORK ) ).Id;

            rockContext.WrapTransaction( () =>
            {
                var newGroupLocations = new List<GroupLocation>();
                foreach ( var locations in FamilyGroupLocations )
                {
                    var familyForeignId = locations.Key;
                    var familyLocations = locations.Value;
                    var familyGroup = familiesToSave.FirstOrDefault( g => g.ForeignId == familyForeignId );

                    Location primaryAddress = familyLocations[0];
                    if ( primaryAddress != null )
                    {
                        var groupLocation = new GroupLocation();
                        groupLocation.GroupId = familyGroup.Id;
                        groupLocation.LocationId = primaryAddress.Id;
                        groupLocation.IsMailingLocation = true;
                        groupLocation.GroupLocationTypeValueId = homeGroupLocationTypeId;
                        newGroupLocations.Add( groupLocation );
                    }

                    if ( familyLocations.Count > 1 )
                    {
                        Location secondaryAddress = familyLocations[1];
                        if ( secondaryAddress != null )
                        {
                            var groupLocation = new GroupLocation();
                            secondaryAddress.IsActive = true;
                            groupLocation.GroupId = familyGroup.Id;
                            groupLocation.LocationId = secondaryAddress.Id;
                            groupLocation.IsMailingLocation = false;
                            groupLocation.IsMappedLocation = false;
                            groupLocation.GroupLocationTypeValueId = workGroupLocationTypeId;
                            newGroupLocations.Add( groupLocation );
                        }
                    }
                }

                if ( newGroupLocations.Count > 0 )
                {
                    rockContext.GroupLocations.AddRange( newGroupLocations );
                    rockContext.SaveChanges( true );
                }
            } );
        }
    }
}
