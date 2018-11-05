using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace ProjectReconstructor.DependencyWalker
{
    public class ProjectWalker
    {
        private readonly string _projectFilePath;

        public ProjectWalker(string solutionFilePath)
        {
            var mbws = MSBuildWorkspace.Create();
            var sln = mbws.OpenSolutionAsync(solutionFilePath).Result;
            var projects = sln.Projects.ToArray();
            foreach (var proj in sln.Projects)
            {
                Console.WriteLine(proj.Name);
            }

            
        }
    }
}
