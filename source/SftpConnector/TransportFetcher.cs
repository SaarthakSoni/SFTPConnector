//-----------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------

namespace SFTPConnector
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading.Tasks;
    using SFTPConnector.Messaging;
    using Newtonsoft.Json;

    /// <summary>
    /// 
    /// </summary>
    public class TransportFetcher
    {
        private readonly BaseTransportController baseController;
        private readonly string pollingPath;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="baseTransportController"></param>
        /// <param name="path"></param>
        public TransportFetcher(BaseTransportController baseTransportController, string path)
        {
            baseController = baseTransportController;
            this.pollingPath = path;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resourceMetadata"></param>
        /// <param name="FileType"></param>
        /// <returns></returns>
        public async Task<File> GetResource(string resourceMetadata, FileType FileType)
        {
            HttpResponseMessage response = await baseController.GetFile(System.IO.Path.Combine(this.pollingPath, resourceMetadata), FileType);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string responseData = await response.Content.ReadAsStringAsync();
                return TransportFetcher.ParseJson<File>(responseData);
            }
            else
            {
                Trace.TraceError("Got error code {0} while trying to fetch file {1}", response.StatusCode, resourceMetadata);
            }

            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<Tuple<IList<string>, HttpResponseMessage>> OnListing()
        {
            return await this.GetFileMetadata();
        }

        internal static C ParseJson<C>(string json)
        {
            return JsonConvert.DeserializeObject<C>(json);
        }

        /// <summary>
        /// GetFileMetadata
        /// </summary>
        /// <returns></returns>
        private async Task<Tuple<IList<string>, HttpResponseMessage>> GetFileMetadata()
        {
            List<string> filesToFetch = new List<string>();

            HttpResponseMessage response = await baseController.ListFiles(this.pollingPath);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string responseData = await response.Content.ReadAsStringAsync();
                Collection<FileInfo> filedata = ParseJson<Collection<FileInfo>>(responseData);

                foreach (FileInfo fileinfo in filedata)
                {
                    if (baseController.CheckRegex(fileinfo.FileName))
                    {
                        filesToFetch.Add(fileinfo.FileName);
                    }
                }
            }
            else
            {
                Trace.TraceError("Failed to get the file listing {0} ", response.StatusCode);
            }

            return new Tuple<IList<string>, HttpResponseMessage>(filesToFetch, response);
        }
    }
}