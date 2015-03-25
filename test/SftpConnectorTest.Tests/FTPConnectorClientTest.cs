namespace FileBasedProtocolConnectorTest.Tests
{
    using SFTPConnector.Messaging;
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net.Http;
    using Newtonsoft.Json;
    using SFTPConnector;
    using WebAPITestFramework;
    using System.Net.Http.Headers;
    using System.Net.Mime;

    public class FileBasedProtocolConnectorClientTest<T> : IFTPConnectorTest where T : BaseTransportController
    {
        private MicroserviceTest ftpMicroserviceTest = null;
        bool isSftpConnector;

        public FileBasedProtocolConnectorClientTest(NameValueCollection namevalue, bool isSftpConnector = true,bool disableCertificateValidation = false, bool usessl = false)
        {
            this.isSftpConnector = isSftpConnector;

            if (isSftpConnector)
            {
                this.ftpMicroserviceTest = new MicroserviceTest(
    Path.Combine(Environment.CurrentDirectory, "SftpConnectorMicroservice"), namevalue);
            }
            
            this.ftpMicroserviceTest.DeployMicroservice();
        }

        public HttpResponseMessage UploadFile(string content, string folderPath, string fileName, bool appendIfExist = false, string temporaryFolder = null, ContentTransferEncoding transferEncoding = ContentTransferEncoding.None, bool isContentNull = false)
        {
            string appendIfExistString = ftpMicroserviceTest.Encode(appendIfExist.ToString().ToLower());
            FileContent reqContent = null;

            if (!isContentNull)
            {
                 reqContent = new FileContent(content, transferEncoding);
            }

            // queryParameters does not take null values
            if (string.IsNullOrWhiteSpace(temporaryFolder))
            {
                temporaryFolder = string.Empty;
            }

            NameValueCollection queryParameters = new NameValueCollection();
            queryParameters.Add("AppendIfExists", appendIfExistString);
            queryParameters.Add("TemporaryFolder", temporaryFolder);

            string relativeUri = Constants.sftpfileUri;

            var request = new HttpRequestMessage();
            request.Content = new StringContent(JsonConvert.SerializeObject(reqContent));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.RequestUri = new Uri(ftpMicroserviceTest.BaseAddressOfWebsite + "/" + relativeUri + ftpMicroserviceTest.ToQueryString(queryParameters));
            request.Method = HttpMethod.Put;

            return ftpMicroserviceTest.Send(relativeUri + "/" + folderPath  + "/" + fileName, request, queryParameters);
        }

        public HttpResponseMessage DeleteFile(string folderPath, string fileName)
        {
            string relativeUri =  Constants.sftpfileUri;
            return ftpMicroserviceTest.Delete(Path.Combine(relativeUri, folderPath, fileName), null);
        }

        public HttpResponseMessage GetFile(string folderPath, string fileName, FileType FileType = FileType.Text)
        {
            string FileTypeString = ftpMicroserviceTest.Encode(FileType.ToString().ToLower());
            NameValueCollection queryParameters = new NameValueCollection();
            queryParameters.Add("FileType", FileTypeString);
            string relativeUri = Constants.sftpfileUri;
            return ftpMicroserviceTest.Get(Path.Combine(relativeUri, folderPath, fileName), queryParameters);
        }

        public HttpResponseMessage ListFile(string folderPath)
        {
            string relativeUri = Constants.sftpfolderUri;
            return ftpMicroserviceTest.Get(Path.Combine(relativeUri, folderPath), null);
        }

        public HttpResponseMessage Poll(string triggerState, string folderPath, string includeMask, string excludeMask)
        {
            NameValueCollection queryParameters = new NameValueCollection();
            queryParameters.Add("FileMask", includeMask);
            queryParameters.Add("ExcludeFileMask", excludeMask);
            queryParameters.Add("triggerstate", triggerState == null ? string.Empty : triggerState);
            string relativeUrl = Constants.sftpPollUri;

            HttpRequestMessage message = new HttpRequestMessage();
            message.Method = HttpMethod.Get;
            return this.ftpMicroserviceTest.Send(Path.Combine(relativeUrl, folderPath), message, queryParameters);
        }

        public HttpResponseMessage AddAuthenticationDetails(string authenticationDetailsAsString)
        {
            string relativeUri = "SFTP/AddAuthenticationDetails";

            var request = new HttpRequestMessage();
            request.Content = new StringContent(authenticationDetailsAsString);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.RequestUri = new Uri(ftpMicroserviceTest.BaseAddressOfWebsite + "/" + relativeUri);
            request.Method = HttpMethod.Post;

            return ftpMicroserviceTest.Send(relativeUri, request, null);
        }

        public HttpResponseMessage GetAuthenticationDetails()
        {
            string relativeUri = "SFTP/GetAuthenticationDetails";

            var request = new HttpRequestMessage();
            request.RequestUri = new Uri(ftpMicroserviceTest.BaseAddressOfWebsite + "/" + relativeUri);
            request.Method = HttpMethod.Get;
            return ftpMicroserviceTest.Send(relativeUri, request, null);
        }

        ~FileBasedProtocolConnectorClientTest()
        {
            ftpMicroserviceTest.UnDeployMicroservice();
        }
    }
}
