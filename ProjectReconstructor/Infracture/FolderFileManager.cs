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
using ProjectReconstructor.DependencyWalker;
using ProjectReconstructor.Domain;
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
        List<MksProjectFile> mksProjectFiles;


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
            var compileSourceDir = ConfigurationManager.AppSettings["compileFilesRoot"];

            _allFiles = compileFileItems.Where(c => c.EvaluatedInclude.StartsWith("Source")).ToArray();

            //Check if target Dir Exits
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            var foo = new Dictionary<string, IEnumerable<ProjectItem>>();

            
            //sort the source files into groups
            SortProjectItems(ref projectGroupings);

            mksProjectFiles = new List<MksProjectFile>();

            //foreach "project" generate the file in the targetDir.
            foreach (var item in projectGroupings)
            {

                mksProjectFiles.Add(GenerateProjectFile(new GenerateProjectFileOptions(item.Key, item.Value, targetDir, _nameSpacePrefix, _guidMap)));
            }

            //now that we have the files grouped, let's walk through each item and collect the references and namespaces
            UpdateReferecesAndNameSpaces(mksProjectFiles);
            root = new DirectoryInfo(sourceDir);
            majorDirs = root.GetDirectories();
        }

        public List<MksProjectFile> MksProjectFiles
        {
            get { return mksProjectFiles; }
        }

        private void UpdateReferecesAndNameSpaces(List<MksProjectFile> mksProjectFiles)
        {
            var walker = new CsharpFileWalker();
            foreach (var mksProjectFile in mksProjectFiles)
            {
                var items = mksProjectFile.ProjectItems;
                foreach (var item in items)
                {
                    walker.ProcessFile(item.AbsoluteSourcePath);
                    if (walker.UsingsDictionary.ContainsKey(item.AbsoluteSourcePath)) 
                    {
                        item.References =  walker.UsingsDictionary[item.AbsoluteSourcePath]?
                            .Select(c => c.ToString().Replace("using ", "").Replace(";", "")).ToArray();
                    }
                }

                mksProjectFile.References = items.SelectMany(d => d.References ?? new []{string.Empty}).Distinct().ToArray();


            }
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
            var projectName = projectSkeleton.ConcatToString("");
            var subDir = projectSkeleton.ConcatToString("\\");
            var fileName = generateProjectFileOptions.ProjectName.ConcatToString("") + ".csproj";
            var fullNameSpace = generateProjectFileOptions.Prefix + (generateProjectFileOptions.Prefix.EndsWith(".") ? "" : ".") + projectSkeleton.ConcatToString(".");

            var newPath = Path.Combine(generateProjectFileOptions.TargetDir, subDir,fileName);

            var mksProjectFile = new MksProjectFile();
            mksProjectFile.AbsoluteTargetPath = newPath;

            mksProjectFile.FileName = fileName;
            var guidElement = _guidMap.FindMandatoryElementWithAttributeName("Project", "Name", projectName);
            var guid = guidElement.Attribute("Guid").Value;
            mksProjectFile.Guid = guid;
            mksProjectFile.Name = projectName;
            mksProjectFile.NameSpace = fullNameSpace;
            mksProjectFile.ProjectItems = generateProjectFileOptions.ProjectItems.Select(c => new MksProjectItem(c, Path.Combine(_sourceDir, c.EvaluatedInclude), c.EvaluatedInclude, _targetDir)).ToArray();
            var mksProjectFileXML = GenerateProjectXML(projectName, _guidMap, _template, fullNameSpace, mksProjectFile);

            mksProjectFile.XML = mksProjectFileXML;


            return mksProjectFile;
        }

        /// <summary>
        /// This is called by GenerateProjectFile. It will use the path, xml and options passed int to create the phyisical file.
        /// </summary>
        /// <param name="guidMap"></param>
        /// <param name="template"></param>
        /// <param name="newPath"></param>
        /// <param name="fileName"></param>
        /// <param name="fullNameSpace"></param>
        private string GenerateProjectXML(string projectName, XElement guidMap, XDocument template,string nameSpace , MksProjectFile mksProjectFile)
        {
            var projFile = template.CopyDoc();
            var guidElement = guidMap.FindMandatoryElementWithAttributeName("Project", "Name", projectName);
            var guid = guidElement.Attribute("Guid").Value;
            projFile.Root.Descendants(XName.Get("ProjectGuid", projFile.Root.GetDefaultNamespace().NamespaceName)).First().SetValue(guid);
            projFile.Root.Descendants(XName.Get("RootNamespace", projFile.Root.GetDefaultNamespace().NamespaceName)).First().SetValue(nameSpace);
            projFile.Root.Descendants(XName.Get("AssemblyName", projFile.Root.GetDefaultNamespace().NamespaceName)).First().SetValue(projectName);

            return projFile.ToString();
        }

        private void SortProjectItems(ref Dictionary<string, ICollection<ProjectItem>> _projectGroupings)
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
            var projectName = ProjectName(dir.Name);
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
