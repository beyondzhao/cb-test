using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace System.IO.Compression
{
    public static class ZipHelper
    {
        public static string[] fileFilter = { ".csproj", ".sln", ".suo", ".tmp", ".userprefs", ".app", ".VC.", ".DS_Store", ".swp", ".log", ".pyc", ".git", ".vs", "UploadProject.meta" };
        public static string[] dirFilter = { "/Temp/", "/Build/", "/Builds/", "/temp/", "/build/", "/builds/", "/UploadProject/" };

        public static string CompressProject(string source, string target, IProgress<double> progress)
        {
            File.Delete(target);

            Console.WriteLine("Start to compress project - {0}", DateTime.Now);
            //CreateFromDirectory(source, target, CompressionLevel.Fastest, true, Encoding.UTF8, fileName => !SkipToCompress(fileName));
            CreateFromDirectory(source, target, CompressionLevel.Fastest, true, fileInfo => !SkipToCompress(fileInfo), new UploadProject.BasicProgress<double>(p => Console.WriteLine($"{p:P2} archiving complete")));
            Console.WriteLine("Finish to compress project - {0}", DateTime.Now);

            //md5 hash
            string MD5Hash = CalculateMD5(target);
            Console.WriteLine("MD5Hash is {0}", MD5Hash);

            string newFileName = String.Format("{0}/{1}.zip", Path.GetDirectoryName(target), MD5Hash);
            Console.WriteLine("Renamed zip file is in {0}", newFileName);
            File.Move(target, newFileName);

            return newFileName;
        }

        public static void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName, CompressionLevel compressionLevel, bool includeBaseDirectory, Predicate<FileInfo> filter, IProgress<double> progress)
        {
            if (string.IsNullOrEmpty(sourceDirectoryName))
            {
                throw new ArgumentNullException("sourceDirectoryName");
            }
            if (string.IsNullOrEmpty(destinationArchiveFileName))
            {
                throw new ArgumentNullException("destinationArchiveFileName");
            }

            FileInfo[] sourceFiles = new DirectoryInfo(sourceDirectoryName).GetFiles("*", SearchOption.AllDirectories);
            double totalBytes = sourceFiles.Sum(f => {
                if (!filter(f))
                {
                    return 0L;
                }
                else
                {
                    return f.Length;
                }
            });
            long currentBytes = 0;

            using (var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
                {
                    foreach (FileInfo file in sourceFiles)
                    {
                        if (!filter(file))
                        {
                            Console.WriteLine(file.FullName);
                            continue;
                        }

                        //string entryName = file.FullName.Substring(sourceDirectoryName.Length + 1);
                        string entryName = GetEntryName(file.FullName, sourceDirectoryName, includeBaseDirectory);
                        ZipArchiveEntry entry = archive.CreateEntry(entryName, compressionLevel);
                        entry.LastWriteTime = file.LastWriteTime;

                        using (Stream inputStream = File.OpenRead(file.FullName))
                        using (Stream outputStream = entry.Open())
                        {
                            Stream progressStream = new UploadProject.StreamWithProgress(inputStream,
                                new UploadProject.BasicProgress<int>(i =>
                                {
                                    currentBytes += i;
                                    progress.Report(currentBytes / totalBytes);
                                }), null);

                            progressStream.CopyTo(outputStream);
                        }
                    }
                }
            }
        }


        public static void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName, CompressionLevel compressionLevel, bool includeBaseDirectory, Encoding entryNameEncoding, Predicate<string> filter)
        {
            if (string.IsNullOrEmpty(sourceDirectoryName))
            {
                throw new ArgumentNullException("sourceDirectoryName");
            }
            if (string.IsNullOrEmpty(destinationArchiveFileName))
            {
                throw new ArgumentNullException("destinationArchiveFileName");
            }

            var filesToAdd = Directory.GetFiles(sourceDirectoryName, "*", SearchOption.AllDirectories);
            var entryNames = GetEntryNames(filesToAdd, sourceDirectoryName, includeBaseDirectory);
            using (var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < filesToAdd.Length; i++)
                    {
                        // Add the following condition to do filtering:
                        if (!filter(filesToAdd[i]))
                        {
                            //Console.WriteLine(filesToAdd[i]);
                            continue;
                        }
                        archive.CreateEntryFromFile(filesToAdd[i], entryNames[i], compressionLevel);
                    }
                }
            }
        }

        private static string GetEntryName(string name, string sourceFolder, bool includeBaseName)
        {
            if (name == null || name.Length == 0)
                return String.Empty;

            if (includeBaseName)
                sourceFolder = Path.GetDirectoryName(sourceFolder);

            int length = string.IsNullOrEmpty(sourceFolder) ? 0 : sourceFolder.Length;
            if (length > 0 && sourceFolder != null && sourceFolder[length - 1] != Path.DirectorySeparatorChar && sourceFolder[length - 1] != Path.AltDirectorySeparatorChar)
                length++;

            return name.Substring(length);
        }

        private static string[] GetEntryNames(string[] names, string sourceFolder, bool includeBaseName)
        {
            if (names == null || names.Length == 0)
                return new string[0];

            if (includeBaseName)
                sourceFolder = Path.GetDirectoryName(sourceFolder);

            int length = string.IsNullOrEmpty(sourceFolder) ? 0 : sourceFolder.Length;
            if (length > 0 && sourceFolder != null && sourceFolder[length - 1] != Path.DirectorySeparatorChar && sourceFolder[length - 1] != Path.AltDirectorySeparatorChar)
                length++;

            var result = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = names[i].Substring(length);
            }

            return result;
        }

        private static bool SkipToCompress(FileInfo file)
        {
            if (file.Attributes.HasFlag(FileAttributes.Hidden))
            {
                return true;
            }

            return fileFilter.Any(c => file.FullName.Contains(c)) || dirFilter.Any(c => file.FullName.Contains(c));
        }

        private static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    StringBuilder sBuilder = new StringBuilder();

                    for (int i = 0; i < hash.Length; i++)
                    {
                        sBuilder.Append(hash[i].ToString("x2"));
                    }

                    return sBuilder.ToString();
                }
            }
        }
    }
}