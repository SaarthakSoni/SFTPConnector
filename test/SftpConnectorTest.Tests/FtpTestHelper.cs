
namespace FileBasedProtocolConnectorTest.Tests
{
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;

    public class FTPTestHelper
    {
        FtpUtil ftpUtil;

        public FTPTestHelper(bool targetSftpServer = false, bool isMultiFactAuthServer = false)
        {
            this.ftpUtil = new FtpUtil(targetSftpServer, isMultiFactAuthServer);
        }

        public bool CheckExistence(string folderPath, string fileName)
        {
            FtpWebRequest request = this.GetFtpWebRequest(folderPath, fileName);
            try
            {
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                }
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode ==
                    FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    return false;
                }

                throw;
            }

            return true;
        }

        public void UploadFile(string uploadContents, string folderPath, string fileName)
        {
            FtpWebRequest request = this.GetFtpWebRequest(folderPath, fileName);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                byte[] byteArray = Encoding.ASCII.GetBytes(uploadContents);
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(byteArray, 0, byteArray.Length);
                }

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                }
        }

        public void Delete(string folderPath, string fileName)
        {
            FtpWebRequest request = this.GetFtpWebRequest(folderPath, fileName);
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                }
        }
      
        public string DownloadFile(string folderPath, string fileName)
        {
            FtpWebRequest request = this.GetFtpWebRequest(folderPath, fileName);
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// To set the FTPWebRequest Connection Properties
        /// </summary>
        /// <param name="fileName">It is the name of file with which we want to connect</param>
        /// <returns>FtpWebRequest</returns>
        public FtpWebRequest GetFtpWebRequest(string folderPath, string fileName)
        {
            NetworkCredential networkCredential = new NetworkCredential(ftpUtil.FtpUserName, ftpUtil.FtpPassword);
            string serverAddress = ftpUtil.FtpServerAddress;

            string requestPath = string.Format(CultureInfo.InvariantCulture, "{0}:{1}/{2}/{3}", string.Format(CultureInfo.InvariantCulture, "ftp://{0}", serverAddress), "21", folderPath, fileName);

            FtpWebRequest request = FtpWebRequest.Create(requestPath) as FtpWebRequest;
            request.EnableSsl = false;
            request.UseBinary = true;
            request.KeepAlive = true;
            request.Credentials = networkCredential;
            return request;
        }

        public void CreateFolder(string folderName)
        {
            FtpWebRequest request = this.GetFtpWebRequest(folderName, string.Empty);
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            request.GetResponse().Close();
        }

        public void RemoveFolder(string folderName)
        {
            FtpWebRequest request = this.GetFtpWebRequest(folderName, string.Empty);
            request.Method = WebRequestMethods.Ftp.RemoveDirectory;
            request.GetResponse().Close();
        }

        internal bool CheckCountOfFiles(string folderPath, int expectedCount)
        {
            FtpWebRequest request = GetFtpWebRequest(folderPath, string.Empty);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        int filesCount = 0;
                        while (!string.IsNullOrEmpty(reader.ReadLine()))
                        {
                            filesCount++;
                        }

                        if (filesCount != expectedCount)
                        {
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                }
            }
        }
    }
}
