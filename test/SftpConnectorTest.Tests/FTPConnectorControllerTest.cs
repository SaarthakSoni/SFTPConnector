
namespace FileBasedProtocolConnectorTest.Tests
{
    using SFTPConnector.Messaging;
    using System.Net.Http;
    using System.Net.Mime;
    using System.Web.Http;
    using SFTPConnector;

    class FileBasedProtocolConnectorControllerTest<T> : IFTPConnectorTest where T : BaseTransportController, new()
    {
        public T controller;

        public FileBasedProtocolConnectorControllerTest(bool targetSftpServer, bool wrongusername = false)
        {
            controller = new T();
            FtpUtil ftpUtil = new FtpUtil(targetSftpServer);

            controller.ServerAddress = ftpUtil.FtpServerAddress;
            controller.UserName = ftpUtil.FtpUserName;
            controller.Password = ftpUtil.FtpPassword;
            if (wrongusername)
            {
                controller.UserName = "wrongusername";
            }
        }

        public HttpResponseMessage UploadFile(string content, string folderPath, string fileName, bool appendIfExist = false, string temporaryFolder = null, ContentTransferEncoding transferEncoding = ContentTransferEncoding.None, bool isContentNull = false)
        {
            SendRequestMessage(controller, HttpMethod.Post);
            FileContent reqContent = new FileContent(); 
            reqContent.Content = content;
            return controller.UploadFile(reqContent, folderPath, appendIfExist, temporaryFolder).Result;
        }

        public HttpResponseMessage DeleteFile(string folderPath, string fileName)
        {
            SendRequestMessage(controller, HttpMethod.Delete);
            return controller.DeleteFile(folderPath).Result;
        }

        public HttpResponseMessage GetFile(string folderPath, string fileName, FileType FileType = FileType.Text)
        {
            SendRequestMessage(controller, HttpMethod.Get);
            return controller.GetFile(folderPath).Result;
        }

        public HttpResponseMessage ListFile(string folderPath)
        {
            SendRequestMessage(controller, HttpMethod.Get);
            return controller.ListFiles(folderPath).Result;
        }

        public HttpResponseMessage Poll(string triggerstate, string folderPath, string includeMask, string excludeMask)
        {
            controller.Request = new HttpRequestMessage();
            return controller.TriggerOnFileAvailable(triggerstate, folderPath, includeMask, excludeMask).Result;
        }

        public HttpResponseMessage AddAuthenticationDetails(string authenticationDetailsAsString)
        {
            throw new System.NotImplementedException();
        }

        public HttpResponseMessage GetAuthenticationDetails()
        {
            throw new System.NotImplementedException();
        }

        public static void SendRequestMessage(T controller, HttpMethod httpMethod)
        {
            controller.Request = new HttpRequestMessage { };
            controller.Request.Method = httpMethod;
            controller.Configuration = new HttpConfiguration();
        }
    }
}
