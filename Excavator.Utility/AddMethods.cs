using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Rock;
using Rock.Attribute;
using Rock.Data;
using Rock.Model;
using Rock.Security;
using Rock.Web.Cache;
using static Excavator.Utility.CachedTypes;
using Attribute = Rock.Model.Attribute;
using Group = Rock.Model.Group;

namespace Excavator.Utility
{
    public static partial class Extensions
    {
        /// <summary>
        /// Add a new defined value to the Rock system.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="typeGuid">todo: describe typeGuid parameter on AddDefinedValue</param>
        /// <param name="value">The value of the new defined value.</param>
        /// <returns></returns>
        public static DefinedValueCache AddDefinedValue( RockContext rockContext, string typeGuid, string value )
        {
            DefinedValueCache definedValueCache = null;
            var definedTypeGuid = typeGuid.AsGuidOrNull();
            if ( definedTypeGuid != null && !string.IsNullOrWhiteSpace( value ) )
            {
                var definedType = DefinedTypeCache.Read( (Guid)definedTypeGuid, rockContext );

                var definedValue = new DefinedValue
                {
                    IsSystem = false,
                    DefinedTypeId = definedType.Id,
                    Value = value,
                    Description = "Imported with Excavator"
                };

                var maxOrder = definedType.DefinedValues.Max( v => (int?)v.Order );
                definedValue.Order = maxOrder + 1 ?? 0;

                DefinedTypeCache.Flush( definedType.Id );
                DefinedValueCache.Flush( 0 );

                rockContext.DefinedValues.Add( definedValue );
                rockContext.SaveChanges( DisableAuditing );
                definedValueCache = DefinedValueCache.Read( definedValue, rockContext );
            }

            return definedValueCache;
        }

        /// <summary>
        /// Adds the attribute qualifier.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="attributeId">The attribute identifier.</param>
        /// <param name="value">The value.</param>
        public static AttributeQualifier AddAttributeQualifier( RockContext rockContext, int? attributeId, string value )
        {
            AttributeQualifier valuesQualifier = null;
            if ( attributeId.HasValue && !string.IsNullOrWhiteSpace( value ) )
            {
                rockContext = rockContext ?? new RockContext();
                valuesQualifier = new AttributeQualifierService( rockContext ).GetByAttributeId( (int)attributeId )
                    .FirstOrDefault( q => q.Key.Equals( "values", StringComparison.CurrentCultureIgnoreCase ) );
                if ( valuesQualifier != null && !valuesQualifier.Value.Contains( value ) )
                {
                    valuesQualifier.Value = $"{valuesQualifier.Value},{value}";
                    rockContext.Entry( valuesQualifier ).State = EntityState.Modified;
                    rockContext.SaveChanges( DisableAuditing );
                }
            }

            return valuesQualifier;
        }

        /// <summary>
        /// Adds the device.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="deviceDescription">The device description.</param>
        /// <param name="deviceTypeId">The device type identifier.</param>
        /// <param name="locationId">The location identifier.</param>
        /// <param name="ipAddress">The ip address.</param>
        /// <param name="dateCreated">The date created.</param>
        /// <param name="deviceForeignKey">The device foreign key.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static Device AddDevice( RockContext rockContext, string deviceName, string deviceDescription, int deviceTypeId, int? locationId,
                string ipAddress, DateTime? dateCreated, string deviceForeignKey, bool instantSave = true, int? creatorPersonAliasId = null )
        {
            var newDevice = new Device
            {
                Name = deviceName,
                Description = deviceDescription,
                DeviceTypeValueId = deviceTypeId,
                LocationId = locationId,
                IPAddress = ipAddress,
                ForeignKey = deviceForeignKey,
                ForeignId = deviceForeignKey.AsIntegerOrNull(),
                CreatedDateTime = dateCreated,
                CreatedByPersonAliasId = creatorPersonAliasId
            };

            if ( instantSave )
            {
                rockContext = rockContext ?? new RockContext();
                rockContext.Devices.Add( newDevice );
                rockContext.SaveChanges( DisableAuditing );
            }

            return newDevice;
        }

