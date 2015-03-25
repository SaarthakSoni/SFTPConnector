//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace WebAPITestFramework
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Threading;
    using System.Web;
    using System.Web.Configuration;
    using SFTPConnector;

    public enum DeployArchitecture
    {
        x86,
        x64
    }

    public enum DeployLocation
    {
        IISExpress,
        Cloud
    }

    public class MicroserviceTest
    {
        DeployLocation deployLocation;
        DeployArchitecture deployArchitecture;
        string baseAddressOfWebsite;
        Process iisProcess = null;
        readonly TimeSpan clientTimeOutInMin;
        private readonly bool redirectOutput;

        public string BaseAddressOfWebsite
        {
            get
            {
                return this.baseAddressOfWebsite;
            }
        }

        // for scenario where website is already deployed
        public MicroserviceTest(string baseAddressOfWebsite, int clientTimeOutInMin = 2)
        {
            this.baseAddressOfWebsite = baseAddressOfWebsite;
            this.deployLocation = DeployLocation.Cloud;
            this.clientTimeOutInMin = TimeSpan.FromMinutes(clientTimeOutInMin);
        }

        public MicroserviceTest(
            string publishedWebsiteFolderPath,
            NameValueCollection appSettingsToBeUpdatedBeforeDeploy,
            int clientTimeOutInMin = 2,
            DeployLocation deployLocation = DeployLocation.IISExpress,
            DeployArchitecture deployArch = DeployArchitecture.x86, bool redirectOutput = false)
        {
            // Create a new folder, copy the published website to this folder and deploy from there
            // This is needed because when hybrid tests run, we deploy both cloud and onprem from same location, 
            // resulting in sometimes receiving appsettings have been updated outside of process by MessageHandler
            string directoryPath = Path.Combine(System.Environment.CurrentDirectory, Guid.NewGuid().ToString());
            Directory.CreateDirectory(directoryPath);
            DirectoryCopy(publishedWebsiteFolderPath, directoryPath, true);
            this.PublishedWebsiteFolderPath = directoryPath;
            this.deployLocation = deployLocation;
            this.deployArchitecture = deployArch;
            this.AppSettingsToBeUpdatedBeforeDeploy = appSettingsToBeUpdatedBeforeDeploy;
            this.clientTimeOutInMin = TimeSpan.FromMinutes(clientTimeOutInMin);
            
            this.redirectOutput = redirectOutput;
        }

        public string PublishedWebsiteFolderPath { get; set; }

        protected NameValueCollection AppSettingsToBeUpdatedBeforeDeploy { get; set; }

        public virtual string DeployMicroservice()
        {
            this.iisProcess = null;
            switch (this.deployLocation)
            {
                case DeployLocation.IISExpress: this.baseAddressOfWebsite = DeployOnIISExpress(out this.iisProcess);
                    return this.baseAddressOfWebsite;
                default: // for cloud
                    return null;
            }
        }

        public virtual void UnDeployMicroservice()
        {
            switch (this.deployLocation)
            {
                case DeployLocation.IISExpress: 
                    this.UnDeployOnIISExpress();
                    Thread.Sleep(2000);
                    Directory.Delete(this.PublishedWebsiteFolderPath, true);
                    break;
                default: // for cloud
                    break;
            }
        }

        public string Encode(string stringToBeEncoded)
        {
            return string.IsNullOrEmpty(stringToBeEncoded) ? string.Empty : Uri.EscapeDataString(stringToBeEncoded);
        }

        public string Decode(string stringToBeDecoded)
        {
            return string.IsNullOrEmpty(stringToBeDecoded) ? string.Empty : HttpUtility.UrlDecode(stringToBeDecoded);
        }

        public HttpResponseMessage Send(string relativeUrl, HttpRequestMessage requestMessage, NameValueCollection queryParameters)
        {
            string queryParams = this.ToQueryString(queryParameters);
            var client = this.GetClientObject();
            Uri completeUrl = new Uri(client.BaseAddress, relativeUrl + queryParams);
            requestMessage.RequestUri = completeUrl;
            return client.SendAsync(requestMessage).Result;
        }

        public HttpResponseMessage Post(string relativeUrl, HttpContent content, NameValueCollection queryParameters)
        {
            string queryParams = this.ToQueryString(queryParameters);

            var client = this.GetClientObject();
            Uri completeUrl = new Uri(client.BaseAddress, relativeUrl + queryParams);
            HttpResponseMessage msg = client.PostAsync(completeUrl, content).Result;
            return msg;
        }

        public HttpResponseMessage Get(string relativeUrl, NameValueCollection queryParameters)
        {
            string queryParams = this.ToQueryString(queryParameters);
            var client = this.GetClientObject();
            Uri completeUrl = new Uri(client.BaseAddress, relativeUrl + queryParams);
            return client.GetAsync(completeUrl).Result;
        }

        public HttpResponseMessage Delete(string relativeUrl, NameValueCollection queryParameters)
        {
            string queryParams = this.ToQueryString(queryParameters);
            var client = this.GetClientObject();
            Uri completeUrl = new Uri(client.BaseAddress, relativeUrl + queryParams);
            return client.DeleteAsync(completeUrl).Result;
        }

        public HttpResponseMessage Put(string relativeUrl, HttpContent content, NameValueCollection queryParameters)
        {
            string queryParams = this.ToQueryString(queryParameters);
            var client = this.GetClientObject();
            Uri completeUrl = new Uri(client.BaseAddress, relativeUrl + queryParams);
            return client.PutAsync(completeUrl, content).Result;
        }

        protected string DeployOnIISExpress(out Process process)
        {
            int port = GetUnusedPort();
            var address = @"http://localhost:" + port;
            process = this.InstallMicroservice(address);
            return address;
        }

        protected static string ProgramFilesx86()
        {
            if (8 == IntPtr.Size || (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
            {
                return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            }

            return Environment.GetEnvironmentVariable("ProgramFiles");
        }

        protected static string ProgramFiles()
        {
            return Environment.GetEnvironmentVariable("ProgramFiles");
        }

        private Process InstallMicroservice(string address)
        {
            Uri uri = new Uri(address);
            int port = uri.Port;
 
            string iisexpresslocation = string.Empty;
            if (this.deployArchitecture == DeployArchitecture.x86)
            {
                iisexpresslocation = Path.Combine(ProgramFilesx86(), @"iis express\iisexpress.exe");
            }
            else
            {
                iisexpresslocation = Path.Combine(ProgramFiles(), @"iis express\iisexpress.exe");
            }

            this.UpdateWebConfigFiles(this.PublishedWebsiteFolderPath);
            var iisExpressProcess = new Process();
            iisExpressProcess.StartInfo.CreateNoWindow = true;
            iisExpressProcess.StartInfo.UseShellExecute = false;

            iisExpressProcess.StartInfo.RedirectStandardOutput = this.redirectOutput;
            iisExpressProcess.StartInfo.RedirectStandardError = this.redirectOutput;

            iisExpressProcess.StartInfo.FileName = iisexpresslocation;
            iisExpressProcess.StartInfo.Arguments = string.Format("/path:\"{0}\" /port:{1}", this.PublishedWebsiteFolderPath, port);

            iisExpressProcess.Start();

            Console.WriteLine("Deploying on iis express {0} with Pid {1} and Arguments {2} ", iisexpresslocation, iisExpressProcess.Id, iisExpressProcess.StartInfo.Arguments);
            return iisExpressProcess;
        }

        private static bool CheckIfUsedTCPPort(int port)
        {
            IPGlobalProperties globalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = globalProperties.GetActiveTcpListeners();
            IEnumerator myEnum = tcpConnInfoArray.GetEnumerator();

            while (myEnum.MoveNext())
            {
                IPEndPoint tcpInfo = (IPEndPoint)myEnum.Current;
                if (tcpInfo.Port == port)
                {
                    return true;
                }
            }

            return false;
        }

        protected void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the source directory does not exist, throw an exception.
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory does not exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the file contents of the directory to copy.
            System.IO.FileInfo[] files = dir.GetFiles();

            foreach (System.IO.FileInfo file in files)
            {
                // Create the path to the new copy of the file.
                string temppath = Path.Combine(destDirName, file.Name);

                // Copy the file.
                file.CopyTo(temppath, false);
            }

            // If copySubDirs is true, copy the subdirectories.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    // Create the subdirectory.
                    string tempPath = Path.Combine(destDirName, subdir.Name);

                    // Copy the subdirectories.
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        public static int GetUnusedPort()
        {
            Random rand = new Random(DateTime.UtcNow.Millisecond);
            int port = rand.Next(4000, 8000);
            while (CheckIfUsedTCPPort(port))
            {
                port = rand.Next(4000, 8000);
            }

            return port;
        }

        public string ToQueryString(NameValueCollection nvc)
        {
            if (nvc == null)
            {
                return string.Empty;
            }

            var array = (from key in nvc.AllKeys
                         from value in nvc.GetValues(key)
                         select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(value)))
                .ToArray();

            if (array.Length > 0)
            {
                return "?" + string.Join("&", array);
            }
            else
            {
                return string.Empty;
            }
        }

        private void UpdateWebConfigFiles(string directory)
        {
            if (this.AppSettingsToBeUpdatedBeforeDeploy == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentNullException("directory");
            }

            Configuration webConfig = GetWebConfiguration(directory);
            AppSettingsSection appSettingsSection = (AppSettingsSection)webConfig.GetSection("appSettings");

            if (appSettingsSection != null)
            {
                foreach (string key in this.AppSettingsToBeUpdatedBeforeDeploy.AllKeys)
                {
                    if (appSettingsSection.Settings.AllKeys.Contains(key))
                    {
                        appSettingsSection.Settings.Remove(key);
                        appSettingsSection.Settings.Add(key, this.AppSettingsToBeUpdatedBeforeDeploy[key]);
                    }
                    else
                    {
                        appSettingsSection.Settings.Add(key, this.AppSettingsToBeUpdatedBeforeDeploy[key]);
                    }
                }

                webConfig.Save(ConfigurationSaveMode.Modified);
            }
        }

        private Configuration GetWebConfiguration(string websiteDirectory)
        {
            string dummyVirtualPath = "/" + Guid.NewGuid();
            WebConfigurationFileMap map = new WebConfigurationFileMap();
            map.VirtualDirectories.Add(dummyVirtualPath, new VirtualDirectoryMapping(websiteDirectory, true));
            return WebConfigurationManager.OpenMappedWebConfiguration(map, dummyVirtualPath);
        }

        private void UnDeployOnIISExpress()
        {
            if (this.iisProcess != null)
            {
                this.iisProcess.Kill();
                
                Trace.TraceInformation("IIS Express Output {0} with Pid {1} is {2}", this.baseAddressOfWebsite, this.iisProcess.Id, this.redirectOutput ? this.iisProcess.StandardOutput.ReadToEnd() : "(no output read)");
            }

            this.iisProcess = null;
        }

        private HttpClient GetClientObject()
        {
            HttpClient client = new HttpClient();
            client.Timeout = this.clientTimeOutInMin;
            client.BaseAddress = new Uri(this.baseAddressOfWebsite);
            
            return client;
        }
    }
}
