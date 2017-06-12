using System;
using System.Collections.Generic;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.Utility
{
    public static class CachedTypes
    {
        // Global flag to turn off Rock processing

        public static bool DisableAuditing = true;

        // Common Delimiters

        public static char[] ValidDelimiters = new char[] { '*', '-', '|' };

        // Default Datetimes

        public static DateTime DefaultDateTime = new DateTime();
        public static DateTime DefaultSQLDateTime = new DateTime( 1900, 1, 1 );

        // Field Types

        public static int BooleanFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.BOOLEAN.AsGuid() ).Id;
        public static int CampusFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.CAMPUS.AsGuid() ).Id;
        public static int SingleSelectFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.SINGLE_SELECT.AsGuid() ).Id;
        public static int DateFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.DATE.AsGuid() ).Id;
        public static int DefinedValueFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.DEFINED_VALUE.AsGuid() ).Id;
        public static int IntegerFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.INTEGER.AsGuid() ).Id;
        public static int TextFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.TEXT.AsGuid() ).Id;
        public static int EncryptedTextFieldTypeId = FieldTypeCache.Read( Rock.SystemGuid.FieldType.ENCRYPTED_TEXT.AsGuid() ).Id;

        // Entity Types

        public static int AttributeEntityTypeId = EntityTypeCache.Read( typeof( Rock.Model.Attribute ) ).Id;
        public static int BatchEntityTypeId = EntityTypeCache.Read( typeof( FinancialBatch ) ).Id;
        public static int PersonEntityTypeId = EntityTypeCache.Read( typeof( Person ) ).Id;
        public static int UserLoginTypeId = EntityTypeCache.Read( typeof( UserLogin ) ).Id;
        public static int TransactionEntityTypeId = EntityTypeCache.Read( typeof( FinancialTransaction ) ).Id;
        public static int DatabaseStorageTypeId = EntityTypeCache.Read( typeof( Rock.Storage.Provider.Database ) ).Id;
        public static int FileSystemStorageTypeId = EntityTypeCache.Read( typeof( Rock.Storage.Provider.FileSystem ) ).Id;
        public static int EmailCommunicationMediumTypeId = EntityTypeCache.Read( typeof( Rock.Communication.Medium.Email ) ).Id;
        public static int TestGatewayTypeId = EntityTypeCache.Read( typeof( Rock.Financial.TestGateway ) ).Id;

        // Group Types

        public static int FamilyGroupTypeId = GroupTypeCache.Read( Rock.SystemGuid.GroupType.GROUPTYPE_FAMILY.AsGuid() ).Id;
        public static int GeneralGroupTypeId = GroupTypeCache.Read( "8400497B-C52F-40AE-A529-3FCCB9587101".AsGuid() ).Id;
        public static int SmallGroupTypeId = GroupTypeCache.Read( Rock.SystemGuid.GroupType.GROUPTYPE_SMALL_GROUP.AsGuid() ).Id;

        public static GroupTypeCache KnownRelationshipGroupType = GroupTypeCache.Read( Rock.SystemGuid.GroupType.GROUPTYPE_KNOWN_RELATIONSHIPS );
        public static GroupTypeCache ImpliedRelationshipGroupType = GroupTypeCache.Read( Rock.SystemGuid.GroupType.GROUPTYPE_IMPLIED_RELATIONSHIPS );

        // Group Location Types

        public static int HomeLocationTypeId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_HOME.AsGuid() ).Id;
        public static int PreviousLocationTypeId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_PREVIOUS.AsGuid() ).Id;
        public static int WorkLocationTypeId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_WORK.AsGuid() ).Id;

        // Defined Type/Value Types

        public static int PersonRecordTypeId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_PERSON.AsGuid() ).Id;
        public static int BusinessRecordTypeId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_TYPE_BUSINESS.AsGuid() ).Id;

        public static int MemberConnectionStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_MEMBER.AsGuid() ).Id;
        public static int AttendeeConnectionStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_ATTENDEE.AsGuid() ).Id;
        public static int VisitorConnectionStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_CONNECTION_STATUS_VISITOR.AsGuid() ).Id;

        public static int ConnectionStatusTypeId = DefinedTypeCache.Read( Rock.SystemGuid.DefinedType.PERSON_CONNECTION_STATUS.AsGuid() ).Id;
        public static int ActivePersonRecordStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_ACTIVE.AsGuid() ).Id;
        public static int InactivePersonRecordStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_INACTIVE.AsGuid() ).Id;
        public static int PendingPersonRecordStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_PENDING.AsGuid() ).Id;
        public static int DeceasedPersonRecordReasonId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_REASON_DECEASED.AsGuid() ).Id;
        public static int NoActivityPersonRecordReasonId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.PERSON_RECORD_STATUS_REASON_NO_ACTIVITY.AsGuid() ).Id;

        public static int TransactionTypeContributionId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.TRANSACTION_TYPE_CONTRIBUTION.AsGuid() ).Id;
        public static int TransactionSourceTypeOnsiteId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_ONSITE_COLLECTION.AsGuid() ).Id;
        public static int TransactionSourceTypeWebsiteId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_WEBSITE.AsGuid() ).Id;
        public static int TransactionSourceTypeKioskId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.FINANCIAL_SOURCE_TYPE_KIOSK.AsGuid() ).Id;

        public static int GroupTypeMeetingLocationId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.GROUP_LOCATION_TYPE_MEETING_LOCATION.AsGuid() ).Id;
        public static int DeviceTypeCheckinKioskId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.DEVICE_TYPE_CHECKIN_KIOSK.AsGuid() ).Id;

        public static int BenevolenceApprovedStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.BENEVOLENCE_APPROVED.AsGuid() ).Id;
        public static int BenevolenceDeniedStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.BENEVOLENCE_DENIED.AsGuid() ).Id;
        public static int BenevolencePendingStatusId = DefinedValueCache.Read( Rock.SystemGuid.DefinedValue.BENEVOLENCE_PENDING.AsGuid() ).Id;

        // Campus Types

        public static List<CampusCache> CampusList = CampusCache.All();

        // Note Types

        public static int PersonalNoteTypeId = NoteTypeCache.Read( Rock.SystemGuid.NoteType.PERSON_TIMELINE_NOTE.AsGuid() ).Id;

        // Relationship Types

        private static readonly GroupTypeRoleService groupTypeRoleService = new GroupTypeRoleService( new RockContext() );
        public static int FamilyAdultRoleId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_ADULT.AsGuid() ).Id;
        public static int FamilyChildRoleId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_FAMILY_MEMBER_CHILD.AsGuid() ).Id;

        public static int InviteeKnownRelationshipId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED.AsGuid() ).Id;
        public static int InvitedByKnownRelationshipId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_INVITED_BY.AsGuid() ).Id;
        public static int CanCheckInKnownRelationshipId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_CAN_CHECK_IN.AsGuid() ).Id;
        public static int AllowCheckInByKnownRelationshipId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_ALLOW_CHECK_IN_BY.AsGuid() ).Id;
        public static int KnownRelationshipOwnerRoleId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_KNOWN_RELATIONSHIPS_OWNER.AsGuid() ).Id;
        public static int ImpliedRelationshipOwnerRoleId = groupTypeRoleService.Get( Rock.SystemGuid.GroupRole.GROUPROLE_IMPLIED_RELATIONSHIPS_OWNER.AsGuid() ).Id;

        // Category Types

        public static int AllChurchCategoryId = CategoryCache.Read( "5A94E584-35F0-4214-91F1-D72531CC6325".AsGuid() ).Id; // Prayer Parent Cagetory for All Church
    }
}
