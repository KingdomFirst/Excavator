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
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Excavator;
using Excavator.BinaryFile;
using Excavator.Utility;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;

namespace Excavator.BinaryFile
{
    /// <summary>
    /// This example extends the base Excavator class to consume a database model.
    /// Currently does nothing, this is just an example.
    /// Data models and mapping methods can be in other partial classes.
    /// </summary>
    [Export( typeof( ExcavatorComponent ) )]
    public class BinaryFileComponent : ExcavatorComponent
    {
        #region Fields

        /// <summary>
        /// Gets the full name of the excavator type.
        /// </summary>
        /// <value>
        /// The name of the database being imported.
        /// </value>
        public override string FullName
        {
            get { return "Binary File"; }
        }

        /// <summary>
        /// Gets the supported file extension type(s).
        /// </summary>
        /// <value>
        /// The supported extension type(s).
        /// </value>
        public override string ExtensionType
        {
            get { return ".zip"; }
        }

        /// <summary>
        /// All the people who've been imported
        /// </summary>
        protected static List<PersonKeys> ImportedPeople;

        // Database StorageEntity Type
        protected static int? DatabaseStorageTypeId;

        // File System StorageEntity Type
        protected static int? FileSystemStorageTypeId;

        // Binary File RootPath Attribute
        protected static AttributeCache RootPathAttribute;

        /// <summary>
        /// The file types
        /// </summary>
        protected List<BinaryFileType> FileTypes;

        /// <summary>
        /// The person assigned to do the import
        /// </summary>
        protected static int? ImportPersonAliasId;

        #endregion Fields

        #region Methods

        /// <summary>
        /// Loads the database for this instance.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override bool LoadSchema( string fileName )
        {
            if ( DataNodes == null )
            {
                DataNodes = new List<DataNode>();
            }

            var folderItem = new DataNode();
            var previewFolder = new ZipArchive( new FileStream( fileName, FileMode.Open ) );
            folderItem.Name = Path.GetFileNameWithoutExtension( fileName );

            foreach ( var document in previewFolder.Entries.Take( 50 ) )
            {
                if ( document != null )
                {
                    var entryItem = new DataNode();
                    entryItem.Name = document.FullName;
                    string content = new StreamReader( document.Open() ).ReadToEnd();
                    entryItem.Value = Encoding.UTF8.GetBytes( content ) ?? null;
                    entryItem.NodeType = typeof( byte[] );
                    entryItem.Parent.Add( folderItem );
                    folderItem.Children.Add( entryItem );
                }
            }

            DataNodes.Add( folderItem );

            return DataNodes.Count() > 0 ? true : false;
        }

        /// <summary>
        /// Transforms the data from the dataset.
        /// </summary>
        public override int TransformData( Dictionary<string, string> settings )
        {
            var importUser = settings["ImportUser"];

            ReportProgress( 0, "Starting health checks..." );
            var rockContext = new RockContext();
            var personService = new PersonService( rockContext );
            var importPerson = personService.GetByFullName( importUser, allowFirstNameOnly: true ).FirstOrDefault();

            if ( importPerson == null )
            {
                importPerson = personService.Queryable().AsNoTracking().FirstOrDefault();
            }

            ImportPersonAliasId = importPerson.PrimaryAliasId;
            ReportProgress( 0, "Checking for existing attributes..." );
            LoadExistingRockData( rockContext );

            // only import things that the user checked
            var selectedFiles = DataNodes.Where( n => n.Checked != false ).ToList();

            foreach ( var file in selectedFiles )
            {
                var nameWithoutPath = Path.GetFileNameWithoutExtension( file.Name );
                var archiveFolder = new ZipArchive( new FileStream( file.Name, FileMode.Open ) );

                IBinaryFile worker = IMapAdapterFactory.GetAdapter( nameWithoutPath );
                if ( worker != null )
                {
                    worker.Map( archiveFolder );
                }
                else
                {
                    LogException( "Binary File", string.Format( "Unknown File: {0} does not start with the name of a known data map.", nameWithoutPath ) );
                }
            }

            // Report the final imported count
            ReportProgress( 100, string.Format( "Completed import: {0:N0} records imported.", 100 ) );
            return 0;
        }

