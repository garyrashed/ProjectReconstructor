using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProjectReconstructor.Extensions
{
    public static class LinqToXmlExtensions
    {
        public static XElement FirstMandatoryElement(this XElement root, string localName)
        {
            var candidates = FindMandatoryElements(root, localName);
            return candidates.First();
        }

        public static IEnumerable<XElement> MandatoryElements(this XElement root, string localName)
        {
            var candidates = FindMandatoryElements(root, localName);
            return candidates;
        }


        private static IEnumerable<XElement> FindMandatoryElements(XElement root, string localName)
        {
            var candidates = root.Elements().Where(c => c.Name.LocalName == localName).ToArray();

            if(candidates.Length == 0)
                throw new MandatoryXElementNotFound($"Could not find the element in {root.Name.LocalName}", localName, root);

            return candidates;
        }

        /// <summary>
        /// This will be a new XElement with the given name and the same namespace
        /// you will need to add it to the root again to make it one of the elements. 
        /// </summary>
        /// <param name="root"></param>
        /// <param name="localName"></param>
        /// <returns></returns>
        private static XElement MakeElement(this XElement root, string localName)
        {
            var defaultNameSpace = root.GetDefaultNamespace();
            var name = XName.Get(localName, defaultNameSpace.NamespaceName);
            var newElement = new XElement(name);
            return newElement;
        }



        public static XDocument CopyDoc(this XDocument document)
        {
            return XDocument.Parse(document.ToString());
        }

        /// <summary>
        /// This will return the XElement with the supplied attribute name and value or
        /// a new XElement. The chaining of the XElement will be a no op so it
        /// will be up to the calledr to check if the returned value is correct. 
        /// </summary>
        /// <param name="root"></param>
        /// <param name="attributeName"></param>
        /// <param name="value"></param>
        /// <returns></returns>


        public static FindMandatoryAttributeFromXElement(this XElement root, string attributeName)
        {
            
        }

        public static IEnumerable<XElement> FindXElementWithMandatoryAttributes(this XElement root, string attributeName)
        {
            var elements = root.Elements();
            var candidateElements =  elements.Where(c => c.Attributes(attributeName).Any()).ToArray();


        }

    }
}
