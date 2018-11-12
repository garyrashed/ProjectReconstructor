using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;
using log4net;
using ProjectReconstructor.DependencyWalker;
using ProjectReconstructor.Domain;
using ProjectReconstructor.Extensions;

namespace ProjectReconstructor.Infracture
{
    public class MksProjectFileCreator
    {
        private readonly Project _projectFile;
        private readonly string _sourceProjectPath;
        private readonly string _rootOfSourceDir;
        private readonly string _rootOfTargetDir;
        private readonly string _rootOfSource;
        private readonly string _nameSpacePrefix;
        private ILog logger = log4net.LogManager.GetLogger(typeof(MksProjectFileCreator));
        private IEnumerable<ProjectItem> _compileFiles;
        private ProjectItem[] _allReferences;
        private ProjectItem[] _systemReferences;
                
        static string _pattern = @".*Source\\" + @"(.*)";
        private static string _nameSpacePattern = @"namespace (.*)";
        Regex regex = new Regex(_pattern);

        private List<MksProjectFile> _mksProjectFiles;
        private XElement _guidMap;

        public MksProjectFileCreator(Project projectFile, string sourceProjectPath, string rootOfSourceDir, string rootOfTargetDir, string rootOfSource, string nameSpacePrefix)
        {
            _projectFile = projectFile;
            _sourceProjectPath = sourceProjectPath;
            _rootOfSourceDir = rootOfSourceDir;
            _rootOfTargetDir = rootOfTargetDir;
            _rootOfSource = rootOfSource;
            _nameSpacePrefix = nameSpacePrefix;
            _compileFiles = _projectFile.Items.Where(c => c.ItemType == "Compile" && c.EvaluatedInclude.StartsWith(rootOfSource));
            _allReferences = _projectFile.Items.Where(c => c.ItemType.Contains("Reference")).ToArray();
            _systemReferences =  _allReferences.Where(c =>
                c.DirectMetadataCount == 0 || !c.DirectMetadata.Select(d => d.Name).Contains("HintPath")).ToArray();

            _mksProjectFiles = new List<MksProjectFile>();

            var template = XDocument.Load(@".\template.csproj");
            var templateNameSpace = template.Root.GetDefaultNamespace().NamespaceName;

            SortProjectItems();

            SetGuidsTemplateAndNameSpace();

            SetProjectItemsRefsAndNamespaces();

            SetTheSystemRefs();

           // SplitMksCommonIntoPieces();
        }

        //we need to split Common into 2 pieces
        //the first truly has no dependencies to outher projects
        //the second may.
        private void SplitMksCommonIntoPieces()
        {
            var common = _mksProjectFiles.FirstOrDefault(c => c.Name == "Common");

            var mksCommonBase = new MksProjectFile();
            foreach (var item in common.ProjectItems)
            {
                if (item.References.Any(c => c.StartsWith("MKS")) == false)
                {
                    mksCommonBase.ProjectItems.Add(item);
                    

                }
            }

            foreach (var item in mksCommonBase.ProjectItems)
            {
                common.ProjectItems.Remove(item);
            }

        }

        private void SetProjectItemsRefsAndNamespaces()
        {
            var namespaceRegex = new Regex(_nameSpacePattern);

            foreach (var mksProjectFile in MksProjectFiles)
            {
                var items = mksProjectFile.ProjectItems;
                foreach (var item in items)
                {
                    var walker = new CsharpFileWalker();
                    walker.ProcessFile(item.AbsoluteSourcePath);
                    if(walker.UsingsDictionary.ContainsKey(item.AbsoluteSourcePath))
                    {
                        item.References = walker.UsingsDictionary[item.AbsoluteSourcePath]
                            .Select(c => c.ToString().Replace("using ", "").Replace(";", "")).ToArray();
                    }
                    else
                    {
                        item.References = new string[0];
                    }
                    
                    if(walker.NameSpacesDictionary.ContainsKey(item.AbsoluteSourcePath))
                    {
                        item.NameSpaces = walker.NameSpacesDictionary[item.AbsoluteSourcePath]
                            .Select(c => namespaceRegex.Match(c.ToString()).Groups[1].Value.Trim()).ToArray();
                        //.Select(c => c.ToString().Replace("namespace ", "").Replace(";", "")).ToArray();
                    }
                    else
                    {
                        item.NameSpaces = new string[0];
                    }
                }
            }
        }

