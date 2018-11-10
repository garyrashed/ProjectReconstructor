using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProjectReconstructor.Domain
{
    public class XElementComparerHintPath :IEqualityComparer<XElement>
    {
        public bool Equals(XElement x, XElement y)
        {
            if(x.Attribute("Include").Value == y.Attribute("Include").Value)
                return true;
            return false;
        }

        public int GetHashCode(XElement obj)
        {
           return obj.Attribute("Include").Value.GetHashCode();
        }
    }

    public class XElementComparerPackage :IEqualityComparer<XElement>
    {
        public bool Equals(XElement x, XElement y)
        {
            if(x.Attribute("id").Value == y.Attribute("id").Value)
                return true;
            return false;
        }

        public int GetHashCode(XElement obj)
        {
            return obj.Attribute("id").Value.GetHashCode();
        }
    }
}
