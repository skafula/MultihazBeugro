﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using static Program;

internal class Program
{
    private static void Main(string[] args)
    {
        string domainBasePath = @"C:\Users\skafula\Desktop\Domain\DomainPath\Test";
        string subdomainBasePath = @"C:\Users\skafula\Desktop\Subdomain\SubPath\Test";
        FileManager fileManager = new FileManager(new CatalogManager(domainBasePath), new CatalogManager(subdomainBasePath));
        fileManager.SyncFiles();
    }
    #region FileManager class for doing final operations with file + catalog/log update

    public class FileManager
    {
        private CatalogManager _domainCatalogManager;
        private CatalogManager _subdomainCatalogManager;
        private string _logPath;
        private static List<CurrentFile> _subdomainFinalCatalog;

        public FileManager(CatalogManager domainCatalogManager, CatalogManager subdomainCatalogManager)
        {
            _domainCatalogManager = domainCatalogManager;
            _subdomainCatalogManager = subdomainCatalogManager;
            _logPath = Path.Combine(subdomainCatalogManager.BasePath, "log.txt");
            _subdomainFinalCatalog = new List<CurrentFile>();
        }
        //Makes operation/reverse operation to check differences and call neccessery methods
        public void SyncFiles()
        {

            foreach (CurrentFile domainFile in _domainCatalogManager.CurrentFilesList)
            {
                CurrentFile? correspondingSubdomainFile = _subdomainCatalogManager.CurrentFilesList
                    .FirstOrDefault(sdFile => sdFile.FileName == domainFile.FileName && sdFile.RelativePath == domainFile.RelativePath);

                if(correspondingSubdomainFile == null)
                {
                    CreateFile(domainFile);
                }
                else if (correspondingSubdomainFile.LastModifiedDate.ToString("yyyy.MM.dd") != domainFile.LastModifiedDate.ToString("yyyy.MM.dd"))
                {
                    OverwriteFile(domainFile, correspondingSubdomainFile);
                }
            }

            //Reverse checking if a file has been deleted
            foreach (CurrentFile subdomainFile in _subdomainCatalogManager.CurrentFilesList)
            {
                CurrentFile? correspondingDomainFile = _domainCatalogManager.CurrentFilesList
                    .FirstOrDefault(dFile => dFile.FileName == subdomainFile.FileName && dFile.RelativePath == subdomainFile.RelativePath);

                if (correspondingDomainFile == null)
                {
                    DeletedFile(subdomainFile);
                }
            }
            UpdateCatalog();
        }
        //Method making copy new file 
        //TODO: finish catalogupdate!!
        private void CreateFile(CurrentFile domainFile)
        {
            string srcFileFullPath = Path.Combine(_domainCatalogManager.BasePath, domainFile.RelativePath);
            string targetPath = Path.Combine(_subdomainCatalogManager.BasePath, domainFile.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            using (FileStream sourceStream = new FileStream(srcFileFullPath, FileMode.Open))
            using (FileStream destinationStream = new FileStream(targetPath, FileMode.Create))
            {
                sourceStream.CopyTo(destinationStream);
            }

            FileTimestampCorrection(new FileInfo(srcFileFullPath), new FileInfo(targetPath));
            domainFile.Status = FileStatus.Copied;
            _subdomainCatalogManager.CurrentFilesList.Add(domainFile);
        }

        //Method makes overwrite if a file modified
        //TODO: add log line and update object entity to finalize serialization
        private void OverwriteFile(CurrentFile srcFile, CurrentFile destFile)
        {
            string srcFileFullPath = Path.Combine(_domainCatalogManager.BasePath, srcFile.RelativePath);
            string destFileFullPath = Path.Combine(_subdomainCatalogManager.BasePath, destFile.RelativePath);

            using (FileStream sourceStream = new FileStream(srcFileFullPath, FileMode.Open))
            using (FileStream destinationStream = new FileStream(destFileFullPath, FileMode.Create))
            {
                sourceStream.CopyTo(destinationStream);
            }

            FileTimestampCorrection(new FileInfo(srcFileFullPath), new FileInfo(destFileFullPath));
            destFile.Status = FileStatus.Modified;
            destFile.LastModifiedDate = srcFile.LastModifiedDate;
            LogEvent(destFile);
        }

        //Method for checking if a file has been removed
        private void DeletedFile(CurrentFile fileToDelete)
        {
            fileToDelete.Status = FileStatus.Deleted;
            LogEvent(fileToDelete);
        }

        //____Need to check if this method neccessery or not!! Or if it's in the right class?!
        private void UpdateCatalog()
        {
            foreach (CurrentFile file in _domainCatalogManager.CurrentFilesList)
            {
                _subdomainFinalCatalog.Add(file);
            }
            foreach (CurrentFile file in _subdomainCatalogManager.CurrentFilesList)
            {
                if (file.Status == FileStatus.Deleted)
                {
                    _subdomainFinalCatalog.Add(file);
                }
            }

            string jsonString = JsonConvert.SerializeObject(_subdomainFinalCatalog, new IsoDateTimeConverter { DateTimeFormat = "yyyy.MM.dd" });
            string catalogFilePath = Path.Combine(_subdomainCatalogManager.BasePath, "catalog.json");
            File.WriteAllText(catalogFilePath, jsonString);
        }

        //Method to update log file depends on the action 
        private void LogEvent(CurrentFile file)
        {
            using (StreamWriter sw = new StreamWriter(_logPath, true)) // true for append mode
            {
                string logText = $"{file.RelativePath}, {file.FileName}, {file.Status}, {DateTime.Now.ToString("yyyy.MM.dd")}";
                sw.WriteLine(logText);
            }
        }
        //Changes subdomain copied/overwrited file's Dates to the Original
        private void FileTimestampCorrection(FileInfo srcFileInfo, FileInfo destFileInfo)
        {

            destFileInfo.CreationTime = srcFileInfo.CreationTime;
            destFileInfo.LastWriteTime = srcFileInfo.LastWriteTime;
        }
    }
    #endregion

