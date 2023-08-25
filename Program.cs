internal class Program
{
    private static void Main(string[] args)
    {
        string domainBasePath = @"C:\Users\skafula\Desktop\Domain\DomainPath\Test";
        string subdomainBasePath = @"C:\Users\skafula\Desktop\Subdomain\SubPath\Test";
    }

    //Class to create file object with neccessery informations.
    public class FileInformation
    {
        private long _fileSize;
        private string _fileName;
        private string _relativePath;
        private DateTime _creationDate;
        private DateTime _lastModifiedDate;
        
        public DateTime CreationDate { get { return _creationDate; } }
        public string RelativePath { get { return _relativePath; } }
        public string FileName { get { return _fileName; } }
        public long FileSize { get { return _fileSize; } }
        public DateTime LastModifiedDate { get { return _lastModifiedDate; } }
        public FileStatus Status { get; set; }

        public FileInformation(DateTime creationDate, string fullPath, string basePath, string fileName, long fileSize, DateTime lastModified, FileStatus status)
        {
            _fileSize = fileSize;
            _creationDate = creationDate;
            _relativePath = GetRelativePath(fullPath, basePath);
            _fileName = fileName;
            _lastModifiedDate = lastModified;
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

    public enum FileStatus
    {
        Created,
        Deleted,
        Modified
    }
}