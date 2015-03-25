
using System.Security.Cryptography;

namespace SftpConnectorTest.Tests
{
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using FileBasedProtocolConnectorTest.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using SFTPConnector;
    using System;
    using System.Collections.Specialized;
    using System.Net;

    [TestClass]
    [DeploymentItem("rsa2048.ppk")]
    [DeploymentItem("MultiFactorAuth.ppk")]
    [DeploymentItem("SftpConnectorMicroservice", "SftpConnectorMicroservice")]
    [DeploymentItem("FileBasedTestArtifacts", "FileBasedTestArtifacts")]
    public class SFTPConnectorTest
    {
        private static BaseTransportDestinationTestHelper<SFTPController> baseTransportDestinationTestHelper;
        private static BaseTransportDestinationTestHelper<SFTPController> baseTransportDestTestHelperAbsPath;
        private static BaseTransportDestinationTestHelper<SFTPController> baseTransportDestinationTestHelperForMultiFact;
        private IFTPConnectorTest sftpConnectorTest;
        bool isControllerTest = false;
        private const string JsonContentType = "application/json";
        private static string MultiFactorAuthPpkResourceName = typeof(SFTPConnectorTest).Namespace + ".MultiFactorAuth.ppk";
        private static string Rsa2048ResourceName = typeof(SFTPConnectorTest).Namespace + ".rsa2048.ppk";
        public TestContext TestContext { get; set; }
        private string rootFolder;
        
        public SFTPConnectorTest()
        {
        }

        [TestInitialize]
        public void TestInitliaze()
        {
            if (TestContext.TestName == "SftpPollAsyncPositive")
            {
                rootFolder = Guid.NewGuid().ToString();
            }
            else
            {
                rootFolder = string.Empty;
            }

            baseTransportDestinationTestHelper = new BaseTransportDestinationTestHelper<SFTPController>(true, rootfolderpath:rootFolder);
            baseTransportDestinationTestHelperForMultiFact = new BaseTransportDestinationTestHelper<SFTPController>(true, true, true, rootfolderpath: rootFolder);
            FtpUtil ftpUtil = new FtpUtil(true);

            if (isControllerTest)
            {
                (baseTransportDestinationTestHelper.ftpConnectorTest as FileBasedProtocolConnectorControllerTest<SFTPController>).controller.AcceptAnySSHServerHostKey = true;
                sftpConnectorTest = new FileBasedProtocolConnectorControllerTest<SFTPController>(true);
                (sftpConnectorTest as FileBasedProtocolConnectorControllerTest<SFTPController>).controller.AcceptAnySSHServerHostKey = true;
            }
            else
            {
                sftpConnectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(ftpUtil.GetNameValueCollection(false, true));
            }
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpAddGetAuthenticationDetailsTest()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, "DummyUser", "DummyPassword", MultiFactorAuthPpkResourceName, "rsa2048");
            
