//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace SFTPConnector
{
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using SFTPConnector.Messaging;

    /// <summary>
    /// Get File Operation return object of File
    /// </summary>
    public class File : FileContent
    {
        /// <summary>
        /// Empty Constructor
        /// </summary>
        public File()
        {
        }

        /// <summary>
        /// Initializes new instance of the class and converts stream to base64 format and save
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="allProperties"></param>
        /// <param name="encoding"></param>
        public File(Stream stream, FileInfo allProperties, ContentTransferEncoding encoding) : base(stream, encoding)
        {
            this.FilePath = allProperties.FilePath;
            this.FolderPath = allProperties.FolderPath;
            this.FileName = allProperties.FileName;
            this.ServerAddress = allProperties.ServerAddress;
        }

       
        /// <summary>
        /// FileName
        /// </summary>
        [Required]
        public string FileName { get; set; }

        /// <summary>
        /// Folder Path
        /// </summary>
        [Required]
        public string FolderPath { get; set; }

        /// <summary>
        /// File Path
        /// </summary>
        [Required]

        public string FilePath { get; set; }

        /// <summary>
        /// Server Address
        /// </summary>
        [Required]
        public string ServerAddress { get; set; }
    }
}