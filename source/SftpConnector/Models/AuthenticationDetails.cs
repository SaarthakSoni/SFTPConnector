//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace SFTPConnector
{
    /// <summary>
    /// Authentication Details.
    /// </summary>
    public class AuthenticationDetails
    {
        /// <summary>
        /// Authentication Typeb
        /// </summary>
        public string AuthenticationType { get; set; }

        /// <summary>
        /// UserName
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Content of ppk file in base64 encoded string.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Password for the above ppk file.
        /// </summary>
        public string PpkFilePassword { get; set; }

        /// <summary>
        /// PPK file name.
        /// </summary>
        public string PpkFileName { get; set; }
    }
}