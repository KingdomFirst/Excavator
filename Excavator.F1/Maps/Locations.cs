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
        /// The imported Activity Id's. Used in RLC & ActivityMinistry
        /// </summary>
        private Dictionary<int?, int?> ImportedActivities;

        /// <summary>
        /// Maps the RLC data to rooms, locations & classes
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private int MapRLC( IQueryable<Row> tableData )
        {
            int locationEntityTypeId = EntityTypeCache.Read( "Rock.Model.Location" ).Id;
            int groupEntityTypeId = EntityTypeCache.Read( "Rock.Model.Group" ).Id;
            var attributeService = new AttributeService();

            // Add an Attribute for the unique F1 RLC Id
            var rlcAttributeId = attributeService.Queryable().Where( a => a.EntityTypeId == locationEntityTypeId
                && a.Key == "F1RLCId" ).Select( a => a.Id ).FirstOrDefault();
            if ( rlcAttributeId == 0 )
            {
                var newRLCAttribute = new Rock.Model.Attribute();
                newRLCAttribute.Key = "F1RLCId";
                newRLCAttribute.Name = "F1 RLC Id";
                newRLCAttribute.FieldTypeId = IntegerFieldTypeId;
                newRLCAttribute.EntityTypeId = locationEntityTypeId;
                newRLCAttribute.EntityTypeQualifierValue = string.Empty;
                newRLCAttribute.EntityTypeQualifierColumn = string.Empty;
                newRLCAttribute.Description = "The FellowshipOne identifier for the RLC (Room/Location/Class) that was imported";
                newRLCAttribute.DefaultValue = string.Empty;
                newRLCAttribute.IsMultiValue = false;
                newRLCAttribute.IsRequired = false;
                newRLCAttribute.Order = 0;

                attributeService.Add( newRLCAttribute, ImportPersonAlias );
                attributeService.Save( newRLCAttribute, ImportPersonAlias );
                rlcAttributeId = newRLCAttribute.Id;
            }

            var activityAttributeId = attributeService.Queryable().Where( a => a.EntityTypeId == locationEntityTypeId
                && a.Key == "F1ActivityId" ).Select( a => a.Id ).FirstOrDefault();
            if ( rlcAttributeId == 0 )
            {
                var newActivityAttribute = new Rock.Model.Attribute();
                newActivityAttribute.Key = "F1ActivityId";
                newActivityAttribute.Name = "F1 Activity Id";
                newActivityAttribute.FieldTypeId = IntegerFieldTypeId;
                newActivityAttribute.EntityTypeId = locationEntityTypeId;
                newActivityAttribute.EntityTypeQualifierValue = string.Empty;
                newActivityAttribute.EntityTypeQualifierColumn = string.Empty;
                newActivityAttribute.Description = "The FellowshipOne identifier for the activity that was imported";
                newActivityAttribute.DefaultValue = string.Empty;
                newActivityAttribute.IsMultiValue = false;
                newActivityAttribute.IsRequired = false;
                newActivityAttribute.Order = 0;

                attributeService.Add( newActivityAttribute, ImportPersonAlias );
                attributeService.Save( newActivityAttribute, ImportPersonAlias );
                activityAttributeId = newActivityAttribute.Id;
            }

            var rlcAttribute = AttributeCache.Read( rlcAttributeId );
            var activityAttribute = AttributeCache.Read( activityAttributeId );

            // Get previously imported RLCs
            var importedRLC = new AttributeValueService().GetByAttributeId( rlcAttributeId )
                .Select( av => new { RLCId = av.Value.AsType<int?>(), LocationId = av.EntityId } )
                .ToDictionary( t => t.RLCId, t => t.LocationId );

            ImportedActivities = new AttributeValueService().GetByAttributeId( activityAttributeId )
                .Select( av => new { ActivityId = av.Value.AsType<int?>(), GroupId = av.EntityId } )
                .ToDictionary( t => t.ActivityId, t => t.GroupId );

            foreach ( var row in tableData )
            {
                int? rlcId = row["RLC_ID"] as int?;
                if ( rlcId != null && !importedRLC.ContainsKey( rlcId ) )
                {
                    // Activity_ID
                    // RLC_Name
                    // Activity_Group_ID
                    // Start_Age_Date
                    // End_Age_Date
                    // Is_Active
                    // Room_Code
                    // Room_Desc
                    // Room_Name
                    // Max_Capacity
                    // Building_Name
                }
            }

            return tableData.Count();
        }

        /// <summary>
        /// Maps the activity ministry.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private int MapActivityMinistry( IQueryable<Row> tableData )
        {
            int groupEntityTypeId = EntityTypeCache.Read( "Rock.Model.Group" ).Id;
            var attributeService = new AttributeService();

            // Add an Attribute for the unique F1 Ministry Id
            var ministryAttributeId = attributeService.Queryable().Where( a => a.EntityTypeId == groupEntityTypeId
                && a.Key == "F1MinistryId" ).Select( a => a.Id ).FirstOrDefault();
            if ( ministryAttributeId == 0 )
            {
                var newMinistryAttribute = new Rock.Model.Attribute();
                newMinistryAttribute.Key = "F1MinistryId";
                newMinistryAttribute.Name = "F1 Ministry Id";
                newMinistryAttribute.FieldTypeId = IntegerFieldTypeId;
                newMinistryAttribute.EntityTypeId = groupEntityTypeId;
                newMinistryAttribute.EntityTypeQualifierValue = string.Empty;
                newMinistryAttribute.EntityTypeQualifierColumn = string.Empty;
                newMinistryAttribute.Description = "The FellowshipOne identifier for the ministry that was imported";
                newMinistryAttribute.DefaultValue = string.Empty;
                newMinistryAttribute.IsMultiValue = false;
                newMinistryAttribute.IsRequired = false;
                newMinistryAttribute.Order = 0;

                attributeService.Add( newMinistryAttribute, ImportPersonAlias );
                attributeService.Save( newMinistryAttribute, ImportPersonAlias );
                ministryAttributeId = newMinistryAttribute.Id;
            }

            // Get previously imported Ministries
            var importedMinistries = new AttributeValueService().GetByAttributeId( ministryAttributeId )
                .Select( av => new { RLCId = av.Value.AsType<int?>(), LocationId = av.EntityId } )
                .ToDictionary( t => t.RLCId, t => t.LocationId );

            foreach ( var row in tableData )
            {
                int? ministryId = row["Ministry_ID"] as int?;
                if ( ministryId != null && !importedMinistries.ContainsKey( ministryId ) )
                {
                    // Activity_ID
                    // Ministry_Name
                    // Activity_Name
                    // Ministry_Active
                    // Activity_Active
                }
            }

            return tableData.Count();
        }

        /// <summary>
        /// Maps the family address.
        /// </summary>
        /// <param name="tableData">The table data.</param>
        /// <returns></returns>
        private int MapFamilyAddress( IQueryable<Row> tableData )
        {
            int groupEntityTypeId = EntityTypeCache.Read( "Rock.Model.Group" ).Id;

            List<DefinedValue> groupLocationTypeList = new DefinedValueService().Queryable().Where( dv => dv.DefinedType.Guid == new Guid( Rock.SystemGuid.DefinedType.GROUP_LOCATION_TYPE ) ).ToList();

            int homeGroupLocationTypeId = groupLocationTypeList.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME ) ).Id;
            int workGroupLocationTypeId = groupLocationTypeList.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_WORK ) ).Id;
            int previousGroupLocationTypeId = groupLocationTypeList.FirstOrDefault( dv => dv.Guid == new Guid( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_PREVIOUS ) ).Id;

            foreach ( var row in tableData )
            {
                int? individualId = row["Individual_ID"] as int?;
                int? householdId = row["Household_ID"] as int?;
                int? associatedPersonId = GetPersonId( individualId, householdId );
                if ( associatedPersonId != null )
                {
                    var familyGroup = new GroupMemberService().Queryable().Where( gm => gm.Group.GroupType.Guid == new Guid( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY )
                        && gm.PersonId == (int)associatedPersonId ).Select( gm => gm.Group ).FirstOrDefault();

                    if ( familyGroup != null )
                    {
                        var groupLocation = new GroupLocation();

                        string address = row["Address_1"] as string;
                        string supplemental = row["Address_2"] as string;
                        string city = row["City"] as string;
                        string state = row["State"] as string;
                        string country = row["country"] as string; // NOT A TYPO: F1 has property in lower-case
                        string zip = row["Postal_Code"] as string;

                        // Get new or existing location and associate it with group
                        var familyAddress = new LocationService().Get( address, supplemental, city, state, zip );
                        familyAddress.CreatedByPersonAliasId = ImportPersonAlias.Id;
                        familyAddress.Name = familyGroup.Name;
                        familyAddress.IsActive = true;

                        groupLocation.Location = familyAddress;
                        groupLocation.IsMailingLocation = true;
                        groupLocation.IsMappedLocation = true;

                        string addressType = row["Address_Type"] as string;

                        if ( addressType.Equals( "Primary" ) )
                        {
                            groupLocation.GroupLocationTypeValueId = homeGroupLocationTypeId;
                        }
                        else if ( addressType.Equals( "Business" ) || addressType.Equals( "Org" ) )
                        {
                            groupLocation.GroupLocationTypeValueId = workGroupLocationTypeId;
                        }
                        else if ( addressType.Equals( "Previous" ) )
                        {
                            groupLocation.GroupLocationTypeValueId = previousGroupLocationTypeId;
                        }
                        else if ( !string.IsNullOrEmpty( addressType ) )
                        {
                            groupLocation.GroupLocationTypeValueId = groupLocationTypeList.Where( dv => dv.Name.Equals( addressType ) )
                                .Select( dv => (int?)dv.Id ).FirstOrDefault();
                        }

                        groupLocation.GroupId = familyGroup.Id;
                        RockTransactionScope.WrapTransaction( () =>
                        {
                            var groupLocationService = new GroupLocationService();
                            groupLocationService.Add( groupLocation );
                            groupLocationService.Save( groupLocation );
                        } );
                    }
                }
            }

            return tableData.Count();
        }
    }
}
