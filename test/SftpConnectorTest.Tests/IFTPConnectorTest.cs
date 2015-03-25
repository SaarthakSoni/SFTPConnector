

namespace FileBasedProtocolConnectorTest.Tests
{
    using System.Net.Http;
    using System.Net.Mime;
    using SFTPConnector.Messaging;

    interface IFTPConnectorTest
    {
        HttpResponseMessage UploadFile(string content, string folderPath, string fileName, bool appendIfExist = false, string temporaryFolder = null, ContentTransferEncoding transferEncoding = ContentTransferEncoding.None, bool isContentNull = false);

        HttpResponseMessage DeleteFile(string folderPath, string fileName);

        HttpResponseMessage GetFile(string folderPath, string fileName, FileType FileType = FileType.Text);

        HttpResponseMessage ListFile(string folderPath);

        HttpResponseMessage Poll(string triggerstate, string folderPath, string includeMask, string excludeMask);

        HttpResponseMessage AddAuthenticationDetails(string authenticationDetailsAsString);
        
        HttpResponseMessage GetAuthenticationDetails();
    }
}
