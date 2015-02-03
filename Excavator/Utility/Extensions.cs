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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OrcaMDF.Core.MetaData;

namespace Excavator.Utility
{
    /// <summary>
    /// Extensions to the base components
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Gets the C# type from a SQL or OrcaMDF type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Conversion failed: type not recognized  + type</exception>
        public static Type GetSQLType( ColumnType type )
        {
            switch ( type )
            {
                case ColumnType.BigInt:
                    return typeof( long );

                case ColumnType.Binary:
                case ColumnType.Image:
                case ColumnType.VarBinary:
                    return typeof( byte[] );

                case ColumnType.Bit:
                    return typeof( bool );

                case ColumnType.Char:
                case ColumnType.NChar:
                case ColumnType.NText:
                case ColumnType.NVarchar:
                case ColumnType.RID:
                case ColumnType.Text:
                case ColumnType.Varchar:
                    return typeof( string );

                case ColumnType.DateTime:
                case ColumnType.SmallDatetime:
                    return typeof( DateTime );

                case ColumnType.Decimal:
                case ColumnType.Money:
                case ColumnType.SmallMoney:
                    return typeof( decimal );

                case ColumnType.Int:
                    return typeof( int );

                case ColumnType.Uniquifier:
                case ColumnType.UniqueIdentifier:
                    return typeof( Guid );

                case ColumnType.SmallInt:
                    return typeof( short );

                case ColumnType.TinyInt:
                    return typeof( byte );

                case ColumnType.Variant:
                    return typeof( object );

                default:
                    throw new ArgumentOutOfRangeException( "Conversion failed: type not recognized " + type );
            }
        }

        /// <summary>
        /// Gets the enumerable values.
        /// http://damieng.com/blog/2008/04/10/using-linq-to-foreach-over-an-enum-in-c
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerable<T> Get<T>()
        {
            return Enum.GetValues( typeof( T ) ).Cast<T>();
        }

        /// <summary>
        /// Strips the whitespace.
        /// </summary>
        /// <param name="str">The string.</param>
        /// <returns></returns>
        public static string RemoveWhitespace( this string str )
        {
            StringBuilder sb = new StringBuilder();
            foreach ( char c in str )
            {
                if ( !char.IsWhiteSpace( c ) )
                {
                    sb.Append( c );
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns true if the given string is a valid email address.
        /// </summary>
        /// <param name="email">The string to validate</param>
        /// <returns>true if valid email, false otherwise</returns>
        public static bool IsEmail( this string email )
        {
            return Regex.IsMatch( email, @"^(?!((http|https)://|www.))[\w\.\'_%-]+(\+[\w-]*)?@([\w-]+\.)+[\w-]+$" );
        }
    }
}