        /// <summary>
        /// Adds the account.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="fundName">Name of the fund.</param>
        /// <param name="fundDescription">The fund description.</param>
        /// <param name="accountGL">The account gl.</param>
        /// <param name="fundCampusId">The fund campus identifier.</param>
        /// <param name="parentAccountId">The parent account identifier.</param>
        /// <param name="isActive">The is active.</param>
        /// <param name="dateCreated">The date created.</param>
        /// <param name="accountForeignKey">The account foreign key.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static FinancialAccount AddFinancialAccount( RockContext rockContext, string fundName, string fundDescription, string accountGL, int? fundCampusId,
            int? parentAccountId, bool? isActive, DateTime? dateCreated, string accountForeignKey, bool instantSave = true, int? creatorPersonAliasId = null )
        {
            rockContext = rockContext ?? new RockContext();

            var account = new FinancialAccount
            {
                Name = fundName.Truncate( 50 ),
                Description = fundDescription,
                PublicName = fundName.Truncate( 50 ),
                GlCode = accountGL,
                IsTaxDeductible = true,
                IsActive = isActive ?? true,
                IsPublic = false,
                Order = 0,
                CampusId = fundCampusId,
                ParentAccountId = parentAccountId,
                CreatedDateTime = dateCreated,
                CreatedByPersonAliasId = creatorPersonAliasId,
                ForeignKey = accountForeignKey,
                ForeignId = accountForeignKey.AsIntegerOrNull()
            };

            if ( instantSave )
            {
                rockContext.FinancialAccounts.Add( account );
                rockContext.SaveChanges( DisableAuditing );
            }

            return account;
        }

        /// <summary>
        /// Adds the named location.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="parentLocationId">The parent location identifier.</param>
        /// <param name="locationName">Name of the location.</param>
        /// <param name="locationActive">if set to <c>true</c> [location active].</param>
        /// <param name="locationCapacity">The location capacity.</param>
        /// <param name="dateCreated">The date created.</param>
        /// <param name="locationForeignKey">The location foreign key.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static Location AddNamedLocation( RockContext rockContext, int? parentLocationId, string locationName, bool? locationActive,
                int? locationCapacity, DateTime? dateCreated, string locationForeignKey, bool instantSave = true, int? creatorPersonAliasId = null )
        {
            var newLocation = new Location
            {
                Name = locationName,
                IsActive = locationActive ?? true,
                ParentLocationId = parentLocationId,
                FirmRoomThreshold = locationCapacity,
                ForeignKey = locationForeignKey,
                ForeignId = locationForeignKey.AsIntegerOrNull(),
                CreatedDateTime = dateCreated,
                CreatedByPersonAliasId = creatorPersonAliasId
            };

            if ( instantSave )
            {
                rockContext = rockContext ?? new RockContext();
                rockContext.Locations.Add( newLocation );
                rockContext.SaveChanges( DisableAuditing );
            }

            return newLocation;
        }

        /// <summary>
        /// Adds the group.
        /// </summary>
        /// <param name="rockContext">todo: describe rockContext parameter on AddGroup</param>
        /// <param name="groupTypeId">The group type identifier.</param>
        /// <param name="parentGroupId">The parent group identifier.</param>
        /// <param name="groupName">Name of the group.</param>
        /// <param name="groupActive">if set to <c>true</c> [group active].</param>
        /// <param name="campusId">The campus identifier.</param>
        /// <param name="dateCreated">todo: describe dateCreated parameter on AddGroup</param>
        /// <param name="groupForeignKey">The group foreign key.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="creatorPersonAliasId">todo: describe creatorPersonAliasId parameter on AddGroup</param>
        /// <param name="scheduleId">The schedule identifier.</param>
        /// <returns></returns>
        public static Group AddGroup( RockContext rockContext, int? groupTypeId, int? parentGroupId, string groupName, bool? groupActive,
            int? campusId, DateTime? dateCreated, string groupForeignKey, bool instantSave = true, int? creatorPersonAliasId = null, int? scheduleId = null )
        {
            var newGroup = new Group
            {
                IsSystem = false,
                IsPublic = false,
                IsSecurityRole = false,
                Name = groupName,
                Description = $"{groupName} imported {RockDateTime.Now}",
                CampusId = campusId,
                ParentGroupId = parentGroupId,
                IsActive = groupActive ?? true,
                ScheduleId = scheduleId,
                CreatedDateTime = dateCreated,
                GroupTypeId = groupTypeId ?? GeneralGroupTypeId,
                ForeignKey = groupForeignKey,
                ForeignId = groupForeignKey.AsIntegerOrNull(),
                CreatedByPersonAliasId = creatorPersonAliasId
            };

            if ( instantSave )
            {
                rockContext = rockContext ?? new RockContext();
                rockContext.Groups.Add( newGroup );
                rockContext.SaveChanges( DisableAuditing );
            }

            return newGroup;
        }

        /// <summary>
        /// Adds a new group type to the Rock system.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="typeName">Name of the type.</param>
        /// <param name="typeDescription">The type description.</param>
        /// <param name="typeParentId">The type parent identifier.</param>
        /// <param name="inheritedGroupTypeId">The inherited group type identifier.</param>
        /// <param name="typePurposeValueId">The type purpose value identifier.</param>
        /// <param name="typeTakesAttendance">if set to <c>true</c> [type takes attendance].</param>
        /// <param name="attendanceIsWeekendService">if set to <c>true</c> [attendance is weekend service].</param>
        /// <param name="showInGroupList">if set to <c>true</c> [show in group list].</param>
        /// <param name="showInNavigation">if set to <c>true</c> [show in navigation].</param>
        /// <param name="typeOrder">todo: describe typeOrder parameter on AddGroupType</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="dateCreated">The date created.</param>
        /// <param name="typeForeignKey">todo: describe typeForeignKey parameter on AddGroupType</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static GroupType AddGroupType( RockContext rockContext, string typeName, string typeDescription, int? typeParentId, int? inheritedGroupTypeId,
                    int? typePurposeValueId, bool typeTakesAttendance, bool attendanceIsWeekendService, bool showInGroupList, bool showInNavigation,
                    int typeOrder = 0, bool instantSave = true, DateTime? dateCreated = null, string typeForeignKey = null, int? creatorPersonAliasId = null )
        {
            rockContext = rockContext ?? new RockContext();

            var newGroupType = new GroupType
            {
                // set required properties (terms set by default)
                IsSystem = false,
                Name = typeName,
                Description = typeDescription,
                InheritedGroupTypeId = inheritedGroupTypeId,
                GroupTypePurposeValueId = typePurposeValueId,
                TakesAttendance = typeTakesAttendance,
                AttendanceCountsAsWeekendService = attendanceIsWeekendService,
                ShowInGroupList = showInGroupList,
                ShowInNavigation = showInNavigation,
                Order = typeOrder,
                CreatedDateTime = dateCreated,
                ModifiedDateTime = RockDateTime.Now,
                CreatedByPersonAliasId = creatorPersonAliasId,
                ModifiedByPersonAliasId = creatorPersonAliasId,
                ForeignKey = typeForeignKey,
                ForeignId = typeForeignKey.AsIntegerOrNull()
            };

            // set meeting location
            newGroupType.LocationTypes.Add( new GroupTypeLocationType { LocationTypeValueId = GroupTypeMeetingLocationId } );

            // add default role of member
            newGroupType.Roles.Add( new GroupTypeRole { Guid = Guid.NewGuid(), Name = "Member" } );

            // add parent
            if ( typeParentId.HasValue )
            {
                var parentType = new GroupTypeService( rockContext ).Get( (int)typeParentId );
                if ( parentType != null )
                {
                    newGroupType.ParentGroupTypes.Add( parentType );
                }
            }

            // allow children of the same type
            newGroupType.ChildGroupTypes.Add( newGroupType );

            if ( instantSave )
            {
                rockContext.GroupTypes.Add( newGroupType );
                rockContext.SaveChanges();

                newGroupType.DefaultGroupRole = newGroupType.Roles.FirstOrDefault();
                rockContext.SaveChanges();
            }

            return newGroupType;
        }

        /// <summary>
        /// Adds the named schedule.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="scheduleName">Name of the schedule.</param>
        /// <param name="iCalendarContent">Content of the i calendar.</param>
        /// <param name="dayOfWeek">The day of week.</param>
        /// <param name="timeOfDay">The time of day.</param>
        /// <param name="dateCreated">The date created.</param>
        /// <param name="scheduleForeignKey">The schedule foreign key.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static Schedule AddNamedSchedule( RockContext rockContext, string scheduleName, string iCalendarContent, DayOfWeek? dayOfWeek,
            DateTime? timeOfDay, DateTime? dateCreated, string scheduleForeignKey, bool instantSave = true, int? creatorPersonAliasId = null )
        {
            var newSchedule = new Schedule
            {
                Name = scheduleName,
                Description = $"{scheduleName} imported {RockDateTime.Now}",
                iCalendarContent = iCalendarContent,
                WeeklyDayOfWeek = dayOfWeek,
                WeeklyTimeOfDay = timeOfDay.HasValue ? ( (DateTime)timeOfDay ).TimeOfDay as TimeSpan? : null,
                CreatedDateTime = dateCreated,
                ForeignKey = scheduleForeignKey,
                ForeignId = scheduleForeignKey.AsIntegerOrNull(),
                CreatedByPersonAliasId = creatorPersonAliasId
            };

            if ( instantSave )
            {
                rockContext = rockContext ?? new RockContext();
                rockContext.Schedules.Add( newSchedule );
                rockContext.SaveChanges( DisableAuditing );
            }

            return newSchedule;
        }

        /// <summary>
        /// Gets the group type role.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="groupTypeId">The group type identifier.</param>
        /// <param name="roleName">Name of the role.</param>
        /// <param name="roleDescription">The role description.</param>
        /// <param name="isLeader">if set to <c>true</c> [is leader].</param>
        /// <param name="roleOrder">The role order.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="dateCreated">The date created.</param>
        /// <param name="roleForeignKey">The role foreign key.</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static GroupTypeRole GetGroupTypeRole( RockContext rockContext, int groupTypeId, string roleName, string roleDescription, bool isLeader = false,
                    int roleOrder = 0, bool instantSave = true, DateTime? dateCreated = null, string roleForeignKey = null, int? creatorPersonAliasId = null )
        {
            rockContext = rockContext ?? new RockContext();

            var queryable = new GroupTypeRoleService( rockContext ).Queryable().Where( r => r.GroupTypeId == groupTypeId );

            if ( string.IsNullOrWhiteSpace( roleName ) )
            {
                return queryable.OrderByDescending( r => r.Id == r.GroupType.DefaultGroupRoleId ).ThenBy( r => r.Order ).FirstOrDefault();
            }

            // match grouptype role of
            var groupTypeRole = queryable.FirstOrDefault( r => r.Name.Equals( roleName ) );
            if ( groupTypeRole == null )
            {
                groupTypeRole = new GroupTypeRole
                {
                    IsSystem = false,
                    GroupTypeId = groupTypeId,
                    Name = roleName,
                    Description = !string.IsNullOrWhiteSpace( roleDescription )
                        ? roleDescription
                        : $"{roleName} imported {RockDateTime.Now}",
                    Order = roleOrder,
                    IsLeader = isLeader,
                    CanView = isLeader,
                    CanEdit = isLeader, // leaders should be able to edit their own groups
                    ForeignKey = roleForeignKey,
                    ForeignId = roleForeignKey.AsIntegerOrNull(),
                    CreatedDateTime = dateCreated,
                    CreatedByPersonAliasId = creatorPersonAliasId
                };

                if ( instantSave )
                {
                    rockContext.GroupTypeRoles.Add( groupTypeRole );
                    rockContext.SaveChanges();
                }
            }

            return groupTypeRole;
        }

        /// <summary>
        /// Finds an existing attribute category or adds a new one if it doesn't exist.
        /// </summary>
        /// <param name="rockContext">The RockContext to use when searching or creating categories</param>
        /// <param name="categoryName">The name of the category to search for, whitespace and capitalization is ignored when searching</param>
        /// <param name="findOnly">todo: describe findOnly parameter on GetAttributeCategory</param>
        /// <param name="entityTypeId">todo: describe entityTypeId parameter on GetAttributeCategory</param>
        /// <param name="importPeronAliasId">todo: describe importPeronAliasId parameter on GetAttributeCategory</param>
        /// <returns>A reference to the Category object, attached to the lookupContext</returns>
        public static Category GetAttributeCategory( RockContext rockContext, string categoryName, bool findOnly = false, int entityTypeId = -1, int? importPeronAliasId = null )
        {
            var categoryService = new CategoryService( rockContext );
            Category category;
            if ( entityTypeId == -1 )
            {
                entityTypeId = PersonEntityTypeId;
            }

            //
            // Try to find an existing category.
            //
            category = categoryService
                .GetByEntityTypeId( AttributeEntityTypeId )
                .FirstOrDefault( c => c.EntityTypeQualifierValue == entityTypeId.ToString() && c.Name.ToUpper() == categoryName.ToUpper() );

            //
            // If not found, create one.
            //
            if ( category == null && !findOnly )
            {
                category = new Category
                {
                    IsSystem = false,
                    EntityTypeId = AttributeEntityTypeId,
                    EntityTypeQualifierColumn = "EntityTypeId",
                    EntityTypeQualifierValue = entityTypeId.ToString(),
                    Name = categoryName,
                    Order = 0,
                    CreatedByPersonAliasId = importPeronAliasId,
                    ModifiedByPersonAliasId = importPeronAliasId
                };

                rockContext.Categories.Add( category );
                rockContext.SaveChanges( DisableAuditing );
            }

            return category;
        }

        /// <summary>
        /// Tries to find an existing Attribute with the given name for an Entity Type. If the attribute is found then ensure
        /// it is a member of the specified category. If attribute must be added to the category then a save is
        /// performed instantly.
        /// </summary>
        /// <param name="rockContext">The RockContext object to work in for database access</param>
        /// <param name="categoryName">Name of the Category to assign this Attribute to, pass null for none</param>
        /// <param name="attributeName">Name of the attribute to find or create</param>
        /// <param name="entityTypeId">The Id of the Entity Type for the attribute</param>
        /// <param name="attributeForeignKey">The Foreign Key of the attribute</param>
        /// <returns>Attribute object of the found Entity Attribute</returns>
        public static Attribute FindEntityAttribute( RockContext rockContext, string categoryName, string attributeName, int entityTypeId, string attributeForeignKey = null )
        {
            var attributeService = new AttributeService( rockContext );
            var categoryService = new CategoryService( rockContext );
            Attribute attribute = null;

            if ( !string.IsNullOrWhiteSpace( attributeForeignKey ) )
            {
                attribute = attributeService.GetByEntityTypeId( entityTypeId ).Include( "Categories" )
                    .FirstOrDefault( a => a.ForeignKey == attributeForeignKey );
            }
            else
            {
                attribute = attributeService.GetByEntityTypeId( entityTypeId ).Include( "Categories" )
                    .FirstOrDefault( a =>
                        ( a.Name.Replace( " ", "" ).ToUpper() == attributeName.Replace( " ", "" ).ToUpper() || a.Key == attributeName )
                    &&
                        ( ( string.IsNullOrEmpty( categoryName ) ) || ( a.Categories.Count( c => c.Name.ToUpper() == categoryName.ToUpper() ) > 0 ) )
                    );
            }

            return attribute;
        }

        /// <summary>
        /// Adds the communication.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="mediumEntityTypeId">The medium entity type identifier.</param>
        /// <param name="itemCaption">The item caption.</param>
        /// <param name="communicationText">The communication text.</param>
        /// <param name="isBulkEmail">if set to <c>true</c> [is bulk email].</param>
        /// <param name="itemStatus">The item status.</param>
        /// <param name="recipients">The recipients.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="dateCreated">The date created.</param>
        /// <param name="itemForeignKey">The item foreign key.</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static Communication AddCommunication( RockContext rockContext, int mediumEntityTypeId, string itemCaption, string communicationText, bool isBulkEmail, CommunicationStatus itemStatus,
           List<CommunicationRecipient> recipients = null, bool instantSave = true, DateTime? dateCreated = null, string itemForeignKey = null, int? creatorPersonAliasId = null )
        {
            var mediumData = new Dictionary<string, string>();
            mediumData.Add( "HtmlMessage", communicationText );
            mediumData.Add( "FromName", string.Empty );
            mediumData.Add( "FromAddress", string.Empty );
            mediumData.Add( "DefaultPlainText", communicationText );

            var communication = new Communication
            {
                Subject = itemCaption,
                IsBulkCommunication = isBulkEmail,
                Status = itemStatus,
                MediumData = mediumData,
                MediumEntityTypeId = mediumEntityTypeId,
                CreatedDateTime = dateCreated,
                CreatedByPersonAliasId = creatorPersonAliasId,
                SenderPersonAliasId = creatorPersonAliasId,
                ReviewerPersonAliasId = creatorPersonAliasId,
                ForeignKey = itemForeignKey,
                ForeignId = itemForeignKey.AsIntegerOrNull(),
                Recipients = recipients
            };

            if ( instantSave )
            {
                rockContext = rockContext ?? new RockContext();
                rockContext.Communications.Add( communication );
                rockContext.SaveChanges();
            }

            return communication;
        }

        /// <summary>
        /// Adds the entity note and the note type if it doesn't exist.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="noteEntityTypeId">The note entity type identifier.</param>
        /// <param name="noteEntityId">The note entity identifier.</param>
        /// <param name="noteCaption">The note caption.</param>
        /// <param name="noteText">The note text.</param>
        /// <param name="isAlert">if set to <c>true</c> [is alert].</param>
        /// <param name="isPrivate">if set to <c>true</c> [is private].</param>
        /// <param name="noteTypeName">Name of the note type.</param>
        /// <param name="noteTypeId">The note type identifier.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="noteCreated">todo: describe noteCreated parameter on AddEntityNote</param>
        /// <param name="noteForeignKey">todo: describe noteForeignKey parameter on AddEntityNote</param>
        /// <param name="creatorPersonAliasId">The import person alias identifier.</param>
        /// <returns></returns>
        public static Note AddEntityNote( RockContext rockContext, int noteEntityTypeId, int noteEntityId, string noteCaption, string noteText, bool isAlert, bool isPrivate,
            string noteTypeName, int? noteTypeId = null, bool instantSave = true, DateTime? noteCreated = null, string noteForeignKey = null, int? creatorPersonAliasId = null )
        {
            // ensure we have enough information to create a note/notetype
            if ( noteEntityTypeId <= 0 || noteEntityId <= 0 || ( noteTypeId == null && string.IsNullOrEmpty( noteTypeName ) ) )
            {
                return null;
            }

            rockContext = rockContext ?? new RockContext();

            // get the note type id (create if it doesn't exist)
            noteTypeId = noteTypeId ?? new NoteTypeService( rockContext ).Get( noteEntityTypeId, noteTypeName ).Id;

            // replace special HTML characters that destroy Rock
            noteText = Regex.Replace( noteText, @"\t|\&nbsp;", " " );
            noteText = noteText.Replace( "&lt;", "<" );
            noteText = noteText.Replace( "&gt;", ">" );
            noteText = noteText.Replace( "&amp;", "&" );
            noteText = noteText.Replace( "&quot;", @"""" );
            noteText = noteText.Replace( "&#45;", "-" );
            noteText = noteText.Replace( "&#x0D", string.Empty );

            // create the note on this person
            var note = new Note
            {
                IsSystem = false,
                IsAlert = isAlert,
                IsPrivateNote = isPrivate,
                NoteTypeId = (int)noteTypeId,
                EntityId = noteEntityId,
                Caption = noteCaption,
                Text = noteText.Trim(),
                ForeignKey = noteForeignKey,
                ForeignId = noteForeignKey.AsIntegerOrNull(),
                CreatedDateTime = noteCreated,
                CreatedByPersonAliasId = creatorPersonAliasId
            };

            if ( instantSave )
            {
                rockContext.Notes.Add( note );
                rockContext.SaveChanges( DisableAuditing );
            }

            return note;
        }

        /// <summary>
        /// Add an Attribute with the given category and attribute name for supplied Entity Type.
        /// </summary>
        /// <param name="rockContext">The RockContext object to work in for database access</param>
        /// <param name="entityTypeId">The Id of the Entity Type this attribute is for</param>
        /// <param name="entityTypeQualifierName">If a qualifer name is needed supply, otherwise use string.Empty</param>
        /// <param name="entityTypeQualifierValue">If a qualifier value is needed supply, othewise use string.Empty</param>
        /// <param name="foreignKey">String matching an existing ForeignKey, otherwise use null</param>
        /// <param name="categoryName">Name of the Category to assign this Attribute to, pass null for none</param>
        /// <param name="attributeName">Name of the attribute to find or create</param>
        /// <param name="key">Attribute key to use, if not provided the attributeName without any whitespace is used</param>
        /// <param name="fieldTypeId">todo: describe fieldTypeId parameter on AddEntityAttribute</param>
        /// <param name="instantSave">If true always save changes before returning, otherwise only save when absolutely necessary</param>
        /// <param name="definedTypeForeignId">Used to determine if a Defined Type should be created from scratch or link to an existing, imported Defined Type</param>
        /// <param name="definedTypeForeignKey">Used to determine if a Defined Type should be created from scratch or link to an existing, imported Defined Type</param>
        /// <param name="importPersonAliasId">todo: describe importPersonAliasId parameter on AddEntityAttribute</param>
        /// <returns>
        /// Newly created Entity Attribute
        /// </returns>
        public static Attribute AddEntityAttribute( RockContext rockContext, int entityTypeId, string entityTypeQualifierName, string entityTypeQualifierValue, string foreignKey,
            string categoryName, string attributeName, string key, int fieldTypeId, bool instantSave = true, int? definedTypeForeignId = null, string definedTypeForeignKey = null, int? importPersonAliasId = null )
        {
            rockContext = rockContext ?? new RockContext();
            var attributeService = new AttributeService( rockContext );
            AttributeQualifier attributeQualifier;
            Attribute attribute;
            var newAttribute = true;

            //
            // Get a reference to the existing attribute if there is one.
            //
            attribute = FindEntityAttribute( rockContext, categoryName, attributeName, entityTypeId, foreignKey );
            if ( attribute != null )
            {
                newAttribute = false;
            }

            //
            // If no attribute has been found, create a new one.
            if ( attribute == null && fieldTypeId != -1 )
            {
                attribute = new Attribute
                {
                    Name = attributeName,
                    FieldTypeId = fieldTypeId,
                    EntityTypeId = entityTypeId,
                    EntityTypeQualifierColumn = entityTypeQualifierName,
                    EntityTypeQualifierValue = entityTypeQualifierValue,
                    DefaultValue = string.Empty,
                    IsMultiValue = false,
                    IsGridColumn = false,
                    IsRequired = false,
                    Order = 0,
                    CreatedByPersonAliasId = importPersonAliasId,
                    ModifiedByPersonAliasId = importPersonAliasId,
                    ForeignId = foreignKey.AsIntegerOrNull(),
                    ForeignKey = foreignKey
                };

                if ( !string.IsNullOrEmpty( key ) )
                {
                    attribute.Key = key;
                }
                else
                {
                    if ( !string.IsNullOrEmpty( categoryName ) )
                    {
                        attribute.Key = $"{categoryName.RemoveWhitespace()}_{attributeName.RemoveWhitespace()}";
                    }
                    else
                    {
                        attribute.Key = attributeName.RemoveWhitespace();
                    }
                }

                var attributeQualifiers = new List<AttributeQualifier>();

                // Do specific value type settings.
                if ( fieldTypeId == DateFieldTypeId )
                {
                    attribute.Description = attributeName + " Date created by import";

                    // Add date attribute qualifiers
                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "format",
                        Value = "",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );

                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "displaydiff",
                        Value = "false",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );

                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "displaycurrentoption",
                        Value = "false",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );
                }
                else if ( fieldTypeId == BooleanFieldTypeId )
                {
                    attribute.Description = attributeName + " Boolean created by import";

                    //
                    // Add boolean attribute qualifiers
                    //
                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "truetext",
                        Value = "Yes",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );

                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "falsetext",
                        Value = "No",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );
                }
                else if ( fieldTypeId == DefinedValueFieldTypeId )
                {
                    var typeService = new DefinedTypeService( rockContext );
                    DefinedType definedType = null;

                    // Check for the defined type by the original name only, id, or key.
                    var definedTypeExists = typeService.Queryable().Any( t => t.Name.Equals( attributeName + " Defined Type" )
                        || ( definedTypeForeignId.HasValue && t.ForeignId.HasValue && t.ForeignId == definedTypeForeignId )
                        || ( !( definedTypeForeignKey == null || definedTypeForeignKey.Trim() == string.Empty ) && !( t.ForeignKey == null || t.ForeignKey.Trim() == string.Empty ) && t.ForeignKey.Equals( definedTypeForeignKey, StringComparison.CurrentCultureIgnoreCase ) )
                        );

                    if ( !definedTypeExists )
                    {
                        definedType = new DefinedType
                        {
                            IsSystem = false,
                            Order = 0,
                            FieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.TEXT, rockContext ).Id,
                            Name = attributeName.Left( 87 ) + " Defined Type",
                            Description = attributeName + " Defined Type created by import",
                            ForeignId = definedTypeForeignId,
                            ForeignKey = definedTypeForeignKey
                        };

                        typeService.Add( definedType );
                        rockContext.SaveChanges();
                    }
                    else
                    {
                        definedType = typeService.Queryable().FirstOrDefault( t => t.Name.Equals( attributeName + " Defined Type" ) || ( t.ForeignId != null && t.ForeignId == definedTypeForeignId ) || ( !( t.ForeignKey == null || t.ForeignKey.Trim() == string.Empty ) && t.ForeignKey == definedTypeForeignKey ) );
                    }

                    attribute.Description = attributeName + " Defined Type created by import";

                    //
                    // Add defined value attribute qualifiers
                    //
                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "definedtype",
                        Value = definedType.Id.ToString(),
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );

                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "allowmultiple",
                        Value = "False",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );

                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "displaydescription",
                        Value = "false",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );
                }
                else if ( fieldTypeId == SingleSelectFieldTypeId )
                {
                    attribute.Description = attributeName + " Single Select created by import";
                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "values",
                        Value = "Pass,Fail",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );

                    attributeQualifier = new AttributeQualifier
                    {
                        Key = "fieldtype",
                        Value = "ddl",
                        IsSystem = false
                    };

                    attribute.AttributeQualifiers.Add( attributeQualifier );
                }
                else
                {
                    attribute.Description = attributeName + " created by import";
                }
            }

            if ( instantSave )
            {
                if ( newAttribute )
                {
                    rockContext.Attributes.Add( attribute );
                }
                rockContext.SaveChanges( DisableAuditing );
            }

            return attribute;
        }

        /// <summary>
        /// Add, or update, an entity's attribute value to the specified value and optionally create the history information.
        /// This method does not save changes automatically. You must call SaveChanges() on your context when you are
        /// ready to save all attribute values that have been added.
        /// </summary>
        /// <param name="rockContext">The RockContext object to work in for database access</param>
        /// <param name="attribute">The attribute of the value to set</param>
        /// <param name="entity">The Entity for which the attribute is being saved</param>
        /// <param name="value">The string-value to set the attribute to, must be parseable into the target type</param>
        /// <param name="changes">List to place any changes string into, or null. If null and instantSave is true then the History entry is saved instantly</param>
        /// <param name="csv">Bool to indicate this call was made via CSV maps. Important for how the save is processed.</param>
        /// <returns>true if the attribute value was successfuly coerced into the target type</returns>
        public static bool AddEntityAttributeValue( RockContext rockContext, Attribute attribute, IHasAttributes entity, string value, List<string> changes = null, bool csv = false )
        {
            rockContext = rockContext ?? new RockContext();
            string newValue = null;

            //
            // Determine the field type and coerce the value into that type.
            //
            if ( attribute.FieldTypeId == DateFieldTypeId )
            {
                var dateValue = ParseDateOrDefault( value, null );
                if ( dateValue != null && dateValue != DefaultDateTime && dateValue != DefaultSQLDateTime )
                {
                    newValue = ( (DateTime)dateValue ).ToString( "s" );
                }
            }
            else if ( attribute.FieldTypeId == BooleanFieldTypeId )
            {
                var boolValue = ParseBoolOrDefault( value, null );
                if ( boolValue != null )
                {
                    newValue = ( (bool)boolValue ).ToString();
                }
            }
            else if ( attribute.FieldTypeId == DefinedValueFieldTypeId )
            {
                Guid definedValueGuid;
                int definedTypeId;

                definedTypeId = int.Parse( attribute.AttributeQualifiers.FirstOrDefault( aq => aq.Key == "definedtype" ).Value );
                var attributeValueTypes = DefinedTypeCache.Read( definedTypeId, rockContext );

                //
                // Add the defined value if it doesn't exist.
                //
                var attributeExists = attributeValueTypes.DefinedValues.Any( a => a.Value.Equals( value ) );
                if ( !attributeExists )
                {
                    var newDefinedValue = new DefinedValue
                    {
                        DefinedTypeId = attributeValueTypes.Id,
                        Value = value,
                        Order = 0
                    };

                    DefinedTypeCache.Flush( attributeValueTypes.Id );

                    rockContext.DefinedValues.Add( newDefinedValue );
                    rockContext.SaveChanges( DisableAuditing );

                    definedValueGuid = newDefinedValue.Guid;
                }
                else
                {
                    definedValueGuid = attributeValueTypes.DefinedValues.FirstOrDefault( a => a.Value.Equals( value ) ).Guid;
                }

                newValue = definedValueGuid.ToString().ToUpper();
            }
            else if ( attribute.FieldTypeId == EncryptedTextFieldTypeId )
            {
                newValue = Encryption.EncryptString( value );
            }
            else
            {
                newValue = value;
            }

            // set the value on the entity
            if ( !string.IsNullOrWhiteSpace( newValue ) )
            {
                if ( entity.Id > 0 && csv )
                {
                    AttributeValue attributeValue = null;
                    var attributeValueService = new AttributeValueService( rockContext );

                    attributeValue = rockContext.AttributeValues.Local.AsQueryable().FirstOrDefault( av => av.AttributeId == attribute.Id && av.EntityId == entity.Id );
                    if ( attributeValue == null )
                    {
                        attributeValue = attributeValueService.GetByAttributeIdAndEntityId( attribute.Id, entity.Id );
                    }

                    if ( attributeValue == null )
                    {
                        attributeValue = new AttributeValue
                        {
                            EntityId = entity.Id,
                            AttributeId = attribute.Id
                        };

                        attributeValueService.Add( attributeValue );
                    }
                    var originalValue = attributeValue.Value;
                    if ( originalValue != newValue )
                    {
                        attributeValue.Value = newValue;
                    }
                }
                else
                {
                    if ( entity.Attributes == null )
                    {
                        entity.LoadAttributes();
                    }

                    if ( !entity.Attributes.ContainsKey( attribute.Key ) )
                    {
                        entity.Attributes.Add( attribute.Key, AttributeCache.Read( attribute.Id, rockContext ) );
                    }

                    if ( !entity.AttributeValues.ContainsKey( attribute.Key ) )
                    {
                        entity.AttributeValues.Add( attribute.Key, new AttributeValueCache
                        {
                            AttributeId = attribute.Id,
                            Value = newValue
                        } );
                    }
                    else
                    {
                        var avc = entity.AttributeValues[attribute.Key];
                        avc.Value = newValue;
                    }
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds the user login.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="authProviderTypeId">The authentication provider type identifier.</param>
        /// <param name="personId">The person identifier.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="isConfirmed">The is confirmed.</param>
        /// <param name="instantSave">if set to <c>true</c> [instant save].</param>
        /// <param name="userCreated">The user created.</param>
        /// <param name="userForeignKey">The user foreign key.</param>
        /// <param name="creatorPersonAliasId">The creator person alias identifier.</param>
        /// <returns></returns>
        public static UserLogin AddUserLogin( RockContext rockContext, int? authProviderTypeId, int personId, string username, string password,
            bool? isConfirmed = true, bool instantSave = true, DateTime? userCreated = null, string userForeignKey = null, int? creatorPersonAliasId = null )
        {
            rockContext = rockContext ?? new RockContext();

            // Make sure we can create a valid userlogin
            if ( string.IsNullOrWhiteSpace( username ) || !authProviderTypeId.HasValue || rockContext.UserLogins.Any( u => u.UserName.Equals( username, StringComparison.CurrentCultureIgnoreCase ) ) )
            {
                return null;
            }

            var userLogin = new UserLogin
            {
                UserName = username,
                Password = password,
                EntityTypeId = authProviderTypeId,
                PersonId = personId,
                IsConfirmed = isConfirmed,
                ForeignKey = userForeignKey,
                ForeignId = userForeignKey.AsIntegerOrNull(),
                CreatedDateTime = userCreated,
                CreatedByPersonAliasId = creatorPersonAliasId
            };

            if ( instantSave )
            {
                rockContext.UserLogins.Add( userLogin );
                rockContext.SaveChanges( DisableAuditing );
            }

            return userLogin;
        }

        /// <summary>
        /// Add a new group role to the system.
        /// </summary>
        /// <param name="rockContext"></param>
        /// <param name="type">The GUID of the group type to add this role to.</param>
        /// <param name="value">The value of the new role.</param>
        public static void AddGroupRole( RockContext rockContext, string type, string value )
        {
            var groupTypeRoleService = new GroupTypeRoleService( rockContext );
            var groupTypeRole = new GroupTypeRole();
            var typeId = GroupTypeCache.Read( new Guid( type ), rockContext ).Id;

            groupTypeRole.GroupTypeId = typeId;
            groupTypeRole.IsSystem = false;

            var orders = groupTypeRoleService.Queryable()
                .Where( g => g.GroupTypeId == typeId )
                .Select( g => g.Order )
                .ToList();
            groupTypeRole.Order = orders.Any() ? orders.Max() + 1 : 0;

            groupTypeRole.Name = value;
            groupTypeRole.Description = "Imported with Excavator";

            GroupTypeCache.Flush( typeId );

            groupTypeRoleService.Add( groupTypeRole );
            rockContext.SaveChanges( DisableAuditing );
        }

        /// <summary>
        /// Gets the campus identifier.
        /// </summary>
        /// <param name="property">Name of the property.</param>
        /// <param name="includeCampusName">if set to <c>true</c> [include campus name].</param>
        /// <param name="direction">The direction, default is begins with.</param>
        /// <returns></returns>
        public static int? GetCampusId( string property, bool includeCampusName = true, SearchDirection direction = SearchDirection.Begins )
        {
            int? campusId = null;
            if ( !string.IsNullOrWhiteSpace( property ) )
            {
                var queryable = CampusList.AsQueryable();

                if ( direction == SearchDirection.Begins )
                {
                    queryable = queryable.Where( c => property.StartsWith( c.ShortCode, StringComparison.CurrentCultureIgnoreCase )
                        || ( includeCampusName && property.StartsWith( c.Name, StringComparison.CurrentCultureIgnoreCase ) ) );
                }
                else
                {
                    queryable = queryable.Where( c => property.EndsWith( c.ShortCode, StringComparison.CurrentCultureIgnoreCase )
                        || ( includeCampusName && property.EndsWith( c.Name, StringComparison.CurrentCultureIgnoreCase ) ) );
                }

                campusId = queryable.Select( c => (int?)c.Id ).FirstOrDefault();
            }

            return campusId;
        }

        /// <summary>
        /// Strips the prefix from the text value.
        /// </summary>
        /// <param name="textValue">The text value.</param>
        /// <param name="campusId">The campus identifier.</param>
        /// <returns></returns>
        public static string StripPrefix( string textValue, int? campusId )
        {
            var fixedValue = string.Empty;
            if ( !string.IsNullOrWhiteSpace( textValue ) && campusId.HasValue )
            {
                // Main Campus -> Main Campus
                // Main -> MAIN
                // Main-Baptized -> MAIN
                var campus = CampusList.FirstOrDefault( c => c.Id == campusId );
                textValue = textValue.StartsWith( campus.Name, StringComparison.CurrentCultureIgnoreCase ) ? textValue.Substring( campus.Name.Length ) : textValue;
                textValue = textValue.StartsWith( campus.ShortCode, StringComparison.CurrentCultureIgnoreCase ) ? textValue.Substring( campus.ShortCode.Length ) : textValue;
            }

            // strip the prefix including delimiters
            fixedValue = textValue.IndexOfAny( ValidDelimiters ) >= 0
                ? textValue.Substring( textValue.IndexOfAny( ValidDelimiters ) + 1 )
                : textValue;

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase( fixedValue.Trim() );
        }

        /// <summary>
        /// Strips the suffix from the text value.
        /// </summary>
        /// <param name="textValue">The text value.</param>
        /// <param name="campusId">The campus identifier.</param>
        /// <returns></returns>
        public static string StripSuffix( string textValue, int? campusId )
        {
            var fixedValue = string.Empty;
            if ( !string.IsNullOrWhiteSpace( textValue ) && campusId.HasValue )
            {
                var campus = CampusList.FirstOrDefault( c => c.Id == campusId );
                textValue = textValue.EndsWith( campus.Name, StringComparison.CurrentCultureIgnoreCase ) ? textValue.Substring( 0, textValue.IndexOf( campus.Name ) ) : textValue;
                textValue = textValue.EndsWith( campus.ShortCode, StringComparison.CurrentCultureIgnoreCase ) ? textValue.Substring( 0, textValue.IndexOf( campus.ShortCode ) ) : textValue;
            }

            // strip the suffix including delimiters
            fixedValue = textValue.IndexOfAny( ValidDelimiters ) >= 0
                ? textValue.Substring( 0, textValue.LastIndexOfAny( ValidDelimiters ) - 1 )
                : textValue;

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase( fixedValue.Trim() );
        }

        /// <summary>
        /// Adds a prayer request.
        /// </summary>
        /// <param name="rockContext">The RockContext object to work in for database access.</param>
        /// <param name="categoryName">The category of the prayer request.</param>
        /// <param name="requestText">The text of the prayer request.</param>
        /// <param name="requestDate">The request date of the prayer request.</param>
        /// <param name="foreignKey">The foreign key of the prayer request.</param>
        /// <param name="firstName">The first name for whom the request was submitted.</param>
        /// <param name="lastName">The last name for whom the request was submitted.</param>
        /// <param name="email">The email for whom the request was submitted.</param>
        /// <param name="expireDate">The date that the prayer request expires. Default: 14 days after request date.</param>
        /// <param name="allowComments">Flag to determine if the prayer request should allow comments. Default: <c>true</c>></param>
        /// <param name="isPublic">Flag to determine if the prayer request should be public. Default: <c>true</c></param>
        /// <param name="isApproved">Flag to determine if the prayer request is approved. Default: <c>true</c></param>
        /// <param name="approvedDate">Date the prayer request was approved. Default: <c>ImportDateTime</c></param>
        /// <param name="approvedById">Alias Id of who approved the prayer request. Default: <c>null</c></param>
        /// <param name="createdById">Alias Id of who entered the prayer request. Default: <c>null</c></param>
        /// <param name="requestedById">Alias Id of who submitted the prayer request. Default: <c>null</c></param>
        /// <param name="instantSave">Flag to determine if the prayer request should be saved to the rockContext prior to return. Default: <c>true</c></param>
        /// <returns>A newly created prayer request.</returns>
        public static PrayerRequest AddPrayerRequest( RockContext rockContext, string categoryName, string requestText, string requestDate, string foreignKey, string firstName,
            string lastName = "", string email = "", string expireDate = "", bool? allowComments = true, bool? isPublic = true, bool? isApproved = true, string approvedDate = "",
            int? approvedByAliasId = null, int? createdByAliasId = null, int? requestedByAliasId = null, string answerText = "", bool instantSave = true )
        {
            PrayerRequest prayerRequest = null;
            if ( !string.IsNullOrWhiteSpace( requestText ) )
            {
                rockContext = rockContext ?? new RockContext();

                if ( !string.IsNullOrWhiteSpace( foreignKey ) )
                {
                    prayerRequest = rockContext.PrayerRequests.AsQueryable().FirstOrDefault( p => p.ForeignKey.ToLower().Equals( foreignKey.ToLower() ) );
                }

                if ( prayerRequest == null )
                {
                    var prayerRequestDate = (DateTime)ParseDateOrDefault( requestDate, ExcavatorComponent.ImportDateTime );

                    prayerRequest = new PrayerRequest
                    {
                        FirstName = string.IsNullOrWhiteSpace( firstName ) ? "-" : firstName,
                        LastName = lastName,
                        Email = email,
                        Text = requestText,
                        EnteredDateTime = prayerRequestDate,
                        ExpirationDate = ParseDateOrDefault( expireDate, prayerRequestDate.AddDays( 14 ) ),
                        AllowComments = allowComments,
                        IsPublic = isPublic,
                        IsApproved = isApproved,
                        ApprovedOnDateTime = (bool)isApproved ? ParseDateOrDefault( approvedDate, ExcavatorComponent.ImportDateTime ) : null,
                        ApprovedByPersonAliasId = approvedByAliasId,
                        CreatedByPersonAliasId = createdByAliasId,
                        RequestedByPersonAliasId = requestedByAliasId,
                        ForeignKey = foreignKey,
                        ForeignId = foreignKey.AsType<int?>(),
                        Answer = answerText
                    };

                    if ( !string.IsNullOrWhiteSpace( categoryName ) )
                    {
                        //
                        // Try to find an existing category.
                        //
                        var category = rockContext.Categories.AsNoTracking().FirstOrDefault( c => c.EntityTypeId.Equals( prayerRequest.TypeId ) && c.Name.ToUpper().Equals( categoryName.ToUpper() ) );

                        //
                        // If not found, create one.
                        //
                        if ( category == null )
                        {
                            category = new Category
                            {
                                IsSystem = false,
                                EntityTypeId = prayerRequest.TypeId,
                                Name = categoryName,
                                Order = 0,
                                ParentCategoryId = AllChurchCategoryId
                            };

                            rockContext.Categories.Add( category );
                            rockContext.SaveChanges( DisableAuditing );
                        }

                        prayerRequest.CategoryId = category.Id;
                    }

                    if ( instantSave )
                    {
                        rockContext.PrayerRequests.Add( prayerRequest );
                        rockContext.SaveChanges( DisableAuditing );
                    }
                }
            }
            return prayerRequest;
        }

        /// <summary>
        /// Adds a person previous last name.
        /// </summary>
        /// <param name="rockContext">The RockContext object to work in for database access.</param>
        /// <param name="personPreviousName">The person previous last name to be added.</param>
        /// <param name="personAliasId">The person alias for the person previous name.</param>
        /// <param name="fk">The foreign key for the person previous name.</param>
        /// <param name="instantSave">Flag to determine if the prayer request should be saved to the rockContext prior to return. Default: <c>true</c></param>
        /// <returns>A newly created prayer request.</returns>
        public static PersonPreviousName AddPersonPreviousName( RockContext rockContext, string personPreviousName, int personAliasId, string fk = "", bool instantSave = true )
        {
            PersonPreviousName previousName = null;
            if ( !string.IsNullOrWhiteSpace( personPreviousName ) )
            {
                rockContext = rockContext ?? new RockContext();

                previousName = new PersonPreviousName
                {
                    LastName = personPreviousName,
                    PersonAliasId = personAliasId,
                    ForeignKey = fk,
                    ForeignGuid = fk.AsGuidOrNull(),
                    ForeignId = fk.AsIntegerOrNull()
                };

                if ( instantSave )
                {
                    rockContext.PersonPreviousNames.Add( previousName );
                    rockContext.SaveChanges( DisableAuditing );
                }
            }
            return previousName;
        }

        /// <summary>
        /// Add a new financial gateway to the Rock system using the Test Gateway entity.
        /// </summary>
        /// <param name="rockContext">The rock context.</param>
        /// <param name="gatewayName">The name of the gateway to be added to Rock.</param>
        /// <returns>A newly created financial gateway.</returns>
        public static FinancialGateway AddFinancialGateway( RockContext rockContext, string gatewayName, bool instantSave = true )
        {
            var gateway = new FinancialGateway();
            gateway.Name = string.IsNullOrWhiteSpace( gatewayName ) ? "Imported Gateway" : gatewayName;
            gateway.EntityTypeId = TestGatewayTypeId;
            gateway.BatchTimeOffsetTicks = 0;
            gateway.IsActive = true;

            if ( instantSave )
            {
                var gatewayService = new Rock.Model.FinancialGatewayService( rockContext );
                gatewayService.Add( gateway );

                rockContext.SaveChanges();
            }
            return gateway;
        }
    }
}
