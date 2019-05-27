using System;
using System.IO;
using System.Net;
using UnityEngine;

namespace UploadProject
{
    public class FTP
    {
        private string remoteHost;
        private string remotePort;
        private string username;
        private string password;

        public FTP()
        {
            this.remoteHost = string.Empty;
            this.remotePort = "21";
            this.username = string.Empty;
            this.password = string.Empty;
        }

        public FTP(string remoteHost, string username, string password)
        {
            this.remoteHost = remoteHost;
            this.remotePort = "21";
            this.username = username;
            this.password = password;
        }

        public string getRemoteHost()
        {
            return remoteHost;
        }

        public void setRemoteHost(string remoteHost)
        {
            this.remoteHost = remoteHost;
        }

        public string getRemotePort()
        {
            return remotePort;
        }

        public void setRemotePort(string remotePort)
        {
            this.remotePort = remotePort;
        }

        public string getUsername()
        {
            return username;
        }

        public void setUsername(string username)
        {
            this.username = username;
        }

        public string getPassword()
        {
            return password;
        }

        public void setPassword(string password)
        {
            this.password = password;
        }

        public string GetEditorLog(string logUrl)
        {
            Uri uri = new Uri(logUrl);
            Console.WriteLine("ftp url is {0} - {1}", uri, DateTime.Now);
            WebClient request = new WebClient();

            // This example assumes the FTP site uses anonymous logon.
            request.Credentials = new NetworkCredential(username, password);
            try
            {
                byte[] newFileData = request.DownloadData(logUrl);
                string fileString = System.Text.Encoding.UTF8.GetString(newFileData);
                return fileString;
            }
            catch (WebException e)
            {
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        public void DownloadProject(string downloadPath, string localStoragePath, IProgress<double> progress)
        {
            Console.WriteLine("Start to download apk from FTP server - {0}", DateTime.Now);

            Uri uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}", remoteHost, remotePort, downloadPath));
            Console.WriteLine("ftp url is {0} - {1}", uri, DateTime.Now);

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Credentials = new NetworkCredential(username, password);
            request.UsePassive = false;
            request.KeepAlive = true;
            request.UseBinary = true;

            request.Method = WebRequestMethods.Ftp.DownloadFile;

            FtpWebResponse response = (FtpWebResponse)request.GetResponse();
            long totalBytes = response.ContentLength, currentBytes = 0;

            Stream responseStream = response.GetResponseStream();

            using (FileStream fileStream = new FileStream(localStoragePath, FileMode.Create))
            {
                int bufferSize = 2048;
                int readCount;
                byte[] buffer = new byte[bufferSize];

                readCount = responseStream.Read(buffer, 0, bufferSize);
                while (readCount > 0)
                {
                    currentBytes += readCount;
                    progress.Report(currentBytes / (double)totalBytes);

                    fileStream.Write(buffer, 0, readCount);
                    readCount = responseStream.Read(buffer, 0, bufferSize);
                }
            }

            Console.WriteLine("Download Complete, status {0} - {1}", response.StatusDescription, DateTime.Now);

            response.Close();
        }

        public void UploadProject(string fileName, string projectId, IProgress<double> progress)
        {
            if (CheckFileExists(fileName, projectId))
            {
                Debug.Log(string.Format(@"File exists."));
                return;
            }

            Debug.Log(string.Format(@"Start to upload zip file to ftp server - {0}", DateTime.Now));

            Uri uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}/{3}", remoteHost, remotePort, projectId, Path.GetFileName(fileName)));
            Debug.Log(string.Format(@"ftp url is {0} - {1}", uri, DateTime.Now));

            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Credentials = new NetworkCredential(username, password);
            request.UsePassive = false;
            request.KeepAlive = true;
            request.UseBinary = true;

            request.Method = WebRequestMethods.Ftp.UploadFile;

            Debug.Log(string.Format(@"*** {0} ***", DateTime.Now));
            Stream dest = request.GetRequestStream();
            Debug.Log(string.Format(@"*** {0} ***", DateTime.Now));

            FileStream src = File.OpenRead(fileName);

            int bufSize = (int)Math.Min(src.Length, 2048);
            byte[] buffer = new byte[bufSize];
            int bytesRead = 0;
            long currentBytes = 0;

            do
            {
                bytesRead = src.Read(buffer, 0, bufSize);
                dest.Write(buffer, 0, bufSize);

                currentBytes += bytesRead;
                progress.Report(currentBytes / (double)src.Length);
            }
            while (bytesRead != 0);

            dest.Close();
            src.Close();

            Debug.Log(string.Format(@"Finish to upload zip file to ftp server - {0}", DateTime.Now));

            using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
            {
                Debug.Log($"Upload File Complete, status {response.StatusDescription}");
            }
        }

        private bool CheckFileExists(string fileName, string projectId)
        {
            Uri uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}", remoteHost, remotePort, projectId));
            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            request.UsePassive = false;
            request.UseBinary = true;
            request.Credentials = new NetworkCredential(username, password);

            try
            {
                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    Console.WriteLine($"Make directory {projectId}, status {response.StatusDescription}");
                    return false;
                }

            }
            catch (WebException ex)
            {
                FtpWebResponse resp = (FtpWebResponse)ex.Response;
                if (resp.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    uri = new Uri(string.Format(@"ftp://{0}:{1}/{2}/{3}", remoteHost, remotePort, projectId, Path.GetFileName(fileName)));
                    request = (FtpWebRequest)WebRequest.Create(uri);
                    request.Method = WebRequestMethods.Ftp.GetFileSize;
                    request.UsePassive = false;
                    request.UseBinary = true;
                    request.Credentials = new NetworkCredential(username, password);

                    try
                    {
                        using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                        {
                            Console.WriteLine(response.ContentLength);  // for resume uploading
                            return true;
                        }
                    }
                    catch (WebException e)
                    {
                        Console.WriteLine(e);
                        return false;
                    }
                }
            }

            return false;
        }
    }
}
