using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectReconstructor.Config
{
    public class ServiceConfigSectionHandler : ConfigurationSection
    {
        
        public class ServiceConfig : ConfigurationElement
        {
            [ConfigurationProperty("name", IsRequired = true)]
            public string Name
            {
                get { return (string) this["name"]; }
                set { this["name"] = value; }
            }

            [ConfigurationProperty("namespace", IsRequired = true)]
            public string NameSpace
            {
                get { return (string) this["namespace"]; }
                set { this["namespace"] = value; }
            }
        }

    }
}
