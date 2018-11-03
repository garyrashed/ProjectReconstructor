using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProjectReconstructor.Extensions
{
    public class MandatoryXElementNotFound : Exception
    {
        public string SearchName
        {
            get;
        }

        public string SearchedElement { get; }

        public MandatoryXElementNotFound(string message, string searchName, XElement searchedElement) : base(message)
        {
            SearchName = searchName;
            SearchedElement = searchedElement.ToString();
        }
    }
}