            var authDetails = this.GetAuthenticationDetailsFromIsolatedStorage();
            Assert.IsTrue(authDetails != null);
            Assert.IsTrue(authDetails.UserName.Equals("DummyUser") && authDetails.Password.Equals("DummyPassword"));
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForServerAddress()
        {
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "false");
            namevalue.Set("SftpServerAddress", string.Empty);

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, "rsa2048");
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.ListFile(folderPath);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForSSHServerHostKey()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "false");

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, "rsa2048");
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.GetFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForInvalidSSHServerHostKey()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "false");
            namevalue.Add("SSHServerHostKey", Guid.NewGuid().ToString());

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, "rsa2048");
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.GetFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForEmptyUserName()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Add("FtpUserName", string.Empty);

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, "rsa2048");
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.DeleteFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForVerifyPrivateKey()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);
            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Add("PrivateKey", Environment.CurrentDirectory + "/rsa2048.ppk");
            namevalue.Add("PrivateKeyPassword", Guid.NewGuid().ToString());
            namevalue.Add("FtpPassword", Guid.NewGuid().ToString());

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, namevalue["FtpUserName"], namevalue["FtpPassword"], Rsa2048ResourceName, namevalue["PrivateKeyPassword"]);
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.GetFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidatePositiveTestForVerifyPrivateKey()
        {
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);
            FTPTestHelper ftpTestHelper = new FTPTestHelper(true);
            ftpTestHelper.CreateFolder(folderPath);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("PrivateKey", Environment.CurrentDirectory + "\\rsa2048.ppk");
            namevalue.Add("PrivateKeyPassword", ftpUtil.SftpPrivateKeyPassword);
            namevalue.Set("FtpPassword", Guid.NewGuid().ToString());
            namevalue.Add("AcceptAnySSHServerHostKey", "true");

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, namevalue["FtpUserName"], namevalue["FtpPassword"], Rsa2048ResourceName, namevalue["PrivateKeyPassword"]);
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.ListFile(folderPath);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForVerifyPrivateKeyMultiFactAuthServer()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true, true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, ftpUtil.SftpPrivateKeyPassword);

            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue, true);

            var message = connectorTest.GetFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForVerifyPrivateKeyMultiFactAuthServerInvalidUser()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true, true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Add("PrivateKey", Environment.CurrentDirectory + "/MultiFactorAuth.ppk");

            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.GetFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidatePositiveTestForVerifyPrivateKeyMultiFactAuthServer()
        {
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true, true);
            FTPTestHelper ftpTestHelper = new FTPTestHelper(true, true);
            ftpTestHelper.CreateFolder(folderPath);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Add("PrivateKey", Environment.CurrentDirectory + "\\MultiFactorAuth.ppk");
            namevalue.Add("PrivateKeyPassword", ftpUtil.SftpPrivateKeyPassword);

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, ftpUtil.SftpPrivateKeyPassword);
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue, true);

            var message = connectorTest.ListFile(folderPath);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForInvalidPortGreaterThanRange()
        {
            baseTransportDestinationTestHelper.BaseValidateNegativeTestForInvalidPortGreaterThanRange();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForInvalidPortLesserThanRange()
        {
            baseTransportDestinationTestHelper.BaseValidateNegativeTestForInvalidPortLesserThanRange();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForInvalidPort()
        {
            baseTransportDestinationTestHelper.BaseValidateNegativeTestForInvalidPort();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForInvalidAcceptAnySSHServerHostKey()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", Guid.NewGuid().ToString());

            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.DeleteFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestForInvalidEncryptCipher()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("EncryptCipher", Guid.NewGuid().ToString());

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, ftpUtil.SftpPrivateKeyPassword);
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.DeleteFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeInvalidUsername()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Set("FtpUserName", Guid.NewGuid().ToString());

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, namevalue["PrivateKeyPassword"]);
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.UploadFile(Constants.UploadContents, folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidateNegativeTestInvalidCipher()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Add("EncryptCipher", "Des");

            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);
            var message = connectorTest.UploadFile(Constants.UploadContents, folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidatePositiveTestValidTripleDesCipher()
        {
            string folderPath = Guid.NewGuid().ToString();
            FTPTestHelper ftpTestHelper = new FTPTestHelper(true);
            ftpTestHelper.CreateFolder(folderPath);
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Add("EncryptCipher", "TripleDes");

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, ftpUtil.SftpPrivateKeyPassword);
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.ListFile(folderPath);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, "Could not validate correctly");
        }


        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpValidatePositiveTestValidAesCipher()
        {
            string folderPath = Guid.NewGuid().ToString();
            FTPTestHelper ftpTestHelper = new FTPTestHelper(true);
            ftpTestHelper.CreateFolder(folderPath);
            FtpUtil ftpUtil = new FtpUtil(true);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(false, true);
            namevalue.Add("AcceptAnySSHServerHostKey", "true");
            namevalue.Add("EncryptCipher", "Aes");

            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, namevalue["FtpUserName"], namevalue["FtpPassword"], MultiFactorAuthPpkResourceName, ftpUtil.SftpPrivateKeyPassword);
            IFTPConnectorTest connectorTest = new FileBasedProtocolConnectorClientTest<SFTPController>(namevalue);

            var message = connectorTest.ListFile(folderPath);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollAsyncPositive()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncPositive(rootFolder);
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollAsyncEmptyFolderPositive()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncEmptyFolderPositive();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncWithRegexPositive()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncWithRegexPositive();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncPositiveWithMultipleFiles()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncPositiveWithMultipleFiles();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncNegativeAbandonMessage()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncNegativeAbandonMessage();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncNegativeWithRegex()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncNegativeWithRegex();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncNegativeFolderDoesNotExist()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncNegativeFolderDoesNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncNegativeTriggerFileDoesNotExist()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncNegativeTriggerFileDoesNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncNegativeTriggerFileDoesNotExistNoFileUploaded()
        {
            baseTransportDestinationTestHelper.BasePollFileAsyncNegativeTriggerFileDoesNotExistNoFileUploaded();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpPollFileAsyncNegativeUnauthorized()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "randomusername", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            BaseTransportDestinationTestHelper<SFTPController> newbaseTransportDestinationTestHelper = new BaseTransportDestinationTestHelper<SFTPController>(true, true, false, wrongusername: true);

            newbaseTransportDestinationTestHelper.BasePollFileAsyncNegativeUnAuthorized();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveAppendIfExistNotSpecified()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveAppendIfExistNotSpecified();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveArtifacts()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveArtifacts();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveAppendIfExistTrueFileIsPresent()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveAppendIfExistTrueFileIsPresent();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveAppendIfExistTrueFileIsNotPresent()
        {
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveAppendIfExistTrueFileIsNotPresent();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveAppendIfExistFalseFileIsPresent()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveAppendIfExistFalseFileIsPresent();
        }

        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveBigFile()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveBigFile();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveTemporaryFolderExist()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveTemporaryFolderExist();
        }

        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveTemporaryFolderBigFile()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveTemporaryFolderBigFile();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveTempFolderAppendFalseFileExistInDestFolder()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveTempFolderAppendFalseFileExistInDestFolder();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveTemporaryFolderIsGivenAppendIsFalse()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveTemporaryFolderIsGivenAppendIsFalse();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveAppendIfExistTrueFileIsNotPresentFilenameWithDot()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveAppendIfExistTrueFileIsNotPresentFilenameWithDot();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveWithAbsolutePath()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestTestHelperAbsPath = new BaseTransportDestinationTestHelper<SFTPController>(true, rootfolderpath : "/");
            if (isControllerTest)
            {
                (baseTransportDestTestHelperAbsPath.ftpConnectorTest as FileBasedProtocolConnectorControllerTest<SFTPController>).controller.AcceptAnySSHServerHostKey = true;
            }

            baseTransportDestTestHelperAbsPath.BaseUploadFileAsyncPositiveWithAbsolutePath();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        [Description("Method to test if UploadFileAsync works or not if absolute path is given for both temporary and destination folder and file already exist in destination folder.")]
        public void SftpUploadFileAsyncPositiveTempFolderAppendFalseFileExistInDestFolderAbsolutePath()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestTestHelperAbsPath = new BaseTransportDestinationTestHelper<SFTPController>(true, rootfolderpath: "/");
            if (isControllerTest)
            {
                (baseTransportDestTestHelperAbsPath.ftpConnectorTest as FileBasedProtocolConnectorControllerTest<SFTPController>).controller.AcceptAnySSHServerHostKey = true;
            }

            baseTransportDestTestHelperAbsPath.BaseUploadFileAsyncPositiveTempFolderAppendFalseFileExistInDestFolderAbsolutePath();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveWithEmptyContent()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncPositiveWithEmptyContent();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncNegativeFileNameExceedLength()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncNegativeFileNameExceedLength();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncNegativeContentIsNull()
        {
            baseTransportDestinationTestHelper.BaseUploadFileAsyncNegativeContentIsNull();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncNegativeWhenContentIsNotBase64()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncNegativeWhenContentIsNotBase64();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncNegativeInvalidCharInFilename()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncNegativeInvalidCharInFilename();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncNegativeTemporaryFolderNotExist()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFileAsyncNegativeTemporaryFolderNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFilesAsyncNegativeIfFolderNotExist()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseUploadFilesAsyncNegativeIfFolderNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpDeleteFileAsyncPositive()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseDeleteFileAsyncPositive();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpDeleteFileAsyncNegativeFileNotAccesible()
        {
            // "testsftp" username account is already created with password as "testsftp"
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "testsftp", "testsftp", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestTestHelperAbsPath = new BaseTransportDestinationTestHelper<SFTPController>(true, rootfolderpath: "/home/azureuser");

            baseTransportDestTestHelperAbsPath.BaseDeleteFileAsyncNegativeFileNotAccesible();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpDeleteFileAsyncNegativeIfFileNotExist()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseDeleteFileAsyncNegativeFileNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpDeleteFileAsyncNegativeIfFolderNotExist()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseDeleteFileAsyncNegativeFolderNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpGetFileAsyncPositive()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseGetFileAsyncPositive();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpGetFileAsyncNegativeIfFileNotExist()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseGetFileAsyncNegativeIfFileNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpGetFileAsyncNegativeIfFolderNotExist()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseGetFileAsyncNegativeIfFolderNotExist();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpListFileAsyncPositive()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseListFileAsyncPositive();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpListFileAsyncPositivePathHasSlash()
        {
            baseTransportDestinationTestHelper.BaseListFileAsyncPositivePathHasSlash();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpListFilesAsyncPositiveFolderIsEmpty()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseListFilesAsyncPositiveFolderIsEmpty();
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpListFileAsyncNegativeFolderNotExists()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.Password, "azureuser", "Azureuser@123", Rsa2048ResourceName, "biztalk2013");
            baseTransportDestinationTestHelper.BaseListFileAsyncNegativeFolderNotExists();
        }


        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpUploadFileAsyncPositiveForMultiAuth()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, "btsuser", "Microsoft2012", MultiFactorAuthPpkResourceName, "rsa2048");
            baseTransportDestinationTestHelperForMultiFact.BaseUploadFileAsyncPositiveAppendIfExistNotSpecified(true);
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpListFileAsyncPositiveForMultiAuth()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, "btsuser", "Microsoft2012", MultiFactorAuthPpkResourceName, "rsa2048");
            baseTransportDestinationTestHelperForMultiFact.BaseListFileAsyncPositive(true);
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpGetFileAsyncPositiveForMultiAuth()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, "btsuser", "Microsoft2012", MultiFactorAuthPpkResourceName, "rsa2048");
            baseTransportDestinationTestHelperForMultiFact.BaseGetFileAsyncPositive(true);
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void SftpDeleteFileAsyncPositiveForMultiAuth()
        {
            this.AddAuthenticationDetailsToIsolatedStorage(AuthenticationType.MultiFactor, "btsuser", "Microsoft2012", MultiFactorAuthPpkResourceName, "rsa2048");
            baseTransportDestinationTestHelperForMultiFact.BaseDeleteFileAsyncPositive(true);
        }

        private void AddAuthenticationDetailsToIsolatedStorage(AuthenticationType authenticationType, string userName, string password, string ppkResourceName, string ppkPassword)
        {
            byte[] ppkFileContent = GetEmbeddedResourceContent(ppkResourceName);
            var authenticationDetails = GetAuthenticationDetails(authenticationType, userName, password, ppkFileContent, ppkPassword);
            HttpResponseMessage responseMessage = this.sftpConnectorTest.AddAuthenticationDetails(JsonConvert.SerializeObject(authenticationDetails));
            Assert.AreEqual(responseMessage.StatusCode, HttpStatusCode.OK);
        }

        private AuthenticationDetails GetAuthenticationDetailsFromIsolatedStorage()
        {
            var response = this.sftpConnectorTest.GetAuthenticationDetails();

            if (response.Content != null)
            {
                return JsonConvert.DeserializeObject<AuthenticationDetails>(response.Content.ReadAsStringAsync().Result);
            }

            return null;
        }

        private static AuthenticationDetails GetAuthenticationDetails(AuthenticationType authenticationType, string userName, string password, byte[] content, string ppkPassword = null)
        {
            AuthenticationDetails authenticationDetails = new AuthenticationDetails();
            authenticationDetails.AuthenticationType = authenticationType.ToString();
            authenticationDetails.UserName = userName;
            authenticationDetails.Password = password;

            if (authenticationType == AuthenticationType.PrivateKey || authenticationType == AuthenticationType.MultiFactor)
            {
                authenticationDetails.Content = Convert.ToBase64String(content);
                authenticationDetails.PpkFilePassword = ppkPassword;

                // Harcoding this, as this is used only in UX to display.
                authenticationDetails.PpkFileName = "DummyName.ppk";
            }

            return authenticationDetails;
        }

        private static byte[] GetEmbeddedResourceContent(string resourceName)
        {
            using (Stream stream = GetEmbeddedResource(resourceName))
            {
                stream.Position = 0;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        private static Stream GetEmbeddedResource(string resourceName)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceStream(resourceName);
        }

        private HttpRequestMessage GetRequestMessage(string content, HttpMethod method, string relativeUrl)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(method, relativeUrl);
            requestMessage.Content = new StringContent(content);
            requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(JsonContentType);
            return requestMessage;
        }
    }
}