        /// <summary>
        /// Loads Rock data that's used globally by the transform
        /// </summary>
        private void LoadExistingRockData( RockContext lookupContext = null )
        {
            lookupContext = lookupContext ?? new RockContext();

            FileTypes = new BinaryFileTypeService( lookupContext ).Queryable().AsNoTracking().ToList();

            // core-specified guid for setting file root path
            RootPathAttribute = AttributeCache.Read( new Guid( "3CAFA34D-9208-439B-A046-CB727FB729DE" ) );

            DatabaseStorageTypeId = EntityTypeCache.GetId( typeof( Rock.Storage.Provider.Database ) );
            FileSystemStorageTypeId = EntityTypeCache.GetId( typeof( Rock.Storage.Provider.FileSystem ) );

            // load attributes to get the default storage location
            foreach ( var type in FileTypes )
            {
                type.LoadAttributes( lookupContext );
            }

            var personAliasIds = new PersonAliasService( lookupContext ).Queryable().AsNoTracking().ToList();
            var ImportedPeople = personAliasIds.Select( pa => new PersonKeys()
                {
                    PersonAliasId = pa.Id,
                    PersonId = pa.PersonId,
                    IndividualId = pa.ForeignId.AsType<int?>(),
                } ).ToList();
        }

        /// <summary>
        /// Gets the person keys.
        /// </summary>
        /// <param name="individualId">The individual identifier.</param>
        /// <param name="householdId">The household identifier.</param>
        /// <param name="includeVisitors">if set to <c>true</c> [include visitors].</param>
        /// <returns></returns>
        protected static PersonKeys GetPersonKeys( int? individualId = null )
        {
            if ( individualId != null )
            {
                return ImportedPeople.FirstOrDefault( p => p.IndividualId == individualId );
            }
            else
            {
                return null;
            }
        }

        #endregion Methods
    }

    #region Helper Classes

    /// <summary>
    /// Generic map interface
    /// </summary>
    public interface IBinaryFile
    {
        void Map( ZipArchive zipData );
    }

    /// <summary>
    /// Adapter helper method to call the write object map
    /// </summary>
    public static class IMapAdapterFactory
    {
        public static IBinaryFile GetAdapter( string fileName )
        {
            IBinaryFile adapter = null;

            var configFileTypes = ConfigurationManager.GetSection( "binaryFileTypes" ) as NameValueCollection;

            // ensure we have a file matching a config type
            //if ( configFileTypes != null && configFileTypes.AllKeys.Any( k => fileName.StartsWith( k.RemoveWhitespace() ) ) )
            //{
            var iBinaryFileType = typeof( IBinaryFile );
            var mappedFileTypes = iBinaryFileType.Assembly.ExportedTypes
                .Where( p => iBinaryFileType.IsAssignableFrom( p ) && !p.IsInterface );
            var selectedType = mappedFileTypes.FirstOrDefault( t => fileName.StartsWith( t.Name.RemoveWhitespace() ) );
            if ( selectedType != null )
            {
                adapter = (IBinaryFile)Activator.CreateInstance( selectedType );
            }
            else
            {
                adapter = new MinistryDocument();
            }

            //}

            return adapter;
        }
    }

