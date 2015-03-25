//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace SFTPConnector.Exceptions
    {
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// This is for Content being null
    /// </summary>
    [Serializable]
    public class ContentNullException : Exception
        {
        /// <summary>
        /// 
        /// </summary>
        public ContentNullException()
            {
            }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public ContentNullException(string message)
            : base(message)
            {
            }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public ContentNullException(string message, Exception inner)
            : base(message, inner)
            {
            }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serializeInfo"></param>
        /// <param name="context"></param>
        protected ContentNullException(SerializationInfo serializeInfo, StreamingContext context)
            : base(serializeInfo, context)
            {
            }
        }

    /// <summary>
    /// This is for Content Being Invalid Base64 when ContentEncoding is provided as Base64
    /// </summary>
    [Serializable]
    public class ContentInvalidBase64Exception : Exception
        {
        /// <summary>
        /// 
        /// </summary>
        public ContentInvalidBase64Exception()
            {
            }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public ContentInvalidBase64Exception(string message)
            : base(message)
            {
            }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inner"></param>
        public ContentInvalidBase64Exception(string message, Exception inner)
            : base(message, inner)
            {
            }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="inner"></param>
        public ContentInvalidBase64Exception(Exception inner)
            : base("Content has invalid base64 format", inner)
            {
            }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serializeInfo"></param>
        /// <param name="context"></param>
        protected ContentInvalidBase64Exception(SerializationInfo serializeInfo, StreamingContext context)
            : base(serializeInfo, context)
            {
            }
        }
    }
