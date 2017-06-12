using Rock.Model;

namespace Excavator.Utility
{
    public static partial class Extensions
    {
        // Flag to designate household role
        public enum FamilyRole
        {
            Adult = 0,
            Child = 1,
            Visitor = 2
        };

        public enum SearchDirection
        {
            Begins = 0,
            Ends = 1
        };

        /// <summary>
        /// Helper class to store references to people that've been imported
        /// </summary>
        public class PersonKeys
        {
            /// <summary>
            /// Stores the Rock PersonAliasId
            /// </summary>
            public int PersonAliasId;

            /// <summary>
            /// Stores the Rock PersonId
            /// </summary>
            public int PersonId;

            /// <summary>
            /// Stores a Person's Foreign Id
            /// </summary>
            public int? PersonForeignId;

            /// <summary>
            /// Stores a Person's Foreign Key
            /// </summary>
            public string PersonForeignKey;

            /// <summary>
            /// Stores a Group's Foreign Id
            /// </summary>
            public int? GroupForeignId;

            /// <summary>
            /// Stores how the person is connected to the family
            /// </summary>
            public FamilyRole FamilyRoleId;
        }

        /// <summary>
        /// Helper class to store document keys
        /// </summary>
        public class DocumentKeys
        {
            /// <summary>
            /// Stores the Rock PersonId
            /// </summary>
            public int PersonId;

            /// <summary>
            /// Stores the attribute linked to this document
            /// </summary>
            public int AttributeId;

            /// <summary>
            /// Stores the actual document
            /// </summary>
            public BinaryFile File;
        }
    }
}
