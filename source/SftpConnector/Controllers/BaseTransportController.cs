//-----------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------

namespace SFTPConnector
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Description;
    using SftpConnector;
    using Microsoft.Azure.AppService.ApiApps.Service;
    using SFTPConnector.Messaging;
    using SFTPConnector.Exceptions;

    /// <summary>
    /// Base Transport Controller
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1012:AbstractTypesShouldNotHaveConstructors", Justification = "Connectors which have similar connection properties can use this class and set the similar properties in base constructor")]
    public abstract class BaseTransportController : ApiController
    {
        private Regex includeRegex;
        private Regex excludeRegex;
        private bool isConfigValid = true;
        private Exception errException;

        /// <summary>
        /// Inializes instance of BaseTransportController
        /// </summary>
        public BaseTransportController(bool isSFTP = false)
        {
            this.RootFolderPath = ConfigurationManager.AppSettings["RootFolderPath"];

            if (this.RootFolderPath == null)
            {
                this.RootFolderPath = string.Empty;
            }

            if (isSFTP)
            {
                // UserName/Password configuration is only through portal.
                return;
            }

            this.UserName = ConfigurationManager.AppSettings["FtpUserName"];
            this.Password = ConfigurationManager.AppSettings["FtpPassword"];
        }

        #region User Configurable Properties

        /// <summary>
        /// Server Address
        /// </summary>
        public string ServerAddress { get; set; }

        /// <summary>
        /// Server Port
        /// </summary>
        public ushort ServerPort { get; set; }

        /// <summary>
        /// UserName
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Root folder path which is set during configuration time in Web.config
        /// </summary>
        public string RootFolderPath { get; set; }

        #endregion User Configurable Properties

        /// <summary>
        /// 
        /// </summary>
        protected bool IsConfigurationValid
        {
            get
            {
                return this.isConfigValid;
            }

            set
            {
                this.isConfigValid = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected Exception ErrorException
        {
            get
            {
                return this.errException;
            }

            set
            {
                this.errException = value;
            }
        }

        /// <remarks>
        /// Uploads the specified file to the server.
        /// </remarks>
        /// <summary>
        /// Upload File.
        /// </summary>
        /// <param name="content">Specify Content of File</param>
        /// <param name="FilePath">Specify folder path</param>
        /// <param name="AppendIfExists">Enable or Disable 'Append If Exist'. When enabled, the data is appended to the file if it exists. When disabled, the file is overwritten if it exists</param>
        /// <param name="TemporaryFolder">Optional. If provided, the adapter will upload the file to the 'Temporary Folder Path' and once the upload is done the file will be moved to 'Folder Path'. The 'Temporary Folder Path' should be on the same physical disk as the 'Folder Path' to make sure that the move operation is atomic. Temporary folder can be used only when 'Append If Exist' property is disabled.</param>
        /// <returns>HttpResponseMessage</returns>
        /// <response code="200">Successful Upload File operation</response>
        /// <response code="400">Bad Request</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Folder not found</response>
        /// <response code="503">Service Unavailable</response>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "Upload Method requires multiple default parameters"), HttpPut]
        [Route("file/{*FilePath}")]
        [ResponseType(typeof(FileInfo))]
        public async Task<HttpResponseMessage> UploadFile([FromBody]FileContent content, string FilePath, bool AppendIfExists = false, string TemporaryFolder = null)
        {
            Trace.TraceInformation("UploadFile Called : {0} , {1} , {2} ", FilePath, AppendIfExists, TemporaryFolder);
            if (!this.IsConfigurationValid)
            {
                Trace.TraceError("Stacktrace :" + this.ErrorException.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, this.ErrorException);
            }

            if (content == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Content of Request Body is NULL. Please ensure that Request Body adheres to the defined model.");
            }

            try
            {
                FilePath = Path.Combine(this.RootFolderPath, FilePath).Replace("\\", "/");

                if (!string.IsNullOrWhiteSpace(TemporaryFolder))
                {
                    TemporaryFolder = Path.Combine(this.RootFolderPath, TemporaryFolder).Replace("\\", "/");
                }

                this.ValidateParameter(FilePath, "FilePath");

                string folderPath = Path.GetDirectoryName(FilePath).Replace("\\", "/");
                string fileName = Path.GetFileName(FilePath);

                return await this.UploadTransportSpecificFileAsync(content, folderPath, fileName, AppendIfExists, TemporaryFolder);
            }
            catch (ArgumentException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
            catch (ContentInvalidBase64Exception ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
            catch (ContentNullException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        /// <remarks>
        /// Deletes the specified file from the server.
        /// </remarks>
        /// <summary>
        /// Delete File.
        /// </summary>
        /// <param name="FilePath">Specify folder path</param>
        /// <returns>HttpResponseMessage</returns>
        /// <response code="200">Successful Delete File operation</response>
        /// <response code="400">Bad Request</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Folder or File not found</response>
        /// <response code="503">Service Unavailable</response>
        [HttpDelete]
        [Route("file/{*FilePath}")]
        [ResponseType(typeof(string))]
        public async Task<HttpResponseMessage> DeleteFile(string FilePath)
        {
            Trace.TraceInformation("DeleteFile Called : {0} ", FilePath);
            if (!this.IsConfigurationValid)
            {
                Trace.TraceError("Stacktrace :" + this.ErrorException.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, this.ErrorException);
            }

            try
            {
                FilePath = Path.Combine(this.RootFolderPath, FilePath).Replace("\\", "/");

                this.ValidateParameter(FilePath, "FilePath");

                string folderPath = Path.GetDirectoryName(FilePath).Replace("\\", "/");
                string fileName = Path.GetFileName(FilePath);
                HttpResponseMessage message = await this.DeleteTransportSpecificFileAsync(folderPath, fileName);
                return message;
            }
            catch (ArgumentException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        /// <remarks>
        /// Retrieves the specified file from the server.
        /// </remarks>
        /// <summary>
        /// Get File
        /// </summary>
        /// <param name="FilePath">Specify File Path</param>
        /// <param name="FileType">The type of file: text or binary.</param>
        /// <returns>HttpResponseMessage</returns>
        /// <response code="200">Successful Get File operation</response>
        /// <response code="400">Bad Request</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Folder or File not found</response>
        /// <response code="503">Service Unavailable</response>
        [HttpGet]
        [Route("file/{*FilePath}")]
        [ResponseType(typeof(File))]
        public async Task<HttpResponseMessage> GetFile(string FilePath, FileType FileType = FileType.Text)
        {
            Trace.TraceInformation("GetFile Called : {0} ", FilePath);

            if (!this.IsConfigurationValid)
            {
                Trace.TraceError("Stacktrace :" + this.ErrorException.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, this.ErrorException);
            }

            try
            {
                FilePath = Path.Combine(this.RootFolderPath, FilePath).Replace("\\", "/");

                this.ValidateParameter(FilePath, "FilePath");

                string folderPath = Path.GetDirectoryName(FilePath).Replace("\\", "/");
                string fileName = Path.GetFileName(FilePath);
                return await this.GetTransportSpecificFileAsync(folderPath, fileName, FileContent.GetEncodingBasedOnFileType(FileType));
            }
            catch (ArgumentException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        /// <remarks>
        /// Lists the files present in a specified folder.
        /// </remarks>
        /// <summary>
        /// Lists Files
        /// </summary>
        /// <param name="FolderPath">Specify folder path</param>
        /// <returns>HttpResponseMessage</returns>
        /// <response code="200">Successful List Folder operation</response>
        /// <response code="400">Bad Request</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Folder not found</response>B
        /// <response code="503">Service Unavailable</response>
        [HttpGet]
        [Route("folder/{*FolderPath}")]
        [ResponseType(typeof(Collection<FileInfo>))]
        public async Task<HttpResponseMessage> ListFiles(string FolderPath)
        {
            Trace.TraceInformation("ListFolder Called : {0} ", FolderPath);

            if (!this.IsConfigurationValid)
            {
                Trace.TraceError("Stacktrace :" + this.ErrorException.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, this.ErrorException);
            }

            try
            {
                FolderPath = Path.Combine(this.RootFolderPath, FolderPath).Replace("\\", "/");
                this.ValidateParameter(FolderPath, "FolderPath");
                return await this.ListTransportSpecificFileAsync(FolderPath);
            }
            catch (ArgumentException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        /// <remarks>
        /// Get File Data For Source Connector
        /// </remarks>
        /// <summary>
        /// Trigger On Folder
        /// </summary>
        /// <param name="triggerState">Specify Trigger State</param>
        /// <param name="FolderPath">Specify folder path</param>
        /// <param name="FileMask">Specify include mask</param>
        /// <param name="ExcludeFileMask">Specify exclude mask</param>
        /// <param name="FileType">The type of file: text or binary.</param>
        /// <returns></returns>
        /// <response code="200">Successful Poll operation</response>
        /// <response code="202">Successful Poll operation but no new file present</response>
        /// <response code="400">Bad Request</response>
        /// <response code="403">Forbidden</response>
        /// <response code="404">Folder not found</response>
        /// <response code="503">Service Unavailable</response>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "By Design"), SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "By Design")]
        [Route("poll/folder/{*folderpath}")]
        [ResponseType(typeof(File))]
        [HttpGet]
        public async Task<HttpResponseMessage> TriggerOnFileAvailable(string triggerState, string FolderPath, string FileMask = "*", string ExcludeFileMask = null, FileType FileType = FileType.Text)
        {
            Trace.TraceInformation("TriggerOnFolder Called : {0} , {1} ", FolderPath, triggerState);

            if (!this.IsConfigurationValid)
            {
                Trace.TraceError("Stacktrace :" + this.ErrorException.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, this.ErrorException);
            }

            try
            {
                this.SetRegex(FileMask, ExcludeFileMask);
            }
            catch (ArgumentException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }

            try
            {
                HttpResponseMessage deliveryHttpResponseMessage = await this.OnDeliveryCompletion(triggerState, FolderPath);

                if (!deliveryHttpResponseMessage.IsSuccessStatusCode)
                {
                    Trace.TraceWarning("Failed to delete the file from FTP Server {0}", deliveryHttpResponseMessage.StatusCode);
                    return deliveryHttpResponseMessage;
                }

                Tuple<File, HttpResponseMessage> getFileTupleWithResponse = await this.GetNextFile(FolderPath, FileType);
                File polledObject = getFileTupleWithResponse.Item1;
                HttpResponseMessage listResponseMessage = getFileTupleWithResponse.Item2;

                if (!listResponseMessage.IsSuccessStatusCode)
                {
                    Trace.TraceError("Failed to get file list from FTP Server  {0}", listResponseMessage.StatusCode);
                    return listResponseMessage;
                }

                if (polledObject != null)
                {
                    triggerState = polledObject.FileName;
                    Trace.TraceInformation("Returning  triggerstate {0}", triggerState);
                    return this.Request.EventTriggered(polledObject, triggerState, TimeSpan.Zero);
                }
                else
                {
                    triggerState = string.Empty;
                    Trace.TraceInformation("Returning  triggerstate {0}", triggerState);
                    return this.Request.EventWaitPoll(triggerState: triggerState);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Poll on folder {0} hit an exception.", FolderPath);
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        internal async Task<HttpResponseMessage> OnDeliveryCompletion(string fileName, string folderPath)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                HttpResponseMessage deletionResponse = await this.DeleteFile(System.IO.Path.Combine(folderPath, fileName));

                if (deletionResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    return deletionResponse;
                }
            }

            return this.Request.CreateResponse(HttpStatusCode.OK);
        }

        /// <summary>
        /// To set the regex
        /// </summary>
        /// <param name="fileIncludeMask"></param>
        /// <param name="fileExcludeMask"></param>
        internal void SetRegex(string fileIncludeMask, string fileExcludeMask)
        {
            if (string.IsNullOrEmpty(fileIncludeMask))
            {
                fileIncludeMask = "*";
            }

            // The side effect of converting fileMask to regex is that the user now can use explicit regex features within the fileMask.
            // For Ex: *.[xX][mM][lL] is a valid fileMask according to below logic.
            this.includeRegex = new Regex('^' + fileIncludeMask.Replace(".", "[.]").Replace("*", ".*").Replace("?", ".") + '$', RegexOptions.IgnoreCase);

            if (!string.IsNullOrEmpty(fileExcludeMask))
            {
                this.excludeRegex = new Regex('^' + fileExcludeMask.Replace(".", "[.]").Replace("*", ".*").Replace("?", ".") + '$', RegexOptions.IgnoreCase);
            }
        }

        /// <summary>
        /// Check regular expression
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        internal bool CheckRegex(string file)
        {
            bool checkExcludeRegex = true;
            bool checkIncludeRegex = true;

            if (this.excludeRegex != null && this.excludeRegex.IsMatch(file))
            {
                checkExcludeRegex = false;
            }

            if (this.includeRegex != null && !this.includeRegex.IsMatch(file))
            {
                checkIncludeRegex = false;
            }

            return checkExcludeRegex && checkIncludeRegex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="FileType"></param>
        /// <returns></returns>
        protected async Task<Tuple<File, HttpResponseMessage>> GetNextFile(string path, FileType FileType)
        {
            TransportFetcher fetcher = new TransportFetcher(this, path);
            Tuple<IList<string>, HttpResponseMessage> listingResponseTuple = await fetcher.OnListing();
            IList<string> listOfFiles = listingResponseTuple.Item1;
            HttpResponseMessage message = listingResponseTuple.Item2;

            foreach (string fileName in listOfFiles)
            {
                File fileDescription = await fetcher.GetResource(fileName, FileType);

                if (fileDescription != null)
                {
                    return new Tuple<File, HttpResponseMessage>(fileDescription, message);
                }
            }

            return new Tuple<File, HttpResponseMessage>(null, message);
        }

        /// <summary>
        /// Upload Abstract Function
        /// </summary>
        /// <param name="content"></param>
        /// <param name="folderPath"></param>
        /// <param name="fileName"></param>
        /// <param name="appendIfExist"></param>
        /// <param name="temporaryFolder"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed", Justification = "By Design")]
        protected abstract Task<HttpResponseMessage> UploadTransportSpecificFileAsync(FileContent content, string folderPath, string fileName, bool appendIfExist = false, string temporaryFolder = null);

        /// <summary>
        /// Delete Abstract Function
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        protected abstract Task<HttpResponseMessage> DeleteTransportSpecificFileAsync(string folderPath, string fileName);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="fileName"></param>
        /// <param name="contentTransferEncoding"></param>
        /// <returns></returns>
        protected abstract Task<HttpResponseMessage> GetTransportSpecificFileAsync(string folderPath, string fileName, ContentTransferEncoding contentTransferEncoding);

        /// <summary>
        /// List Abstract Function
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        protected abstract Task<HttpResponseMessage> ListTransportSpecificFileAsync(string folderPath);

        /// <summary>
        /// For validation
        /// </summary>
        /// <param name="parameterValue"></param>
        /// <param name="parameterName"></param>
        protected abstract void ValidateParameter(string parameterValue, string parameterName);
    }
}