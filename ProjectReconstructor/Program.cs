using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Microsoft.Build.Evaluation;
using ProjectReconstructor.Infracture;

namespace ProjectReconstructor
{
    public class Reconstructor
    {
        private ILog _logger = log4net.LogManager.GetLogger(typeof(Reconstructor));

        private string sourceProjectPath;
        private string nameSpacePrefix;
        private string sourceSolutionPath;
        private string targetDir;
        private Project sourceProject;
        private IEnumerable<ProjectItem> compileFiles;
        private IDictionary<string, ICollection<Project>>  directReferences;
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
            nameSpacePrefix = ConfigurationManager.AppSettings["nameSpacePrefix"];
            targetDir = ConfigurationManager.AppSettings["targetDir"];

            if (string.IsNullOrEmpty(sourceProjectPath))
                throw new ArgumentException("We need to have a path to a project file.");

            sourceProject = new Project(sourceProjectPath);

            compileFiles = sourceProject.Items.Where(c => c.ItemType == "Compile");

            var systemReferences =  sourceProject.Items.Where(c =>
                        c.DirectMetadataCount == 0 || !c.DirectMetadata.Select(d => d.Name).Contains("HintPath"));


            //1 Create the directory structure;
            CreateDirectoryStructure(sourceProjectPath, targetDir);


        }

        private void CreateDirectoryStructure(string sourceDirPath, string targetDirPath)
        {
            var fileDir = new FolderFileManager(Path.GetDirectoryName(sourceDirPath), targetDirPath, compileFiles, "MKS.Legacy.");
            fileDir.CreateDirectoryStructure();
        }
    }
}