    #region CatalogManagerClass reading and creating catalog

    //Class for read catalog or create catalog depends on the catalog existence
    public class CatalogManager
    {
        private List<CurrentFile> _currentFiles;
        private string _basePath;

        public List<CurrentFile> CurrentFilesList { get { return _currentFiles; } }
        public string BasePath { get { return _basePath; } set { _basePath = value; } }

        public CatalogManager(string basePath)
        {
            _basePath = basePath;
            string catalogFilePath = Path.Combine(_basePath, "catalog.json");

            if (File.Exists(catalogFilePath))
            {
                string jsonString = File.ReadAllText(catalogFilePath);
                _currentFiles = JsonConvert.DeserializeObject<List<CurrentFile>>(jsonString);
                foreach (CurrentFile file in _currentFiles)
                {
                    file.FullPath = Path.Combine(basePath, file.RelativePath);
                }
            }
            else
            {
                _currentFiles = GenerateCatalogFromBasePath(_basePath);

                string jsonString = JsonConvert.SerializeObject(_currentFiles, new IsoDateTimeConverter { DateTimeFormat = "yyyy.MM.dd" });
                File.WriteAllText(catalogFilePath, jsonString);
            }
        }

        //Creates FileInfo while GetFiles from all directories to create List<CurrentFile>
        private List<CurrentFile> GenerateCatalogFromBasePath(string basePath)
        {
            List<CurrentFile> files = new List<CurrentFile>();

            foreach (string filePath in Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories))
            {
                FileInfo fileInfo = new FileInfo(filePath);
                CurrentFile currentFile = new CurrentFile(
                    fileInfo.CreationTime,
                    filePath,
                    basePath,
                    fileInfo.Name,
                    fileInfo.Length,
                    fileInfo.LastWriteTime,
                    FileStatus.Default
                    );

                files.Add(currentFile);
            }
            return files;
        }
    }
    #endregion

    #region CurrentFile class modeling file object & enum for filestatus
    //Class to create file object with neccessery informations.
    public class CurrentFile
    {
        private DateTime _creationDate;
        private string _relativePath;
        private string _fileName;
        private long _fileSize;
        private string _fullPath;
        private DateTime _lastModifiedDate;

        public DateTime CreationDate { get { return _creationDate; } set { _creationDate = value; } }
        public string RelativePath { get { return _relativePath; } set { _relativePath = value; } }
        public string FileName { get { return _fileName; } set { _fileName = value; } }
        public long FileSize { get { return _fileSize; } set { _fileSize = value; } }
        [JsonIgnore]
        public string FullPath { get { return _fullPath; } set { _fullPath = value; } }
        public DateTime LastModifiedDate { get { return _lastModifiedDate; } set { _lastModifiedDate = value; } }
        [JsonIgnore]
        public FileStatus Status { get; set; }

        public CurrentFile()
        {
        }

        public CurrentFile(DateTime creationDate, string fullPath, string basePath, string fileName, long fileSize, DateTime lastModified, FileStatus status)
        {
            FileSize = fileSize;
            CreationDate = creationDate;
            RelativePath = GetRelativePath(fullPath, basePath);
            FileName = fileName;
            LastModifiedDate = lastModified;
            Status = status;
            FullPath= fullPath;
        }

        private string GetRelativePath(string fullPath, string basePath)
        {
            return fullPath.Substring(basePath.Length+1);
        }
    }

    //Short enum to store status for easier catalog making
    public enum FileStatus
    {
        Default,
        Copied,
        Deleted,
        Modified
    }
    #endregion
}