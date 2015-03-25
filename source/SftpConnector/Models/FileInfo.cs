//-----------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------

namespace SFTPConnector
{
    using System.ComponentModel.DataAnnotations;
  
    /// <summary>
    /// Denotes the file properties
    /// </summary>
    public class FileInfo
    {
        /// <summary>
        /// Initializes instance of File Information
        /// </summary>
        public FileInfo()
        {
        }

        /// <summary>
        /// Initializes instance of File Information
        /// </summary>
        public FileInfo(string server, string folder, string fileName)
        {
            this.FileName = fileName;
            this.FolderPath = folder;
            this.FilePath = System.IO.Path.Combine(folder, fileName);
            this.ServerAddress = server;
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
