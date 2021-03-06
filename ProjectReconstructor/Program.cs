﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using log4net;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using ProjectReconstructor.DependencyWalker;
using ProjectReconstructor.Domain;
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
        private string packagesPath;
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
            packagesPath = ConfigurationManager.AppSettings["packages"];

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

            //2 Move the things that are in the roots e.g. App\foo.cs to core and  
            MoveFilesToCoreAndUtil(rootTargetDir);

            //2 go Through the rootSourceDir and create the projectFiles on the target Dir
            //don't worry  about addiing the references, that comes later
            _mksProjectCreator = new MksProjectFileCreator(sourceProject, sourceProjectPath, rootSourceDir, rootTargetDir, rootofSource, nameSpacePrefix);

            _mksProjectFiles = _mksProjectCreator.MksProjectFiles;

            //3 add  the system references
            UpdateSystemReferences(sourceProject, _mksProjectFiles, allReferences);

            //4 now setup the project references
            UpdateProjectReferences(sourceProject, _mksProjectFiles, allReferences);

            UpdateServiceReference(sourceProject, _mksProjectFiles, allReferences,
                ConfigurationManager.AppSettings["serviceReferences"].SplitWithStrings(";"));
            
            //5 for now 
            UpdateWebReferences(sourceProject, _mksProjectFiles, allReferences, ConfigurationManager.AppSettings["webServiceReferences"].SplitWithStrings(";"));

            //6
            WriteOutPackages(sourceProject, _mksProjectFiles, targetDir, packagesPath);

            WriteOutLibraryFiles(sourceProject, _mksProjectFiles, targetDir);
            //7
            UpdateCompileReferences(_mksProjectFiles);
            
            //8 Write out each project File
            WriteOutProjFiles(_mksProjectFiles);

            //Construct a solution file
            WriteOutSolutionFile(_mksProjectFiles, targetDir, rootofSource);
        }

        private void MoveFilesToCoreAndUtil(string rootTargetDirPath)
        {
            var rootUri = new Uri(rootTargetDirPath);
            DirectoryInfo root = new DirectoryInfo(rootTargetDirPath);
            var subDirs = root.GetDirectories();
            foreach (var subDir in subDirs)
            {
                var rootFiles = subDir.GetFiles();
 
                foreach (var file in rootFiles)
                {
                    var fileUri = new Uri(file.FullName);
                    var relativePath = rootUri.MakeRelativeUri(fileUri).ToString().Replace('/', '\\');
                    var depth = (relativePath.SplitWithStrings("\\").Length);

                    var walker = new CsharpFileWalker();
                    walker.ProcessFile(file.FullName);
                    var refs =  walker.UsingsDictionary.SelectMany(c => c.Value)
                        .Select(d => d.ToString().Replace("using ", "").Replace(";", "")).ToArray();

                    if (refs.Any(c => c.StartsWith("MKS")) == false && depth == 2)
                    {
                        var coreFileDir = subDir.CreateSubdirectory(".\\Core\\");
                        var filePath = $"{file.DirectoryName}\\core\\{file.Name}";
                        if (File.Exists(filePath))
                            File.Delete(filePath);

                        file.MoveTo(filePath);
                    }
                    else if(depth == 2)
                    {
                        
                        var helperFileDir = subDir.CreateSubdirectory($".\\{subDir.Name}\\");
                        var filePath = $"{file.DirectoryName}\\{subDir.Name}\\{file.Name}";
                        if (File.Exists(filePath))
                            File.Delete(filePath);

                        file.MoveTo(filePath);
                    }
                }
            }
        }

        private void WriteOutLibraryFiles(Project project, List<MksProjectFile> mksProjectFiles, string targetDir)
        {
            var libraryItems = project.Items
                .Where(c => c.DirectMetadata.Any(d => d.EvaluatedValue.StartsWith("Library\\"))).ToArray();

            DirectoryManager.Copy(project.DirectoryPath + "\\Library", targetDir + "Library\\");
            var targetLibraryUri = new Uri(targetDir + "Library\\");

            var nameSpaceToDll = new Dictionary<string, Tuple<string, ProjectItem>>();
            foreach (var lib in libraryItems)
            {
                var sourceFile = Path.Combine(targetDir, lib.DirectMetadata.FirstOrDefault(c => c.Name == "HintPath").EvaluatedValue);
                var assemblyName = AssemblyName.GetAssemblyName(sourceFile);
                try
                {
                    var nsx = Assembly.Load(assemblyName).GetTypes().Where(c => c.IsPublic && c.Assembly.FullName == assemblyName.FullName).ToArray();

                    var ns = nsx
                    
                        .Select(t => t.Namespace)
                        .Where(q => q != null)
                        .Distinct();

                    foreach (var n in ns)
                    {
                        if(nameSpaceToDll.ContainsKey(n) == false)
                            nameSpaceToDll.Add(n, new Tuple<string, ProjectItem>(sourceFile, lib));
                    }
                }
                catch (Exception e)
                {

                }
            }

            foreach (var proj in mksProjectFiles)
            {
                var doc = XDocument.Parse(proj.XML).Root;
                var itemGroupName = XName.Get("ItemGroup", doc.GetDefaultNamespace().NamespaceName);
                var compileName = XName.Get("Compile", doc.GetDefaultNamespace().NamespaceName);
                var beginInsertion =  doc.Descendants().FirstOrDefault(c => c.Name.LocalName == "BeginInsertion");

                if (proj.References.Any(c => nameSpaceToDll.ContainsKey(c)))
                {
                    var refsToAdd =  proj.References.Where(c => nameSpaceToDll.ContainsKey(c)).ToArray();
                    var librarylist = new List<XElement>();
                    foreach (var s in refsToAdd)
                    {
                        var somethingICantName = nameSpaceToDll[s];
                        var add =  Regex.Replace(somethingICantName.Item2.Xml.OuterElement, @"xmlns="".*""", string.Empty);
                        var hintPath = Regex.Match(add, @"<HintPath>(.*)</HintPath>").Groups[1];
                        var targetURI = new Uri(targetDir + hintPath);
                        var newHintPath =  proj.AbsoluteTargetUri.MakeRelativeUri(targetURI).ToString().Replace('/', '\\');
                        add = Regex.Replace(add, @"<HintPath>.*</HintPath>",
                            @"<HintPath>" + newHintPath + @"</HintPath>");
                        librarylist.Add(XElement.Parse(add));
                    }

                    var itemGroup = new XElement(itemGroupName);
                    itemGroup.Add(librarylist);
                    var foobar1 = Regex.Replace(itemGroup.ToString(), @"xmlns="".*""", string.Empty);
                    proj.XML = proj.XML.Replace(@"  <BeginInsertion />",
                        @"  <BeginInsertion />" + "\r\n" + foobar1);

                }
            }
        }

        private void WriteOutPackages(Project project, List<MksProjectFile> mksProjectFiles, string targetDir, string packagePath)
        {
            var packageItems = project.Items
                .Where(c => c.DirectMetadata.Any(d => d.EvaluatedValue.Contains("..\\packages\\"))).ToArray();

            var packageDictionary = XDocument.Load(packagePath).Root.Descendants()
                .ToDictionary(c => c.Attribute("id").Value + "." + c.Attribute("version").Value, d => d.ToString());

            var nameSpaceToDll = new Dictionary<string, Tuple<string, ProjectItem>>();
            foreach (var pack in packageItems)
            {
                var packPath = Path.Combine(project.DirectoryPath,
                    pack.DirectMetadata.FirstOrDefault(d => d.EvaluatedValue.Contains("..\\package")).EvaluatedValue);

                var assemblyName = AssemblyName.GetAssemblyName(packPath);

                try
                {
                    var nsx = Assembly.Load(assemblyName).GetTypes().Where(c => c.IsPublic).ToArray();

                        var ns = nsx
                    
                        .Select(t => t.Namespace)
                        .Where(q => q != null)
                        .Distinct();

                    foreach (var n in ns)
                    {
                        if(nameSpaceToDll.ContainsKey(n) == false)
                            nameSpaceToDll.Add(n, new Tuple<string, ProjectItem>(packPath, pack));
                    }
                }
                catch (Exception e)
                {

                }

            }

            foreach (var proj in mksProjectFiles)
            {
                var packageRefs = nameSpaceToDll.Keys.Intersect(proj.References);
                if(packageRefs.Any())
                {
                    var intersection = packageRefs.ToArray();
                    //create a packages file
                    var packageDocument = XDocument.Load(@".\template.config");
                    var rootPageDocument = packageDocument.Root;
                    var packageFilePath =  Path.Combine(proj.AbsoluteTargetDir, "packages.config");
                    var packagesToAdd = new List<string>();
                   
                    var doc = XDocument.Parse(proj.XML).Root;
                    var itemGroupName = XName.Get("ItemGroup", doc.GetDefaultNamespace().NamespaceName);
                    var compileName = XName.Get("Compile", doc.GetDefaultNamespace().NamespaceName);
                    var beginInsertion =  doc.Descendants().FirstOrDefault(c => c.Name.LocalName == "BeginInsertion");

                    var itemGroup = new XElement(itemGroupName);
                    foreach (var inter in intersection)
                    {
                        var info = nameSpaceToDll[inter];
                        var foob = info.Item2.Xml.OuterElement;
                        
                        var lengthOfPackage = "packages\\".Length;
                        var pathToPackage = info.Item1.SplitWithStrings("..\\packages\\");
                        
                        var packageName = pathToPackage[1].SplitWithStrings("\\")[0];

                        var depth = pathToPackage[0].SplitWithStrings("\\");
                        var packageRoot = Path.Combine(depth.Take(depth.Length - 1).ConcatToString("\\"), "packages\\");
                        

                        var targetPackageDir = Path.Combine(targetDir, $"packages\\{packageName}\\");
                        var uriPackage = new Uri(targetPackageDir);
                        var hintpath = proj.AbsoluteTargetUri.MakeRelativeUri(uriPackage).ToString().Replace('/', '\\');
                        hintpath = hintpath.Replace(packageName + "\\", pathToPackage[1]);
                        var packageToCopy = Path.Combine(depth.Take(depth.Length - 1).ConcatToString("\\"), "packages\\", packageName + "\\");
                        DirectoryManager.Copy(packageToCopy, targetPackageDir);
                        foob = Regex.Replace(foob, @"\<HintPath\>.*\<\/HintPath\>",
                            @"<HintPath>" + hintpath + @"</HintPath>");

                        foob = Regex.Replace(foob, @"xmlns="".*""", "");
                        
                        if(itemGroup.Descendants().Any(d => d.ToString().ContainsSearch($"{foob}")))
                            ;
                        else
                        {
                            itemGroup.Add(XElement.Parse(foob));
                        }

                        try
                        {
                            var packageLine = XElement.Parse(packageDictionary[packageName]);
                            if(rootPageDocument.Descendants().Contains(packageLine, new XElementComparerPackage()) == false)
                                rootPageDocument.Add(packageLine); 
                        }
                        catch (Exception e)
                        {
                        }
                    }

                    var distinctItemGroup = itemGroup.Descendants().Where(c => c.Name.LocalName == "Reference").Distinct(new XElementComparerHintPath());
                    var newDistinctItemGroup = new XElement(itemGroupName, distinctItemGroup);

                    var packageAdds = Regex.Replace(newDistinctItemGroup.ToString(), @"xmlns=\"".*""", "");
                    
                    //beginInsertion.AddAfterSelf(itemGroup);

                    proj.XML = doc.ToString();
                    proj.XML =  proj.XML.Replace(@"  <BeginInsertion />",
                        @"  <BeginInsertion />" + "\r\n" + packageAdds.ToString());
                    var newPackageContent = packageDocument.ToString();

                    File.WriteAllText(packageFilePath, @"<?xml version=""1.0"" encoding=""utf-8""?>" + newPackageContent);
                }
            }
        }


        private void WriteOutSolutionFile(List<MksProjectFile> mksProjectFiles, string targetDir, string rootOfSource)
        {
            var slnTemplate = File.ReadAllText(@".\template.sln");
            StringBuilder projectGuid = new StringBuilder();
            var blogItems = new List<string>();
            var preText = $"Project(\"{("FAE04EC0-301F-11D3-BF4B-00C04F79EFBC")}\") = ";
            foreach (var mksProjectFile in _mksProjectFiles)
            {
                projectGuid.AppendLine(
                    $"{preText}\"{mksProjectFile.Name}\", \"{mksProjectFile.RelativePath}\\{mksProjectFile.FileName}\", \"{mksProjectFile.Guid}\"");
                projectGuid.AppendLine("EndProject");

                StringBuilder buildSection = new StringBuilder();
                buildSection.AppendLine($"{mksProjectFile.Guid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
                buildSection.AppendLine($"{mksProjectFile.Guid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
                buildSection.AppendLine($"{mksProjectFile.Guid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
                buildSection.AppendLine($"{mksProjectFile.Guid}.Release|Any CPU.Build.0 = Release|Any CPU");
                blogItems.Add(buildSection.ToString());
            }

            slnTemplate =  slnTemplate.Replace("<BeginProjectGuid>", projectGuid.ToString());
            slnTemplate = slnTemplate.Replace(@"<BeginBlogSection>", blogItems.ConcatToString(""));
            slnTemplate = slnTemplate.Replace(@"<SolutionGuid>", "{9A8482DD-FC64-42C5-9DFC-6F3DCAD2C67E}");
            File.WriteAllText(Path.Combine(targetDir, "Mks.sln"), slnTemplate);

        }

        private void WriteOutProjFiles(List<MksProjectFile> mksProjectFiles)
        {
            foreach (var mksProjectFile in mksProjectFiles)
            {
                var path = mksProjectFile.AbsoluteTargetPath;

                File.WriteAllText(path, mksProjectFile.XML);
            }
        }

        private void UpdateCompileReferences(List<MksProjectFile> mksProjectFiles)
        {
            foreach (var proj in mksProjectFiles)
            {
                var doc = XDocument.Parse(proj.XML).Root;
                var itemGroupName = XName.Get("ItemGroup", doc.GetDefaultNamespace().NamespaceName);
                var compileName = XName.Get("Compile", doc.GetDefaultNamespace().NamespaceName);
                var beginInsertion =  doc.Descendants().FirstOrDefault(c => c.Name.LocalName == "BeginInsertion");

                var compileItemGroup = new XElement(itemGroupName);
                foreach (var thing in proj.ProjectItems)
                {
                    var compileElement = thing.Xml.OuterElement;
  

                    var compile = XElement.Parse(compileElement);
                    compileItemGroup.Add(compile);
                }

                beginInsertion.AddAfterSelf(compileItemGroup);

                var compiles = doc.ToString();
                compiles = compiles.Replace(proj.RelativePath + "\\", "");
                proj.XML = compiles;

                proj.XML = Regex.Replace(proj.XML, @"(\<Compile .*)(xmlns=.*) \/\>", @"$1 />");
                proj.XML = proj.XML.Replace(@"  <BeginInsertion />", "").Replace(@"<EndInsertion />", "");

            }
        }

        private void UpdateWebReferences(Project project, List<MksProjectFile> mksProjectFiles, ProjectItem[] allReferences, string[] webReferences)
        {
            var webSpaceDictionary = webReferences.ToDictionary(c => c.SplitWithStrings("=")[0], d => d.SplitWithStrings("=")[1]);

            //get any item that is a service reference from the original project
            var webRefItems = project.Items.Where(d => d.EvaluatedInclude.StartsWith("Web References"));

            //group them by their labels
            var webDictionary = webRefItems.GroupBy(c => c.EvaluatedInclude.SplitWithStrings("\\")[1]);

            var webNameSpaces = webSpaceDictionary.Keys.ToList();
            foreach (var mksProjectFile in mksProjectFiles)
            {
                var hasWebRef = mksProjectFile.ProjectItems.SelectMany(d => d.References).Intersect(webNameSpaces).ToArray();
                if (hasWebRef.Any())
                {
                    var doc = XDocument.Parse(mksProjectFile.XML).Root;
                    var itemGroupName = XName.Get("ItemGroup", doc.GetDefaultNamespace().NamespaceName);
                    var noneName = XName.Get("None", doc.GetDefaultNamespace().NamespaceName);
                    var endInsertion =  doc.Descendants().FirstOrDefault(c => c.Name.LocalName == "EndInsertion");

                    foreach(var refs in hasWebRef)
                    {
                        var originalDirectoryPath = Path.Combine(project.DirectoryPath, $"Web References\\{webSpaceDictionary[refs]}\\");
                        var newServiceDir = Path.Combine(mksProjectFile.AbsoluteTargetDir,
                            $"Service References\\{webSpaceDictionary[refs]}\\");

                        //1 make the directory that will house the service refs
                        DirectoryManager.Copy(originalDirectoryPath, newServiceDir);
                        
                    }

                    //2 mark up the xml
                    if (doc.Descendants("WebReferences").Count() == 0)
                    {
                        endInsertion.AddBeforeSelf(new XElement(itemGroupName,
                            new XElement(XName.Get("WebReferences", doc.GetDefaultNamespace().NamespaceName),
                                new XAttribute("Include", @"Web References\")
                                , hasWebRef.Select(c => new XElement(XName.Get("RelPath", doc.GetDefaultNamespace().NamespaceName), $"Web References\\{webSpaceDictionary[c]}\\" )))));
                    }


                    var element = new XElement(itemGroupName
                        , webRefItems.Where(d => d.EvaluatedInclude.ContainsSearch(hasWebRef.Select(e => webSpaceDictionary[e])) 
                                                     && d.ItemType == "None")
                            .Select(c =>
                                new XElement(noneName
                                    , new XAttribute("Include", c.EvaluatedInclude))));

                    endInsertion.AddBeforeSelf(element);

                    mksProjectFile.XML = doc.ToString();

                }
            }
          
        }

        private void UpdateServiceReference(Project project, List<MksProjectFile> mksProjectFiles, ProjectItem[] allReferences, string[] serviceReferences)
        {
            var nameSpaceToServiceRefernceDictionary =
                serviceReferences.ToDictionary(c => c.SplitWithStrings("=")[0], d => d.SplitWithStrings("=")[1]);


            //get any item that is a service reference from the original project
            var serviceRefItems = project.Items.Where(d => d.EvaluatedInclude.StartsWith("Service References"));

            //group them by their labels
            var serviceRefDictionary = serviceRefItems.GroupBy(c => c.EvaluatedInclude.SplitWithStrings("\\")[1]);

            var serviceNameSpaces = nameSpaceToServiceRefernceDictionary.Keys.ToList();
            foreach (var mksProjectFile in mksProjectFiles)
            {
                var hasServiceRef = mksProjectFile.ProjectItems.SelectMany(d => d.References).Intersect(serviceNameSpaces).ToArray();
                if (hasServiceRef.Length > 0)
                {
                    var doc = XDocument.Parse(mksProjectFile.XML).Root;
                    var itemGroupName = XName.Get("ItemGroup", doc.GetDefaultNamespace().NamespaceName);
                    var noneName = XName.Get("None", doc.GetDefaultNamespace().NamespaceName);
                    var endInsertion =  doc.Descendants().FirstOrDefault(c => c.Name.LocalName == "EndInsertion");
                    foreach(var refs in hasServiceRef)
                    {
                        var originalDirectoryPath = Path.Combine(project.DirectoryPath, $"Service References\\{nameSpaceToServiceRefernceDictionary[refs]}\\");
                        var newServiceDir = Path.Combine(mksProjectFile.AbsoluteTargetDir,
                            $"Service References\\{nameSpaceToServiceRefernceDictionary[refs]}\\");

                        //1 make the directory that will house the service refs
                        DirectoryManager.Copy(originalDirectoryPath, newServiceDir);
                        
                    }

                    //2 mark up the xml
                    if (doc.Descendants("WCFMetadata").Count() == 0)
                    {
                        endInsertion.AddBeforeSelf(new XElement(itemGroupName,
                            new XElement(XName.Get("WCFMetadata", doc.GetDefaultNamespace().NamespaceName),
                            new XAttribute("Include", @"Service References\"))));
                    }

                    endInsertion.AddBeforeSelf(new XElement(itemGroupName,
                        hasServiceRef.Select(c =>
                            new XElement(XName.Get("WCFMetadataStorage", doc.GetDefaultNamespace().NamespaceName),
                                new XAttribute("Include", $"Service Reference\\{nameSpaceToServiceRefernceDictionary[c]}\\")))));

                    var element = new XElement(itemGroupName
                        , serviceRefItems.Where(d => d.EvaluatedInclude.ContainsSearch(hasServiceRef.Select(e => nameSpaceToServiceRefernceDictionary[e])) 
                            && d.ItemType == "None")
                            .Select(c =>
                                new XElement(noneName
                                , new XAttribute("Include", c.EvaluatedInclude))));

                    endInsertion.AddBeforeSelf(element);

                    mksProjectFile.XML = doc.ToString();
                }
            }

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
