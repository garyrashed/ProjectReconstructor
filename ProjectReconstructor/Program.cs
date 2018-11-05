using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using ProjectReconstructor.DependencyWalker;
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


        }

        private void CreateDirectoryStructure(string sourceDirPath, string targetDirPath, string root)
        {
            var fileDir = new FolderFileManager(Path.GetDirectoryName(sourceDirPath), root,  targetDirPath, compileFiles, "MKS.Legacy.");
            fileDir.CreateDirectoryStructure();
        }
    }
}
