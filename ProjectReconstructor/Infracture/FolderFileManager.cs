using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using log4net;
using Microsoft.Build.Evaluation;
using ProjectReconstructor.Extensions;

namespace ProjectReconstructor.Infracture
{
    public class GenerateProjectFileOptions
    {
        public GenerateProjectFileOptions(string projectName, ICollection<ProjectItem> projectItems, string targetDir, string Prefix, XElement guids)
        {
            ProjectName = projectName;
            ProjectItems = projectItems;
            TargetDir = targetDir;
            this.Prefix = Prefix;
            Guids = guids;
        }


        public string ProjectName { get; private set; }
        public ICollection<ProjectItem> ProjectItems { get; private set; }
        public string TargetDir { get; private set; }
        public string Prefix { get; private set; }
        public XElement Guids { get; private set; }
    }

    public class FolderFileManager
    {
        private readonly string _sourceDir;
        private readonly string _targetDir;
        private readonly string _nameSpacePrefix;
        private readonly ProjectItem[] _allFiles;
        private Dictionary<string, ICollection<ProjectItem>> projectGroupings;

        private ILog logger = LogManager.GetLogger(typeof(FolderFileManager));
        private IEnumerable<string> sourceFiles;
        private IEnumerable<DirectoryInfo> majorDirs;
        private DirectoryInfo root;
        
        static string _pattern = @".*Source\\" + @"(.*)";
        Regex regex = new Regex(_pattern);
        private XDocument _template;
        private XElement _guidMap;
        private XNamespace _templateNameSpace;
        private XName ProjectGuidAttributeName;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="targetDir"></param>
        /// <param name="compileFileItems">A list of project items. All of them are assumed to be of ItemType Compile</param>
        /// <param name="nameSpacePrefix"> We will prepend all generated namespaces with this. </param>
        public FolderFileManager(string sourceDir, string targetDir, IEnumerable<ProjectItem> compileFileItems, string nameSpacePrefix)
        {
            _sourceDir = sourceDir;
            _targetDir = targetDir;
            _nameSpacePrefix = nameSpacePrefix;
            _template = XDocument.Parse(File.ReadAllText(@".\template.csproj"));
            _templateNameSpace = _template.Root.GetDefaultNamespace();
            ProjectGuidAttributeName = XName.Get("Name", _nameSpacePrefix);
            _guidMap = XDocument.Load(@".\ProjectGuids.xml").Root;
            _nameSpacePrefix = ConfigurationManager.AppSettings["nameSpacePrefix"];

            _allFiles = compileFileItems.Where(c => c.EvaluatedInclude.StartsWith("Source")).ToArray();

            //Check if target Dir Exits
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            var foo = new Dictionary<string, IEnumerable<ProjectItem>>();

            //sort the source files into groups
            SortProjectItems(projectGroupings);

            //foreach "project" generate the file in the targetDir.
            foreach (var item in projectGroupings)
            {
                GenerateProjectFile(new GenerateProjectFileOptions(item.Key, item.Value, targetDir, _nameSpacePrefix, _guidMap));
            }


            root = new DirectoryInfo(sourceDir);
            majorDirs = root.GetDirectories();
        }

        /// <summary>
        /// This will seed the project item with the base template and compile files from source
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="projectItems"></param>
        /// <param name="targetDir"></param>
        private MksProjectFile GenerateProjectFile(GenerateProjectFileOptions generateProjectFileOptions)
        {
            var projectSkeleton = generateProjectFileOptions.ProjectName.Split(new string[] {"_"}, StringSplitOptions.RemoveEmptyEntries);
            var subDir = projectSkeleton.ConcatToString("\\");
            var fileName = generateProjectFileOptions.ProjectName.ConcatToString("") + ".csproj";
            var fullNameSpace = generateProjectFileOptions.Prefix + (generateProjectFileOptions.Prefix.EndsWith(".") ? "" : ".") + projectSkeleton.ConcatToString(".");

            var newPath = Path.Combine(generateProjectFileOptions.TargetDir, subDir,fileName);

            GenerateProjectXML(generateProjectFileOptions.ProjectName, _guidMap, _template, newPath, fileName, fullNameSpace);

            return newPath;

        }

        /// <summary>
        /// This is called by GenerateProjectFile. It will use the path, xml and options passed int to create the phyisical file.
        /// </summary>
        /// <param name="guidMap"></param>
        /// <param name="template"></param>
        /// <param name="newPath"></param>
        /// <param name="fileName"></param>
        /// <param name="fullNameSpace"></param>
        private MksProjectFile GenerateProjectXML(string projectName, XElement guidMap, XDocument template, string newPath, string fileName, string fullNameSpace)
        {
            if (File.Exists(newPath))
            {
                File.Delete(newPath);
            }

            var projFile = template.CopyDoc();
            var guidElement = projFile.Root.FirstMandatoryElement("ProjectGuid");

            if(guidMap.fi)
            guidElement.SetValue();
        }

        private void SortProjectItems(Dictionary<string, ICollection<ProjectItem>> _projectGroupings)
        {
            _projectGroupings = new Dictionary<string, ICollection<ProjectItem>>();
            foreach (var projectItem in _allFiles)
            {
                var key = ProjectName(projectItem.EvaluatedInclude);

                if (_projectGroupings.ContainsKey(key) == false)
                    _projectGroupings.Add(key, new List<ProjectItem>());

                _projectGroupings[key].Add(projectItem);
            }
        }

        public void CreateDirectoryStructure()
        {
            foreach (var dir in majorDirs)
            {
                
                //Does the dir have any files in it
                var majorDirFile = dir.GetFiles();

                if (majorDirFile != null || majorDirFile.Length > 0)
                {
                    GenerateProjectTemplate(dir, majorDirFile);
                }
            }
        }

        private void GenerateProjectTemplate(DirectoryInfo dir, FileInfo[] majorDirFile)
        {
            logger.Info($"ProjectName: {dir.Name}");
            var projectName = ProjectName(dir.FullName);
        }

        private string ProjectName(string relativePath)
        {
            var match =  regex.Match(relativePath);
            var directoryStructure = match.Groups[1].Value.Split(new string[] {"\\"}, StringSplitOptions.RemoveEmptyEntries);
            //var indexIfCSextension = fileName.LastIndexOf(@"\.cs", StringComparison.Ordinal);

            var projectName = "";
            projectName = projectName  + directoryStructure[0];

            if (directoryStructure.Length == 2)
                ;
            else
                projectName = projectName + "_" + directoryStructure[1];


            var mksProjectFile = new MksProjectFile();
            mksProjectFile.Name = projectName;


            return projectName;
        }
    }
}
