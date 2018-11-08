using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProjectReconstructor.Infracture
{
    public class GuidMap
    {
        private XElement _guidMap;
        public Dictionary<string, string> ProjectGuidMap { get; }

        public GuidMap()
        {
            _guidMap = XElement.Load(@".\ProjectGuids.xml");
            ProjectGuidMap =  _guidMap.Descendants().Select(c => new
                {Name = c.Attribute("Name").Value, ProjectGuid = c.Attribute("Guid").Value}).ToDictionary(c => c.Name, d => d.ProjectGuid);
        }
    }
}
