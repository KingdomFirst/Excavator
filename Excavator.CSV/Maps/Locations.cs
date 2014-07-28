using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.CSV
{
    class Locations
    {
        /// <summary>
        /// Maps the family address. (modeled after Excavator.F1.MapFamilyAddress
        ///             //we need the group id before we can persist the location information
        ///    new Locations().MapFamilyAddresses(familyAddress, familyAddress2, _familyGroup);
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        public void MapFamilyAddresses(Location address, Location address2, int familyGroupId, string familyGroupName)
        {
            if (familyGroupId == 0)
                throw new Exception("You'll need to save the Group to get an Id before saving locations associated with that Group. (Locations.MapFamilyAddress)");
            var lookupContext = new RockContext();
            var lookupService = new LocationService(lookupContext);

            List<DefinedValue> groupLocationTypeList = new DefinedValueService(lookupContext).GetByDefinedTypeGuid(new Guid(Rock.SystemGuid.DefinedType.GROUP_LOCATION_TYPE)).ToList();

            int homeGroupLocationTypeId = groupLocationTypeList.FirstOrDefault(dv => dv.Guid == new Guid(Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME)).Id;
            int workGroupLocationTypeId = groupLocationTypeList.FirstOrDefault(dv => dv.Guid == new Guid(Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_WORK)).Id;

            var newGroupLocations = new List<GroupLocation>();
           
            if (address != null)
            {
                var groupLocation = new GroupLocation();

                groupLocation.GroupId = familyGroupId;
                groupLocation.LocationId = address.Id;
                groupLocation.IsMailingLocation = true;
                groupLocation.GroupLocationTypeValueId = homeGroupLocationTypeId;
                newGroupLocations.Add(groupLocation);
            }

            if (address2 != null)
            {
                var groupLocation = new GroupLocation();

                address2.Name = familyGroupName;
                address2.IsActive = true;

                groupLocation.GroupId = familyGroupId;
                groupLocation.LocationId = address2.Id;
                groupLocation.IsMailingLocation = false;
                groupLocation.IsMappedLocation = false;
                groupLocation.GroupLocationTypeValueId = workGroupLocationTypeId;
                newGroupLocations.Add(groupLocation);
            }

            if (newGroupLocations.Count > 0)
            {
                RockTransactionScope.WrapTransaction(() =>
                    {
                        var rockContext = new RockContext();
                        rockContext.Configuration.AutoDetectChangesEnabled = false;
                        rockContext.GroupLocations.AddRange(newGroupLocations);
                        rockContext.SaveChanges(true);
                    });
            }
        }
    }
}
