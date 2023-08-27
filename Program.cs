using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

internal class Program
{
    private static void Main(string[] args)
    {
        string domainBasePath = @"C:\Users\skafula\Desktop\Domain\DomainPath\Test";
        string subdomainBasePath = @"C:\Users\skafula\Desktop\Subdomain\SubPath\Test";

        CatalogManager domainCatalogManager = new CatalogManager(domainBasePath);
        CatalogManager subdomainCatalogManager = new CatalogManager(subdomainBasePath);

        foreach (CurrentFile file in domainCatalogManager.CurrentFilesList)
        {
            Console.WriteLine($"FileName: {file.FileName}, CreationDate: {file.CreationDate}, FileSize: {file.FileSize}, LastModifiedDate: {file.LastModifiedDate}, RelativePath: {file.RelativePath}\n");
        }
        foreach (CurrentFile file in subdomainCatalogManager.CurrentFilesList)
        {
            Console.WriteLine($"FileName: {file.FileName}, CreationDate: {file.CreationDate}, FileSize: {file.FileSize}, LastModifiedDate: {file.LastModifiedDate}, RelativePath: {file.RelativePath}\n");
        }
    }
    #region FileManager class for doing final operations with file + catalog/log update

    public class FileManager
    {
        private ICatalogManager _domainCatalogManager;
        private ICatalogManager _subdomainCatalogManager;
        private string _logPath;

        public FileManager(ICatalogManager domainCatalogManager, ICatalogManager subdomainCatalogManager)
        {
            _domainCatalogManager = domainCatalogManager;
            _subdomainCatalogManager = subdomainCatalogManager;
            _logPath = Path.Combine(subdomainCatalogManager.BasePath, "log.txt");
        }
        //Makes operation/reverse operation to check differences and call neccessery methods
        public void SyncFiles(List<CurrentFile> domainList, List<CurrentFile> subdomainList)
        {
            foreach (CurrentFile domainFile in domainList)
            {
                CurrentFile? correspondingSubdomainFile = subdomainList
                    .FirstOrDefault(sdFile => sdFile.FileName == domainFile.FileName && sdFile.RelativePath == domainFile.RelativePath);

                if(correspondingSubdomainFile == null)
                {
                    CreateFile(domainFile);
                }
                else if (correspondingSubdomainFile.LastModifiedDate != domainFile.LastModifiedDate)
                {
                    OverwriteFile(domainFile, correspondingSubdomainFile);
                }
            }

            //Reverse checking if a file has been deleted
            foreach (CurrentFile subdomainFile in subdomainList)
            {
                CurrentFile? correspondingDomainFile = domainList
                    .FirstOrDefault(dFile => dFile.FileName == subdomainFile.FileName && dFile.RelativePath == subdomainFile.RelativePath);

                if (correspondingDomainFile == null)
                {
                    DeletedFile(subdomainFile);
                }
            }
        }
        //Method making copy new file 
        //TODO: finish catalogupdate!!
        private void CreateFile(CurrentFile domainFile)
        {
            string targetPath = Path.Combine(_subdomainCatalogManager.BasePath, domainFile.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            using (FileStream sourceStream = new FileStream(domainFile.FullPath, FileMode.Open))
            using (FileStream destinationStream = new FileStream(targetPath, FileMode.Create))
            {
                sourceStream.CopyTo(destinationStream);
            }

            domainFile.Status = FileStatus.Copied;
            _subdomainCatalogManager.CurrentFilesList.Add(domainFile);
        }

        //Method makes overwrite if a file modified
        //TODO: add log line and update object entity to finalize serialization
        private void OverwriteFile(CurrentFile srcFile, CurrentFile destFile)
        {
            using (FileStream sourceStream = new FileStream(srcFile.FullPath, FileMode.Open))
            using (FileStream destinationStream = new FileStream(destFile.FullPath, FileMode.Create))
            {
                sourceStream.CopyTo(destinationStream);
            }

            destFile.Status = FileStatus.Modified;
            destFile.LastModifiedDate = srcFile.LastModifiedDate;
        }

        //Probably ready method for checking if a file has been removed
        private void DeletedFile(CurrentFile fileToDelete)
        {
            fileToDelete.Status = FileStatus.Deleted;
            LogEvent(fileToDelete);
        }

        //____Need to check if this method neccessery or not!! Or if it's in the right class?!
        private void UpdateCatalog(CurrentFile file)
        {

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
    }
    #endregion

    //____Interface for looser dependency (is neccessery?)
    public interface ICatalogManager
    {
        List<CurrentFile> CurrentFilesList { get; }
        string BasePath { get; }
    }

    #region CatalogManagerClass reading and creating catalog

    //Class for read catalog or create catalog depends on the catalog existence
    public class CatalogManager : ICatalogManager
    {
        private List<CurrentFile> _currentFiles;
        private string _basePath;

        public List<CurrentFile> CurrentFilesList { get { return _currentFiles; } }
        public string BasePath { get; set; }

        public CatalogManager(string basePath)
        {
            _basePath = basePath;
            string catalogFilePath = Path.Combine(_basePath, "catalog.json");

            if (File.Exists(catalogFilePath))
            {
                string jsonString = File.ReadAllText(catalogFilePath);
                //Console.WriteLine("JSON to deserialize: " + jsonString);
                _currentFiles = JsonConvert.DeserializeObject<List<CurrentFile>>(jsonString);
                foreach (CurrentFile file in _currentFiles)
                {
                    file.FullPath = Path.Combine(_basePath, file.RelativePath);
                }

                //if (_currentFiles == null || !_currentFiles.Any())
                //{
                //    Console.WriteLine("Deserialization failed or returned empty list.");
                //}
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
        public string FullPath { get { return _fullPath; } set { _fullPath = value; } }
        public DateTime LastModifiedDate { get { return _lastModifiedDate; } set { _lastModifiedDate = value; } }
        [JsonIgnore]
        public FileStatus Status { get; set; }

        public CurrentFile()
        {
        }

        //____Probably this construct doesn't neccessery!! Check it at the end!!
        public CurrentFile(DateTime creationDate, string fullPath, string basePath, string fileName, long fileSize, DateTime lastModified, FileStatus status)
        {
            FileSize = fileSize;
            CreationDate = creationDate;
            RelativePath = GetRelativePath(fullPath, basePath);
            FileName = fileName;
            LastModifiedDate = lastModified;
            Status = status;
        }

        private string GetRelativePath(string fullPath, string basePath)
        {
            if (basePath.EndsWith(@"\"))
            {
               basePath = basePath.Remove(basePath.LastIndexOf(@"\"));
            }
            return fullPath.Substring(basePath.Length);
        }

        ////Probably not neccessery
        //public string GetCreationDateInString()
        //{
        //    return this.CreationDate.ToString("yyyy.MM.dd");
        //}

        //public string GetLastModifiedDateInString()
        //{
        //    return this.LastModifiedDate.ToString("yyyy.MM.dd");
        //}
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