    /// <summary>
    /// Helper class to determine the MIME type of a certain file
    /// Reused from http://stackoverflow.com/a/7161265
    /// </summary>
    public static class MIMEFinder
    {
        private static readonly Dictionary<string, string> MIMETypesDictionary = new Dictionary<string, string>
        {
            {"ai", "application/postscript"},
            {"aif", "audio/x-aiff"},
            {"aifc", "audio/x-aiff"},
            {"aiff", "audio/x-aiff"},
            {"asc", "text/plain"},
            {"atom", "application/atom+xml"},
            {"au", "audio/basic"},
            {"avi", "video/x-msvideo"},
            {"bcpio", "application/x-bcpio"},
            {"bin", "application/octet-stream"},
            {"bmp", "image/bmp"},
            {"cdf", "application/x-netcdf"},
            {"cgm", "image/cgm"},
            {"class", "application/octet-stream"},
            {"cpio", "application/x-cpio"},
            {"cpt", "application/mac-compactpro"},
            {"csh", "application/x-csh"},
            {"css", "text/css"},
            {"dcr", "application/x-director"},
            {"dif", "video/x-dv"},
            {"dir", "application/x-director"},
            {"djv", "image/vnd.djvu"},
            {"djvu", "image/vnd.djvu"},
            {"dll", "application/octet-stream"},
            {"dmg", "application/octet-stream"},
            {"dms", "application/octet-stream"},
            {"doc", "application/msword"},
            {"docx","application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
            {"dotx", "application/vnd.openxmlformats-officedocument.wordprocessingml.template"},
            {"docm","application/vnd.ms-word.document.macroEnabled.12"},
            {"dotm","application/vnd.ms-word.template.macroEnabled.12"},
            {"dtd", "application/xml-dtd"},
            {"dv", "video/x-dv"},
            {"dvi", "application/x-dvi"},
            {"dxr", "application/x-director"},
            {"eps", "application/postscript"},
            {"etx", "text/x-setext"},
            {"exe", "application/octet-stream"},
            {"ez", "application/andrew-inset"},
            {"gif", "image/gif"},
            {"gram", "application/srgs"},
            {"grxml", "application/srgs+xml"},
            {"gtar", "application/x-gtar"},
            {"hdf", "application/x-hdf"},
            {"hqx", "application/mac-binhex40"},
            {"htm", "text/html"},
            {"html", "text/html"},
            {"ice", "x-conference/x-cooltalk"},
            {"ico", "image/x-icon"},
            {"ics", "text/calendar"},
            {"ief", "image/ief"},
            {"ifb", "text/calendar"},
            {"iges", "model/iges"},
            {"igs", "model/iges"},
            {"jnlp", "application/x-java-jnlp-file"},
            {"jp2", "image/jp2"},
            {"jpe", "image/jpeg"},
            {"jpeg", "image/jpeg"},
            {"jpg", "image/jpeg"},
            {"js", "application/x-javascript"},
            {"kar", "audio/midi"},
            {"latex", "application/x-latex"},
            {"lha", "application/octet-stream"},
            {"lzh", "application/octet-stream"},
            {"m3u", "audio/x-mpegurl"},
            {"m4a", "audio/mp4a-latm"},
            {"m4b", "audio/mp4a-latm"},
            {"m4p", "audio/mp4a-latm"},
            {"m4u", "video/vnd.mpegurl"},
            {"m4v", "video/x-m4v"},
            {"mac", "image/x-macpaint"},
            {"man", "application/x-troff-man"},
            {"mathml", "application/mathml+xml"},
            {"me", "application/x-troff-me"},
            {"mesh", "model/mesh"},
            {"mid", "audio/midi"},
            {"midi", "audio/midi"},
            {"mif", "application/vnd.mif"},
            {"mov", "video/quicktime"},
            {"movie", "video/x-sgi-movie"},
            {"mp2", "audio/mpeg"},
            {"mp3", "audio/mpeg"},
            {"mp4", "video/mp4"},
            {"mpe", "video/mpeg"},
            {"mpeg", "video/mpeg"},
            {"mpg", "video/mpeg"},
            {"mpga", "audio/mpeg"},
            {"ms", "application/x-troff-ms"},
            {"msh", "model/mesh"},
            {"mxu", "video/vnd.mpegurl"},
            {"nc", "application/x-netcdf"},
            {"oda", "application/oda"},
            {"ogg", "application/ogg"},
            {"pbm", "image/x-portable-bitmap"},
            {"pct", "image/pict"},
            {"pdb", "chemical/x-pdb"},
            {"pdf", "application/pdf"},
            {"pgm", "image/x-portable-graymap"},
            {"pgn", "application/x-chess-pgn"},
            {"pic", "image/pict"},
            {"pict", "image/pict"},
            {"png", "image/png"},
            {"pnm", "image/x-portable-anymap"},
            {"pnt", "image/x-macpaint"},
            {"pntg", "image/x-macpaint"},
            {"ppm", "image/x-portable-pixmap"},
            {"ppt", "application/vnd.ms-powerpoint"},
            {"pptx","application/vnd.openxmlformats-officedocument.presentationml.presentation"},
            {"potx","application/vnd.openxmlformats-officedocument.presentationml.template"},
            {"ppsx","application/vnd.openxmlformats-officedocument.presentationml.slideshow"},
            {"ppam","application/vnd.ms-powerpoint.addin.macroEnabled.12"},
            {"pptm","application/vnd.ms-powerpoint.presentation.macroEnabled.12"},
            {"potm","application/vnd.ms-powerpoint.template.macroEnabled.12"},
            {"ppsm","application/vnd.ms-powerpoint.slideshow.macroEnabled.12"},
            {"ps", "application/postscript"},
            {"qt", "video/quicktime"},
            {"qti", "image/x-quicktime"},
            {"qtif", "image/x-quicktime"},
            {"ra", "audio/x-pn-realaudio"},
            {"ram", "audio/x-pn-realaudio"},
            {"ras", "image/x-cmu-raster"},
            {"rdf", "application/rdf+xml"},
            {"rgb", "image/x-rgb"},
            {"rm", "application/vnd.rn-realmedia"},
            {"roff", "application/x-troff"},
            {"rtf", "text/rtf"},
            {"rtx", "text/richtext"},
            {"sgm", "text/sgml"},
            {"sgml", "text/sgml"},
            {"sh", "application/x-sh"},
            {"shar", "application/x-shar"},
            {"silo", "model/mesh"},
            {"sit", "application/x-stuffit"},
            {"skd", "application/x-koan"},
            {"skm", "application/x-koan"},
            {"skp", "application/x-koan"},
            {"skt", "application/x-koan"},
            {"smi", "application/smil"},
            {"smil", "application/smil"},
            {"snd", "audio/basic"},
            {"so", "application/octet-stream"},
            {"spl", "application/x-futuresplash"},
            {"src", "application/x-wais-source"},
            {"sv4cpio", "application/x-sv4cpio"},
            {"sv4crc", "application/x-sv4crc"},
            {"svg", "image/svg+xml"},
            {"swf", "application/x-shockwave-flash"},
            {"t", "application/x-troff"},
            {"tar", "application/x-tar"},
            {"tcl", "application/x-tcl"},
            {"tex", "application/x-tex"},
            {"texi", "application/x-texinfo"},
            {"texinfo", "application/x-texinfo"},
            {"tif", "image/tiff"},
            {"tiff", "image/tiff"},
            {"tr", "application/x-troff"},
            {"tsv", "text/tab-separated-values"},
            {"txt", "text/plain"},
            {"ustar", "application/x-ustar"},
            {"vcd", "application/x-cdlink"},
            {"vrml", "model/vrml"},
            {"vxml", "application/voicexml+xml"},
            {"wav", "audio/x-wav"},
            {"wbmp", "image/vnd.wap.wbmp"},
            {"wbmxl", "application/vnd.wap.wbxml"},
            {"wml", "text/vnd.wap.wml"},
            {"wmlc", "application/vnd.wap.wmlc"},
            {"wmls", "text/vnd.wap.wmlscript"},
            {"wmlsc", "application/vnd.wap.wmlscriptc"},
            {"wrl", "model/vrml"},
            {"xbm", "image/x-xbitmap"},
            {"xht", "application/xhtml+xml"},
            {"xhtml", "application/xhtml+xml"},
            {"xls", "application/vnd.ms-excel"},
            {"xml", "application/xml"},
            {"xpm", "image/x-xpixmap"},
            {"xsl", "application/xml"},
            {"xlsx","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
            {"xltx","application/vnd.openxmlformats-officedocument.spreadsheetml.template"},
            {"xlsm","application/vnd.ms-excel.sheet.macroEnabled.12"},
            {"xltm","application/vnd.ms-excel.template.macroEnabled.12"},
            {"xlam","application/vnd.ms-excel.addin.macroEnabled.12"},
            {"xlsb","application/vnd.ms-excel.sheet.binary.macroEnabled.12"},
            {"xslt", "application/xslt+xml"},
            {"xul", "application/vnd.mozilla.xul+xml"},
            {"xwd", "image/x-xwindowdump"},
            {"xyz", "chemical/x-xyz"},
            {"zip", "application/zip"}
        };

        /// <summary>
        /// Gets the MIME type of the file.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        public static string GetMIMEType( string fileName )
        {
            //get file extension
            string extension = Path.GetExtension( fileName ).ToLowerInvariant();

            if ( extension.Length > 0 &&
                MIMETypesDictionary.ContainsKey( extension.Remove( 0, 1 ) ) )
            {
                return MIMETypesDictionary[extension.Remove( 0, 1 )];
            }
            return "application/octet-stream";
        }
    }

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
        /// Stores the F1 Individual Id
        /// </summary>
        public int? IndividualId;

        /// <summary>
        /// Stores the F1 Household Id
        /// </summary>
        public int? HouseholdId;
    }

    #endregion
}