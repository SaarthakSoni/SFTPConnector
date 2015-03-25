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
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web.Http;
    using System.Web.Http.Description;
    using Microsoft.Azure.AppService.ApiApps.Service;
    using Newtonsoft.Json;
    using SFTPConnector.Messaging;
    using SftpConnectorMicroservice;
    using TC = SFTPConnector;
    using WinSCP;

    /// <summary>
    /// Encryption Cipher
    /// </summary>
    public enum EncryptionCipher
    {
#pragma warning disable 1591
        Auto,
        Aes,
        TripleDes,
        Blowfish,
        Arcfour,
        Des
#pragma warning restore 1591
    }

    /// <summary>
    /// Connects to SFTP Server
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces", Justification = "By design"), SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "SFTP", Justification = "By design")]
    public class SFTPController : TC.BaseTransportController
    {
        private const ushort MaxPortRange = 65535;
        private const string DefaultEncryptionCipher = "aes,3des,blowfish,WARN,arcfour,des";
        private const string AuthenticationDetailsBlockName = "authenticationdetails";

        // This constant is added as winscp needs SshServerHostKey in a particular format even if AcceptAnySshServerHostKey is true.
        private const string SshServerHostKey = "ssh-rsa 2048 40:c5:8a:f7:9e:53:44:fc:f4:ae:3e:a3:48:bb:ec:d2";
        private IList<string> validationErrors;

        /// <summary>
        /// Constructor
        /// </summary>
        public SFTPController()
            : base(true)
        {
            this.validationErrors = new List<string>();

            try
            {
                this.ServerAddress = ConfigurationManager.AppSettings["SftpServerAddress"];

                this.ServerPort = (ushort)(string.IsNullOrEmpty(ConfigurationManager.AppSettings["SftpServerPort"]) ? 22 :
                    Convert.ToUInt16(ConfigurationManager.AppSettings["SftpServerPort"], CultureInfo.InvariantCulture));

                this.AcceptAnySSHServerHostKey =
                    string.IsNullOrEmpty(ConfigurationManager.AppSettings["AcceptAnySSHServerHostKey"]) ? false :
                    bool.Parse(ConfigurationManager.AppSettings["AcceptAnySSHServerHostKey"]);

                this.EncryptCipher =
                    string.IsNullOrEmpty(ConfigurationManager.AppSettings["EncryptCipher"]) ? EncryptionCipher.Auto :
                    (EncryptionCipher)Enum.Parse(typeof(EncryptionCipher), ConfigurationManager.AppSettings["EncryptCipher"], true);
            }
            catch (OverflowException ex)
            {
                this.IsConfigurationValid = false;
                this.ErrorException = new ArgumentException(Resources.SftpAdapter_OutOfRangePort, ex);
            }
            catch (Exception ex)
            {
                this.IsConfigurationValid = false;
                this.ErrorException = ex;
            }

            if (this.AcceptAnySSHServerHostKey)
            {
                this.SSHServerHostKey = SshServerHostKey;
            }
            else
            {
                this.SSHServerHostKey = ConfigurationManager.AppSettings["SSHServerHostKey"];
            }

            var task = Task.Run(async () => await this.InitializeAuthenticationDetails());
            task.Wait();
        }

        /// <summary>
        /// Add or update authentication details.
        /// </summary>
        /// <returns>HttpStatusCode.OK is returned if ppk file is successfully added to the isolated storage.
        /// Otherwise HttpStatusCode.BadRequest or HttpStatusCode.InternalServerError is returned.</returns>
        /// <response code="200">PPK file is successfully uploaded into the isolated storage</response>
        /// <response code="400">BadRequest</response>
        /// <response code="500">Internal Server Error</response>
        [HttpPost]
        [ActionName("AddAuthenticationDetails")]
        [ResponseType(typeof(void))]
        [Obsolete]
        public async Task<HttpResponseMessage> AddAuthenticationDetails(AuthenticationDetails authenticationDetails)
        {
            if (authenticationDetails == null)
            {
                Trace.TraceError("AuthenticationDetails are empty");
                return this.Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    string.Format(CultureInfo.InvariantCulture, Resources.InvalidAuthenticationDetails, "AuthenticationDetails are empty!"));
            }

            string errors;

            if (!this.ValidateAuthenticationDetails(authenticationDetails, out errors))
            {
                Trace.TraceError(errors);
                return this.Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    string.Format(CultureInfo.InvariantCulture, Resources.InvalidAuthenticationDetails, errors));
            }

            try
            {
                Runtime runtime = Runtime.FromAppSettings();

                await runtime.IsolatedStorage.WriteAsync(AuthenticationDetailsBlockName, JsonConvert.SerializeObject(authenticationDetails));
                return this.Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return this.Request.CreateResponse(HttpStatusCode.InternalServerError, ex);
            }
        }

        /// <summary>
        /// Gets the authentication details set.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ActionName("GetAuthenticationDetails")]
        [ResponseType(typeof(AuthenticationDetails))]
        [Obsolete]
        public async Task<HttpResponseMessage> GetAuthenticationDetails()
        {
            Runtime runtime = Runtime.FromAppSettings();

            using (var authenticationDetailsStream = await runtime.IsolatedStorage.OpenReadAsync(AuthenticationDetailsBlockName))
            {
                if (authenticationDetailsStream == null)
                {
                    // TODO- Authentication details are not configured yet, but returning empty authentication details(as UX code has issue handling nulls!)
                    return this.Request.CreateResponse(HttpStatusCode.OK, new AuthenticationDetails());
                }

                AuthenticationDetails authenticationDetails;
                using (var reader = new StreamReader(authenticationDetailsStream, Encoding.UTF8))
                {
                    authenticationDetails = JsonConvert.DeserializeObject<AuthenticationDetails>(reader.ReadToEnd());
                }

                return this.Request.CreateResponse(HttpStatusCode.OK, authenticationDetails);
            }
        }

        /// <summary>
        /// Required. Determines if any SSH public host key fingerprint from the Server should be accepted. If set to false, the host key will be matched against the key specified in the “SSH Server Host Key Finger Print” property 
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", Justification = "The identifier represents a proper name")]
        public bool AcceptAnySSHServerHostKey { get; set; }

        /// <summary>
        /// Optional. Specify the fingerprint of the public host key for the SSH server.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", Justification = "The identifier represents a proper name")]
        public string SSHServerHostKey { get; set; }

        /// <summary>
        /// Authentication Type.
        /// </summary>
        public AuthenticationType AuthenticationType { get; set; }

        /// <summary>
        /// Optional. Specify the private Key of the user.
        /// </summary>
        public string PrivateKeyFilePath { get; set; }

        /// <summary>
        /// Optional. Specify the password if the user’s private key is protected by a password.
        /// </summary>
        public string PrivateKeyPassword { get; set; }

        /// <summary>
        /// Optional. Specify the encryption cipher.
        /// </summary>
        public EncryptionCipher EncryptCipher { get; set; }

        #region DestinationTransportSpecificMethods

        /// <summary>
        /// Upload Util Method for SFTP
        /// </summary>
        /// <param name="content"></param>
        /// <param name="folderPath"></param>
        /// <param name="fileName"></param>
        /// <param name="appendIfExist"></param>
        /// <param name="temporaryFolder"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> UploadTransportSpecificFileAsync(FileContent content, string folderPath, string fileName, bool appendIfExist, string temporaryFolder)
        {
            if (!string.IsNullOrWhiteSpace(temporaryFolder) && appendIfExist)
            {
                throw new ArgumentException(Resources.InvalidOperationWithTemporaryFolder, "temporaryFolder");
            }

            if (!this.ValidateController())
            {
                return this.Request.CreateResponse(HttpStatusCode.BadRequest, this.validationErrors);
            }

            string path = folderPath;
            string temporaryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) + "\\";

            try
            {
                Directory.CreateDirectory(temporaryPath);

                using (Session session = new Session())
                {
                    HttpResponseMessage response;
                    if ((response = this.OpenSession(session)).StatusCode != HttpStatusCode.OK)
                    {
                        return response;
                    }

                    if (string.IsNullOrWhiteSpace(temporaryFolder))
                    {
                        await this.UploadToFolder(session, content.GetStream(), folderPath, fileName, temporaryPath, appendIfExist);
                    }
                    else
                    {
                        path = temporaryFolder;
                        await this.UploadToFolder(session, content.GetStream(), temporaryFolder, fileName, temporaryPath);

                        string tempPath = temporaryFolder + "/" + fileName;
                        string destinationPath = folderPath + "/" + fileName;

                        if (session.FileExists(destinationPath))
                        {
                            session.RemoveFiles(destinationPath);
                        }

                        try
                        {
                            session.MoveFile(tempPath, destinationPath);
                        }
                        catch (SessionRemoteException ex)
                        {
                            Trace.TraceError("Stacktrace :" + ex.StackTrace);
                            return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format(
                                CultureInfo.InvariantCulture,
                                Resources.SftpAdapter_NotFound, destinationPath), ex);
                        }
                    }
                }

                TC.FileInfo fileInfo = new TC.FileInfo(this.ServerAddress, folderPath, fileName);
                Trace.TraceInformation("UploadFile returning successfully, {0} , {1} ", folderPath, fileName);
                return Request.CreateResponse(fileInfo);
            }
            catch (SessionRemoteException ex)
            {
                return GetErrorResponseMessage(ex, path);
            }
            catch (PathTooLongException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (IOException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);
                return Request.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, Resources.ServiceUnavailable);
            }
            finally
            {
                Directory.Delete(temporaryPath, true);
            }
        }

        /// <summary>
        /// Delete Util Method for SFTP
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        protected override Task<HttpResponseMessage> DeleteTransportSpecificFileAsync(string folderPath, string fileName)
        {
            string path = folderPath + "/" + fileName;
            return Task.Factory.StartNew(() =>
            {
                if (!this.ValidateController())
                {
                    return this.Request.CreateResponse(HttpStatusCode.BadRequest, this.validationErrors);
                }

                try
                {
                    using (Session session = new Session())
                    {
                        HttpResponseMessage response;
                        if ((response = this.OpenSession(session)).StatusCode != HttpStatusCode.OK)
                        {
                            return response;
                        }

                        session.RemoveFiles(path).Check();
                    }

                    Trace.TraceInformation("DeleteFile returning successfully, {0} , {1} ", folderPath, fileName);
                    return Request.CreateResponse(HttpStatusCode.OK, Resources.SuccessfulDelete);
                }
                catch (SessionRemoteException ex)
                {
                    return GetErrorResponseMessage(ex, path);
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="fileName"></param>
        /// <param name="contentTransferEncoding"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "The disposable object is returned.")]
        protected override Task<HttpResponseMessage> GetTransportSpecificFileAsync(string folderPath, string fileName, ContentTransferEncoding contentTransferEncoding)
        {
            string remotePath = folderPath + "/" + fileName;
            string temporaryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) + "\\";

            return Task.Factory.StartNew(() =>
            {
                if (!this.ValidateController())
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, this.validationErrors);
                }

                try
                {
                    Directory.CreateDirectory(temporaryPath);

                    using (Session session = new Session())
                    {
                        HttpResponseMessage response;
                        if ((response = this.OpenSession(session)).StatusCode != HttpStatusCode.OK)
                        {
                            return response;
                        }

                        session.GetFiles(remotePath, temporaryPath).Check();
                    }

                    TC.FileInfo fileInfo = new TC.FileInfo(this.ServerAddress, folderPath, fileName);
                    TC.File message;

                    // Todo : Change MemoryStream to VirtualFileStream 
                    using (MemoryStream stream = new MemoryStream())
                    {
                        using (FileStream fileStream = System.IO.File.OpenRead(temporaryPath + "/" + fileName))
                        {
                            stream.SetLength(fileStream.Length);
                            fileStream.Read(stream.GetBuffer(), 0, (int)fileStream.Length);
                        }

                        stream.Position = 0;
                        message = new TC.File(stream, fileInfo, contentTransferEncoding);
                    }

                    Trace.TraceInformation("GetFile returning successfully, {0} , {1} ", folderPath, fileName);
                    return Request.CreateResponse(message);
                }
                catch (SessionRemoteException ex)
                {
                    return GetErrorResponseMessage(ex, remotePath);
                }
                catch (IOException ex)
                {
                    Trace.TraceError("Stacktrace :" + ex.StackTrace);
                    return Request.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, Resources.ServiceUnavailable);
                }
                finally
                {
                    Directory.Delete(temporaryPath, true);
                }
            });
        }

        /// <summary>
        ///  List Util Method for SFTP
        /// </summary>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "By design")]
        protected override Task<HttpResponseMessage> ListTransportSpecificFileAsync(string folderPath)
        {
            Collection<TC.FileInfo> files = new Collection<TC.FileInfo>();
            return Task.Factory.StartNew(() =>
            {
                if (!this.ValidateController())
                {
                    return this.Request.CreateResponse(HttpStatusCode.BadRequest, this.validationErrors);
                }

                try
                {
                    using (Session session = new Session())
                    {
                        HttpResponseMessage response;
                        if ((response = this.OpenSession(session)).StatusCode != HttpStatusCode.OK)
                        {
                            return response;
                        }

                        RemoteDirectoryInfo remoteDirectoryInfo = session.ListDirectory(folderPath);

                        for (int i = 0; i < remoteDirectoryInfo.Files.Count; i++)
                        {
                            string fileName = remoteDirectoryInfo.Files[i].Name;

                            if (!fileName.Equals(".") && !fileName.Equals(".."))
                            {
                                TC.FileInfo info = new TC.FileInfo(this.ServerAddress, folderPath, fileName);
                                files.Add(info);
                            }
                        }
                    }

                    Trace.TraceInformation("ListFile returning successfully, {0} ", folderPath);
                    return Request.CreateResponse(files);
                }
                catch (SessionRemoteException ex)
                {
                    return GetErrorResponseMessage(ex, folderPath);
                }
            });
        }

        #endregion DestinationTransportSpecificMethods

        /// <summary>
        /// To Validate Parameter
        /// </summary>
        /// <param name="parameterValue"></param>
        /// <param name="parameterName"></param>
        protected override void ValidateParameter(string parameterValue, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterValue))
            {
                throw new ArgumentException(Resources.IsNullOrWhiteSpace, parameterName);
            }
        }

        private bool ValidateController()
        {
            bool isPrivateKeyNull = string.IsNullOrWhiteSpace(this.PrivateKeyFilePath);
            bool isPrivateKeyPasswordNull = string.IsNullOrWhiteSpace(this.PrivateKeyPassword);
            string errorMessage;

            if (string.IsNullOrWhiteSpace(this.ServerAddress))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture, "Server Address {0}", Resources.IsNullOrWhiteSpace);
                this.validationErrors.Add(errorMessage);
                Trace.TraceError(errorMessage);
            }

            if (this.ServerPort < 0 || this.ServerPort > MaxPortRange)
            {
                errorMessage = Resources.SftpAdapter_OutOfRangePort;
                this.validationErrors.Add(errorMessage);
                Trace.TraceError(errorMessage);
            }

            if (string.IsNullOrWhiteSpace(this.UserName))
            {
                errorMessage = string.Format(CultureInfo.InvariantCulture, "User Name {0}", Resources.IsNullOrWhiteSpace);
                this.validationErrors.Add(errorMessage);
                Trace.TraceError(errorMessage);
            }

            if (isPrivateKeyNull && !isPrivateKeyPasswordNull)
            {
                errorMessage = Resources.SftpAdapter_PrivateKeyPasswordNull;
                this.validationErrors.Add(errorMessage);
                Trace.TraceError(errorMessage);
            }

            if (!this.AcceptAnySSHServerHostKey && string.IsNullOrWhiteSpace(this.SSHServerHostKey))
            {
                errorMessage = Resources.SftpAdapter_AcceptAnySSHServerHostKeyNull;
                this.validationErrors.Add(errorMessage);
                Trace.TraceError(errorMessage);
            }

            return this.validationErrors.Count == 0;
        }

        /// <summary>
        /// Initialize the authentication details(reading from isolated storage).
        /// </summary>
        /// <returns></returns>
        private async Task InitializeAuthenticationDetails()
        {
            AuthenticationDetails authenticationDetails;
            Runtime runtime = Runtime.FromAppSettings();

            using (var authenticationDetailsStream = await runtime.IsolatedStorage.OpenReadAsync(AuthenticationDetailsBlockName))
            {
                if (authenticationDetailsStream == null)
                {
                    // SFTP connector authentication details are not initialized.
                    return;
                }

                using (var reader = new StreamReader(authenticationDetailsStream, Encoding.UTF8))
                {
                    authenticationDetails = JsonConvert.DeserializeObject<AuthenticationDetails>(reader.ReadToEnd());
                }
            }

            AuthenticationType authType;

            if (Enum.TryParse(authenticationDetails.AuthenticationType, out authType))
            {
                this.AuthenticationType = authType;
            }
            else
            {
                return;
            }

            this.UserName = authenticationDetails.UserName;

            if (this.AuthenticationType == AuthenticationType.Password || this.AuthenticationType == AuthenticationType.MultiFactor)
            {
                this.Password = authenticationDetails.Password;
            }

            if (this.AuthenticationType == AuthenticationType.PrivateKey || this.AuthenticationType == AuthenticationType.MultiFactor)
            {
                string temporaryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()) + "\\";
                Directory.CreateDirectory(temporaryPath);
                temporaryPath = temporaryPath + AuthenticationDetailsBlockName;

                byte[] bytes = Convert.FromBase64String(authenticationDetails.Content);
                System.IO.File.WriteAllBytes(temporaryPath, bytes);
                this.PrivateKeyFilePath = temporaryPath;
                this.PrivateKeyPassword = authenticationDetails.PpkFilePassword;
            }
        }

        private HttpResponseMessage GetErrorResponseMessage(SessionRemoteException ex, string path)
        {
            Trace.TraceError("Stacktrace :" + ex.StackTrace);

            //Message = "Permission denied.\nError code: 3\nError message from server: Permission denied"
            if (ex.InnerException != null && ex.InnerException.Message.ToLower().Contains("denied"))
            {
                return Request.CreateErrorResponse(HttpStatusCode.Forbidden, ex.InnerException.Message, ex);
            }

            //Message = "No such file or directory.\nError code: 2\nError message from server: No such file"	
            return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format(
                CultureInfo.InvariantCulture,
                Resources.SftpAdapter_NotFound, path), ex);
        }

        private SessionOptions GetSessionOptions()
        {
            SessionOptions sessionOptions = new SessionOptions
            {
                Protocol = Protocol.Sftp,
                HostName = this.ServerAddress,
                UserName = this.UserName,
                Password = this.Password,
                SshPrivateKeyPath = this.PrivateKeyFilePath,
                SshPrivateKeyPassphrase = this.PrivateKeyPassword,
                GiveUpSecurityAndAcceptAnySshHostKey = this.AcceptAnySSHServerHostKey,
                SshHostKeyFingerprint = this.SSHServerHostKey
            };

            if (this.EncryptCipher.Equals(EncryptionCipher.Auto))
            {
                sessionOptions.AddRawSettings("Cipher", DefaultEncryptionCipher);
            }
            else if (this.EncryptCipher.Equals(EncryptionCipher.TripleDes))
            {
                sessionOptions.AddRawSettings("Cipher", "3des,WARN");
            }
            else
            {
                sessionOptions.AddRawSettings("Cipher", this.EncryptCipher.ToString().ToLower() + ",WARN");
            }

            return sessionOptions;
        }

        private async Task UploadToFolder(Session session, Stream content, string folderPath, string fileName, string temporaryPath, bool appendIfExist = false)
        {
            string remotePath = folderPath + "/" + fileName;

            if (appendIfExist && session.FileExists(remotePath))
            {
                session.GetFiles(remotePath, temporaryPath).Check();
            }

            using (FileStream fs = new FileStream(Path.Combine(temporaryPath, fileName), FileMode.Append))
            {
                await content.CopyToAsync(fs);
            }

            session.PutFiles(localPath: temporaryPath + "/" + fileName, remotePath: remotePath, remove: true, options: null).Check();
        }

        private HttpResponseMessage OpenSession(Session session)
        {
            try
            {
                SessionOptions sessionOptions = this.GetSessionOptions();
                session.Open(sessionOptions);
                return this.Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (SessionRemoteException ex)
            {
                Trace.TraceError("Stacktrace :" + ex.StackTrace);

                // Todo : WinSCP has a bug when a cipher given by user is not supported by server, it will throw exception with message "Invalid access to memory"
                // We will remove the generic message Resources.ConnectionFailed when that bug is resolved.
                return this.Request.CreateErrorResponse(HttpStatusCode.BadRequest, Resources.ConnectionFailed, ex);
            }
        }

        private bool ValidateAuthenticationDetails(AuthenticationDetails authenticationDetails, out string errors)
        {
            errors = string.Empty;

            if (string.IsNullOrWhiteSpace(authenticationDetails.UserName))
            {
                errors = string.Format("AuthenticationType is {0},UserName is Null!", authenticationDetails.AuthenticationType);
                return false;
            }

            AuthenticationType authType = (AuthenticationType)Enum.Parse(typeof(AuthenticationType), authenticationDetails.AuthenticationType);

            if (authType == AuthenticationType.Password)
            {
                if (string.IsNullOrWhiteSpace(authenticationDetails.Password))
                {
                    errors = string.Format("AuthenticationType is {0}, Password is Null!", authenticationDetails.AuthenticationType);
                    return false;
                }
            }

            if (authType == AuthenticationType.PrivateKey)
            {
                if (string.IsNullOrWhiteSpace(authenticationDetails.Content) || string.IsNullOrWhiteSpace(authenticationDetails.PpkFileName))
                {
                    errors = string.Format("AuthenticationType is {0},PPK file content or PPK file name is Null!", authenticationDetails.AuthenticationType);
                    return false;
                }
            }

            if (authType == AuthenticationType.MultiFactor)
            {
                if (string.IsNullOrWhiteSpace(authenticationDetails.Password)
                    || string.IsNullOrWhiteSpace(authenticationDetails.Content)
                    || string.IsNullOrWhiteSpace(authenticationDetails.PpkFileName))
                {
                    errors = string.Format("AuthenticationType is {0},Password/PPKFileContent/PPK file name is Null!", authenticationDetails.AuthenticationType);
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Authentication type used to connect through WinSCP
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// UserName/Password based authentication.
        /// </summary>
        Password,

        /// <summary>
        /// PPK file/Password based authentication.
        /// </summary>
        PrivateKey,

        /// <summary>
        /// Both UserName/Password and PPKFile/Password based authentication.
        /// </summary>
        MultiFactor
    };
}