using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.AnimatedValues;
using System;
using System.Text.RegularExpressions;
using UploadProject;
using System.IO.Compression;
using COSXML.Model;
using COSXML.CosException;
using System.Threading;
using SimpleJSON;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections;
using UcbUtils;

namespace UcbEditorWindow
{
    public class UcbEditorWindow : EditorWindow
    {
        const float SYNC_TASK_INTERVAL_AT_RUNNING = 3f, SYNC_TASK_INTERVAL_OUT_OF_RUNNING = 60f;
        const string cloudBuildTempPath = "/Temp/CloudBuild/";
        const string projectConfigPath = "project-config.conf";
        const string windowsGlobalConfigPath = "/Library/Application Support/Unity/Cloud Build/config.conf";
        const string osxGlobalConfigPath = "/Library/Application Support/Unity/Cloud Build/config.conf";

        bool showAdvancedSettings = false;
        string apiHost, apiPort;
        UcbFacade _ucbFacade;
        UcbFacade ucbFacade
        {
            get
            {
                UcbFacade.GetInstance().UpdateHost(apiHost, apiPort);
                return UcbFacade.GetInstance();
            }
        }

        FTP _ftp;
        FTP ftpInstance {
            get
            {
                if (_ftp == null)
                {
                    _ftp = new FTP();
                    _ftp.setRemoteHost(ftpHost);
                    _ftp.setUsername(ftpUserName);
                    _ftp.setPassword(ftpPassword);
                }
                return _ftp;
            }
            set
            {
                _ftp = value;
            }
        }


        JSONNode _taskInfo;
        JSONNode taskInfo { get { return _taskInfo; } set { _taskInfo = value; UpdateSyncTaskInterval(); } }
        float syncTaskInfoInterval;
        double lastSyncTaskTime = 0;

        private string cosEndPoint;
        //ftp related
        private string ftpHost = "129.211.136.209", ftpPort = "21", ftpUserName = "ucbFtp", ftpPassword = "ucbFtpPassword";
        //repository related
        private string repoUrl, repoUsername, repoPassword, repoToken, repoBranch; 
        private int repoType = (int)RepositoryType.Git;

        private string globalConfigPath;
        private string projectPath = "", projectZipFullName, projectSlug, unityVersion, progressTitle;
        private string taskId;
        private bool isProgressBarVisible, isQrCodeVisible;
        private float progressValue, forceRepaintProgress;
        private FileInfo projectSolutionFile;
        private AnimBool showExtraFields_Upload;
        private long cosUploadProgressCompleted, cosUploadProgressTotal;

        //List<SceneAsset> m_SceneAssets = new List<SceneAsset>();

        int gitCredentialModeFlag = 0;
        string[] gitCredentialOptions = Enum.GetNames(typeof(GitCredentialMode));

        int uploadTargetFlag = 0;
        string[] uploadTargetOptions = Enum.GetNames(typeof(TransferMode));
        bool[] buildTargets = new bool[3] { false, false, true };


        UcbEditorWindow()
        {
            titleContent = new GUIContent("Cloud Build");
            UpdateSyncTaskInterval();
        }

        [MenuItem("Component/Cloud Build")]
        public static void showWindow()
        {
            EditorWindow.GetWindow(typeof(UcbEditorWindow));

            //MyEditorWindow.CreateInstance<MyEditorWindow>().Show();
        }

        //
        // Life cycle
        //
        private void OnEnable()
        {
            autoRepaintOnSceneChange = true;
            minSize = new Vector2(300, 650);

            InitGlobalConfigPath();

            projectPath = Application.dataPath;
            DirectoryInfo projectInfo = Directory.GetParent(projectPath);
            projectPath = projectInfo.FullName;
            Debug.Log("Project path: " + projectPath);

            if (!Directory.Exists(projectPath + cloudBuildTempPath))
            {
                Directory.CreateDirectory(projectPath + cloudBuildTempPath);
            }
            //Directory.CreateDirectory("/Library/Application Support/Unity/Cloud Build/");
            try
            {
                GetLatestZip();
                LoadProjectConfig();
            }
            catch (Exception ex)
            {
                Debug.Log("" + ex);
            }

            unityVersion = Application.unityVersion;
            Debug.Log("Unity Version: " + unityVersion);

            showExtraFields_Upload = new AnimBool(true);
            showExtraFields_Upload.valueChanged.AddListener(Repaint);

            //
            //Get project slug from project.sln
            //
            foreach (FileInfo fileInfo in projectInfo.GetFiles())
            {
                if (fileInfo.Extension.Equals(".sln"))
                {
                    projectSolutionFile = fileInfo;
                }
            }
            if (projectSolutionFile != null)
            {
                string slnText = System.IO.File.ReadAllText(projectSolutionFile.FullName);
                Debug.Log("SLN content:" + slnText);
                string pattern1 = @"(?<=Project\(""\{)(.*)(?=\}""\) =)";
                projectSlug = new Regex(pattern1).Match(slnText).Value;
                Debug.Log("Project Slug:" + projectSlug);
            }
        }

