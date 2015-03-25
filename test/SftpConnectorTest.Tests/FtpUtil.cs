namespace FileBasedProtocolConnectorTest.Tests
{
    using System.Collections.Specialized;

    public class FtpUtil
    {
        public FtpUtil(bool targetSftpServer, bool isMultiFactAuthServer = false)
        {
            if (isMultiFactAuthServer)
            {
                this.FtpUserName = "";
                this.FtpPassword = "";
                this.SftpPrivateKeyPassword = "";
                this.SftpSSHServerHostKey = "";
                this.FtpServerAddress = "";
            }
            else
            {
                this.FtpServerAddress =  "";
                this.FtpUserName = "";
                this.FtpPassword = "";
                this.SftpPrivateKeyPassword = "";
                this.SftpSSHServerHostKey = "";
            }
        }

        public string FtpServerAddress
        {
            get;
            private set;
        }

        public string FtpUserName
        {
            get;
            private set;
        }

        public string FtpPassword
        {
            get;
            private set;
        }

        public string SftpPrivateKeyPassword
        {
            get;
            private set;
        }

        public string SftpSSHServerHostKey
        {
            get;
            private set;
        }

        internal NameValueCollection GetNameValueCollection(bool wrongusername = false, bool isSFTP = false)
        {
            string serverAddress = "";

            if (isSFTP)
            {
                serverAddress = "SftpServerAddress";
            }

            NameValueCollection namevalue = new NameValueCollection();
            namevalue.Add(serverAddress, this.FtpServerAddress);
            namevalue.Add("FtpUserName", wrongusername ? "randomusername" : this.FtpUserName);
            namevalue.Add("FtpPassword", this.FtpPassword);
            return namevalue;
        }
    }
}
