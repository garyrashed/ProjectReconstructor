using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel.PeerResolvers;
using System.Text;
using System.Threading.Tasks;

namespace ProjectReconstructor.DependencyWalker
{
    public class FileWalker
    {
        public static string CoreOrDupe(string sourcePath, string rootofSource, string targetDir)
        {
            var uriSourceRoot = new Uri(rootofSource);
            var uriSourcePath = new Uri(sourcePath);

            var relativePath = uriSourceRoot.MakeRelativeUri(uriSourcePath).ToString().Replace('/', '\\');
            

            var file = new FileInfo(sourcePath);
            var subDir = new DirectoryInfo(targetDir);
            var dirName = file.Directory.Name;
            var walker = new CsharpFileWalker();
            walker.ProcessFile(file.FullName);
            var refs = walker.UsingsDictionary.SelectMany(c => c.Value)
                .Select(d => d.ToString().Replace("using ", "").Replace(";", "")).ToArray();

            if (refs.Any(c => c.StartsWith("MKS")) == false)
            {
                var coreFileDir = subDir.CreateSubdirectory(".\\Core\\");
                var filePath = $"{file.DirectoryName}\\core\\{file.Name}";
                return "Core";
            }
            else
            {
                var helperFileDir = subDir.CreateSubdirectory($".\\{subDir.Name}\\");
                var filePath = $"{file.DirectoryName}\\{subDir.Name}\\{file.Name}";
                return dirName;
            }
        }

        public class FileNameInfo
        {
            public string SourceFilePath { get; }
            public string TargetFilePath { get; }
            public string Target { get; }
            public string ProjectName { get; }

            public FileNameInfo(string sourceFilePath, string targetFilePath, string projectName)
            {
                SourceFilePath = sourceFilePath;
                TargetFilePath = targetFilePath;
                ProjectName = projectName;
            }
        }

    }
}