        private void Update()
        {
            double curTime = EditorApplication.timeSinceStartup;
            if (curTime - lastSyncTaskTime > syncTaskInfoInterval)
            {
                lastSyncTaskTime = curTime;
                SyncBuildTaskInfo();
            }
        }

        void OnProjectChange()
        {

        }

        //
        // UI
        //
        void OnGUI()
        {
            GUI.color = Color.white;

            EditorGUILayout.LabelField("Mode", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            uploadTargetFlag = EditorGUILayout.Popup("Cloud Build Mode:",
                     uploadTargetFlag, uploadTargetOptions, EditorStyles.popup);
            EditorGUI.indentLevel--;

            //
            // Upload
            //
            if (uploadTargetFlag == (int)TransferMode.REPO) 
            {
                EditorGUILayout.Separator();
                EditorGUI.indentLevel++;
                repoUrl = EditorGUILayout.TextField("Repository Url:", repoUrl);
                repoBranch = EditorGUILayout.TextField("Branch name:", repoBranch);
                gitCredentialModeFlag = EditorGUILayout.Popup("Credential Mode:",
                     gitCredentialModeFlag, gitCredentialOptions, EditorStyles.popup);
                if (gitCredentialModeFlag == (int)GitCredentialMode.UserPassword)
                {
                    repoUsername = EditorGUILayout.TextField("Repo Username:", repoUsername);
                    repoPassword = EditorGUILayout.PasswordField("Repo Password:", repoPassword);
                }
                if (gitCredentialModeFlag == (int)GitCredentialMode.GitToken)
                {
                    repoToken = EditorGUILayout.TextField("Git Token:", repoToken);
                }

                EditorGUI.indentLevel--;
                EditorGUILayout.Separator();
            }
            else 
            {
                EditorGUILayout.Separator();
                EditorGUILayout.LabelField("Upload", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;

                if (GUILayout.Button("Pack"))
                {
                    StartPackingProject();
                }
                if (!string.IsNullOrEmpty(projectZipFullName))
                {
                    EditorGUILayout.LabelField("Latest Local Pack:", File.GetLastWriteTime(projectZipFullName).ToString());
                }

                if (uploadTargetFlag == (int)TransferMode.FTP)
                {
                    ftpHost = EditorGUILayout.TextField("FTP Host:", ftpHost);
                    ftpUserName = EditorGUILayout.TextField("FTP Username:", ftpUserName);
                    ftpPassword = EditorGUILayout.PasswordField("FTP Password:", ftpPassword);
                }
                if (GUILayout.Button("Upload"))
                {
                    StartUploadProject();
                }

                EditorGUI.indentLevel--;
            }
            //
            // Build
            //
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            string latestBuildTime = GetLatestBuildTimeString();
            if (GUILayout.Button("Build Settings"))
            {
                EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
            }
            EditorGUILayout.LabelField("Select Build Target");
            EditorGUI.indentLevel++;
            //buildTargets[0] = EditorGUILayout.Toggle("Standalong", buildTargets[0]);
            //buildTargets[1] = EditorGUILayout.Toggle("iOS", buildTargets[1]);
            buildTargets[2] = EditorGUILayout.Toggle("Android", buildTargets[2]);
            EditorGUI.indentLevel--;

            if (!string.IsNullOrEmpty(latestBuildTime))
            {
                EditorGUILayout.LabelField("Latest Build:", latestBuildTime);
            }

            if (string.IsNullOrEmpty(taskId))
            {
                EditorGUILayout.LabelField("Status", "Not Started");
            }
            else
            {
                string statusString = GetBuildStatusString();
                if (statusString == "FAILED")
                {
                    GUI.color = new Color(255, 0, 0);
                }
                EditorGUILayout.LabelField("Status", statusString);
                GUI.color = Color.white;
            }

            if (IsBuildTaskRunning())
            {
                if (GUILayout.Button("Cancel Cloud Build"))
                {
                    CancelCloudBuildAction();
                }
            } else
            {
                if (GUILayout.Button("Start Cloud Build"))
                {
                    StartCloudBuildAction();
                }
            }

            EditorGUI.indentLevel--;
            if (isProgressBarVisible)
            {
                EditorGUI.ProgressBar(new Rect(3, position.height - 30, position.width - 6, 20),
                    progressValue, progressTitle);
            }

            try
            {
                if (taskInfo["jobs"][0]["downloadLink"] != null)
                {
                    EditorGUILayout.Separator();
                    EditorGUILayout.LabelField("Download", EditorStyles.boldLabel);

                    if (GUILayout.Button("Download .apk"))
                    {
                        Application.OpenURL(taskInfo["jobs"][0]["downloadLink"]);
                        EditorUtility.ClearProgressBar();
                    }
                    if (GUILayout.Button("Copy Download Url"))
                    {
                        EditorGUIUtility.systemCopyBuffer = taskInfo["jobs"][0]["downloadLink"];
                        ShowNotification(new GUIContent("Copyed URL to clipboard"));
                        EditorUtility.ClearProgressBar();
                    }
                    if (GUILayout.Button("QR Code"))
                    {
                        UcbQrPopup.Open(taskInfo["jobs"][0]["downloadLink"]);
                    }
                }
                //
                //build just finished
                //
                if (taskInfo["jobs"][0]["exectionLog"] != null || taskInfo["jobs"][0]["logLink"] != null)
                {
                    if (GUILayout.Button("Print Log"))
                    {
                        if (taskInfo["jobs"][0]["exectionLog"] != null)
                        {
                            Debug.Log("Build Execution Log :" + taskInfo["jobs"][0]["exectionLog"]);
                        }
                        if (taskInfo["jobs"][0]["logLink"] != null)
                        {
                            Debug.Log("Editor Log :" + ftpInstance.GetEditorLog(taskInfo["jobs"][0]["logLink"]));
                        }
                    }
                }
            }
            catch (NullReferenceException ex)
            {
            }

            EditorGUILayout.Separator();
            GUIStyle foldOutStyle = new GUIStyle(EditorStyles.foldout);
            foldOutStyle.fontStyle = FontStyle.Bold;
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", foldOutStyle);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                apiHost = EditorGUILayout.TextField("Api Host:", apiHost);
                apiPort = EditorGUILayout.TextField("Api Port:", apiPort);
                cosEndPoint = EditorGUILayout.TextField("COS Host:", cosEndPoint);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawQrCode(string content)
        {
            float margin = 10;
            float qrSize = Math.Min(position.width - margin * 2, 200);
            margin = Math.Max(margin, (position.width - qrSize) / 2);

            GUI.DrawTexture(new Rect(margin, position.height - 300, qrSize, qrSize), UcbUtils.QRHelper.generateQR(content), ScaleMode.ScaleToFit);
        }

        //
        // Progress bar
        //
        private void ShowProgress(string withTitle)
        {
            isProgressBarVisible = true;
            UpdateProgress(0f);
            forceRepaintProgress = 0f;
            progressTitle = withTitle;
        }

        private void UpdateProgress(float progress)
        {
            progressValue = progress;
            if (progressValue - forceRepaintProgress > 0.1f)
            {
                Debug.Log(String.Format("progress = {0:##.##}%", progressValue * 100.0));
                forceRepaintProgress = progressValue;
            }
        }

        private void FinalizeProgress(string withTitle)
        {
            Debug.Log("Progress finished.");
            UpdateProgress(1f);
            progressTitle = withTitle;

            //Repaint();
        }

        private void HideProgress()
        {
            isProgressBarVisible = false;
        }


        //
        // Pack
        //
        void StartPackingProject()
        {
            EditorUtility.DisplayProgressBar("Packing Project", "Don't do any modification to your project before packing finished.", 0f);
            try
            {
                projectZipFullName = ZipHelper.CompressProject(projectPath, projectPath + cloudBuildTempPath + "project-pack.zip", new BasicProgress<double>(OnZipProgressChange));
            }
            catch (IOException ex)
            {
                //file hash exists
                throw ex;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(projectZipFullName);
        }

        void OnZipProgressChange(double value)
        {
            //Debug.Log(String.Format($"{value:P2} archiving complete"));
            EditorUtility.DisplayProgressBar("Packing Project", "Don't do any modification to your project before packing finished.", (float)value);
        }

        //
        // Upload
        //
        void StartUploadProject()
        {
            ShowProgress("Uploading to " + uploadTargetOptions[uploadTargetFlag]);
            UploadProject(projectZipFullName, projectSlug);
        }

        void UploadProject(string fileName, string projectId)
        {
            switch (uploadTargetFlag)
            {
                case (int)TransferMode.FTP:
                    SaveGlobalConfig();
                    ftpInstance.UploadProject(fileName, projectId, new BasicProgress<double>(p => OnFtpProgessChange((float)p)));
                    break;

                case (int)TransferMode.COS:
                    CosFacade cos = CosFacade.GetInstance();
                    cos.UploadProject(fileName, projectId, OnCosUploadProgressChange, OnCosUploadSuccess, OnCosUploadFail);
                    break;

                default:
                    Console.WriteLine("Not supported mode!");
                    return;
            }
        }

        void OnCosUploadProgressChange(long completed, long total)
        {
            UpdateProgress((float)completed / total);

            //Debug.Log(String.Format("progress = {0} / {1} : {2:##.##}%", completed, total, completed * 100.0 / total));
        }

        void OnCosUploadSuccess(CosResult cosResult)
        {
            FinalizeProgress("Upload Done");

            Debug.Log("Upload Succeeded.");
            Debug.Log(cosResult.GetResultInfo());
        }

        void OnCosUploadFail(CosClientException clientEx, CosServerException serverEx)
        {
            FinalizeProgress("Upload Failed");

            Debug.Log("Upload Failed.");
            if (clientEx != null)
            {
                Debug.Log("CosClientException: " + clientEx.StackTrace);
            }
            if (serverEx != null)
            {
                Debug.Log("CosServerException: " + serverEx.GetInfo());
            }
            Debug.Log(String.Format("currentThread id = {0}", Thread.CurrentThread.ManagedThreadId));
        }

        void OnFtpProgessChange(float progress)
        {
            if (1 - progress < 0.00001f)
            {
                FinalizeProgress("Upload Done");
            }
            else
            {
                UpdateProgress(progress);
            }
        }

        //
        // Build
        //
        private JSONNode BuildStartCloudBuildOptions()
        {
            JSONNode result = JSON.Parse("{}");
            result["projectUuid"] = projectSlug;
            result["type"] = "BUILD";
            result["uploadType"] = uploadTargetOptions[uploadTargetFlag];
            //TODO: format unity version
            result["parameters"]["unityVersion"] = "2018.3.12";
            string hashPattern = @"(?<=Temp\/CloudBuild\/)(.*)(?=\.zip)";
            //result["parameters"]["unityVersion"] = unityVersion;
            if (!string.IsNullOrEmpty(projectZipFullName))
            {
                string projectHash = new Regex(hashPattern).Match(projectZipFullName).Value;
                result["parameters"]["projectHash"] = projectHash;
            }
            result["parameters"]["buildTargets"][0] = "ANDROID";

            if (uploadTargetFlag == (int)TransferMode.FTP)
            {
                result["parameters"]["ftpServer"] = ftpHost;
                //TODO: ftp port
                result["parameters"]["ftpPort"] = "21";
                result["parameters"]["ftpUser"] = ftpUserName;
                result["parameters"]["ftpPwd"] = ftpPassword;
            }

            if (uploadTargetFlag == (int)TransferMode.REPO)
            {
                result["parameters"]["gitUrl"] = repoUrl;
                result["parameters"]["gitBranch"] = repoBranch;
                if (string.IsNullOrEmpty(repoToken))
                {
                    result["parameters"]["gitUser"] = repoUsername;
                    result["parameters"]["gitPwd"] = repoPassword;
                }
                else
                {
                    result["parameters"]["gitToken"] = repoToken;
                }
            }


            return result;
        }


        void StartCloudBuildAction()
        {
            HideProgress();

            JSONNode data = BuildStartCloudBuildOptions();
            JSONNode response = ucbFacade.PostTask(data);
            taskId = response["taskUuid"];
            taskInfo = response;
            UpdateSyncTaskInterval();
            SaveProjectConfig();

            Debug.Log(string.Format(@"Build Task [{0}] started with Jobs [{1}].", taskId, response["jobs"].ToString()));
        }

        void CancelCloudBuildAction()
        {
            if (!string.IsNullOrEmpty(taskId))
            {
                ucbFacade.CancelTask(taskId);
            }
        }

        void UpdateSyncTaskInterval()
        {
            if (IsBuildTaskRunning())
            {
                syncTaskInfoInterval = SYNC_TASK_INTERVAL_AT_RUNNING;
            } 
            else
            {
                syncTaskInfoInterval = SYNC_TASK_INTERVAL_OUT_OF_RUNNING;
            }
        }

        bool IsBuildTaskRunning() 
        {
            if (string.IsNullOrEmpty(GetBuildStatusString()))
            {
                return false;
            }
            return (int)Enum.Parse(typeof(JobStatus), GetBuildStatusString()) <= (int)JobStatus.RUNNING;
        }

        //
        // Collecting informations
        //
        void SyncBuildTaskInfo()
        {
            if (!string.IsNullOrEmpty(taskId))
            {
                taskInfo = ucbFacade.GetTaskDetail(taskId);
                UpdateSyncTaskInterval();
            }
        }

        string GetLatestBuildTimeString()
        {
            try
            {
                return DateTime.Parse(taskInfo["createdTime"]).ToLocalTime().ToString();
            }
            catch (NullReferenceException ex)
            {
                //Debug.Log(ex);
                return null;
            }
        }

        string GetBuildStatusString()
        {
            try
            {
                return taskInfo["jobs"][0]["status"];
            }
            catch (NullReferenceException ex)
            {
                //Debug.Log(ex);
                return null;
            }
        }

        string GetLatestZip()
        {
            string tempPath = projectPath + cloudBuildTempPath;
            string[] fileNames = Directory.GetFiles(tempPath);
            string result = null;
            DateTime latestZipTime = DateTime.MinValue;
            foreach (string fileName in fileNames)
            {
                if (fileName.Contains(".zip"))
                {
                    DateTime thisZipTime = File.GetLastWriteTime(fileName);
                    if (thisZipTime > latestZipTime)
                    {
                        result = fileName;
                        latestZipTime = thisZipTime;
                    }
                }
            }
            if (!string.IsNullOrEmpty(result))
            {
                projectZipFullName = result;
                return File.GetLastWriteTime(result).ToLongTimeString();
            }
            return null;
        }

        //
        // Project Config
        //
        void SaveProjectConfig()
        {
            string path = projectPath + cloudBuildTempPath + projectConfigPath;
            JSONNode configJson = JSON.Parse("{}");
            configJson["taskId"] = taskId;
            configJson["gitUserName"] = repoUsername;
            configJson["gitPassword"] = repoPassword;
            configJson["gitUrl"] = repoUrl;

            //TODO: save repository config
            File.WriteAllText(path, configJson.ToString());
        }

        void LoadProjectConfig()
        {
            string path = projectPath + cloudBuildTempPath + projectConfigPath;
            try
            {
                string configJsonString = File.ReadAllText(path);
                JSONNode configJson = JSON.Parse(configJsonString);
                taskId = configJson["taskId"];
                repoUsername = configJson["gitUserName"];
                repoPassword = configJson["gitPassword"];
                repoUrl = configJson["gitUrl"];

                Debug.Log("Load Project Config: " + configJsonString);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        //
        // Global config
        //
        // Save user related information to appdata path of Unity editor.
        //
        void InitGlobalConfigPath()
        {
            //use project temp path for undetermined OS
            globalConfigPath = projectPath + cloudBuildTempPath + projectConfigPath;

            // Prints "Windows 7 (6.1.7601) 64bit" on 64 bit Windows 7
            // Prints "Mac OS X 10.10.4" on Mac OS X Yosemite
            if (SystemInfo.operatingSystem.Contains("Windows"))
            {
                globalConfigPath = windowsGlobalConfigPath;
            }
            else if (SystemInfo.operatingSystem.Contains("Mac OS"))
            {
                globalConfigPath = osxGlobalConfigPath;
            }
        }

        void SaveGlobalConfig()
        {
            JSONNode configJson = JSON.Parse("{}");
            configJson["ftpHost"] = ftpHost;
            configJson["ftpPort"] = ftpPort;
            configJson["ftpUserName"] = ftpUserName;
            configJson["ftpPassword"] = ftpPassword;
            configJson["apiHost"] = apiHost;
            configJson["apiPort"] = apiPort;
            configJson["cosEndPoint"] = cosEndPoint;
            Debug.Log("Save Global Config: " + configJson.ToString());
            File.WriteAllText(globalConfigPath, configJson.ToString());
        }

        void LoadGlobalConfig()
        {
            try
            {
                string configJsonString = File.ReadAllText(globalConfigPath);
                JSONNode configJson = JSON.Parse(configJsonString);
                ftpHost = configJson["ftpHost"];
                ftpPort = configJson["ftpPort"];
                ftpUserName = configJson["ftpUserName"];
                ftpPassword = configJson["ftpPassword"];
                apiHost = configJson["apiHost"];
                apiPort = configJson["apiPort"];
                cosEndPoint = configJson["cosEndPoint"];
                Debug.Log("Load Global Config: " + configJsonString);
            }
            catch (Exception ex)
            {
                Debug.Log("No Global Config");
            }
        }
    }
}