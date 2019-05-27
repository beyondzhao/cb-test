using System;
using System.IO;
using System.Threading;

using COSXML;
using COSXML.Auth;
using COSXML.Transfer;
using COSXML.CosException;
using COSXML.Model;
using COSXML.Model.Object;
using COSXML.Utils;
using SimpleJSON;

namespace UploadProject
{
    public class CosFacade
    {

        private CosXml cosXml;
        private JSONNode cosInfo;
        private TransferManager transferManager;

        private static CosFacade instance;

        private CosFacade()
        {
            init();
        }

        private void init()
        {
            UcbFacade ucbFacade = UcbFacade.GetInstance();
            cosInfo = ucbFacade.GetCredantial();

            CosXmlConfig config = new CosXmlConfig.Builder()
                .SetConnectionTimeoutMs(60000)  //ms
                .SetReadWriteTimeoutMs(40000)  //ms
                .IsHttps(true)
                .SetAppid(cosInfo["appId"])
                .SetRegion(cosInfo["region"])
                .SetDebugLog(true)
                .Build();

            QCloudCredentialProvider cosCredentialProvider = new DefaultSessionQCloudCredentialProvider(cosInfo["secretId"], cosInfo["secretKey"], cosInfo["expireTime"], cosInfo["token"]);
            cosXml = new CosXmlServer(config, cosCredentialProvider);
            transferManager = new TransferManager(cosXml, new TransferConfig());
        }

        public static CosFacade GetInstance()
        {
            lock (typeof(CosFacade))
            {
                if (instance == null)
                {
                    instance = new CosFacade();
                }
            }
            return instance;
        }

        public void DownloadFile(string key, string localDir, string localFileName)
        {
            COSXMLDownloadTask downloadTask = new COSXMLDownloadTask(cosInfo["bucket"], null, key, localDir, localFileName)
            {
                progressCallback = delegate (long completed, long total)
                {
                    Console.WriteLine(String.Format("progress = {0} / {1} : {2:##.##}%", completed, total, completed * 100.0 / total));
                },
                successCallback = delegate (CosResult cosResult)
                {
                    COSXML.Transfer.COSXMLDownloadTask.DownloadTaskResult result = cosResult as COSXML.Transfer.COSXMLDownloadTask.DownloadTaskResult;
                    Console.WriteLine(result.GetResultInfo());
                    Console.WriteLine(String.Format("currentThread id = {0}", Thread.CurrentThread.ManagedThreadId));
                    Console.WriteLine("Finish to download apk from COS - {0}", DateTime.Now);
                },
                failCallback = delegate (CosClientException clientEx, CosServerException serverEx)
                {
                    if (clientEx != null)
                    {
                        Console.WriteLine("CosClientException: " + clientEx.StackTrace);
                    }
                    if (serverEx != null)
                    {
                        Console.WriteLine("CosServerException: " + serverEx.GetInfo());
                    }
                    Console.WriteLine(String.Format("currentThread id = {0}", Thread.CurrentThread.ManagedThreadId));
                }
            };
            transferManager.Download(downloadTask);
            Console.WriteLine("Start to download apk from COS - {0}", DateTime.Now);
        }

        public void UploadProject(string fileName, string projectId, COSXML.Callback.OnProgressCallback mProgressCallback, COSXML.Callback.OnSuccessCallback<CosResult> mSuccessCallback, COSXML.Callback.OnFailedCallback mFailCallback)
        {
            if (cosInfo["appId"]== null || cosXml == null) 
            {
                init();
            }
            
            string key = String.Format("{0}/{1}", projectId, Path.GetFileName(fileName));

            if (CheckFileExists(key))
            {
                Console.WriteLine("File exists in bucket {0}.", cosInfo["bucket"]);
                return;
            }

            COSXMLUploadTask uploadTask = new COSXMLUploadTask(cosInfo["bucket"], null, key)
            {
                progressCallback = delegate (long completed, long total)
                {
                    Console.WriteLine(String.Format("progress = {0} / {1} : {2:##.##}%", completed, total, completed * 100.0 / total));

                    mProgressCallback(completed, total);
                },
                successCallback = delegate (CosResult cosResult)
                {
                    COSXML.Transfer.COSXMLUploadTask.UploadTaskResult result = cosResult as COSXML.Transfer.COSXMLUploadTask.UploadTaskResult;
                    Console.WriteLine(result.GetResultInfo());
                    Console.WriteLine(String.Format("currentThread id = {0}", Thread.CurrentThread.ManagedThreadId));
                    Console.WriteLine("Finish to upload zip file to COS - {0}", DateTime.Now);

                    mSuccessCallback(cosResult);
                },
                failCallback = delegate (CosClientException clientEx, CosServerException serverEx)
                {
                    if (clientEx != null)
                    {
                        Console.WriteLine("CosClientException: " + clientEx.StackTrace);
                    }
                    if (serverEx != null)
                    {
                        Console.WriteLine("CosServerException: " + serverEx.GetInfo());
                    }
                    Console.WriteLine(String.Format("currentThread id = {0}", Thread.CurrentThread.ManagedThreadId));

                    mFailCallback(clientEx, serverEx);
                }

            };
            //uploadTask.SetSrcPath(newFileName, offset, sendContentLength);
            uploadTask.SetSrcPath(fileName);

            transferManager.Upload(uploadTask);

            Console.WriteLine("Start to upload zip file to COS - {0}", DateTime.Now);
        }

        private bool CheckFileExists(string key)
        {
            try
            {
                HeadObjectRequest request = new HeadObjectRequest(cosInfo["bucket"], key);
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                HeadObjectResult result = cosXml.HeadObject(request);

                Console.WriteLine(result.GetResultInfo());
                return true;
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                Console.WriteLine("CosClientException: " + clientEx.StackTrace);
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                Console.WriteLine("CosServerException: " + serverEx.GetInfo());
            }

            return false;
        }
    }
}
