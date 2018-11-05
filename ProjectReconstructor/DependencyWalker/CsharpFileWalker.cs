using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ProjectReconstructor.DependencyWalker
{
    public class CsharpFileWalker : CSharpSyntaxWalker
    {
        private string _workingFile;

        public Dictionary<string, ICollection<UsingDirectiveSyntax>> UsingsDictionary { get; } = new Dictionary<string, ICollection<UsingDirectiveSyntax>>();

        public Dictionary<string, ICollection<NamespaceDeclarationSyntax>> NameSpacesDictionary { get; } = new Dictionary<string, ICollection<NamespaceDeclarationSyntax>>();


        public void ProcessFile(string file)
        {
            
            try
            {
                if (File.Exists(file) == false)
                {
                    throw new FileNotFoundException($"The file {file} was not found.");
                }

                var csharpText = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(csharpText);
                var root = tree.GetCompilationUnitRoot();

                _workingFile = file;

                this.Visit(root);

            }
            finally
            {
                _workingFile = string.Empty;
            }
            
        }

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            if(UsingsDictionary.ContainsKey(_workingFile) == false)
                UsingsDictionary.Add(_workingFile, new List<UsingDirectiveSyntax>());

            UsingsDictionary[_workingFile].Add(node);
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            if(NameSpacesDictionary.ContainsKey(_workingFile) == false)
                NameSpacesDictionary.Add(_workingFile, new List<NamespaceDeclarationSyntax>());

            NameSpacesDictionary[_workingFile].Add(node);
        }
    }

    public class InspectionResult
    {
        public string FullPath { get; }

        public string LocalName { get; }

        public IEnumerable<string> ReferenceStrings { get; }

        public IEnumerable<string> NameSpaceStrings { get; }

        public ProjectItem ProjectItem { get; }

        public string ProjectName { get; }

        public string RelativePath { get; }

        public InspectionResult(string fullPath)
        {
            FullPath = fullPath;
        }


    }
}
