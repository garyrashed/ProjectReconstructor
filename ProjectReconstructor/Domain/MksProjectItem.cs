using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.SqlServer.Server;

namespace ProjectReconstructor.Domain
{
    public class MksProjectItem
    {

        private ProjectItem _projectItem;
        public ProjectMetadata GetMetadata(string name)
        {
            return _projectItem.GetMetadata(name);
        }

        public string GetMetadataValue(string name)
        {
            return _projectItem.GetMetadataValue(name);
        }

        public bool HasMetadata(string name)
        {
            return _projectItem.HasMetadata(name);
        }

        public ProjectMetadata SetMetadataValue(string name, string unevaluatedValue)
        {
            return _projectItem.SetMetadataValue(name, unevaluatedValue);
        }

        public ProjectMetadata SetMetadataValue(string name, string unevaluatedValue, bool propagateMetadataToSiblingItems)
        {
            return _projectItem.SetMetadataValue(name, unevaluatedValue, propagateMetadataToSiblingItems);
        }

        public bool RemoveMetadata(string name)
        {
            return _projectItem.RemoveMetadata(name);
        }

        public void Rename(string name)
        {
            _projectItem.Rename(name);
        }

        public ProjectItemElement Xml => _projectItem.Xml;

        public string ItemType
        {
            get => _projectItem.ItemType;
            set => _projectItem.ItemType = value;
        }

        public string UnevaluatedInclude
        {
            get => _projectItem.UnevaluatedInclude;
            set => _projectItem.UnevaluatedInclude = value;
        }

        public string EvaluatedInclude => _projectItem.EvaluatedInclude;

        public Project Project => _projectItem.Project;

        public bool IsImported => _projectItem.IsImported;

        public IEnumerable<ProjectMetadata> DirectMetadata => _projectItem.DirectMetadata;

        public int DirectMetadataCount => _projectItem.DirectMetadataCount;

        public ICollection<ProjectMetadata> Metadata => _projectItem.Metadata;

        public int MetadataCount => _projectItem.MetadataCount;

        public MksProjectItem(ProjectItem projectItem, string sourcePath, string relativePath, string targetDir)
        {
            AbsoluteSourcePath = sourcePath;
            FileName = Path.GetFileName(AbsoluteSourcePath);
            AbsoluteTargetPath = Path.Combine(targetDir, relativePath);

            _projectItem = projectItem;
        }

        public string AbsoluteTargetPath { get; set; }
        public string AbsoluteSourcePath { get; set; }
        public string FileName { get; set; }

        public string[] References { get; set; }

        public string[] NameSpaces { get; set; }
    }
}
