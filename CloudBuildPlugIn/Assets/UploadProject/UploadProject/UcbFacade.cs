using System;
using System.IO;
using System.Net;
using UnityEngine;
using SimpleJSON;
using System.Text;

namespace UploadProject
{
    public class UcbFacade
    {
        const string defaultApiHost = "http://129.211.136.209";
        const string defaultApiPort = "3005";

        private string host;
        private string port;

        private static UcbFacade instance;

        public string getHost()
        {
            return host;
        }

        public void setHost(string host)
        {
            this.host = host;
        }

        public string getPort()
        {
            return port;
        }

        public void setPort(string port)
        {
            this.port = port;
        }

        public static UcbFacade GetInstance()
        {
            lock (typeof(UcbFacade))
            {
                if (instance == null)
                {
                    instance = new UcbFacade();
                }
            }
            return instance;
        }

        public UcbFacade()
        {
            setHost(defaultApiHost);
            setPort(defaultApiPort);
        }

        public void UpdateHost(string apiHost, string apiPort)
        {
            if (string.IsNullOrEmpty(apiHost) || string.IsNullOrEmpty(apiPort))
            {
                setHost(defaultApiHost);
                setPort(defaultApiPort);
            }
            else
            {
                setHost(apiHost);
                setPort(apiPort);
            }
        }

        public JSONNode GetCredantial()
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}", host, port, @"v1/credential"));
            Console.WriteLine(uri);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine("Success to get credential from ucb server.");
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        return JSON.Parse(reader.ReadToEnd());
                    }
                }
                else
                {
                    Console.WriteLine("Error to get credential from ucb server.");
                    return null;
                }
            }
        }

        public JSONNode PostTask(JSONNode data)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}", host, port, @"v1/task"));
            Console.WriteLine(uri);
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Post;
            request.Accept = "application/json";
            request.ContentType = "application/json";
            Debug.Log("Post Url: " + uri.ToString());
            Debug.Log(data.ToString());
            byte[] dataBytes = Encoding.UTF8.GetBytes(data.ToString());
            request.ContentLength = dataBytes.Length;
            using (Stream reqStream = request.GetRequestStream())
            {
                reqStream.Write(dataBytes, 0, dataBytes.Length);
                reqStream.Close();
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        JSONNode result = JSON.Parse(reader.ReadToEnd());
                        Console.WriteLine(string.Format(@"Post task [{0}] success.", result["taskUuid"]));
                        return result;
                    }
                }
                else
                {
                    Console.WriteLine("Error to Post Task to ucb server.");
                    return null;
                }
            }
        }

        public JSONNode GetTaskDetail(string taskId)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}/{3}", host, port, @"v1/task", taskId));
            Console.WriteLine(uri);
            //Debug.Log("Get Task Url: " + uri.ToString());
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Get;
            request.Accept = "application/json";

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        string jsonString = reader.ReadToEnd();
                        JSONNode result = JSON.Parse(jsonString);
                        Console.WriteLine(string.Format(@"Task [{0}] Detail: {1}", taskId, jsonString));
                        //Debug.Log(string.Format(@"Task [{0}] Detail: {1}", taskId, jsonString));
                        return result;
                    }
                }
                else
                {
                    Console.WriteLine("Error to get Task Detail from ucb server.");
                    return null;
                }
            }
        }

        public JSONNode CancelTask(string taskId)
        {
            Uri uri = new Uri(string.Format(@"{0}:{1}/{2}/{3}/cancel", host, port, @"v1/task", taskId));
            Console.WriteLine(uri);
            //Debug.Log("Get Task Url: " + uri.ToString());
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = WebRequestMethods.Http.Post;
            request.Accept = "application/json";

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        string jsonString = reader.ReadToEnd();
                        JSONNode result = JSON.Parse(jsonString);
                        Debug.Log(string.Format(@"Cancel Task: {0}", result));
                        return result;
                    }
                }
                else
                {
                    Console.WriteLine("Error to Cancel Task.");
                    return null;
                }
            }
        }
    }
}
