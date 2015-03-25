namespace FileBasedProtocolConnectorTest.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Web.Http;
    using Microsoft.Azure.AppService.ApiApps.Service;
    using SFTPConnector;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using FileBasedProtocolConnectorTest.Tests;
    using System.Threading;
    using WebAPITestFramework;
    using System.Collections.Specialized;
    using System.Net.Mime;
    using SFTPConnector.Messaging;
    using IO = System.IO;

    class BaseTransportDestinationTestHelper<T> where T : BaseTransportController, new()
    {
        bool targetSftpServer;
        public IFTPConnectorTest ftpConnectorTest;
        public IFTPConnectorTest connectorTest;
        
        public BaseTransportDestinationTestHelper(
            bool targetSftpServer, bool isSftpConnector = true, bool isMultiFactAuthServer = false, bool wrongusername = false, string rootfolderpath = "", bool usessl = false, bool disablecertvalidation = false)
        {
            bool isControllerTest = false;
        
            FtpUtil ftpUtil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);
            this.targetSftpServer = targetSftpServer;

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection(wrongusername, isSftpConnector);
            namevalue.Add("RootFolderPath", rootfolderpath);

            if (isSftpConnector)
            {
                namevalue.Add("AcceptAnySSHServerHostKey", "true");
            }

            if (isMultiFactAuthServer)
            {
                namevalue.Add("PrivateKey", Environment.CurrentDirectory + "/MultiFactorAuth.ppk");
                namevalue.Add("PrivateKeyPassword", ftpUtil.SftpPrivateKeyPassword);
            }

            if (isControllerTest)
            {
                this.ftpConnectorTest = new FileBasedProtocolConnectorControllerTest<T>(targetSftpServer, wrongusername);
            }
            else
            {
                this.ftpConnectorTest = new FileBasedProtocolConnectorClientTest<T>(namevalue, isSftpConnector, disableCertificateValidation: disablecertvalidation, usessl : usessl);
            }
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void BaseValidateNegativeTestForInvalidPortGreaterThanRange()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(this.targetSftpServer);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection();

            if (this.targetSftpServer)
            {
                namevalue.Add("SftpServerPort", "2121212");
                connectorTest = new FileBasedProtocolConnectorClientTest<T>(namevalue);
            }

            var message = connectorTest.DeleteFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        [TestMethod]
        [Owner(Constants.OwnerAlias)]
        public void BaseValidateNegativeTestForInvalidPortLesserThanRange()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(this.targetSftpServer);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection();

            if (this.targetSftpServer)
            {
                namevalue.Add("SftpServerPort", "-2");
                connectorTest = new FileBasedProtocolConnectorClientTest<T>(namevalue);
            }

            var message = connectorTest.DeleteFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        public void BaseValidateNegativeTestForInvalidPort()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftpUtil = new FtpUtil(this.targetSftpServer);

            NameValueCollection namevalue = ftpUtil.GetNameValueCollection();

            if (this.targetSftpServer)
            {
                namevalue.Add("SftpServerPort", Guid.NewGuid().ToString());
                connectorTest = new FileBasedProtocolConnectorClientTest<T>(namevalue);
            }

            var message = connectorTest.DeleteFile(folderPath, fileName);
            Assert.AreEqual(message.StatusCode, HttpStatusCode.BadRequest, "Could not validate correctly");
        }

        public void BaseUploadFileAsyncPositiveAppendIfExistNotSpecified(bool isMultiFactAuthServer = false)
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            FtpUtil ftputil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);
            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                fileinfo.TryGetValue("FilePath", out obj);
                Assert.AreEqual(obj as string, System.IO.Path.Combine(folderPath, fileName), "FilePath of content has invalid value.");

                string actualContent = ftpTestHelper.DownloadFile(folderPath, fileName);
                Assert.AreEqual(actualContent, Constants.UploadContents, "Upload file operation did not create a file with fileName in the specified path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveArtifacts(bool isMultiFactAuthServer = false)
        {
            string testArtifactdirectory = "FileBasedTestArtifacts";
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftputil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                if (System.IO.Directory.Exists(testArtifactdirectory))
                {
                    string[] fileEntries = System.IO.Directory.GetFiles(testArtifactdirectory);

                    foreach (var file in fileEntries)
                    {
                        string fileName = Guid.NewGuid().ToString();
                        string content = System.IO.File.ReadAllText(file);

                        var response = this.ftpConnectorTest.UploadFile(content, folderPath, fileName);
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                        Dictionary<string, object> fileinfo =
                            JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                        object obj;

                        fileinfo.TryGetValue("FolderPath", out obj);
                        Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                        fileinfo.TryGetValue("FileName", out obj);
                        Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                        fileinfo.TryGetValue("ServerAddress", out obj);
                        Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                        string actualContent = ftpTestHelper.DownloadFile(folderPath, fileName);
                        Assert.AreEqual(actualContent, content, "Upload file operation did not create a file with fileName in the specified path.");

                        ftpTestHelper.Delete(folderPath, fileName);
                    }
                }
                else
                {
                    Assert.Fail("TestArtifactdirectory does not exist");
                }
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseVerifyCertificateValidationFailsWhenEnabled(bool isMultiFactAuthServer = false)
        {
            string testArtifactdirectory = "FileBasedTestArtifacts";
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            string folderPath = Guid.NewGuid().ToString();
            FtpUtil ftputil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                if (System.IO.Directory.Exists(testArtifactdirectory))
                {
                    string[] fileEntries = System.IO.Directory.GetFiles(testArtifactdirectory);

                    foreach (var file in fileEntries)
                    {
                        string fileName = Guid.NewGuid().ToString();
                        string content = System.IO.File.ReadAllText(file);

                        var response = this.ftpConnectorTest.UploadFile(content, folderPath, fileName);
                        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, "Http Status Code returned is incorrect"  +  response.Content.ReadAsStringAsync().Result);
                        break;
                    }
                }
                else
                {
                    Assert.Fail("TestArtifactdirectory does not exist");
                }
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveWithEmptyContent()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(string.Empty, folderPath, fileName);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                string actualContent = ftpTestHelper.DownloadFile(folderPath, fileName);
                Assert.AreEqual(actualContent, string.Empty, "Upload file operation did not create a file with fileName in the specified path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncNegativeContentIsNull(bool isMultiFactAuthServer = false)
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName, isContentNull: true);
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncNegativeWhenContentIsNotBase64()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(Guid.NewGuid().ToString(), folderPath, fileName, false, null, ContentTransferEncoding.Base64);
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncNegativeInvalidCharInFilename()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString() + "/\\?<|:>*";

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, System.Web.HttpUtility.UrlEncode(fileName));
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncNegativeFileNameExceedLength()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();

            string longFileName = fileName;
            while (longFileName.Length < 1024)
            {
                longFileName += longFileName;
            }

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(string.Empty, folderPath, longFileName);
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveAppendIfExistTrueFileIsPresent()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);
            ftpTestHelper.CreateFolder(folderPath);
            ftpTestHelper.UploadFile(Constants.PreviousContents, folderPath, fileName);

            try
            {
                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName, true);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string actualContent = ftpTestHelper.DownloadFile(folderPath, fileName);
                string expectedContent = string.Format(CultureInfo.InvariantCulture, "{0}{1}", Constants.PreviousContents, Constants.UploadContents);
                Assert.AreEqual(expectedContent, actualContent, "Upload file operation did not append a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveAppendIfExistTrueFileIsNotPresent()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();

            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);
                ftpTestHelper.Delete(folderPath, fileName);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName, true);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string actualContent = ftpTestHelper.DownloadFile(folderPath, fileName);
                Assert.AreEqual(Constants.UploadContents, actualContent, "Upload file operation did not append a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveAppendIfExistTrueFileIsNotPresentFilenameWithDot()
        {
            string fileName = Guid.NewGuid().ToString() + ".txt";
            string folderPath = Guid.NewGuid().ToString();

            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);
                ftpTestHelper.Delete(folderPath, fileName);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName, true);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string actualContent = ftpTestHelper.DownloadFile(folderPath, fileName);
                Assert.AreEqual(Constants.UploadContents, actualContent, "Upload file operation did not append a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveAppendIfExistFalseFileIsPresent()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.PreviousContents, folderPath, fileName);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string newcontent = ftpTestHelper.DownloadFile(folderPath, fileName);
                Assert.AreEqual(Constants.UploadContents, newcontent, "Upload file operation did not create a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveBigFile()
        {
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            string expectedContent;

            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                using (IO.MemoryStream stream = new IO.MemoryStream())
                {
                    stream.SetLength(Constants.BigFileSize);

                    using (IO.StreamReader streamReader = new IO.StreamReader(stream))
                    {
                        stream.Position = 0;
                        expectedContent = streamReader.ReadToEnd();
                    }


                    var response = this.ftpConnectorTest.UploadFile(expectedContent, folderPath, fileName);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                    Dictionary<string, object> fileinfo =
                        JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                    object obj;

                    fileinfo.TryGetValue("FolderPath", out obj);
                    Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                    fileinfo.TryGetValue("FileName", out obj);
                    Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                    fileinfo.TryGetValue("ServerAddress", out obj);
                    Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                    string newcontent = ftpTestHelper.DownloadFile(folderPath, fileName);
                    Assert.AreEqual(expectedContent, newcontent, "Upload file operation did not create a file with the mentioned name in the mentioned path.");
                }
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveTemporaryFolderExist()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            string temporaryFolder = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(temporaryFolder);
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName, false, temporaryFolder);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string newcontent = ftpTestHelper.DownloadFile(folderPath, fileName);
                Assert.AreEqual(Constants.UploadContents, newcontent, "Upload file operation did not create a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(temporaryFolder);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveTemporaryFolderBigFile()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            string temporaryFolder = Guid.NewGuid().ToString();
            string expectedContent;

            try
            {
                ftpTestHelper.CreateFolder(temporaryFolder);
                ftpTestHelper.CreateFolder(folderPath);

                using (IO.MemoryStream stream = new IO.MemoryStream())
                {
                    stream.SetLength(Constants.BigFileSize);

                    using (IO.StreamReader streamReader = new IO.StreamReader(stream))
                    {
                        stream.Position = 0;
                        expectedContent = streamReader.ReadToEnd();
                    }

                    var response = this.ftpConnectorTest.UploadFile(expectedContent, folderPath, fileName, false, temporaryFolder);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                    Dictionary<string, object> fileinfo =
                        JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                    object obj;

                    fileinfo.TryGetValue("FolderPath", out obj);
                    Assert.AreEqual(obj as string, folderPath, "FolderPath of content has invalid value.");

                    fileinfo.TryGetValue("FileName", out obj);
                    Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                    fileinfo.TryGetValue("ServerAddress", out obj);
                    Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                    string newcontent = ftpTestHelper.DownloadFile(folderPath, fileName);
                    Assert.AreEqual(expectedContent, newcontent, "Upload file operation did not create a big file with the mentioned name in the mentioned path.");
                }
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(temporaryFolder);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFileAsyncPositiveTempFolderAppendFalseFileExistInDestFolder()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);
            string fileName = Guid.NewGuid().ToString();
            string destinationFolder = Guid.NewGuid().ToString();
            string temporaryFolder = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(destinationFolder);
                ftpTestHelper.UploadFile(Constants.PreviousContents, destinationFolder, fileName);

                ftpTestHelper.CreateFolder(temporaryFolder);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, destinationFolder, fileName, false, temporaryFolder);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, destinationFolder, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string actualContent = ftpTestHelper.DownloadFile(destinationFolder, fileName);
                Assert.AreEqual(Constants.UploadContents, actualContent, "Upload file operation did not create a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(destinationFolder, fileName);
                ftpTestHelper.RemoveFolder(destinationFolder);
                ftpTestHelper.RemoveFolder(temporaryFolder);
            }
        }

        public void BaseUploadFileAsyncPositiveTemporaryFolderIsGivenAppendIsFalse()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);
            string fileName = Guid.NewGuid().ToString();
            string destinationFolder = Guid.NewGuid().ToString();
            string temporaryFolder = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(destinationFolder);
                ftpTestHelper.CreateFolder(temporaryFolder);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, destinationFolder, fileName, false, temporaryFolder);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, destinationFolder, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string actualContent = ftpTestHelper.DownloadFile(destinationFolder, fileName);
                Assert.AreEqual(Constants.UploadContents, actualContent, "Upload file operation did not create a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(destinationFolder, fileName);
                ftpTestHelper.RemoveFolder(destinationFolder);
                ftpTestHelper.RemoveFolder(temporaryFolder);
            }
        }

        public void BaseUploadFileAsyncPositiveWithAbsolutePath()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(true);
            FtpUtil ftpUtil = new FtpUtil(true);
            string fileName = Guid.NewGuid().ToString();
            string folderName = Guid.NewGuid().ToString();
            string absolutePath = string.Format(CultureInfo.InvariantCulture, "home/{0}/{1}", ftpUtil.FtpUserName, folderName);

            try
            {
                ftpTestHelper.CreateFolder(folderName);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, absolutePath, fileName);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                
                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, "/" + absolutePath, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftpUtil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string newcontent = ftpTestHelper.DownloadFile(folderName, fileName);
                Assert.AreEqual(Constants.UploadContents, newcontent, "Upload file operation did not create a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(folderName, fileName);
                ftpTestHelper.RemoveFolder(folderName);
            }
        }

        public void BaseUploadFileAsyncPositiveTempFolderAppendFalseFileExistInDestFolderAbsolutePath()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(true);
            FtpUtil ftpUtil = new FtpUtil(true);
            string fileName = Guid.NewGuid().ToString();
            string destinationFolder = Guid.NewGuid().ToString();
            string temporaryFolder = Guid.NewGuid().ToString();
            string absolutePathForDestination = "home/" + ftpUtil.FtpUserName + "/" + destinationFolder;
            string absolutePathForTemporary = "home/" + ftpUtil.FtpUserName + "/" + temporaryFolder;

            try
            {
                ftpTestHelper.CreateFolder(destinationFolder);
                ftpTestHelper.UploadFile(Constants.UploadContents, destinationFolder, fileName);

                ftpTestHelper.CreateFolder(temporaryFolder);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, absolutePathForDestination, fileName, false, absolutePathForTemporary);
                Assert.AreEqual(response.StatusCode, HttpStatusCode.OK, "Http Status Code returned is incorrect");

                Dictionary<string, object> fileinfo =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content.ReadAsStringAsync().Result);

                object obj;

                fileinfo.TryGetValue("FolderPath", out obj);
                Assert.AreEqual(obj as string, "/" + absolutePathForDestination, "FolderPath of content has invalid value.");

                fileinfo.TryGetValue("FileName", out obj);
                Assert.AreEqual(obj as string, fileName, "FileName of content has invalid value.");

                fileinfo.TryGetValue("ServerAddress", out obj);
                Assert.AreEqual(obj as string, ftpUtil.FtpServerAddress, "ServerAddress of content has invalid value.");

                string actualContent = ftpTestHelper.DownloadFile(destinationFolder, fileName);
                Assert.AreEqual(Constants.UploadContents, actualContent, "Upload file operation did not create a file with the mentioned name in the mentioned path.");
            }
            finally
            {
                ftpTestHelper.Delete(destinationFolder, fileName);
                ftpTestHelper.RemoveFolder(destinationFolder);
                ftpTestHelper.RemoveFolder(temporaryFolder);
            }
        }

        public void BaseUploadFileAsyncNegativeTemporaryFolderNotExist()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            string temporaryFolder = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName, false, temporaryFolder);
                Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound);
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseUploadFilesAsyncNegativeIfFolderNotExist()
        {
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            FTPTestHelper ftpTestHelper = new FTPTestHelper();

            ftpTestHelper.CreateFolder(folderPath);
            ftpTestHelper.RemoveFolder(folderPath);

            var response = this.ftpConnectorTest.UploadFile(Constants.UploadContents, folderPath, fileName);

            Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound);
        }

        public void BaseDeleteFileAsyncPositive(bool isMultiFactAuthServer = false)
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            bool isFileExist = true;

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);

                var response = this.ftpConnectorTest.DeleteFile(folderPath, fileName);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                isFileExist = ftpTestHelper.CheckExistence(folderPath, fileName);
                Assert.IsFalse(isFileExist, "File is not deleted by delete operation");
            }
            finally
            {
                if (isFileExist)
                {
                    ftpTestHelper.Delete(folderPath, fileName);
                }

                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseDeleteFileAsyncNegativeFileNotAccesible()
        {
            string folderPath = "DontDeleteThis";
            string fileName = "DontDeleteThisFile";

            // Created a read only file with name  = "DontDeleteThisFile" manually in "DontDeleteThis" folder
            // as there is no way to create a read only file from FtpWebRequest 

            var response = this.ftpConnectorTest.DeleteFile(folderPath, fileName);

            Assert.AreEqual(response.StatusCode, HttpStatusCode.Forbidden, "Http Status Code returned is incorrect");
        }

        public void BaseDeleteFileAsyncNegativeFolderNotExist()
        {
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();

            var response = this.ftpConnectorTest.DeleteFile(folderPath, fileName);

            Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound, "Http Status Code returned is incorrect");
        }

        public void BaseDeleteFileAsyncNegativeFileNotExist()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper();
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                var response = this.ftpConnectorTest.DeleteFile(folderPath, fileName);

                Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        private string fileNameForSource;
        private string folderPathForSource;
        private int noOfFiles;

        public void BasePollFileAsyncPositive(string rootFolder = "")
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
                        
            folderPathForSource = Guid.NewGuid().ToString();
            fileNameForSource = Guid.NewGuid().ToString();
            noOfFiles = 1;

            string completeFolderPath = folderPathForSource;
            try
            {
                if (!string.IsNullOrEmpty(rootFolder))
                {
                    ftpTestHelper.CreateFolder(rootFolder);
                    completeFolderPath = rootFolder + "/" + folderPathForSource;
                }
                
                ftpTestHelper.CreateFolder(completeFolderPath);
                

                ftpTestHelper.UploadFile(Constants.UploadContents, completeFolderPath, fileNameForSource);
                
                HttpResponseMessage message;

                message = this.ftpConnectorTest.Poll(null, folderPathForSource, "*", string.Empty);

                Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, message.Content.ReadAsStringAsync().Result);
                File messageDescription =
                    JsonConvert.DeserializeObject<File>(
                        message.Content.ReadAsStringAsync().Result);
                Assert.AreEqual(messageDescription.FileName, fileNameForSource);
                using (IO.StreamReader reader = new IO.StreamReader(messageDescription.GetStream()))
                {
                    Assert.AreEqual(reader.ReadToEnd(), Constants.UploadContents);
                }

                message = this.ftpConnectorTest.Poll(fileNameForSource, folderPathForSource, "*", string.Empty);
                Assert.AreEqual(message.StatusCode, HttpStatusCode.Accepted, message.Content.ReadAsStringAsync().Result);
                Assert.IsTrue(ftpTestHelper.CheckCountOfFiles(completeFolderPath, 0), "Files are not deleted");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(completeFolderPath);

                if(!string.IsNullOrEmpty(rootFolder))
                {
                    ftpTestHelper.RemoveFolder(rootFolder);
                }
            }
        }

        public void BasePollFileAsyncWithRegexPositive()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            folderPathForSource = Guid.NewGuid().ToString();
            fileNameForSource = Guid.NewGuid().ToString() + ".xml";
            string fileName = Guid.NewGuid().ToString() + ".txt";
            noOfFiles = 2;

            try
            {
                ftpTestHelper.CreateFolder(folderPathForSource);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPathForSource, fileNameForSource);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPathForSource, fileName);

                HttpResponseMessage message;

                message = this.ftpConnectorTest.Poll(null, folderPathForSource, "*.xml", "*.txt");
                Assert.AreEqual(message.StatusCode, System.Net.HttpStatusCode.OK, message.Content.ReadAsStringAsync().Result);

                JObject obj = JObject.Parse(message.Content.ReadAsStringAsync().Result);

                File messageDescription =
                    obj.ToObject<File>();
                Assert.AreEqual(messageDescription.FileName, fileNameForSource);

                // no file should be returned
                message = this.ftpConnectorTest.Poll(obj.GetTriggerState(), folderPathForSource, "*.xml", "*.txt");
                Assert.AreEqual(message.StatusCode, System.Net.HttpStatusCode.Accepted, message.Content != null?message.Content.ReadAsStringAsync().Result : "(null)");
                
                Assert.IsTrue(ftpTestHelper.CheckCountOfFiles(folderPathForSource, 1), "Files are not deleted");
            }
            finally
            {
                ftpTestHelper.Delete(folderPathForSource, fileName);
                ftpTestHelper.RemoveFolder(folderPathForSource);
            }
        }

        public void BasePollFileAsyncEmptyFolderPositive()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                string triggerstate = null;
                HttpResponseMessage message;
                message = this.ftpConnectorTest.Poll(triggerstate, folderPath, "*", string.Empty);
                Assert.AreEqual(message.StatusCode, System.Net.HttpStatusCode.Accepted, message.Content != null ? message.Content.ReadAsStringAsync().Result : "(null)");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BasePollFileAsyncPositiveWithMultipleFiles()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            int count = 5;
            string[] fileName = new string[count];

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                for (int i = 0; i < count; i++)
                {
                    fileName[i] = Guid.NewGuid().ToString();
                    ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName[i]);
                }

                string triggerstate = null;
                HttpResponseMessage message;

                for (int i = 0; i < count; i++)
                {
                    message = this.ftpConnectorTest.Poll(triggerstate, folderPath, "*", "");
                    Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, message.Content.ReadAsStringAsync().Result);
                    JObject obj = JObject.Parse(message.Content.ReadAsStringAsync().Result);
                        
                    triggerstate = obj.GetTriggerState();
                    File messageDescription = obj.ToObject<File>();
                    Assert.IsTrue(fileName.Contains(messageDescription.FileName), messageDescription.FileName);
                }

                // finally to delete the last file
                message = this.ftpConnectorTest.Poll(triggerstate, folderPath, "*", "");
                Assert.AreEqual(message.StatusCode, HttpStatusCode.Accepted);
                Assert.IsTrue(ftpTestHelper.CheckCountOfFiles(folderPath, 0), "Files are not deleted");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BasePollFileAsyncNegativeAbandonMessage()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            folderPathForSource = Guid.NewGuid().ToString();
            fileNameForSource = Guid.NewGuid().ToString();
            noOfFiles = 1;

            try
            {
                ftpTestHelper.CreateFolder(folderPathForSource);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPathForSource, fileNameForSource);

                string triggerstate = null;
                HttpResponseMessage message;

                message = this.ftpConnectorTest.Poll(triggerstate, folderPathForSource, "*", string.Empty);
                Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, message.Content.ReadAsStringAsync().Result);
                
                // assume trigger state got lost by wolfkrow and it is starting again, same file should be returned and should not be deleted
                message = this.ftpConnectorTest.Poll(triggerstate, folderPathForSource, "*", string.Empty);
                Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, message.Content.ReadAsStringAsync().Result);
                
                Assert.IsTrue(ftpTestHelper.CheckCountOfFiles(folderPathForSource, 1), "Files are deleted");
            }
            finally
            {
                ftpTestHelper.Delete(folderPathForSource, fileNameForSource);
                ftpTestHelper.RemoveFolder(folderPathForSource);
            }
        }

        public void BasePollFileAsyncNegativeTriggerFileDoesNotExist()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            folderPathForSource = Guid.NewGuid().ToString();
            fileNameForSource = Guid.NewGuid().ToString();
            noOfFiles = 1;

            try
            {
                ftpTestHelper.CreateFolder(folderPathForSource);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPathForSource, fileNameForSource);

                string triggerstate = "RandomFile";
                HttpResponseMessage message;

                message = this.ftpConnectorTest.Poll(triggerstate, folderPathForSource, "*", string.Empty);
                Assert.AreEqual(message.StatusCode, HttpStatusCode.OK, message.Content.ReadAsStringAsync().Result);
                
                // verify uploaded file is returned
                File messageDescription =
                    JsonConvert.DeserializeObject<File>(
                        message.Content.ReadAsStringAsync().Result);
                Assert.AreEqual(messageDescription.FileName, fileNameForSource);
                
                Assert.IsTrue(ftpTestHelper.CheckCountOfFiles(folderPathForSource, 1), "Files are deleted");
            }
            finally
            {
                ftpTestHelper.Delete(folderPathForSource, fileNameForSource);
                ftpTestHelper.RemoveFolder(folderPathForSource);
            }
        }

        public void BasePollFileAsyncNegativeTriggerFileDoesNotExistNoFileUploaded()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            folderPathForSource = Guid.NewGuid().ToString();
            fileNameForSource = Guid.NewGuid().ToString();
            noOfFiles = 1;

            try
            {
                ftpTestHelper.CreateFolder(folderPathForSource);
               
                string triggerstate = "RandomFile";
                HttpResponseMessage message;

                message = this.ftpConnectorTest.Poll(triggerstate, folderPathForSource, "*", string.Empty);
                Assert.AreEqual(message.StatusCode, HttpStatusCode.Accepted, message.Content.ReadAsStringAsync().Result);
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPathForSource);
            }
        }

        public void BasePollFileAsyncNegativeWithRegex()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);

                HttpResponseMessage message;

                message = this.ftpConnectorTest.Poll(null, folderPath, "[abc", string.Empty);
                Assert.AreEqual(HttpStatusCode.BadRequest, message.StatusCode, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BasePollFileAsyncNegativeFolderDoesNotExist()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);

                HttpResponseMessage message;
                string newFolderPath = folderPath + "random";
                message = this.ftpConnectorTest.Poll(null, newFolderPath, "*", string.Empty);
                Assert.AreEqual(HttpStatusCode.NotFound, message.StatusCode, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BasePollFileAsyncNegativeUnAuthorized()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();
            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);

                HttpResponseMessage message;
                message = this.ftpConnectorTest.Poll(null, folderPath, "*", string.Empty);

                if (this.targetSftpServer)
                {
                    Assert.AreEqual(HttpStatusCode.BadRequest, message.StatusCode, "Http Status Code returned is incorrect");
                }
                else
                {
                    Assert.AreEqual(HttpStatusCode.Forbidden, message.StatusCode, "Http Status Code returned is incorrect");
                }
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        private void CheckProperties(System.IO.Stream request)
        {
            File returnedMessage = null;
            string content = null;
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);

            using (IO.StreamReader reader = new IO.StreamReader(request))
            {
                content = reader.ReadToEnd();
            }

            returnedMessage = JsonConvert.DeserializeObject<File>(content);

            Assert.IsTrue(ftpTestHelper.CheckCountOfFiles(folderPathForSource, noOfFiles), "File deleted in get file operation");
            Assert.AreEqual(returnedMessage.FileName, fileNameForSource, "Wrong file returned with address" + returnedMessage.FileName);
            Assert.AreEqual(returnedMessage.FolderPath, folderPathForSource, "Wrong file returned with address" + returnedMessage.FolderPath);
            Assert.AreEqual(Encoding.UTF8.GetString(Convert.FromBase64String(returnedMessage.Content)),
                Constants.UploadContents, "Content of file is incorrect");
        }

        public void BaseGetFileAsyncPositive(bool isMultiFactAuthServer = false)
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);
            string folderPath = Guid.NewGuid().ToString();
            string fileName = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);

                var response = this.ftpConnectorTest.GetFile(folderPath, fileName);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                File responseValue =
                    JsonConvert.DeserializeObject<File>(response.Content.ReadAsStringAsync().Result);
                Assert.AreEqual(Constants.UploadContents, responseValue.Content,
                    "The contents in the file are not consistent with the file that was uploaded");

                Assert.AreEqual(responseValue.FolderPath, folderPath, "FolderPath of content has invalid value.");

                Assert.AreEqual(responseValue.ServerAddress, ftputil.FtpServerAddress, "ServerAddress of content has invalid value.");

                Assert.AreEqual(responseValue.FileName, fileName, "FileName of content has invalid value.");

                Assert.AreEqual(responseValue.FilePath, System.IO.Path.Combine(folderPath, fileName), "FilePath of content has invalid value.");
            }
            finally
            {
                ftpTestHelper.Delete(folderPath, fileName);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseGetFileAsyncNegativeIfFileNotExist()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();
            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, fileName);
                ftpTestHelper.Delete(folderPath, fileName);

                var response = this.ftpConnectorTest.GetFile(folderPath, fileName);
                Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseGetFileAsyncNegativeIfFolderNotExist()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            string fileName = Guid.NewGuid().ToString();
            string folderPath = Guid.NewGuid().ToString();

            var response = this.ftpConnectorTest.GetFile(folderPath, fileName);
            Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound, "Http Status Code returned is incorrect");
        }

        public void BaseListFileAsyncPositive(bool isMultiFactAuthServer = false)
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);

            string folderPath = Guid.NewGuid().ToString();
            string[] paths = new string[3];
            paths[0] = Guid.NewGuid().ToString();
            paths[1] = Guid.NewGuid().ToString();
            paths[2] = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.CreateFolder(folderPath + "/" + paths[0]);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, paths[1]);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPath, paths[2]);

                var response = this.ftpConnectorTest.ListFile(folderPath);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Collection<FileInfo> files = JsonConvert.DeserializeObject<Collection<FileInfo>>(response.Content.ReadAsStringAsync().Result);

                if (!(paths.Contains(files[0].FileName) && paths.Contains(files[1].FileName) && paths.Contains(files[2].FileName)))
                {
                    Assert.Fail("The list of files return by list operation is not consistent with the files present in the folder.");
                }
            }
            finally
            {
                ftpTestHelper.RemoveFolder(string.Format(CultureInfo.InvariantCulture, "{0}/{1}", folderPath, paths[0]));
                ftpTestHelper.Delete(folderPath, paths[1]);
                ftpTestHelper.Delete(folderPath, paths[2]);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseListFileAsyncPositivePathHasSlash(bool isMultiFactAuthServer = false)
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer, isMultiFactAuthServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);

            string folderPath = Guid.NewGuid().ToString();
            string folderPathWithSlash = folderPath + "/" + Guid.NewGuid().ToString();
            string[] paths = new string[2];
            paths[0] = Guid.NewGuid().ToString();
            paths[1] = Guid.NewGuid().ToString();

            try
            {
                ftpTestHelper.CreateFolder(folderPath);
                ftpTestHelper.CreateFolder(folderPathWithSlash);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPathWithSlash, paths[1]);
                ftpTestHelper.UploadFile(Constants.UploadContents, folderPathWithSlash, paths[0]);

                var response = this.ftpConnectorTest.ListFile(folderPathWithSlash);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");

                Collection<FileInfo> files = JsonConvert.DeserializeObject<Collection<FileInfo>>(response.Content.ReadAsStringAsync().Result);

                if (!(paths.Contains(files[0].FileName) && paths.Contains(files[1].FileName)))
                {
                    Assert.Fail("The list of files return by list operation is not consistent with the files present in the folder.");
                }
            }
            finally
            {             
                ftpTestHelper.Delete(folderPathWithSlash, paths[1]);
                ftpTestHelper.Delete(folderPathWithSlash, paths[0]);
                ftpTestHelper.RemoveFolder(folderPathWithSlash);
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseListFilesAsyncPositiveFolderIsEmpty()
        {
            string folderPath = Guid.NewGuid().ToString();

            FTPTestHelper ftpTestHelper = new FTPTestHelper(targetSftpServer);
            FtpUtil ftputil = new FtpUtil(targetSftpServer);

            try
            {
                ftpTestHelper.CreateFolder(folderPath);

                var response = this.ftpConnectorTest.ListFile(folderPath);

                Collection<FileInfo> files = JsonConvert.DeserializeObject<Collection<FileInfo>>(response.Content.ReadAsStringAsync().Result);

                Assert.IsTrue(files.Count == 0, "The list of files return by list operation is not consistent with the files present in the folder.");

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Http Status Code returned is incorrect");
            }
            finally
            {
                ftpTestHelper.RemoveFolder(folderPath);
            }
        }

        public void BaseListFileAsyncNegativeFolderNotExists()
        {
            FTPTestHelper ftpTestHelper = new FTPTestHelper();

            string folderPath = Guid.NewGuid().ToString();

            ftpTestHelper.CreateFolder(folderPath);
            ftpTestHelper.RemoveFolder(folderPath);

            var response = this.ftpConnectorTest.ListFile(folderPath);

            Assert.AreEqual(response.StatusCode, HttpStatusCode.NotFound, "Http Status Code returned is incorrect");
        }
    }
}
