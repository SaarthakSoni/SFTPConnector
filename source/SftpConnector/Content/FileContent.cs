//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace SFTPConnector.Messaging
    {
        using Newtonsoft.Json;
        using Newtonsoft.Json.Converters;
        using System;
        using System.ComponentModel.DataAnnotations;
        using System.Globalization;
        using System.IO;
        using SFTPConnector.Exceptions;
    
    /// <summary>
    /// Enum to specify content encoding type
    /// </summary>
    public enum FileType
        {
        /// <summary>
        /// For FileType as Text
        /// </summary>
        Text = 0,

        /// <summary>
        /// For FileType as Binary
        /// </summary>
        Binary = 1
        }

    /// <summary>
    /// Enum to specify content encoding type
    /// </summary>
    public enum ContentTransferEncoding
        {
        /// <summary>
        /// For FileType as Text
        /// </summary>
        None = 0,

        /// <summary>
        /// For FileType as Binary
        /// </summary>
        Base64 = 1
        }

    /// <summary>
    /// This class denotes the content of file/document/message
    /// For a content: we need to know the stream/content string, its encoding type
    /// </summary>
    public class FileContent
        {
        /// <summary>
        /// Initializes a new instance of the Content class.
        /// </summary>
        public FileContent()
            {
            }

        /// <summary>
        /// Initializes a new instance of the Content class. To be used for string content.
        /// </summary>
        /// <param name="content"></param>
        public FileContent(string content)
            : this(content, ContentTransferEncoding.None)
            {
            }

        /// <summary>
        /// Initializes a new instance of the Content class. To be used for string content.
        /// </summary>
        public FileContent(string content, ContentTransferEncoding contentTransferEncoding)
            {
            this.Content = content;
            this.ContentTransferEncoding = contentTransferEncoding;
            }

        /// <summary>
        /// Constructor that when given a stream, converts to encoded string based on encoding provided and saves it to content data
        /// </summary>
        /// <param name="stream"></param>
        public FileContent(Stream stream) : this(stream, ContentTransferEncoding.None)
            {
            }

        /// <summary>
        /// Constructor that when given a stream, converts to encoded string based on encoding provided and saves it to content data
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encoding"></param>
        public FileContent(Stream stream, ContentTransferEncoding encoding)
            {
            this.Content = encoding == ContentTransferEncoding.Base64 ? System.Convert.ToBase64String(ReadStreamAsByteArray(stream)) : ReadStreamAsString(stream);
            this.ContentTransferEncoding = encoding;
            }

        /// <summary>
        /// This will store the information in base64 encoded for streams and as-is for string type of data
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// This denotes the content-transfer encoding used to transfer content-data for base64 it is Base64, keep empty if no encoding is used.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ContentTransferEncoding ContentTransferEncoding { get; set; }

        /// <summary>
        /// From ContentData converts to Stream format based on Content-Transfer Encoding
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ContentNullException"></exception>
        /// <exception cref="ContentInvalidBase64Exception"></exception>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Needed"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Needed")]
        public Stream GetStream()
            {
            // to do change this to virtual file stream when larger files are supported
            MemoryStream stream = new MemoryStream();
            if (this.Content != null)
                {
                if (this.ContentTransferEncoding == ContentTransferEncoding.Base64)
                    {
                    try
                        {
                        byte[] bytes = System.Convert.FromBase64String(this.Content);
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                        }
                    catch (FormatException ex)
                        {
                        throw new ContentInvalidBase64Exception(ex);
                        }
                    }
                else
                    {
                    // if content is not base64 encoded, we will write the content as is.
                    StreamWriter writer = new StreamWriter(stream);
                    writer.Write(this.Content);
                    writer.Flush();
                    }
                }
            else
                {
                throw new ContentNullException("Content is null");
                }

            stream.Position = 0;
            return stream;
            }

        /// <summary>
        /// Utility function needed to read stream as string
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>string</returns>
        /// <exception cref="System.ArgumentNullException">throws ArgumentNullException when stream is null</exception>
        public static string ReadStreamAsString(System.IO.Stream stream)
            {
            if (stream == null)
                {
                throw new ArgumentNullException("stream");
                }

            long originalPosition = 0;

            if (stream.CanSeek)
                {
                originalPosition = stream.Position;
                }

            try
                {
                using (StreamReader reader = new StreamReader(stream))
                    {
                    return reader.ReadToEnd();
                    }
                }
            finally
                {
                if (stream.CanSeek)
                    {
                    stream.Position = originalPosition;
                    }
                }
            }

        /// <summary>
        /// Utility functions needed to read stream as bytes
        /// </summary>
        /// <param name="stream"></param>
        /// <returns>byte[]</returns>
        /// <exception cref="System.ArgumentNullException">throws ArgumentNullException when stream is null</exception>
        public static byte[] ReadStreamAsByteArray(System.IO.Stream stream)
            {
            if (stream == null)
                {
                throw new ArgumentNullException("stream");
                }

            long originalPosition = 0;

            if (stream.CanSeek)
                {
                originalPosition = stream.Position;
                }

            try
                {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                    {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                        {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                            {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                            }
                        }
                    }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                    {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                    }

                return buffer;
                }
            finally
                {
                if (stream.CanSeek)
                    {
                    stream.Position = originalPosition;
                    }
                }
            }

        /// <summary>
        /// returns currently supported content transfer encoding based on file type
        /// </summary>
        /// <param name="fileType"></param>
        /// <returns></returns>
        public static ContentTransferEncoding GetEncodingBasedOnFileType(FileType fileType)
            {
            switch (fileType)
                {
                case FileType.Text: return ContentTransferEncoding.None;
                case FileType.Binary: return ContentTransferEncoding.Base64;
                default: return ContentTransferEncoding.None;
                }
            }
        }
    }