        private void SetTheSystemRefs()
        {
            foreach (var mksProj in _mksProjectFiles)
            {
                var items = mksProj.ProjectItems;
                var references = items.SelectMany(c =>c == null ? new string[] {} : c.References).Where(d => d !=  null).Distinct().ToArray();
                var projectXml = XDocument.Parse(mksProj.XML);
                var nameSpace = projectXml.Root.GetDefaultNamespace();
                var baseRoot = projectXml.Root.Descendants().Single(c => c.Name.LocalName == "EndInsertion");

                var itemGroup = new XElement("{" + nameSpace.NamespaceName +  "}ItemGroup");
                
                baseRoot.AddBeforeSelf(itemGroup);

                foreach(var refer in references)
                {
                    var matchinReferenceFromProj = _systemReferences.FirstOrDefault(c => c.EvaluatedInclude == refer);

                }
                
            }
        }

        private void SetGuidsTemplateAndNameSpace()
        {
            _guidMap = XDocument.Load(@".\ProjectGuids.xml").Root;
            var guidMap =  _guidMap.Descendants("Project").Select(c => new
                {Name = c.Attribute("Name").Value, ProjectGuid = c.Attribute("Guid").Value}).ToArray();
            var template = XDocument.Load(@".\template.csproj");
            var templateNameSpace = template.Root.GetDefaultNamespace().NamespaceName;
            foreach (var mksProj in _mksProjectFiles)
            {
                var guidName = mksProj.Name.Replace("_", "");
                var defaultNameSpace = _nameSpacePrefix + "." + mksProj.Name.Replace("_", ".");
                mksProj.NameSpace = defaultNameSpace;
                mksProj.AssemblyName = guidName;
                var guid = guidMap.FirstOrDefault(c => c.Name == guidName);
                if(guid == null)
                    throw new InstanceNotFoundException($"Could not find the guid in the file. AppName {guidName}");
                mksProj.Guid = guid.ProjectGuid;

                var projectXML = template.CopyDoc();
                projectXML.Root.Descendants().Single(c => c.Name.LocalName == "ProjectGuid").Value = mksProj.Guid;
                projectXML.Root.Descendants().Single(c => c.Name.LocalName == "RootNamespace").Value = mksProj.NameSpace;
                projectXML.Root.Descendants().Single(c => c.Name.LocalName == "AssemblyName").Value = mksProj.AssemblyName;
                mksProj.XML = projectXML.ToString();
            }
            
        }

        public List<MksProjectFile> MksProjectFiles
        {
            get { return _mksProjectFiles; }
           
        }

        public IEnumerable<MksProjectFile> CreateFiles(string sourceDirectory, string targetDirectory)
        {
            foreach (var project in _mksProjectFiles)
            {
                //set the absoluteTarget
              
            }

            return _mksProjectFiles;
        }

        private void SortProjectItems()
        {
            
            foreach (var projectItem in _compileFiles)
            {
                var key = ProjectNameFromPath(projectItem.EvaluatedInclude);
                if(_mksProjectFiles.Contains(key) == false)
                    _mksProjectFiles.Add(key);

                var mksProj =  _mksProjectFiles.Find(c => c.Equals(key));

                    mksProj.ProjectItems.Add(new MksProjectItem(mksProj, projectItem
                    , Path.Combine(_rootOfSourceDir, projectItem.EvaluatedInclude.Substring(_rootOfSource.Length))
                    , projectItem.EvaluatedInclude
                    , _rootOfTargetDir.Substring(0, _rootOfTargetDir.LastIndexOf(_rootOfSource))
                    )
                    );
            }
        }


        private MksProjectFile ProjectNameFromPath(string relativePathToSourceProj)
        {
            var match =  regex.Match(relativePathToSourceProj);
            var directoryStructure = match.Groups[1].Value.Split(new string[] {"\\"}, StringSplitOptions.RemoveEmptyEntries);
            //var indexIfCSextension = fileName.LastIndexOf(@"\.cs", StringComparison.Ordinal);



            var projectName = "";
            projectName = projectName  + directoryStructure[0];

            if (directoryStructure.Length == 1 ||
                (directoryStructure.Length == 2 && directoryStructure[1].Contains(".cs")))
                projectName = projectName + "_" +
                              FileWalker.CoreOrDupe(Path.Combine(_rootOfSourceDir, directoryStructure.ConcatToString("\\")),
                                  _rootOfSourceDir, _rootOfTargetDir);
            else
                projectName = projectName + "_" + directoryStructure[1];

            if (projectName.Contains('_') == false)
            {

            }

            var mksProjectFile = new MksProjectFile();
            mksProjectFile.Name = projectName;
            mksProjectFile.RelativePath = _rootOfSource + projectName.Replace("_", "\\").Trim();
            mksProjectFile.FileName = projectName.Replace("_", "").Trim() + ".csproj";
            mksProjectFile.AbsoluteTargetPath = _rootOfTargetDir + projectName.Replace("_", "\\").Trim() + "\\" + mksProjectFile.FileName;

            return mksProjectFile;
        }
    }
}