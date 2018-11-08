using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using log4net;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using ProjectReconstructor.DependencyWalker;
using ProjectReconstructor.Extensions;
using ProjectReconstructor.Infracture;

namespace ProjectReconstructor
{
    public class Reconstructor
    {
        private ILog _logger = log4net.LogManager.GetLogger(typeof(Reconstructor));

        private string sourceProjectPath;
        private string sourceProjectDir;
        private string rootofSource;
        private string rootSourceDir;
        private string rootTargetDir;
        private string nameSpacePrefix;
        private string originalNameSpacePrefix;
        private string sourceSolutionPath;
        private string targetDir;
        private Project sourceProject;
        private IEnumerable<ProjectItem> compileFiles;
        private IDictionary<string, ICollection<Project>>  directReferences;
        private MksProjectFileCreator _mksProjectCreator;
        private List<MksProjectFile> _mksProjectFiles;

        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            log4net.GlobalContext.Properties["LogName"] = "ProjectReconstructor";
            var p = new Reconstructor();
            p.Run();
        }

        public void Run()
        {
            _logger.Info("Starting run.");
            sourceProjectPath = ConfigurationManager.AppSettings["project"];
            sourceSolutionPath = ConfigurationManager.AppSettings["solution"];
            sourceProjectDir = Path.GetDirectoryName(sourceProjectPath);
            targetDir = ConfigurationManager.AppSettings["targetDir"];
            rootofSource = ConfigurationManager.AppSettings["rootOfSource"] ?? "";
            rootSourceDir = Path.Combine(sourceProjectDir, rootofSource);
            rootTargetDir = Path.Combine(targetDir, rootofSource);
            nameSpacePrefix = ConfigurationManager.AppSettings["nameSpacePrefix"];
            originalNameSpacePrefix = ConfigurationManager.AppSettings["originalNameSpacePrefix"];
            targetDir = ConfigurationManager.AppSettings["targetDir"];
            

            if (string.IsNullOrEmpty(sourceProjectPath))
                throw new ArgumentException("We need to have a path to a project file.");

            sourceProject = new Project(sourceProjectPath);

            var itemTypes = sourceProject.Items.Select(d => d.ItemType).Distinct().ToArray();
            compileFiles = sourceProject.Items.Where(c => c.ItemType == "Compile");
            var allReferences = sourceProject.Items.Where(c => c.ItemType.Contains("Reference")).ToArray();
            var systemReferences =  allReferences.Where(c =>
                        c.DirectMetadataCount == 0 || !c.DirectMetadata.Select(d => d.Name).Contains("HintPath")).ToArray();



            //1 Create the directory structure;
            DirectoryManager.Copy(rootSourceDir,  rootTargetDir, _logger);

            //2 go Through the rootSourceDir and create the projectFiles on the target Dir
            //don't worry  about addiing the references, that comes later
            _mksProjectCreator = new MksProjectFileCreator(sourceProject, sourceProjectPath, rootSourceDir, rootTargetDir, rootofSource, nameSpacePrefix);

            _mksProjectFiles = _mksProjectCreator.MksProjectFiles;

            //3 add  the system references
            UpdateSystemReferences(sourceProject, _mksProjectFiles, allReferences);

            //4 now setup the project references
            UpdateProjectReferences(sourceProject, _mksProjectFiles, allReferences);

            UpdateServiceReference(sourceProject, _mksProjectFiles, allReferences)
            //5 for now 

            

        }

        private void UpdateServiceReference(Project project, List<MksProjectFile> mksProjectFiles, ProjectItem[] allReferences)
        {
            var emailService = "MKS.Vendor.Microsoft.Exchange.ExchangeWebServiceReference";
            

        }

        private void UpdateSystemReferences(Project project, List<MksProjectFile> mksProjectFiles, ProjectItem[] allReferences)
        {
            var projectGuidMap = new GuidMap();
            
           
            var projectRefsWithoutHint =  project.Items.Where(c =>
                c.ItemType == "Reference" &&
                c.DirectMetadata.Any(d => d.Name == "HintPath") == false).ToArray();

            foreach (var mksProject in mksProjectFiles)
            {
                var itemGroup = new XElement("ItemGroup");
                foreach(var sysRef in projectRefsWithoutHint)
                {
                    itemGroup.Add(new XElement("Reference"
                    , new  XAttribute("Include", sysRef.EvaluatedInclude)
                        
                    ));
                }

                mksProject.XML = mksProject.XML.Replace(@"  <BeginInsertion />",
                    @"  <BeginInsertion />" + "\r\n" + itemGroup.ToString());
            }
        }

        private void UpdateProjectReferences(Project project, IList<MksProjectFile> mksProjectFiles, ProjectItem[] sysRefs)
        {
            var projectGuidMap = new GuidMap();
            foreach (var mksProjectFile in mksProjectFiles)
            {
                var originalNameSpace = mksProjectFile.NameSpace.Replace(nameSpacePrefix, originalNameSpacePrefix);
                var projectRefs = mksProjectFile.ProjectItems
                    .SelectMany(c => c.References)
                    .Where(d => d.StartsWith("MKS") && d.StartsWith(originalNameSpace) == false)
                    .Distinct();
                
                var projectNames = projectRefs 
                    
                    .Select(c => c.Remove(0, 4).Split('.'))
                    .Select(c => c.Take(2).ConcatToString(""));

                var ItemGroup = new XElement("ItemGroup");
                foreach (var projectRef in projectNames)
                {
                    var listOfProjects = new List<XElement>();
                    if(projectGuidMap.ProjectGuidMap.ContainsKey(projectRef))
                    {
                        var guid = projectGuidMap.ProjectGuidMap[projectRef];
                        var foo = mksProjectFiles.FirstOrDefault(c => c.AssemblyName == projectRef);
                        var IncludePath = mksProjectFile.AbsoluteTargetUri.MakeRelativeUri(foo.AbsoluteTargetUri).ToString().Replace('/', '\\');
                        var projectElement = new XElement(
                            new XElement("ProjectReference"
                                , new XAttribute("Include", IncludePath)
                                , new XElement("Project", guid)
                                , new XElement("Name", projectRef)
                            ));
                        listOfProjects.Add(projectElement);
                    }

                    if (listOfProjects.Count > 0)
                    {
                        foreach (var element in listOfProjects)
                        {
                            ItemGroup.Add(element);
                        }
                    }
                }

                mksProjectFile.XML =  mksProjectFile.XML.Replace(@"  <BeginInsertion />",
                    @"  <BeginInsertion />" + "\r\n" + ItemGroup.ToString());

            }
        }

        private void CreateDirectoryStructure(string sourceDirPath, string targetDirPath, string root)
        {
            var fileDir = new FolderFileManager(Path.GetDirectoryName(sourceDirPath), root,  targetDirPath, compileFiles, "MKS.Legacy.");
            fileDir.CreateDirectoryStructure();
        }
    }
}
